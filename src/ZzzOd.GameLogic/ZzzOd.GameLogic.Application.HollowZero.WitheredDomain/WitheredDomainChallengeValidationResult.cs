using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 挑战配置文本校验结果。
/// </summary>
public sealed record WitheredDomainChallengeValidationResult(IReadOnlyList<string> Values, string Error);
