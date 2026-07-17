using System.Collections.Generic;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 覆盖矩阵条目。
/// </summary>
public sealed record E2ECoverageMatrixItem(string Id, E2ECoverageArea Area, string DisplayName, E2EVerificationMode VerificationMode, IReadOnlyList<string> Components, string Evidence, string? BlockedReason = null);
