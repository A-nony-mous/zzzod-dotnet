using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>路线操作。</summary>
public sealed record ZzzWorldPatrolOperationDto(string OpType, IReadOnlyList<string> Data);
