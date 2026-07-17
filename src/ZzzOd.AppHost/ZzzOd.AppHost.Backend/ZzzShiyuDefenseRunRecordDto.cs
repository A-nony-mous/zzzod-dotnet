using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 式舆防卫战运行记录重置结果。
/// </summary>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="CriticalHistory">重置后的剧变节点完成记录。</param>
public sealed record ZzzShiyuDefenseRunRecordDto(int InstanceIndex, IReadOnlyList<int> CriticalHistory);
