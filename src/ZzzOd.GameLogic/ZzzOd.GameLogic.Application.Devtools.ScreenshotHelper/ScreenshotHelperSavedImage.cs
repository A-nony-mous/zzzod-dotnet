using System;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 已保存的截图。
/// </summary>
public sealed record ScreenshotHelperSavedImage(string FileName, string FilePath, string Prefix, DateTimeOffset CaptureTimeUtc);
