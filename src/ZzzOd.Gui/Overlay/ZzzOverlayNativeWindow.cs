using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Runtime.InteropServices;

namespace ZzzOd.Gui.Overlay;

internal static class ZzzOverlayNativeWindow
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmNcDestroy = 0x0082;
    private const int HtTransparent = -1;
    private const int GwlpWndProc = -4;
    internal const uint WdaNone = 0x00000000;
    internal const uint WdaExcludeFromCapture = 0x00000011;
    private static readonly ConcurrentDictionary<nint, bool> ClickThroughByHandle = new();
    private static readonly ConcurrentDictionary<nint, nint> FallbackWindowProcedures = new();
    private static readonly SubclassProcedure HitTestProcedure = HitTestProcedureImpl;
    private static readonly WindowProcedure FallbackHitTestProcedure = FallbackHitTestProcedureImpl;
    private const nuint HitTestSubclassId = 0x5A5A4F44;

    public static void Apply(Window window, bool clickThrough, bool preventCapture)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!TryGetWindowHandle(window, out nint hwnd))
        {
            return;
        }

        if (InstallHitTestSubclass(hwnd))
        {
            ClickThroughByHandle[hwnd] = clickThrough;
        }
        int style = GetWindowLongW(hwnd, GwlExStyle);
        style |= WsExNoActivate;
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
        SetWindowPos(
            hwnd,
            0,
            0,
            0,
            0,
            0,
            SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        SetWindowDisplayAffinity(hwnd, preventCapture ? WdaExcludeFromCapture : WdaNone);
    }

    internal static bool TryGetDisplayAffinity(Window window, out uint affinity)
        => TryGetDisplayAffinity(window, out affinity, out _);

    internal static bool TryGetDisplayAffinity(Window window, out uint affinity, out int errorCode)
    {
        ArgumentNullException.ThrowIfNull(window);
        affinity = WdaNone;
        errorCode = 0;
        if (!OperatingSystem.IsWindows() || !TryGetWindowHandle(window, out nint hwnd))
        {
            return false;
        }

        bool success = GetWindowDisplayAffinity(hwnd, out affinity);
        errorCode = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }

    internal static bool HasClickThroughStyle(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return TryGetWindowHandle(window, out nint hwnd) &&
            (GetWindowLongW(hwnd, GwlExStyle) & WsExTransparent) != 0;
    }

    internal static bool TryGetWindowHandle(Window window, out nint hwnd)
    {
        ArgumentNullException.ThrowIfNull(window);
        IPlatformHandle? handle = window.TryGetPlatformHandle();
        if (handle?.Handle is null or 0)
        {
            hwnd = 0;
            return false;
        }

        hwnd = handle.Handle;
        return true;
    }

    private static bool InstallHitTestSubclass(nint hwnd)
    {
        if (ClickThroughByHandle.ContainsKey(hwnd))
        {
            return true;
        }

        try
        {
            if (SetWindowSubclass(hwnd, HitTestProcedure, HitTestSubclassId, 0))
            {
                ClickThroughByHandle.TryAdd(hwnd, false);
                return true;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return InstallFallbackHitTestProcedure(hwnd);
    }

    private static bool InstallFallbackHitTestProcedure(nint hwnd)
    {
        nint fallbackProcedure = Marshal.GetFunctionPointerForDelegate(FallbackHitTestProcedure);
        nint originalProcedure = GetWindowLongPointer(hwnd, GwlpWndProc);
        if (originalProcedure == 0)
        {
            return false;
        }

        Marshal.SetLastPInvokeError(0);
        nint previousProcedure = SetWindowLongPointer(hwnd, GwlpWndProc, fallbackProcedure);
        if (previousProcedure == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            return false;
        }

        if (!FallbackWindowProcedures.TryAdd(hwnd, originalProcedure))
        {
            SetWindowLongPointer(hwnd, GwlpWndProc, previousProcedure);
            return ClickThroughByHandle.ContainsKey(hwnd);
        }

        ClickThroughByHandle.TryAdd(hwnd, false);
        return true;
    }

    private static nint HitTestProcedureImpl(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (message == WmNcDestroy)
        {
            ClickThroughByHandle.TryRemove(hwnd, out _);
        }
        else if (message == WmNcHitTest && ClickThroughByHandle.TryGetValue(hwnd, out bool clickThrough) && clickThrough)
        {
            return HtTransparent;
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private static nint FallbackHitTestProcedureImpl(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmNcHitTest && ClickThroughByHandle.TryGetValue(hwnd, out bool clickThrough) && clickThrough)
        {
            return HtTransparent;
        }

        if (!FallbackWindowProcedures.TryGetValue(hwnd, out nint originalProcedure))
        {
            return DefWindowProcW(hwnd, message, wParam, lParam);
        }

        nint result = CallWindowProcW(originalProcedure, hwnd, message, wParam, lParam);
        if (message == WmNcDestroy)
        {
            FallbackWindowProcedures.TryRemove(hwnd, out _);
            ClickThroughByHandle.TryRemove(hwnd, out _);
        }

        return result;
    }

    private static nint GetWindowLongPointer(nint hwnd, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtrW(hwnd, index)
            : new nint(GetWindowLongW(hwnd, index));

    private static nint SetWindowLongPointer(nint hwnd, int index, nint value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtrW(hwnd, index, value)
            : new nint(SetWindowLongW(hwnd, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLongW(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLongW(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(nint hWnd, out uint dwAffinity);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProcedure(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassProcedure procedure, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hWnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CallWindowProcW(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);
}
