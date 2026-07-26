using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;
using OneDragon.Core.Screening;
using OneDragon.Core.Windows.Capture;
using OneDragon.Core.Windows.Platform;
using OneDragon.Core.Windows.Screening;
using OpenCvSharp;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using GeometryRect = OneDragon.Core.Abstractions.Geometry.Rect;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("GUI evidence capture requires Windows.");
    return 2;
}

// 交由框架层统一处理，业务侧不再自行判定 DPI 感知级别。
WindowsDpiAwareness.TryEnablePerMonitorDpiAwareness();

CaptureOptions options;
try
{
    options = CaptureOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Usage: ZzzOd.GuiEvidenceCapture (--title <window-title> | --process-id <pid>) --output <png-path> [--expected-size <width>x<height>] [--timeout-seconds <seconds>]");
    return 2;
}

IGameWindow window = options.ProcessId is int processId
    ? new ProcessGameWindow(processId, options.ExpectedWidth ?? 1140, options.ExpectedHeight ?? 760)
    : new WindowsGameWindow(options.Title, options.ExpectedWidth ?? 1140, options.ExpectedHeight ?? 760);
GeometryRect? rect = await WaitForWindowAsync(window, options.Timeout);
if (rect is null)
{
    Console.Error.WriteLine($"Window was not ready: {options.Title ?? options.ProcessId?.ToString()}");
    return 3;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
List<object> attempts = [];
uint windowDpi = NativeMethods.GetDpiForWindow(window.Handle);
int windowDpiAwareness = NativeMethods.GetAwarenessFromDpiAwarenessContext(
    NativeMethods.GetWindowDpiAwarenessContext(window.Handle));

try
{
    using FreeThreadedWindowCapture compositorCapture = new(window.Handle);
    using Mat? image = await compositorCapture.CaptureAsync(options.Timeout);
    if (image is null || image.Empty())
    {
        attempts.Add(new
        {
            method = FreeThreadedWindowCapture.MethodName,
            status = "empty",
            captureItemWidth = compositorCapture.CaptureItemWidth,
            captureItemHeight = compositorCapture.CaptureItemHeight,
            windowDpi,
            windowDpiAwareness,
        });
    }
    else if (options.ExpectedWidth is int expectedWidth && image.Width != expectedWidth
        || options.ExpectedHeight is int expectedHeight && image.Height != expectedHeight)
    {
        attempts.Add(new
        {
            method = FreeThreadedWindowCapture.MethodName,
            status = "size-mismatch",
            width = image.Width,
            height = image.Height,
            captureItemWidth = compositorCapture.CaptureItemWidth,
            captureItemHeight = compositorCapture.CaptureItemHeight,
            windowDpi,
            windowDpiAwareness,
        });
    }
    else if (!Cv2.ImWrite(options.Output, image))
    {
        attempts.Add(new { method = FreeThreadedWindowCapture.MethodName, status = "write-failed" });
    }
    else
    {
        string sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(options.Output)));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            title = window.WindowTitle,
            processId = options.ProcessId,
            output = Path.GetFullPath(options.Output),
            method = FreeThreadedWindowCapture.MethodName,
            width = image.Width,
            height = image.Height,
            captureItemWidth = compositorCapture.CaptureItemWidth,
            captureItemHeight = compositorCapture.CaptureItemHeight,
            windowDpi,
            windowDpiAwareness,
            sha256,
            attempts,
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
catch (Exception ex)
{
    attempts.Add(new
    {
        method = FreeThreadedWindowCapture.MethodName,
        status = "failed",
        error = ex.Message,
        windowDpi,
        windowDpiAwareness,
    });
}

IReadOnlyList<IScreenCapturer> capturers =
[
    new WindowsGraphicsCaptureScreenCapturer(window),
    new PrintWindowScreenCapturer(window),
];

foreach (IScreenCapturer capturer in capturers)
{
    try
    {
        if (!capturer.Initialize())
        {
            attempts.Add(new { method = capturer.Method.ToString(), status = "initialize-failed" });
            continue;
        }

        using Mat? image = capturer.Capture(rect.Value, independent: true);
        if (image is null || image.Empty())
        {
            attempts.Add(new { method = capturer.Method.ToString(), status = "empty" });
            continue;
        }

        if (options.ExpectedWidth is int expectedWidth && image.Width != expectedWidth
            || options.ExpectedHeight is int expectedHeight && image.Height != expectedHeight)
        {
            attempts.Add(new
            {
                method = capturer.Method.ToString(),
                status = "size-mismatch",
                width = image.Width,
                height = image.Height,
                windowDpi,
                windowDpiAwareness,
            });
            continue;
        }

        if (!Cv2.ImWrite(options.Output, image))
        {
            attempts.Add(new { method = capturer.Method.ToString(), status = "write-failed" });
            continue;
        }

        string sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(options.Output)));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            title = window.WindowTitle,
            processId = options.ProcessId,
            output = Path.GetFullPath(options.Output),
            method = capturer.Method.ToString(),
            width = image.Width,
            height = image.Height,
            sha256,
            attempts,
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    catch (Exception ex)
    {
        attempts.Add(new
        {
            method = capturer.Method.ToString(),
            status = "failed",
            error = ex.Message,
        });
    }
    finally
    {
        capturer.Cleanup();
    }
}

Console.Error.WriteLine(JsonSerializer.Serialize(new
{
    title = window.WindowTitle,
    processId = options.ProcessId,
    output = Path.GetFullPath(options.Output),
    windowDpi,
    windowDpiAwareness,
    attempts,
}, new JsonSerializerOptions { WriteIndented = true }));
return 4;

static async Task<GeometryRect?> WaitForWindowAsync(IGameWindow window, TimeSpan timeout)
{
    DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
    while (DateTimeOffset.UtcNow < deadline)
    {
        window.Refresh();
        GeometryRect? rect = window.WindowRect;
        if (window.Handle != 0 && rect is { Width: > 0, Height: > 0 })
        {
            return rect;
        }

        await Task.Delay(200);
    }

    return null;
}

internal sealed record CaptureOptions(
    string? Title,
    int? ProcessId,
    string Output,
    int? ExpectedWidth,
    int? ExpectedHeight,
    TimeSpan Timeout)
{
    internal static CaptureOptions Parse(IReadOnlyList<string> args)
    {
        string? title = null;
        int? processId = null;
        string? output = null;
        int? expectedWidth = null;
        int? expectedHeight = null;
        int timeoutSeconds = 20;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            string ReadValue()
            {
                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new ArgumentException($"Missing value for {argument}.");
                }

                return args[index];
            }

            switch (argument)
            {
                case "--title":
                    title = ReadValue();
                    break;
                case "--process-id":
                    if (!int.TryParse(ReadValue(), out int parsedProcessId) || parsedProcessId <= 0)
                    {
                        throw new ArgumentException("--process-id must be a positive integer.");
                    }

                    processId = parsedProcessId;
                    break;
                case "--output":
                    output = ReadValue();
                    break;
                case "--expected-size":
                    string[] parts = ReadValue().Split('x', 'X');
                    if (parts.Length != 2
                        || !int.TryParse(parts[0], out int width)
                        || !int.TryParse(parts[1], out int height)
                        || width <= 0
                        || height <= 0)
                    {
                        throw new ArgumentException("--expected-size must use <width>x<height>.");
                    }

                    expectedWidth = width;
                    expectedHeight = height;
                    break;
                case "--timeout-seconds":
                    if (!int.TryParse(ReadValue(), out timeoutSeconds) || timeoutSeconds <= 0)
                    {
                        throw new ArgumentException("--timeout-seconds must be a positive integer.");
                    }

                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        if ((string.IsNullOrWhiteSpace(title) && processId is null) || string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException("--output and either --title or --process-id are required.");
        }

        if (!string.IsNullOrWhiteSpace(title) && processId is not null)
        {
            throw new ArgumentException("Use only one of --title or --process-id.");
        }

        return new CaptureOptions(title, processId, output, expectedWidth, expectedHeight, TimeSpan.FromSeconds(timeoutSeconds));
    }
}

internal sealed class ProcessGameWindow(int processId, int standardWidth, int standardHeight) : IGameWindow
{
    private nint _handle;

    public string? WindowTitle { get; private set; }

    public nint Handle
    {
        get
        {
            Refresh();
            return _handle;
        }
    }

    public GeometryRect? WindowRect
    {
        get
        {
            Refresh();
            if (_handle == 0 || !NativeMethods.GetClientRect(_handle, out NativeMethods.NativeRect clientRect))
            {
                return null;
            }

            NativeMethods.NativePoint origin = new(0, 0);
            if (!NativeMethods.ClientToScreen(_handle, ref origin))
            {
                return null;
            }

            return new GeometryRect(
                origin.X,
                origin.Y,
                origin.X + clientRect.Right,
                origin.Y + clientRect.Bottom);
        }
    }

    public bool IsWindowScale => WindowRect is GeometryRect rect
        && (rect.Width != standardWidth || rect.Height != standardHeight);

    public bool IsWindowValid
    {
        get
        {
            Refresh();
            return _handle != 0 && NativeMethods.IsWindow(_handle);
        }
    }

    public void Refresh()
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
            process.Refresh();
            _handle = process.MainWindowHandle;
            WindowTitle = process.MainWindowTitle;
        }
        catch (ArgumentException)
        {
            _handle = 0;
            WindowTitle = null;
        }
    }
}

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint(int x, int y)
    {
        internal int X = x;
        internal int Y = y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern nint GetWindowDpiAwarenessContext(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern int GetAwarenessFromDpiAwarenessContext(nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(nint hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(nint hWnd, ref NativePoint point);
}

internal sealed class FreeThreadedWindowCapture : IDisposable
{
    internal const string MethodName = "WindowsGraphicsCapture.FreeThreaded";

    private readonly object _syncRoot = new();
    private readonly GraphicsCaptureItem _captureItem;
    private readonly IDirect3DDevice _direct3DDevice;
    private readonly Direct3D11CaptureFramePool _framePool;
    private readonly GraphicsCaptureSession _session;
    private readonly TaskCompletionSource<Mat?> _firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;

    internal FreeThreadedWindowCapture(nint hwnd)
    {
        if (hwnd == 0)
        {
            throw new ArgumentException("Window handle is required.", nameof(hwnd));
        }

        _captureItem = CreateCaptureItemForWindow(hwnd);
        CaptureItemWidth = _captureItem.Size.Width;
        CaptureItemHeight = _captureItem.Size.Height;
        _direct3DDevice = CreateDirect3DDevice();
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _direct3DDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            _captureItem.Size);
        _framePool.FrameArrived += OnFrameArrived;
        _captureItem.Closed += OnCaptureItemClosed;
        _session = _framePool.CreateCaptureSession(_captureItem);
        _session.IsCursorCaptureEnabled = false;
        _session.StartCapture();
    }

    internal int CaptureItemWidth { get; }

    internal int CaptureItemHeight { get; }

    internal async Task<Mat?> CaptureAsync(TimeSpan timeout)
    {
        try
        {
            return await _firstFrame.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _framePool.FrameArrived -= OnFrameArrived;
            _captureItem.Closed -= OnCaptureItemClosed;
            _session.Dispose();
            _framePool.Dispose();
            _direct3DDevice.Dispose();
            _firstFrame.TrySetResult(null);
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (_disposed || _firstFrame.Task.IsCompleted)
        {
            return;
        }

        try
        {
            using Direct3D11CaptureFrame frame = sender.TryGetNextFrame();
            using SharpDX.Direct3D11.Texture2D texture = WinRtSurfaceInterop.CreateSharpDXTexture2D(frame.Surface);
            Mat image = CaptureInteropHelper.CreateMat(texture);
            if (image.Empty() || !_firstFrame.TrySetResult(image))
            {
                image.Dispose();
            }
        }
        catch (Exception ex)
        {
            _firstFrame.TrySetException(ex);
        }
    }

    private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args) =>
        _firstFrame.TrySetResult(null);

    private static GraphicsCaptureItem CreateCaptureItemForWindow(nint hwnd)
    {
        const string runtimeClass = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Guid interopGuid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
        Marshal.ThrowExceptionForHR(WindowsCreateString(runtimeClass, (uint)runtimeClass.Length, out nint className));
        try
        {
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(className, ref interopGuid, out nint factoryPointer));
            try
            {
                IGraphicsCaptureItemInterop interop =
                    (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPointer);
                Guid itemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
                nint itemPointer = interop.CreateForWindow(hwnd, ref itemGuid);
                return GraphicsCaptureItem.FromAbi(itemPointer);
            }
            finally
            {
                Marshal.Release(factoryPointer);
            }
        }
        finally
        {
            WindowsDeleteString(className);
        }
    }

    private static IDirect3DDevice CreateDirect3DDevice()
    {
        using SharpDX.Direct3D11.Device d3dDevice = new(
            SharpDX.Direct3D.DriverType.Hardware,
            SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);
        using SharpDX.DXGI.Device dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
        uint result = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out nint devicePointer);
        Marshal.ThrowExceptionForHR(unchecked((int)result));
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(devicePointer);
        }
        finally
        {
            Marshal.Release(devicePointer);
        }
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(
        nint dxgiDevice,
        out nint graphicsDevice);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out nint hString);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint hString);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(
        nint activatableClassId,
        [In] ref Guid iid,
        out nint factory);

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow([In] nint window, [In] ref Guid iid);

        nint CreateForMonitor([In] nint monitor, [In] ref Guid iid);
    }
}

internal static class WinRtSurfaceInterop
{
    private static readonly Guid Direct3DDxgiInterfaceAccessGuid =
        new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    private static readonly Guid Direct3D11Texture2DGuid =
        new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    internal static SharpDX.Direct3D11.Texture2D CreateSharpDXTexture2D(IDirect3DSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        nint surfacePointer = MarshalInspectable<IDirect3DSurface>.FromManaged(surface);
        nint accessPointer = 0;
        try
        {
            Guid accessGuid = Direct3DDxgiInterfaceAccessGuid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(surfacePointer, in accessGuid, out accessPointer));

            nint vtable = Marshal.ReadIntPtr(accessPointer);
            nint getInterfacePointer = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
            GetInterfaceDelegate getInterface =
                Marshal.GetDelegateForFunctionPointer<GetInterfaceDelegate>(getInterfacePointer);

            Guid textureGuid = Direct3D11Texture2DGuid;
            Marshal.ThrowExceptionForHR(getInterface(accessPointer, ref textureGuid, out nint texturePointer));
            return new SharpDX.Direct3D11.Texture2D(texturePointer);
        }
        finally
        {
            if (accessPointer != 0)
            {
                Marshal.Release(accessPointer);
            }

            MarshalInspectable<IDirect3DSurface>.DisposeAbi(surfacePointer);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetInterfaceDelegate(nint @this, [In] ref Guid iid, out nint instance);
}
