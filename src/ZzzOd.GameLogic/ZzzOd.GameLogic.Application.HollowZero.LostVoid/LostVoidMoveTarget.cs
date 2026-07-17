using System.Collections.Generic;
using OneDragon.Core.Abstractions.Geometry;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地入口移动目标。
/// </summary>
public sealed class LostVoidMoveTarget
{
	/// <summary>目标名称列表。</summary>
	public IReadOnlyList<string> TargetNames { get; }

	/// <summary>完整区域。</summary>
	public Rect EntireRect { get; }

	/// <summary>
	/// 初始化入口移动目标。
	/// </summary>
	public LostVoidMoveTarget(IReadOnlyList<string> targetNames, Rect entireRect)
	{
		TargetNames = targetNames;
		EntireRect = entireRect;
	}
}
