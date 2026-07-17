using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ZzzOd.Gui.Services.Windows;

internal sealed class ZzzGlobalInputMonitor : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmXButtonDown = 0x020B;
    private const uint WmQuit = 0x0012;

    private readonly Lock _lock = new();
    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private Thread? _thread;
    private uint _threadId;
    private nint _keyboardHook;
    private nint _mouseHook;
    private ManualResetEventSlim? _ready;
    private bool _disposed;

    public ZzzGlobalInputMonitor()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
    }

    public event EventHandler<string>? InputPressed;

    public string? LastError { get; private set; }

    public bool EnsureStarted()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread?.IsAlive == true)
            {
                return _keyboardHook != 0 && _mouseHook != 0;
            }

            if (!OperatingSystem.IsWindows())
            {
				LastError = "全局按键监听仅支持 Windows。";
                return false;
            }

            LastError = null;
            _ready?.Dispose();
            _ready = new ManualResetEventSlim(false);
            _thread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "zzz-global-input-monitor",
            };
            _thread.Start();
        }

        _ready!.Wait(TimeSpan.FromSeconds(3));
        return _keyboardHook != 0 && _mouseHook != 0;
    }

    public void Dispose()
    {
        Thread? thread;
        uint threadId;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            thread = _thread;
            threadId = _threadId;
        }

        if (threadId != 0)
        {
            _ = PostThreadMessageW(threadId, WmQuit, 0, 0);
        }

        if (thread is not null && thread != Thread.CurrentThread)
        {
            _ = thread.Join(TimeSpan.FromSeconds(2));
        }

        _ready?.Dispose();
    }

    internal static string? NormalizeVirtualKey(uint virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString().ToLowerInvariant(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x60 and <= 0x69 => $"numpad_{virtualKey - 0x60}",
        >= 0x70 and <= 0x87 => $"f{virtualKey - 0x6F}",
        0x08 => "backspace",
        0x09 => "tab",
        0x0D => "enter",
        0x10 => "shift",
        0x11 => "ctrl",
        0x12 => "alt",
        0x1B => "esc",
        0x20 => "space",
        0x21 => "page_up",
        0x22 => "page_down",
        0x23 => "end",
        0x24 => "home",
        0x25 => "left",
        0x26 => "up",
        0x27 => "right",
        0x28 => "down",
        0x2D => "insert",
        0x2E => "delete",
        _ => $"vk_{virtualKey}",
    };

    private void RunMessageLoop()
    {
        _threadId = GetCurrentThreadId();
        nint module = GetModuleHandleW(null);
        _keyboardHook = SetWindowsHookExW(WhKeyboardLl, _keyboardProc, module, 0);
        _mouseHook = SetWindowsHookExW(WhMouseLl, _mouseProc, module, 0);
        if (_keyboardHook == 0 || _mouseHook == 0)
        {
            int error = Marshal.GetLastWin32Error();
            LastError = new Win32Exception(error).Message;
            Unhook();
            _ready?.Set();
            return;
        }

        _ready?.Set();
        while (GetMessageW(out NativeMessage message, 0, 0, 0) > 0)
        {
            _ = TranslateMessage(in message);
            _ = DispatchMessageW(in message);
        }

        Unhook();
    }

    private nint KeyboardCallback(int code, nuint message, nint data)
    {
        if (code >= 0 && (message == WmKeyDown || message == WmSysKeyDown))
        {
            uint virtualKey = unchecked((uint)Marshal.ReadInt32(data));
            Publish(NormalizeVirtualKey(virtualKey));
        }

        return CallNextHookEx(_keyboardHook, code, message, data);
    }

    private nint MouseCallback(int code, nuint message, nint data)
    {
        if (code >= 0)
        {
            string? key = unchecked((uint)message) switch
            {
                WmLButtonDown => "mouse_left",
                WmRButtonDown => "mouse_right",
                WmMButtonDown => "mouse_middle",
                WmXButtonDown => ((Marshal.PtrToStructure<MouseHookData>(data).MouseData >> 16) & 0xffff) == 1
                    ? "mouse_x1"
                    : "mouse_x2",
                _ => null,
            };
            Publish(key);
        }

        return CallNextHookEx(_mouseHook, code, message, data);
    }

    private void Publish(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        EventHandler<string>? handler = InputPressed;
        if (handler is not null)
        {
            ThreadPool.QueueUserWorkItem(_ => handler(this, key));
        }
    }

    private void Unhook()
    {
        if (_keyboardHook != 0)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }

        if (_mouseHook != 0)
        {
            _ = UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }
    }

    private delegate nint HookProc(int code, nuint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int hookId, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nuint message, nint data);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessageW(out NativeMessage message, nint window, uint min, uint max);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(in NativeMessage message);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern nint DispatchMessageW(in NativeMessage message);

    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}

