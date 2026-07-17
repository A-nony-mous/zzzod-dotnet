using System.Collections.Generic;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// Screen state used by real-game preflight guards.
/// </summary>
public sealed record RealGamePreflightScreenState(string? WorldScreenName, string? ActiveScreenName, IReadOnlyList<string> OcrTexts);
