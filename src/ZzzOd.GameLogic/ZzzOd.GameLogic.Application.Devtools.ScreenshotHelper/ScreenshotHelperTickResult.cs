using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 单轮截图处理结果。
/// </summary>
public sealed record ScreenshotHelperTickResult(bool Captured, IReadOnlyList<ScreenshotHelperSavedImage> SavedImages, TimeSpan NextDelay, bool IsSavePending, bool IsSavingAfterKey);
