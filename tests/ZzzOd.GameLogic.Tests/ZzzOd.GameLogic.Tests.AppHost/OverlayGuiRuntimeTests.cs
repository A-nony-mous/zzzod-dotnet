using System.Collections.Immutable;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;
using ZzzOd.Gui.Overlay;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class OverlayGuiRuntimeTests
{
    [Fact]
    public void LockedPanelLayoutUsesClientRelativeCoordinatesAfterGameWindowMoves()
    {
        ZzzOverlayPanelSettings panel = new("state", "状态面板", true, 0, 0, 300, 120);
        ZzzWindowStatusDto first = Window(100, 200, 1000, 500, 144);
        ZzzOverlayPanelLayout.StoreLocked(panel, new ZzzOverlayPhysicalRect(200, 250, 300, 120), first);

        Assert.Equal(0.1d, panel.LockedX!.Value, 4);
        Assert.Equal(0.1d, panel.LockedY!.Value, 4);
        Assert.Equal(0.3d, panel.LockedWidth!.Value, 4);
        Assert.Equal(0.24d, panel.LockedHeight!.Value, 4);

        ZzzOverlayPhysicalRect moved = ZzzOverlayPanelLayout.ResolveLocked(panel, Window(500, 300, 1000, 500, 144));
        Assert.Equal(600d, moved.X, 4);
        Assert.Equal(350d, moved.Y, 4);
        Assert.Equal(300d, moved.Width, 4);
        Assert.Equal(120d, moved.Height, 4);
    }

    [Fact]
    public void FreePanelLayoutPersistsLogicalCoordinatesAcrossDpi()
    {
        ZzzOverlayPanelSettings panel = new("log", "日志面板", true, 0, 0, 480, 200)
        {
            IsFreeMode = true,
        };
        ZzzOverlayPanelLayout.StoreFree(panel, new ZzzOverlayPhysicalRect(300, 180, 720, 300), 1.5d, 144);

        Assert.Equal(200d, panel.FreeX!.Value, 4);
        Assert.Equal(120d, panel.FreeY!.Value, 4);
        Assert.Equal(480d, panel.FreeWidth!.Value, 4);
        Assert.Equal(200d, panel.FreeHeight!.Value, 4);
        Assert.Equal(144u, panel.FreeDpi);

        ZzzOverlayPhysicalRect onOtherDisplay = ZzzOverlayPanelLayout.ResolveFree(panel, 2d);
        Assert.Equal(400d, onOtherDisplay.X, 4);
        Assert.Equal(240d, onOtherDisplay.Y, 4);
        Assert.Equal(960d, onOtherDisplay.Width, 4);
        Assert.Equal(400d, onOtherDisplay.Height, 4);

        ZzzOverlayPanelSettings legacy = new("log", "日志面板", true, 0, 0, 480, 200)
        {
            IsFreeMode = true,
            LayoutVersion = 1,
            FreeX = 300d,
            FreeY = 180d,
            FreeWidth = 720d,
            FreeHeight = 300d,
            FreeDpi = 144,
        };
        ZzzOverlayPhysicalRect migrated = ZzzOverlayPanelLayout.ResolveFree(legacy, 2d);
        Assert.Equal(400d, migrated.X, 4);
        Assert.Equal(240d, migrated.Y, 4);
    }

    [Fact]
    public void PanelLayoutConvertsBetweenGlobalLockedAndFreeModesUsingTheCurrentGameGeometry()
    {
        ZzzWindowStatusDto gameWindow = Window(100, 200, 1000, 500, 144);
        ZzzOverlayPanelSettings panel = new("state", "状态面板", true, 0, 0, 300, 120);
        ZzzOverlayPanelLayout.StoreLocked(panel, new ZzzOverlayPhysicalRect(200, 250, 300, 120), gameWindow);

        ZzzOverlayPanelLayout.ConvertMode(
            panel,
            sourceIsFreeMode: false,
            targetIsFreeMode: true,
            gameWindow,
            desktopScaling: 1.5d,
            displayDpi: 144u);

        Assert.True(panel.IsFreeMode);
        Assert.Equal(133.3333d, panel.FreeX!.Value, 4);
        Assert.Equal(166.6667d, panel.FreeY!.Value, 4);
        Assert.Equal(200d, panel.FreeWidth!.Value, 4);
        Assert.Equal(80d, panel.FreeHeight!.Value, 4);
        Assert.Equal(144u, panel.FreeDpi);

        ZzzOverlayPanelLayout.ConvertMode(
            panel,
            sourceIsFreeMode: true,
            targetIsFreeMode: false,
            gameWindow,
            desktopScaling: 1.5d,
            displayDpi: 144u);

        Assert.False(panel.IsFreeMode);
        Assert.Equal(0.1d, panel.LockedX!.Value, 4);
        Assert.Equal(0.1d, panel.LockedY!.Value, 4);
        Assert.Equal(0.3d, panel.LockedWidth!.Value, 4);
        Assert.Equal(0.24d, panel.LockedHeight!.Value, 4);
    }

    [Fact]
    public void GameWindowFollowOnlyUpdatesForGeometryOrLifecycleChanges()
    {
        ZzzWindowStatusDto baseline = Window(100, 200, 1000, 500, 144);

        Assert.False(ZzzOverlayController.HasWindowStateChanged(baseline, baseline with { WinTitle = "标题改变不影响跟随" }));
        Assert.True(ZzzOverlayController.HasWindowStateChanged(baseline, baseline with { X = 101 }));
        Assert.True(ZzzOverlayController.HasWindowStateChanged(baseline, baseline with { Height = 501 }));
        Assert.True(ZzzOverlayController.HasWindowStateChanged(baseline, baseline with { IsWinMinimized = true }));
        Assert.True(ZzzOverlayController.HasWindowStateChanged(baseline, baseline with { IsWinActive = false }));
        Assert.True(ZzzOverlayController.HasWindowStateChanged(baseline, baseline with { Dpi = 192 }));
    }

    [Fact]
    public void OverlayVisibilityLifecycleRequiresAVisibleActiveClientAreaExceptDuringLayoutEdit()
    {
        ZzzWindowStatusDto ready = Window(100, 200, 1000, 500, 144);

        Assert.False(ZzzOverlayController.CanShowForWindow(ready with { IsWinValid = false }, layoutEditMode: false));
        Assert.False(ZzzOverlayController.CanShowForWindow(ready with { X = null }, layoutEditMode: false));
        Assert.False(ZzzOverlayController.CanShowForWindow(ready with { Width = 0 }, layoutEditMode: false));
        Assert.False(ZzzOverlayController.CanShowForWindow(ready with { IsWinMinimized = true }, layoutEditMode: false));
        Assert.False(ZzzOverlayController.CanShowForWindow(ready with { IsWinActive = false }, layoutEditMode: false));
        Assert.True(ZzzOverlayController.CanShowForWindow(ready with { IsWinActive = false }, layoutEditMode: true));
        Assert.True(ZzzOverlayController.CanShowForWindow(ready, layoutEditMode: false));
    }

    [Fact]
    public void RecreatedVisionWindowForcesGeometryApplicationWithAnUnchangedGameWindow()
    {
        ZzzWindowStatusDto baseline = Window(100, 200, 1000, 500, 144);

        Assert.False(ZzzOverlayController.ShouldApplyGeometry(false, false, baseline, baseline));
        Assert.True(ZzzOverlayController.ShouldApplyGeometry(false, true, baseline, baseline));
        Assert.True(ZzzOverlayController.ShouldApplyGeometry(true, false, baseline, baseline));
    }

    [Fact]
    public void VisionSourceSwitchesAndPythonSourceColorsApplyWithoutPlaceholderItems()
    {
        ZzzOverlayGuiSettings settings = new();
        ZzzOverlayDrawItemDto yolo = new(
            ZzzOverlayDrawItemKind.VisionDrawItem,
            "yolo:target",
            new ZzzOverlayRectDto(10, 20, 30, 40));
        ZzzOverlayDrawItemDto ocr = new(
            ZzzOverlayDrawItemKind.VisionDrawItem,
            "ocr:text",
            new ZzzOverlayRectDto(10, 20, 30, 40));

        settings.Visual.ShowYolo = false;
        Assert.False(ZzzOverlayVisionControl.IsEnabledSource(yolo, settings));
        Assert.True(ZzzOverlayVisionControl.IsEnabledSource(ocr, settings));
        Assert.Equal((byte)0x24, ZzzOverlayVisionControl.ResolveColor(yolo).R);
        Assert.Equal((byte)0xD7, ZzzOverlayVisionControl.ResolveColor(yolo).G);
        Assert.Equal((byte)0xFF, ZzzOverlayVisionControl.ResolveColor(yolo).B);
        Assert.Equal((byte)0xFF, ZzzOverlayVisionControl.ResolveColor(ocr).R);
        Assert.Equal((byte)0x4F, ZzzOverlayVisionControl.ResolveColor(ocr).G);
        Assert.Equal((byte)0xA3, ZzzOverlayVisionControl.ResolveColor(ocr).B);

        ZzzOverlayVisualSettings visual = new()
        {
            ScaleX = 1.2d,
            ScaleY = 0.8d,
            OffsetX = 30,
            OffsetY = -20,
        };
        Avalonia.Rect mapped = ZzzOverlayVisionControl.MapStandardBounds(
            new ZzzOverlayRectDto(192, 108, 192, 108),
            1280,
            720,
            visual,
            1.5d);
        Assert.Equal(173.6d, mapped.X, 4);
        Assert.Equal(44.2667d, mapped.Y, 4);
        Assert.Equal(153.6d, mapped.Width, 4);
        Assert.Equal(57.6d, mapped.Height, 4);

        ZzzOverlayDrawItemDto path = new(
            ZzzOverlayDrawItemKind.VisionDrawItem,
            "path:route",
            new ZzzOverlayRectDto(10, 20, 80, 40),
            Metadata: new Dictionary<string, string>
            {
                ["path_points"] = "[[10,20],[50,60],[90,40]]",
            });
        IReadOnlyList<Avalonia.Point> pathPoints = ZzzOverlayVisionControl.ParsePathPoints(path);
        Assert.Equal(new[] { new Avalonia.Point(10, 20), new Avalonia.Point(50, 60), new Avalonia.Point(90, 40) }, pathPoints);
        Avalonia.Point mappedPathPoint = ZzzOverlayVisionControl.MapStandardPoint(pathPoints[0], 1280, 720, new ZzzOverlayVisualSettings(), 1d);
        Assert.Equal(6.6667d, mappedPathPoint.X, 4);
        Assert.Equal(13.3333d, mappedPathPoint.Y, 4);
    }

    [Fact]
    public void PanelFormatterOnlyReturnsStructuredRuntimeData()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ZzzOverlaySnapshotDto snapshot = new(
            now,
            true,
            null,
            new ZzzOverlayRunStateDto(
                "运行中",
                "lost-void",
                "迷失之地",
                "识别事件",
                "进入副本",
                2,
                null,
                null,
                now,
                new ZzzOverlayAutoBattleStateDto(true, "安比", true, false, "闪避黄光", true, "妮可", 12.5d)),
            ImmutableArray<ZzzOverlayOperationDto>.Empty,
            ImmutableArray.Create(new ZzzOverlayDecisionDto("lost_void", "入口", "可进入", "点击", "success", now, ImmutableDictionary<string, string>.Empty)),
            ImmutableArray.Create(new ZzzOverlayTimelineItemDto("operation", "识别事件", "成功", "INFO", now, ImmutableDictionary<string, string>.Empty)),
            ImmutableArray.Create(new ZzzOverlayPerformanceSampleDto("yolo_ms", 8.5, "ms", now)),
            ImmutableArray.Create(new ZzzOverlayLogEntryDto(now, "Information", "Zzz", "真实日志", null)));
        ZzzOverlayGuiSettings settings = new();

        Assert.Contains("CurrentNode: 识别事件", ZzzOverlayPanelTextFormatter.Format("state", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("AutoBattle: RUNNING", ZzzOverlayPanelTextFormatter.Format("state", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("FrontAgent: 安比", ZzzOverlayPanelTextFormatter.Format("state", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("FrontSpecial: Y", ZzzOverlayPanelTextFormatter.Format("state", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("FrontUltimate: N", ZzzOverlayPanelTextFormatter.Format("state", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("Chain: READY", ZzzOverlayPanelTextFormatter.Format("state", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("Distance: 12.5m", ZzzOverlayPanelTextFormatter.Format("state", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("入口 => 可进入 / 点击 [success]", ZzzOverlayPanelTextFormatter.Format("decision", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("[INFO] [operation] 识别事件 成功", ZzzOverlayPanelTextFormatter.Format("timeline", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("真实日志", ZzzOverlayPanelTextFormatter.Format("log", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Contains("yolo_ms: 8.50 ms", ZzzOverlayPanelTextFormatter.Format("performance", snapshot, settings, now), StringComparison.Ordinal);
        Assert.Equal(string.Empty, ZzzOverlayPanelTextFormatter.FormatLogs([], 120, 12, now));
    }

    [Fact]
    public void OverlayMapperReadsPanelModesAppearanceAndInputPollingFromScope()
    {
        string root = Path.Combine(Path.GetTempPath(), $"overlay-gui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            ZzzConfigScopeService scopes = new(root);
            ZzzBackendResult<ZzzConfigScopeValuesDto> result = scopes.Read("overlay", null, null);
            Assert.True(result.Success, result.Error);

            ZzzOverlayGuiSettings settings = ZzzOverlaySettingsMapper.Create(result.Value.Values);
            Assert.True(settings.PanelLockToGameWindow);
            Assert.Equal(50, settings.InputPollIntervalMs);
            Assert.False(settings.PanelFreeModeMap["log_panel"]);
            Assert.Equal(12d, settings.Panels.Single(panel => panel.Id == "log").FontSize);
            Assert.Equal(70d, settings.Panels.Single(panel => panel.Id == "log").Opacity);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// 出厂默认几何下所有已启用面板的停靠矩形必须两两不相交，并落在游戏客户区边距内。
    /// </summary>
    [Fact]
    public void DefaultDockLayoutKeepsEnabledPanelsDisjointInsideGameBounds()
    {
        ZzzOverlayGuiSettings settings = new();
        ZzzWindowStatusDto game = Window(0, 0, 1920, 1080, 96);

        Dictionary<string, ZzzOverlayPhysicalRect> docks = settings.Panels
            .Where(panel => panel.Enabled)
            .ToDictionary(
                panel => panel.Id,
                panel => ZzzOverlayPanelLayout.ResolveLocked(panel, game, settings.Panels),
                StringComparer.Ordinal);

        Assert.Equal(16d, docks["state"].Y, 4);
        Assert.Equal(144d, docks["battle"].Y, 4);
        Assert.Equal(372d, docks["decision"].Y, 4);
        Assert.Equal(520d, docks["timeline"].Y, 4);
        Assert.Equal(698d, docks["performance"].Y, 4);

        foreach (KeyValuePair<string, ZzzOverlayPhysicalRect> entry in docks)
        {
            Assert.True(
                entry.Value.X >= 16d && entry.Value.Y >= 16d &&
                entry.Value.Right <= 1904d && entry.Value.Bottom <= 1064d,
                $"面板 {entry.Key} 的停靠矩形超出了游戏客户区边距。");
        }

        string[] ids = [.. docks.Keys];
        for (int i = 0; i < ids.Length; i++)
        {
            for (int j = i + 1; j < ids.Length; j++)
            {
                Assert.False(
                    Intersects(docks[ids[i]], docks[ids[j]]),
                    $"面板 {ids[i]} 与 {ids[j]} 的停靠矩形重叠。");
            }
        }
    }

    /// <summary>
    /// 右列中被禁用的面板不占位，其后的面板向上递补。
    /// </summary>
    [Fact]
    public void DisabledPanelDoesNotOccupyDefaultDockSlot()
    {
        ZzzOverlayGuiSettings settings = new();
        settings.Panels.Single(panel => panel.Id == "decision").Enabled = false;
        ZzzWindowStatusDto game = Window(0, 0, 1920, 1080, 96);

        ZzzOverlayPhysicalRect timeline = ZzzOverlayPanelLayout.ResolveLocked(
            settings.Panels.Single(panel => panel.Id == "timeline"),
            game,
            settings.Panels);
        ZzzOverlayPhysicalRect performance = ZzzOverlayPanelLayout.ResolveLocked(
            settings.Panels.Single(panel => panel.Id == "performance"),
            game,
            settings.Panels);

        Assert.Equal(372d, timeline.Y, 4);
        Assert.Equal(550d, performance.Y, 4);
    }

    /// <summary>
    /// 战斗运行中，battle 面板输出三行现场加状态行。
    /// </summary>
    [Fact]
    public void BattlePanelFormatsCurrentExecutionAndStateRows()
    {
        ZzzOverlayAutoBattleStateDto autoBattle = new(
            IsRunning: true,
            "安比",
            true,
            false,
            null,
            null,
            null,
            null,
            "闪避识别-黄光",
            "[前台-安比] and not [后台-妮可]",
            2.46d,
            [
                new ZzzOverlayBattleStateRowDto("前台-安比", 0.42d, 3),
                new ZzzOverlayBattleStateRowDto("连携技-准备", 1.5d, null),
            ]);

        string text = ZzzOverlayPanelTextFormatter.FormatBattle(autoBattle);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "[触发器] 闪避识别-黄光",
                "[条件集] [前台-安比] and not [后台-妮可]",
                "[持续] 2.5s",
                string.Empty,
                "前台-安比 0.4 3",
                "连携技-准备 1.5"),
            text);
    }

    /// <summary>
    /// 自动战斗未运行时三行现场为 `/`，状态区为空，且不输出任何说明文字。
    /// </summary>
    [Fact]
    public void BattlePanelFormatsEmptyStateWithoutExplanatoryCopy()
    {
        string stopped = ZzzOverlayPanelTextFormatter.FormatBattle(
            new ZzzOverlayAutoBattleStateDto(false, null, null, null, null, null, null, null));
        string missing = ZzzOverlayPanelTextFormatter.FormatBattle(null);
        string expected = string.Join(
            Environment.NewLine,
            "[触发器] /",
            "[条件集] /",
            "[持续] /");

        Assert.Equal(expected, stopped);
        Assert.Equal(expected, missing);
    }

    private static bool Intersects(ZzzOverlayPhysicalRect left, ZzzOverlayPhysicalRect right) =>
        left.X < right.Right && right.X < left.Right &&
        left.Y < right.Bottom && right.Y < left.Bottom;

    private static ZzzWindowStatusDto Window(int x, int y, int width, int height, uint dpi) =>
        new(null, true, true, false, x, y, width, height, false, dpi);
}
