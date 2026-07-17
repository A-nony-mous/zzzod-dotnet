namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 闪避截图工具常量。
/// </summary>
public static class ScreenshotHelperConstants
{
	/// <summary>
	/// 应用 id。
	/// </summary>
	public const string AppId = "screenshot_helper";

	/// <summary>
	/// 应用名称。
	/// </summary>
	public const string AppName = "闪避截图";

	/// <summary>
	/// BaselineParity 默认应用组。
	/// </summary>
	public const string DefaultGroupId = "one_dragon";

	/// <summary>
	/// 是否属于一条龙默认组。
	/// </summary>
	public const bool DefaultGroup = false;

	/// <summary>
	/// 是否需要通知。
	/// </summary>
	public const bool NeedNotify = false;

	/// <summary>
	/// BaselineParity 上下文按键事件 id。
	/// </summary>
	public const string KeyboardPressEventId = "context_keyboard_press";

	/// <summary>
	/// 按键截图保存前缀。
	/// </summary>
	public const string SwitchImagePrefix = "switch";

	/// <summary>
	/// 闪避检测截图保存前缀。
	/// </summary>
	public const string DodgeImagePrefix = "dodge";

	/// <summary>
	/// 小地图角度检测截图保存前缀。
	/// </summary>
	public const string MiniMapAngleImagePrefix = "mini_map_angle";
}
