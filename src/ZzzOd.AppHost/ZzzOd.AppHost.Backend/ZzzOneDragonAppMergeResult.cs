using System.Collections.Generic;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 一条龙应用列表合并结果。
/// </summary>
/// <param name="AllApps">已持久化的完整有序列表（含未注册项，已剔除被清除的项）。</param>
/// <param name="VisibleApps">展示列表：置顶的未持久化临时项 + 默认组与已迁移的持久化项。</param>
/// <param name="MigratedAppIds">已注册但不在默认组、因启用而保留显示的应用（已迁移）。</param>
/// <param name="TransientAppIds">本次合并生成的未持久化临时项（按展示顺序）。</param>
/// <param name="Changed">持久化列表是否发生需要写盘的变化（清除已禁用的非默认组应用）。</param>
internal sealed record ZzzOneDragonAppMergeResult(
	IReadOnlyList<OneDragonApplicationConfigItem> AllApps,
	IReadOnlyList<OneDragonApplicationConfigItem> VisibleApps,
	IReadOnlyList<string> MigratedAppIds,
	IReadOnlyList<string> TransientAppIds,
	bool Changed);
