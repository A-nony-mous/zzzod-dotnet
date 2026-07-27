[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BaselineImagePath,

    [Parameter(Mandatory = $true)]
    [string] $DotNetImagePath,

    [Parameter(Mandatory = $true)]
    [string] $AbOutputPath,

    [Parameter(Mandatory = $true)]
    [string] $OverlayOutputPath,

    [Parameter(Mandatory = $true)]
    [string] $ReportPath,

    [ValidateScript({ [string]::IsNullOrWhiteSpace($_) -or $_ -match '^\d+x\d+$' })]
    [string] $ExpectedPixelSize = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'GUI parity comparison requires Windows System.Drawing.'
}

$expectedWidth = $null
$expectedHeight = $null
$expectedPixelSizeSource = 'explicit'
if (-not [string]::IsNullOrWhiteSpace($ExpectedPixelSize)) {
    $ExpectedPixelSize -match '^(?<width>\d+)x(?<height>\d+)$' | Out-Null
    $expectedWidth = [int] $Matches.width
    $expectedHeight = [int] $Matches.height
}
$baselinePath = (Resolve-Path -LiteralPath $BaselineImagePath).Path
$dotNetPath = (Resolve-Path -LiteralPath $DotNetImagePath).Path
$abPath = [IO.Path]::GetFullPath($AbOutputPath)
$overlayPath = [IO.Path]::GetFullPath($OverlayOutputPath)
$reportFullPath = [IO.Path]::GetFullPath($ReportPath)

foreach ($path in @($abPath, $overlayPath, $reportFullPath)) {
    $directory = [IO.Path]::GetDirectoryName($path)
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    }
}

Add-Type -AssemblyName System.Drawing.Common

if ($null -eq $expectedWidth -or $null -eq $expectedHeight) {
    $inputImage = [System.Drawing.Image]::FromFile($baselinePath)
    try {
        $expectedWidth = $inputImage.Width
        $expectedHeight = $inputImage.Height
        $ExpectedPixelSize = "$($expectedWidth)x$($expectedHeight)"
        $expectedPixelSizeSource = 'baseline-input'
    }
    finally {
        $inputImage.Dispose()
    }
}

if (-not ('ZzzOd.GuiEvidence.GuiParityImageProcessor' -as [type])) {
    $drawingAssembly = [System.Drawing.Bitmap].Assembly.Location
    $drawingPrimitivesAssembly = [System.Drawing.Rectangle].Assembly.Location
    $windowsCoreAssembly = Join-Path $PSHOME 'System.Private.Windows.Core.dll'
    $windowsGdiPlusAssembly = Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'
    Add-Type -ReferencedAssemblies @(
        $drawingAssembly,
        $drawingPrimitivesAssembly,
        $windowsCoreAssembly,
        $windowsGdiPlusAssembly
    ) -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ZzzOd.GuiEvidence
{
    public sealed class GuiParityMetrics
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public long TotalPixels { get; set; }
        public long DifferentPixels { get; set; }
        public double DifferentPixelRatio { get; set; }
        public double MeanAbsoluteChannelError { get; set; }
        public double BlueMeanAbsoluteError { get; set; }
        public double GreenMeanAbsoluteError { get; set; }
        public double RedMeanAbsoluteError { get; set; }
        public double RootMeanSquareChannelError { get; set; }
        public int MaximumChannelError { get; set; }
        public int? DifferenceLeft { get; set; }
        public int? DifferenceTop { get; set; }
        public int? DifferenceRight { get; set; }
        public int? DifferenceBottom { get; set; }
    }

    public static class GuiParityImageProcessor
    {
        public static GuiParityMetrics Process(
            string baselinePath,
            string dotNetPath,
            string abPath,
            string overlayPath,
            int expectedWidth,
            int expectedHeight)
        {
            using var baselineSource = new Bitmap(baselinePath);
            using var dotNetSource = new Bitmap(dotNetPath);

            if (baselineSource.Width != dotNetSource.Width || baselineSource.Height != dotNetSource.Height)
                throw new InvalidOperationException(
                    $"Input dimensions differ: Baseline={baselineSource.Width}x{baselineSource.Height}, .NET={dotNetSource.Width}x{dotNetSource.Height}. Resampling is forbidden.");

            if (baselineSource.Width != expectedWidth || baselineSource.Height != expectedHeight)
                throw new InvalidOperationException(
                    $"Input dimensions do not match the explicit expected size: actual={baselineSource.Width}x{baselineSource.Height}, expected={expectedWidth}x{expectedHeight}. Resampling is forbidden.");

            using var baseline = NormalizeWithoutScaling(baselineSource);
            using var dotNet = NormalizeWithoutScaling(dotNetSource);
            var baselineBytes = ReadPixels(baseline);
            var dotNetBytes = ReadPixels(dotNet);
            var overlayBytes = new byte[baselineBytes.Length];

            long differentPixels = 0;
            long absoluteError = 0;
            var channelAbsoluteError = new long[3];
            double squaredError = 0;
            int maximumError = 0;
            var differenceLeft = expectedWidth;
            var differenceTop = expectedHeight;
            var differenceRight = -1;
            var differenceBottom = -1;

            for (var offset = 0; offset < baselineBytes.Length; offset += 4)
            {
                var pixelDiffers = false;
                for (var channel = 0; channel < 3; channel++)
                {
                    var difference = Math.Abs(baselineBytes[offset + channel] - dotNetBytes[offset + channel]);
                    if (difference != 0)
                        pixelDiffers = true;
                    absoluteError += difference;
                    channelAbsoluteError[channel] += difference;
                    squaredError += difference * difference;
                    if (difference > maximumError)
                        maximumError = difference;
                    overlayBytes[offset + channel] = (byte)((baselineBytes[offset + channel] + dotNetBytes[offset + channel]) / 2);
                }

                overlayBytes[offset + 3] = 255;
                if (pixelDiffers)
                {
                    differentPixels++;
                    var pixelIndex = offset / 4;
                    var x = pixelIndex % expectedWidth;
                    var y = pixelIndex / expectedWidth;
                    if (x < differenceLeft) differenceLeft = x;
                    if (y < differenceTop) differenceTop = y;
                    if (x > differenceRight) differenceRight = x;
                    if (y > differenceBottom) differenceBottom = y;
                }
            }

            using (var ab = new Bitmap(expectedWidth * 2, expectedHeight, PixelFormat.Format24bppRgb))
            using (var graphics = Graphics.FromImage(ab))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(baseline, 0, 0);
                graphics.DrawImageUnscaled(dotNet, expectedWidth, 0);
                ab.Save(abPath, ImageFormat.Png);
            }

            using (var overlay = new Bitmap(expectedWidth, expectedHeight, PixelFormat.Format24bppRgb))
            {
                WriteRgbPixels(overlay, overlayBytes);
                overlay.Save(overlayPath, ImageFormat.Png);
            }

            var totalPixels = (long)expectedWidth * expectedHeight;
            var channelCount = totalPixels * 3;
            return new GuiParityMetrics
            {
                Width = expectedWidth,
                Height = expectedHeight,
                TotalPixels = totalPixels,
                DifferentPixels = differentPixels,
                DifferentPixelRatio = totalPixels == 0 ? 0 : (double)differentPixels / totalPixels,
                MeanAbsoluteChannelError = channelCount == 0 ? 0 : (double)absoluteError / channelCount,
                BlueMeanAbsoluteError = totalPixels == 0 ? 0 : (double)channelAbsoluteError[0] / totalPixels,
                GreenMeanAbsoluteError = totalPixels == 0 ? 0 : (double)channelAbsoluteError[1] / totalPixels,
                RedMeanAbsoluteError = totalPixels == 0 ? 0 : (double)channelAbsoluteError[2] / totalPixels,
                RootMeanSquareChannelError = channelCount == 0 ? 0 : Math.Sqrt(squaredError / channelCount),
                MaximumChannelError = maximumError,
                DifferenceLeft = differentPixels == 0 ? null : differenceLeft,
                DifferenceTop = differentPixels == 0 ? null : differenceTop,
                DifferenceRight = differentPixels == 0 ? null : differenceRight,
                DifferenceBottom = differentPixels == 0 ? null : differenceBottom
            };
        }

        private static Bitmap NormalizeWithoutScaling(Bitmap source)
        {
            var normalized = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(normalized);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(source, 0, 0);
            return normalized;
        }

        private static byte[] ReadPixels(Bitmap bitmap)
        {
            var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void WriteRgbPixels(Bitmap bitmap, byte[] bgraBytes)
        {
            var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                var output = new byte[Math.Abs(data.Stride) * bitmap.Height];
                for (var y = 0; y < bitmap.Height; y++)
                {
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var sourceOffset = (y * bitmap.Width + x) * 4;
                        var targetOffset = y * Math.Abs(data.Stride) + x * 3;
                        output[targetOffset] = bgraBytes[sourceOffset];
                        output[targetOffset + 1] = bgraBytes[sourceOffset + 1];
                        output[targetOffset + 2] = bgraBytes[sourceOffset + 2];
                    }
                }
                Marshal.Copy(output, 0, data.Scan0, output.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
'@
}

foreach ($output in @($abPath, $overlayPath, $reportFullPath)) {
    Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
}

$metrics = [ZzzOd.GuiEvidence.GuiParityImageProcessor]::Process(
    $baselinePath,
    $dotNetPath,
    $abPath,
    $overlayPath,
    $expectedWidth,
    $expectedHeight)

$report = [ordered]@{
    schemaVersion = 1
    generatedAt = [DateTimeOffset]::Now.ToString('O')
    expectedPixelSize = $ExpectedPixelSize
    expectedPixelSizeSource = $expectedPixelSizeSource
    resampled = $false
    overlayAlpha = 0.5
    inputs = [ordered]@{
        baseline = [ordered]@{
            path = $baselinePath
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $baselinePath).Hash
        }
        dotnet = [ordered]@{
            path = $dotNetPath
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dotNetPath).Hash
        }
    }
    outputs = [ordered]@{
        ab = [ordered]@{
            path = $abPath
            width = $expectedWidth * 2
            height = $expectedHeight
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $abPath).Hash
        }
        overlay50 = [ordered]@{
            path = $overlayPath
            width = $expectedWidth
            height = $expectedHeight
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $overlayPath).Hash
        }
    }
    difference = [ordered]@{
        totalPixels = $metrics.TotalPixels
        differentPixels = $metrics.DifferentPixels
        differentPixelRatio = $metrics.DifferentPixelRatio
        meanAbsoluteChannelError = $metrics.MeanAbsoluteChannelError
        channelMeanAbsoluteError = [ordered]@{
            blue = $metrics.BlueMeanAbsoluteError
            green = $metrics.GreenMeanAbsoluteError
            red = $metrics.RedMeanAbsoluteError
        }
        rootMeanSquareChannelError = $metrics.RootMeanSquareChannelError
        maximumChannelError = $metrics.MaximumChannelError
        boundingBox = if ($null -eq $metrics.DifferenceLeft) {
            $null
        }
        else {
            [ordered]@{
                left = $metrics.DifferenceLeft
                top = $metrics.DifferenceTop
                right = $metrics.DifferenceRight
                bottom = $metrics.DifferenceBottom
            }
        }
    }
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFullPath -Encoding utf8
$report

