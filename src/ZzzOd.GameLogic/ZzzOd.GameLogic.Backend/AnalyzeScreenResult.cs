using System.Collections.Generic;

namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 画面分析结果。
/// </summary>
/// <param name="Success">是否成功。</param>
/// <param name="OcrTexts">OCR 文本。</param>
/// <param name="Error">错误信息。</param>
/// <param name="Screens">匹配到的画面。</param>
public sealed record AnalyzeScreenResult(bool Success, IReadOnlyList<OcrText> OcrTexts, string? Error = null, IReadOnlyList<MatchedScreen>? Screens = null);
