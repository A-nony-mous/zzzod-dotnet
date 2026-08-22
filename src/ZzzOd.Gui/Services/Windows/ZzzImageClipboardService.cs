using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace ZzzOd.Gui.Services.Windows;

internal interface IZzzImageClipboardService
{
    Task CopyPngAsync(byte[] pngBytes, CancellationToken cancellationToken);
}

/// <summary>
/// 用 Win32 CF_DIB 同步写入剪贴板。
/// 不依赖 Avalonia OLE 延迟渲染，CloseClipboard 后数据立即可用。
/// </summary>
internal sealed class ZzzImageClipboardService : IZzzImageClipboardService
{
    private const uint CfDib = 8;
    private const uint CfDibV5 = 17;

    private readonly ILogger<ZzzImageClipboardService> _logger;

    public ZzzImageClipboardService(ILogger<ZzzImageClipboardService> logger)
    {
        _logger = logger;
    }

    public Task CopyPngAsync(byte[] pngBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        return Task.Run(() => CopyPng(pngBytes, cancellationToken), cancellationToken);
    }

    private void CopyPng(byte[] pngBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] dibBytes = EncodeDib(pngBytes);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OpenClipboard(IntPtr.Zero))
        {
            _logger.LogWarning("复制截图到剪贴板失败：OpenClipboard 返回 false");
            throw new InvalidOperationException("无法打开剪贴板。");
        }

        try
        {
            EmptyClipboard();
            nint hGlobal = CopyToHGlobal(dibBytes);
            if (hGlobal == 0)
            {
                _logger.LogWarning("复制截图到剪贴板失败：无法分配全局内存");
                throw new OutOfMemoryException("分配剪贴板内存失败。");
            }

            try
            {
                if (SetClipboardData(CfDib, hGlobal) == IntPtr.Zero)
                {
                    _logger.LogWarning("复制截图到剪贴板失败：SetClipboardData 返回零");
                    throw new InvalidOperationException("SetClipboardData 失败。");
                }

                hGlobal = IntPtr.Zero; // 成功移交剪贴板所有，不再释放
            }
            finally
            {
                if (hGlobal != IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                }
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static byte[] EncodeDib(byte[] pngBytes)
    {
        using Mat mat = Cv2.ImDecode(pngBytes, ImreadModes.Color);
        if (mat.Empty())
        {
            throw new InvalidDataException("游戏截图不是有效图像。");
        }

        Cv2.ImEncode(".bmp", mat, out byte[] bmpBytes);
        // BMP 文件头固定 14 字节（BITMAPFILEHEADER），其后是 BITMAPINFOHEADER + 像素 = CF_DIB。
        if (bmpBytes.Length < 14)
        {
            throw new InvalidDataException("BMP 编码结果不完整。");
        }

        return bmpBytes[14..];
    }

    private static nint CopyToHGlobal(byte[] bytes)
    {
        nint hGlobal = GlobalAlloc(0x0002 /* GMEM_MOVEABLE */, (nuint)bytes.Length);
        if (hGlobal == 0)
        {
            return 0;
        }

        nint pointer = GlobalLock(hGlobal);
        try
        {
            if (pointer == 0)
            {
                GlobalFree(hGlobal);
                return 0;
            }

            Marshal.Copy(bytes, 0, pointer, bytes.Length);
        }
        finally
        {
            GlobalUnlock(hGlobal);
        }

        return hGlobal;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
