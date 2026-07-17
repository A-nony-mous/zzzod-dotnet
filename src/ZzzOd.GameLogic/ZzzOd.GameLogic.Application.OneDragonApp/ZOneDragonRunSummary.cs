using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>一条龙运行结果摘要。</summary>
public sealed record ZOneDragonRunSummary(int InstanceIndex, string GroupId, IReadOnlyList<ZOneDragonApplicationResult> Results);
