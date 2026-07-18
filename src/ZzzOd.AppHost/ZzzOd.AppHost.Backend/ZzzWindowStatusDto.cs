namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 游戏窗口状态。
/// </summary>
/// <param name="WinTitle">窗口标题。</param>
/// <param name="IsWinValid">窗口是否有效。</param>
/// <param name="IsWinActive">窗口是否激活。</param>
/// <param name="IsWinScale">窗口比例是否异常。</param>
/// <param name="X">客户区左上角 X。</param>
/// <param name="Y">客户区左上角 Y。</param>
/// <param name="Width">客户区宽度。</param>
/// <param name="Height">客户区高度。</param>
/// <param name="IsWinMinimized">窗口是否最小化。</param>
/// <param name="Dpi">窗口 DPI。</param>
public sealed record ZzzWindowStatusDto(
    string? WinTitle,
    bool IsWinValid,
    bool IsWinActive,
    bool IsWinScale,
    int? X = null,
    int? Y = null,
    int? Width = null,
    int? Height = null,
    bool IsWinMinimized = false,
    uint Dpi = 96);
