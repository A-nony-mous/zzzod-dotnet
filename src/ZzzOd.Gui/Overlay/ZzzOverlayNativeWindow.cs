using Avalonia.Controls;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace ZzzOd.Gui.Overlay;

internal static class ZzzOverlayNativeWindow
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;

    public static void Apply(Window window, bool clickThrough, bool preventCapture)
    {
        IPlatformHandle? handle = window.TryGetPlatformHandle();
        if (handle?.Handle is null or 0)
        {
            return;
        }

        nint hwnd = handle.Handle;
        int style = GetWindowLongW(hwnd, GwlExStyle);
        if (clickThrough)
        {
            style |= WsExTransparent | WsExLayered;
        }
        else
        {
            style &= ~WsExTransparent;
            style |= WsExLayered;
        }

        SetWindowLongW(hwnd, GwlExStyle, style);
        SetWindowDisplayAffinity(hwnd, preventCapture ? WdaExcludeFromCapture : WdaNone);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLongW(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLongW(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);
}
