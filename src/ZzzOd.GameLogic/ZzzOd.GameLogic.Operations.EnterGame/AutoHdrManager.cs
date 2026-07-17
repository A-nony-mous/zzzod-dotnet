using System;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Pure Auto HDR business logic over a replaceable preference store.
/// </summary>
public static class AutoHdrManager
{
	/// <summary>
	/// Value used by BaselineParity to disable Auto HDR for the executable.
	/// </summary>
	public const string DisabledValue = "AutoHDREnable=2096;";

	/// <summary>
	/// Disable Auto HDR and return the original value.
	/// </summary>
	public static AutoHdrChangeResult Disable(string gamePath, IAutoHdrPreferenceStore store)
	{
		ArgumentNullException.ThrowIfNull(store, "store");
		if (string.IsNullOrWhiteSpace(gamePath))
		{
			return new AutoHdrChangeResult(IsSuccess: false, "未配置游戏路径");
		}
		try
		{
			string originalValue = store.ReadValue(gamePath);
			store.WriteValue(gamePath, "AutoHDREnable=2096;");
			return new AutoHdrChangeResult(IsSuccess: true, "已禁用HDR", originalValue);
		}
		catch (Exception error)
		{
			return new AutoHdrChangeResult(IsSuccess: false, "设置注册表失败", null, error);
		}
	}

	/// <summary>
	/// Restore the original Auto HDR value, or remove the game-specific value when none existed.
	/// </summary>
	public static AutoHdrChangeResult Enable(string gamePath, string? originalValue, IAutoHdrPreferenceStore store)
	{
		ArgumentNullException.ThrowIfNull(store, "store");
		if (string.IsNullOrWhiteSpace(gamePath))
		{
			return new AutoHdrChangeResult(IsSuccess: false, "未配置游戏路径");
		}
		try
		{
			if (string.IsNullOrWhiteSpace(originalValue))
			{
				store.DeleteValue(gamePath);
			}
			else
			{
				store.WriteValue(gamePath, originalValue);
			}
			return new AutoHdrChangeResult(IsSuccess: true, "已启用HDR", originalValue);
		}
		catch (Exception error)
		{
			return new AutoHdrChangeResult(IsSuccess: false, "修改注册表失败", null, error);
		}
	}
}
