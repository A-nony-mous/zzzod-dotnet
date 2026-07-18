using Xunit;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// Overlay 面板锁定与自由布局配置的 round-trip 合同。
/// </summary>
public sealed class ZzzOverlayConfigLayoutTests
{
	/// <summary>
	/// 保存几何时必须保留归一化坐标、自由坐标和 DPI，局部更新不得覆盖已有字段。
	/// </summary>
	[Fact]
	public void OverlayScopeRoundTripsLockedAndFreePanelGeometry()
	{
		string root = CreateTempRoot();
		try
		{
			ZzzConfigScopeService scopes = new(root);
			ZzzBackendResult<ZzzConfigScopeValuesDto> saved = scopes.Save(new ZzzSaveConfigScopeRequest("overlay", new Dictionary<string, object?>
			{
				["panel_lock_to_game_window"] = true,
				["panel_free_mode_map"] = new Dictionary<string, object?>
				{
					["log_panel"] = true
				},
				["panel_geometry"] = new Dictionary<string, object?>
				{
					["log_panel"] = new Dictionary<string, object?>
					{
						["x"] = 123.5d,
						["y"] = -10.25d,
						["w"] = 500.75d,
						["h"] = 251.25d,
						["layout_version"] = 2,
						["locked_x"] = 0.125d,
						["locked_y"] = 0.25d,
						["locked_w"] = 0.5d,
						["locked_h"] = 0.33d,
						["free_x"] = 222.5d,
						["free_y"] = 333.25d,
						["free_w"] = 555.5d,
						["free_h"] = 444.75d,
						["free_dpi"] = 144u
					}
				}
			}));
			Assert.True(saved.Success, saved.Error);
			AssertLogPanel(saved.Value.Values, freeDpi: 144u);
			IReadOnlyDictionary<string, bool> initialModes = ReadBoolMap(saved.Value.Values["panel_free_mode_map"]);
			Assert.False(initialModes["log_panel"]);
			Assert.False(initialModes["state_panel"]);

			ZzzBackendResult<ZzzConfigScopeValuesDto> updated = scopes.Save(new ZzzSaveConfigScopeRequest("overlay", new Dictionary<string, object?>
			{
				["panel_free_mode_map"] = new Dictionary<string, object?> { ["state_panel"] = true },
				["panel_geometry"] = new Dictionary<string, object?>
				{
					["log_panel"] = new Dictionary<string, object?> { ["free_dpi"] = 192u }
				}
			}));
			Assert.True(updated.Success, updated.Error);
			AssertLogPanel(updated.Value.Values, freeDpi: 192u);
			IReadOnlyDictionary<string, bool> updatedModes = ReadBoolMap(updated.Value.Values["panel_free_mode_map"]);
			Assert.False(updatedModes["log_panel"]);
			Assert.True(updatedModes["state_panel"]);
			Assert.False(Convert.ToBoolean(updated.Value.Values["panel_lock_to_game_window"]));

			ZzzConfigScopeService rereadScopes = new(root);
			ZzzBackendResult<ZzzConfigScopeValuesDto> reread = rereadScopes.Read("overlay", null, null);
			Assert.True(reread.Success, reread.Error);
			AssertLogPanel(reread.Value.Values, freeDpi: 192u);
			string yaml = File.ReadAllText(Path.Combine(root, "config", "overlay.yml"));
			Assert.Contains("layout_version: 2", yaml, StringComparison.Ordinal);
			Assert.Contains("locked_x: 0.125", yaml, StringComparison.Ordinal);
			Assert.Contains("free_dpi: 192", yaml, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// 旧配置包含全局锁定和单面板自由状态时，重载结果应以完整面板模式为准，避免向 GUI 暴露相互矛盾的开关。
	/// </summary>
	[Fact]
	public void OverlayScopeReconcilesGlobalLockWithPanelModesOnReload()
	{
		string root = CreateTempRoot();
		try
		{
			string configDirectory = Path.Combine(root, "config");
			Directory.CreateDirectory(configDirectory);
			File.WriteAllText(
				Path.Combine(configDirectory, "overlay.yml"),
				"overlay:\n  panel_lock_to_game_window: true\n  panel_free_mode_map:\n    log_panel: true\n");

			ZzzBackendResult<ZzzConfigScopeValuesDto> read = new ZzzConfigScopeService(root).Read("overlay", null, null);
			Assert.True(read.Success, read.Error);
			IReadOnlyDictionary<string, bool> modes = ReadBoolMap(read.Value.Values["panel_free_mode_map"]);
			Assert.True(modes["log_panel"]);
			Assert.False(Convert.ToBoolean(read.Value.Values["panel_lock_to_game_window"]));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// 旧绝对像素布局应保留原始位置尺寸，并带上可由 GUI 在游戏窗口可用时迁移的版本标识。
	/// </summary>
	[Fact]
	public void OverlayScopeMarksLegacyAbsoluteGeometryForLaterMigration()
	{
		string root = CreateTempRoot();
		try
		{
			string configDirectory = Path.Combine(root, "config");
			Directory.CreateDirectory(configDirectory);
			File.WriteAllText(Path.Combine(configDirectory, "overlay.yml"), "overlay:\n  panel_geometry:\n    log_panel:\n      x: 101\n      y: -20\n      w: 480\n      h: 200\n");

			ZzzBackendResult<ZzzConfigScopeValuesDto> read = new ZzzConfigScopeService(root).Read("overlay", null, null);
			Assert.True(read.Success, read.Error);
			IReadOnlyDictionary<string, object?> geometry = ReadMap(read.Value.Values["panel_geometry"]);
			IReadOnlyDictionary<string, object?> logPanel = ReadMap(geometry["log_panel"]);
			Assert.Equal(101d, Convert.ToDouble(logPanel["x"]));
			Assert.Equal(-20d, Convert.ToDouble(logPanel["y"]));
			Assert.Equal(480d, Convert.ToDouble(logPanel["w"]));
			Assert.Equal(200d, Convert.ToDouble(logPanel["h"]));
			Assert.Equal(1, Convert.ToInt32(logPanel["layout_version"]));
			Assert.Equal(0d, Convert.ToDouble(logPanel["locked_x"]));
			Assert.Equal(101d, Convert.ToDouble(logPanel["free_x"]));
			Assert.Equal(-20d, Convert.ToDouble(logPanel["free_y"]));
			Assert.Equal(96u, Convert.ToUInt32(logPanel["free_dpi"]));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// 连续 YOLO 绘制项去重阈值沿用 Python 的 0.3 默认值，保存时限制在有效 IoU 范围。
	/// </summary>
	[Fact]
	public void OverlayScopeSuppliesAndNormalizesYoloDedupIouThreshold()
	{
		string root = CreateTempRoot();
		try
		{
			ZzzConfigScopeService scopes = new(root);
			ZzzBackendResult<ZzzConfigScopeValuesDto> defaults = scopes.Read("overlay", null, null);
			Assert.True(defaults.Success, defaults.Error);
			Assert.Equal(0.3d, Convert.ToDouble(defaults.Value.Values["vision_yolo_dedup_iou_threshold"]));

			ZzzBackendResult<ZzzConfigScopeValuesDto> aboveRange = scopes.Save(new ZzzSaveConfigScopeRequest("overlay", new Dictionary<string, object?>
			{
				["vision_yolo_dedup_iou_threshold"] = 2d,
			}));
			Assert.True(aboveRange.Success, aboveRange.Error);
			Assert.Equal(1d, Convert.ToDouble(aboveRange.Value.Values["vision_yolo_dedup_iou_threshold"]));

			ZzzBackendResult<ZzzConfigScopeValuesDto> belowRange = scopes.Save(new ZzzSaveConfigScopeRequest("overlay", new Dictionary<string, object?>
			{
				["vision_yolo_dedup_iou_threshold"] = -1d,
			}));
			Assert.True(belowRange.Success, belowRange.Error);
			Assert.Equal(0.01d, Convert.ToDouble(belowRange.Value.Values["vision_yolo_dedup_iou_threshold"]));

			ZzzBackendResult<ZzzConfigScopeValuesDto> nonFinite = scopes.Save(new ZzzSaveConfigScopeRequest("overlay", new Dictionary<string, object?>
			{
				["vision_yolo_dedup_iou_threshold"] = double.NaN,
			}));
			Assert.True(nonFinite.Success, nonFinite.Error);
			Assert.Equal(0.3d, Convert.ToDouble(nonFinite.Value.Values["vision_yolo_dedup_iou_threshold"]));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static void AssertLogPanel(IReadOnlyDictionary<string, object> values, uint freeDpi)
	{
		IReadOnlyDictionary<string, object?> geometry = ReadMap(values["panel_geometry"]);
		IReadOnlyDictionary<string, object?> logPanel = ReadMap(geometry["log_panel"]);
		Assert.Equal(123.5d, Convert.ToDouble(logPanel["x"]));
		Assert.Equal(-10.25d, Convert.ToDouble(logPanel["y"]));
		Assert.Equal(500.75d, Convert.ToDouble(logPanel["w"]));
		Assert.Equal(251.25d, Convert.ToDouble(logPanel["h"]));
		Assert.Equal(2, Convert.ToInt32(logPanel["layout_version"]));
		Assert.Equal(0.125d, Convert.ToDouble(logPanel["locked_x"]));
		Assert.Equal(0.25d, Convert.ToDouble(logPanel["locked_y"]));
		Assert.Equal(0.5d, Convert.ToDouble(logPanel["locked_w"]));
		Assert.Equal(0.33d, Convert.ToDouble(logPanel["locked_h"]));
		Assert.Equal(222.5d, Convert.ToDouble(logPanel["free_x"]));
		Assert.Equal(333.25d, Convert.ToDouble(logPanel["free_y"]));
		Assert.Equal(555.5d, Convert.ToDouble(logPanel["free_w"]));
		Assert.Equal(444.75d, Convert.ToDouble(logPanel["free_h"]));
		Assert.Equal(freeDpi, Convert.ToUInt32(logPanel["free_dpi"]));
	}

	private static IReadOnlyDictionary<string, object?> ReadMap(object? value)
	{
		return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value);
	}

	private static IReadOnlyDictionary<string, bool> ReadBoolMap(object? value)
	{
		return Assert.IsAssignableFrom<IReadOnlyDictionary<string, bool>>(value);
	}

	private static string CreateTempRoot()
	{
		string root = Path.Combine(Path.GetTempPath(), "zzz-overlay-layout-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		return root;
	}
}
