using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战应用常量。
/// </summary>
public static class ShiyuDefenseConstants
{
	/// <summary>应用 id。</summary>
	public const string AppId = "shiyu_defense";

	/// <summary>应用名称。</summary>
	public const string AppName = "式舆防卫战";

	/// <summary>默认应用组。</summary>
	public const string DefaultGroupId = "one_dragon";

	/// <summary>是否属于一条龙默认组。</summary>
	public const bool DefaultGroup = true;

	/// <summary>是否需要通知。</summary>
	public const bool NeedNotify = true;

	/// <summary>多间模式房间名称。</summary>
	public static IReadOnlyList<string> RoomNames { get; } = new string[3] { "第一间", "第二间", "第三间" };

	/// <summary>多间模式节点。</summary>
	public static IReadOnlySet<int> MultiRoomNodes { get; } = new HashSet<int> { 5 };
}
