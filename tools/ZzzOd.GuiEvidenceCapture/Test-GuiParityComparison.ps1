[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'GUI parity comparison tests require Windows System.Drawing.'
}

Add-Type -AssemblyName System.Drawing.Common

$testRoot = Join-Path $env:TEMP ('zzzod-gui-parity-comparison-' + [Guid]::NewGuid().ToString('N'))
$resolvedTemp = [IO.Path]::GetFullPath($env:TEMP).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
if (-not $resolvedTestRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Test root must remain under the system temp directory: $resolvedTestRoot"
}

[IO.Directory]::CreateDirectory($resolvedTestRoot) | Out-Null

try {
    $baselinePath = Join-Path $resolvedTestRoot 'baseline.png'
    $dotNetPath = Join-Path $resolvedTestRoot 'dotnet.png'
    $mismatchPath = Join-Path $resolvedTestRoot 'mismatch.png'

    $baseline = [System.Drawing.Bitmap]::new(2, 1, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $baseline.SetPixel(0, 0, [System.Drawing.Color]::FromArgb(20, 10, 0))
        $baseline.SetPixel(1, 0, [System.Drawing.Color]::FromArgb(1, 100, 255))
        $baseline.Save($baselinePath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $baseline.Dispose()
    }

    $dotNet = [System.Drawing.Bitmap]::new(2, 1, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $dotNet.SetPixel(0, 0, [System.Drawing.Color]::FromArgb(21, 11, 1))
        $dotNet.SetPixel(1, 0, [System.Drawing.Color]::FromArgb(2, 101, 0))
        $dotNet.Save($dotNetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $dotNet.Dispose()
    }

    $mismatch = [System.Drawing.Bitmap]::new(1, 1, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $mismatch.SetPixel(0, 0, [System.Drawing.Color]::Black)
        $mismatch.Save($mismatchPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $mismatch.Dispose()
    }

    $abPath = Join-Path $resolvedTestRoot 'ab.png'
    $overlayPath = Join-Path $resolvedTestRoot 'overlay.png'
    $reportPath = Join-Path $resolvedTestRoot 'diff.json'

    & (Join-Path $PSScriptRoot 'New-GuiParityComparison.ps1') `
        -BaselineImagePath $baselinePath `
        -DotNetImagePath $dotNetPath `
        -AbOutputPath $abPath `
        -OverlayOutputPath $overlayPath `
        -ReportPath $reportPath | Out-Null

    $ab = [System.Drawing.Bitmap]::new($abPath)
    try {
        if ($ab.Width -ne 4 -or $ab.Height -ne 1) {
            throw "Unexpected A/B dimensions: $($ab.Width)x$($ab.Height)"
        }
        if ($ab.GetPixel(0, 0).ToArgb() -ne [System.Drawing.Color]::FromArgb(20, 10, 0).ToArgb() -or
            $ab.GetPixel(2, 0).ToArgb() -ne [System.Drawing.Color]::FromArgb(21, 11, 1).ToArgb()) {
            throw 'A/B pixels were changed or reordered.'
        }
    }
    finally {
        $ab.Dispose()
    }

    $overlay = [System.Drawing.Bitmap]::new($overlayPath)
    try {
        if ($overlay.Width -ne 2 -or $overlay.Height -ne 1) {
            throw "Unexpected overlay dimensions: $($overlay.Width)x$($overlay.Height)"
        }
        if ($overlay.GetPixel(0, 0).ToArgb() -ne [System.Drawing.Color]::FromArgb(20, 10, 0).ToArgb() -or
            $overlay.GetPixel(1, 0).ToArgb() -ne [System.Drawing.Color]::FromArgb(1, 100, 127).ToArgb()) {
            throw '50% overlay does not use floor((baseline + dotnet) / 2) per RGB channel.'
        }
    }
    finally {
        $overlay.Dispose()
    }

    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($report.resampled -ne $false -or $report.expectedPixelSizeSource -ne 'baseline-input' -or
        $report.difference.totalPixels -ne 2 -or $report.difference.differentPixels -ne 2) {
        throw 'Difference report did not preserve the expected evidence metadata.'
    }

    $rejected = $false
    try {
        & (Join-Path $PSScriptRoot 'New-GuiParityComparison.ps1') `
            -BaselineImagePath $baselinePath `
            -DotNetImagePath $mismatchPath `
            -AbOutputPath (Join-Path $resolvedTestRoot 'invalid-ab.png') `
            -OverlayOutputPath (Join-Path $resolvedTestRoot 'invalid-overlay.png') `
            -ReportPath (Join-Path $resolvedTestRoot 'invalid.json') `
            -ExpectedPixelSize '2x1' | Out-Null
    }
    catch {
        $rejected = $_.Exception.Message -like '*Resampling is forbidden*'
    }

    if (-not $rejected) {
        throw 'Mismatched dimensions were not rejected.'
    }

    if (Test-Path (Join-Path $resolvedTestRoot 'invalid-ab.png')) {
        throw 'Rejected comparison produced an A/B image.'
    }

    [pscustomobject]@{
        status = 'passed'
        abDimensions = '4x1'
        overlayDimensions = '2x1'
        mismatchedDimensionsRejected = $true
        resampled = $false
    }
}
finally {
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
}

