using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OneDragon.Core.Screening;
using OpenCvSharp;
using ZzzOd.AppHost.Backend;
using GeometryRect = OneDragon.Core.Abstractions.Geometry.Rect;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace ZzzOd.Gui.Overlay;

/// <summary>
/// 通过 Avalonia 渲染树获取进程内 Overlay 透明图层。
/// </summary>
internal sealed class ZzzAvaloniaOverlayCapturer : IOverlayCapturer
{
    private readonly ZzzOverlayController _controller;

    /// <summary>
    /// 初始化 Overlay 捕获器。
    /// </summary>
    /// <param name="controller">Overlay 窗口控制器。</param>
    public ZzzAvaloniaOverlayCapturer(ZzzOverlayController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    /// <inheritdoc />
    public IReadOnlyList<OverlayCaptureFrame> CaptureFrames()
    {
        return Dispatcher.UIThread.CheckAccess()
            ? CaptureFramesOnUiThread()
            : Dispatcher.UIThread.InvokeAsync(CaptureFramesOnUiThread).GetAwaiter().GetResult();
    }

    private IReadOnlyList<OverlayCaptureFrame> CaptureFramesOnUiThread()
    {
        Dispatcher.UIThread.VerifyAccess();
        ZzzBackendResult<ZzzWindowStatusDto> windowResult = _controller.GetGameWindowSnapshotForCapture();
        if (!windowResult.Success ||
            windowResult.Value is null ||
            !windowResult.Value.IsWinValid ||
            windowResult.Value.IsWinMinimized ||
            !windowResult.Value.X.HasValue ||
            !windowResult.Value.Y.HasValue ||
            windowResult.Value.Width is not > 0 ||
            windowResult.Value.Height is not > 0)
        {
            return [];
        }

        int gameX = windowResult.Value.X.Value;
        int gameY = windowResult.Value.Y.Value;
        int gameWidth = windowResult.Value.Width.Value;
        int gameHeight = windowResult.Value.Height.Value;
        List<OverlayCaptureFrame> frames = [];
        try
        {
            foreach (ZzzOverlayCaptureTarget target in _controller.GetCaptureTargets())
            {
                if (target.Size.Width <= 0 || target.Size.Height <= 0)
                {
                    continue;
                }

                Mat bgra = RenderWindowToBgra(target.Window, target.Size);
                try
                {
                    frames.Add(new OverlayCaptureFrame(
                        bgra,
                        new GeometryRect(
                            target.Position.X - gameX,
                            target.Position.Y - gameY,
                            target.Position.X - gameX + target.Size.Width,
                            target.Position.Y - gameY + target.Size.Height),
                        gameWidth,
                        gameHeight));
                }
                catch
                {
                    bgra.Dispose();
                    throw;
                }
            }

            return frames;
        }
        catch
        {
            foreach (OverlayCaptureFrame frame in frames)
            {
                frame.Dispose();
            }

            throw;
        }
    }

    internal static Mat RenderWindowToBgra(AvaloniaWindow window, PixelSize size)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Overlay 渲染尺寸必须为正数。");
        }

        double scaling = Math.Max(0.5d, window.DesktopScaling);
        using RenderTargetBitmap bitmap = new(size, new Vector(96d * scaling, 96d * scaling));
        bitmap.Render(window);
        using MemoryStream pngStream = new();
        bitmap.Save(pngStream);
        using Mat decoded = Cv2.ImDecode(pngStream.ToArray(), ImreadModes.Unchanged);
        if (decoded.Empty())
        {
            throw new InvalidOperationException("无法读取 Overlay 渲染结果。");
        }

        return ToBgra(decoded);
    }

    private static Mat ToBgra(Mat image)
    {
        if (image.Type() == MatType.CV_8UC4)
        {
            return image.Clone();
        }

        if (image.Type() == MatType.CV_8UC3)
        {
            Mat bgra = new();
            try
            {
                Cv2.CvtColor(image, bgra, ColorConversionCodes.BGR2BGRA);
                return bgra;
            }
            catch
            {
                bgra.Dispose();
                throw;
            }
        }

        throw new InvalidOperationException("Overlay 渲染结果不是 8 位 BGR 或 BGRA 图像。");
    }
}
