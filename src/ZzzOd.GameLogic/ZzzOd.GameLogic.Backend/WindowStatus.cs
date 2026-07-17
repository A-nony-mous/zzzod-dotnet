namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 游戏窗口状态。
/// </summary>
/// <param name="WinTitle">窗口标题。</param>
/// <param name="IsWinValid">窗口是否有效。</param>
/// <param name="IsWinActive">窗口是否激活。</param>
/// <param name="IsWinScale">窗口是否缩放。</param>
/// <param name="X">左上角 X。</param>
/// <param name="Y">左上角 Y。</param>
/// <param name="Width">宽度。</param>
/// <param name="Height">高度。</param>
public sealed record WindowStatus(string? WinTitle, bool IsWinValid, bool IsWinActive, bool IsWinScale, int? X = null, int? Y = null, int? Width = null, int? Height = null);
