namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 游戏窗口状态。
/// </summary>
/// <param name="WinTitle">窗口标题。</param>
/// <param name="IsWinValid">窗口是否有效。</param>
/// <param name="IsWinActive">窗口是否激活。</param>
/// <param name="IsWinScale">窗口比例是否异常。</param>
public sealed record ZzzWindowStatusDto(string? WinTitle, bool IsWinValid, bool IsWinActive, bool IsWinScale);
