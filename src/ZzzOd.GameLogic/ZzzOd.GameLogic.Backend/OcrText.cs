namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// OCR 文本结果。
/// </summary>
/// <param name="Text">文本内容。</param>
/// <param name="X">左上角 X。</param>
/// <param name="Y">左上角 Y。</param>
/// <param name="Width">宽度。</param>
/// <param name="Height">高度。</param>
public sealed record OcrText(string Text, int X, int Y, int Width, int Height);
