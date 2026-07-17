using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地最终入口 Boss。
/// </summary>
public static class LostVoidBoss
{
	/// <summary>终结之役·牲鬼。</summary>
	public const string ShengGui = "终结之役·牲鬼";

	/// <summary>终结之役·杰佩托。</summary>
	public const string JiePeiTuo = "终结之役·杰佩托";

	/// <summary>全部 Boss 名称。</summary>
	public static IReadOnlyList<string> All { get; } = new string[2] { "终结之役·牲鬼", "终结之役·杰佩托" };
}
