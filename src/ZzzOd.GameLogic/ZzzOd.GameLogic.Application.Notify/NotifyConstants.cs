namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 通知应用常量。
/// </summary>
public static class NotifyConstants
{
	/// <summary>应用 id。</summary>
	public const string AppId = "notify";

	/// <summary>应用名称。</summary>
	public const string AppName = "通知";

	/// <summary>默认应用组。</summary>
	public const string DefaultGroupId = "one_dragon";

	/// <summary>是否属于一条龙默认组。</summary>
	public const bool DefaultGroup = true;

	/// <summary>通知应用自身不再进入通知列表。</summary>
	public const bool NeedNotify = false;

	/// <summary>默认推送标题。</summary>
	public const string DefaultTitle = "一条龙运行通知";
}
