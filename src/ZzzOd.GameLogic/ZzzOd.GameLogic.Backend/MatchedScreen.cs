namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 画面匹配结果。
/// </summary>
/// <param name="ScreenName">画面名称。</param>
/// <param name="IsPrecise">是否精准命中。</param>
public sealed record MatchedScreen(string ScreenName, bool IsPrecise);
