[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$toolPath = Join-Path $PSScriptRoot 'Capture-GuiEvidence.ps1'
$captureProgramPath = Join-Path $PSScriptRoot 'Program.cs'
$dpiHelperPath = Join-Path $PSScriptRoot 'GuiEvidenceDpi.ps1'
$changeEvidenceRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\openspec\changes\fluentdesign-avalonia-260709\evidence')
$baselineScriptsRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\openspec\evidence\gui-parity-fluent-baseline\scripts')

$errors = $null
$tokens = $null
[System.Management.Automation.Language.Parser]::ParseFile($toolPath, [ref] $tokens, [ref] $errors) | Out-Null
if ($errors.Count -ne 0) {
    throw "Capture-GuiEvidence.ps1 has parser errors: $($errors.Message -join '; ')"
}

$toolText = Get-Content -Raw -LiteralPath $toolPath
$requiredTokens = @(
    "[ValidateSet('empty', 'data')]",
    "Copy-Item -LiteralPath `$sourceConfigPath",
    "Get-FileHash -LiteralPath",
    "--run-root",
    "--process-id",
    "dynamicExpectedPixelSize",
    "ConvertTo-GuiEvidenceNativePixelSize",
    "Start-Sleep -Seconds `$SettleSeconds",
    "Remove-Item Env:\ZZZOD_GUI_EVIDENCE_RUN_STATE",
    "sourceConfigUnchanged",
    "asset-snapshot-cache",
    "sourceAssetsUnchanged",
    "assetSnapshotUnchanged",
    "simulatedRunState = `$false"
)
foreach ($requiredToken in $requiredTokens) {
    if (-not $toolText.Contains($requiredToken, [StringComparison]::Ordinal)) {
        throw "Capture tool is missing required guard: $requiredToken"
    }
}

. $dpiHelperPath
$dpiCases = @(
    @{ Dpi = 96; Expected = '1140x760' },
    @{ Dpi = 120; Expected = '1425x950' },
    @{ Dpi = 144; Expected = '1710x1140' },
    @{ Dpi = 192; Expected = '2280x1520' }
)
foreach ($dpiCase in $dpiCases) {
    $actual = ConvertTo-GuiEvidenceNativePixelSize -LogicalSize '1140x760' -WindowDpi $dpiCase.Dpi
    if ($actual.Text -ne $dpiCase.Expected) {
        throw "DPI formula mismatch for $($dpiCase.Dpi): expected $($dpiCase.Expected), actual $($actual.Text)"
    }
}

$forbiddenTokens = @(
    'Write-Utf8File',
    'Initialize-RunRoot',
    '全配队通用',
    'main@example.com',
    'local-secret',
    'app_list:',
    'plan_list:',
    'instance_list:'
)
foreach ($forbiddenToken in $forbiddenTokens) {
    if ($toolText.Contains($forbiddenToken, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Capture tool contains prohibited data injection text: $forbiddenToken"
    }
}

$captureProgramText = Get-Content -Raw -LiteralPath $captureProgramPath
$requiredInteropTokens = @(
    'MarshalInspectable<IDirect3DSurface>.FromManaged(surface)',
    'Marshal.QueryInterface(surfacePointer, in accessGuid, out accessPointer)',
    'Marshal.GetDelegateForFunctionPointer<GetInterfaceDelegate>(getInterfacePointer)'
)
foreach ($requiredInteropToken in $requiredInteropTokens) {
    if (-not $captureProgramText.Contains($requiredInteropToken, [StringComparison]::Ordinal)) {
        throw "Capture program is missing CsWinRT ABI interop: $requiredInteropToken"
    }
}

if ($captureProgramText.Contains('CaptureInteropHelper.CreateSharpDXTexture2D(frame.Surface)', [StringComparison]::Ordinal)) {
    throw 'Capture program still casts the CsWinRT surface through legacy RCW interop.'
}

$legacyScripts = @(
    Get-ChildItem -LiteralPath $changeEvidenceRoot -File -Filter '*.ps1'
    Get-ChildItem -LiteralPath $baselineScriptsRoot -File -Filter '*.ps1'
) | Where-Object { $_.Name -ne 'Test-GuiEvidenceTool.ps1' }

foreach ($legacyScript in $legacyScripts) {
    $legacyText = Get-Content -Raw -LiteralPath $legacyScript.FullName
    if (-not $legacyText.Contains('LEGACY_GUI_EVIDENCE_ENTRY_FROZEN', [StringComparison]::Ordinal)) {
        throw "Legacy evidence entry is not frozen: $($legacyScript.FullName)"
    }
}

[pscustomobject]@{
    tool = $toolPath
    legacyEntriesChecked = $legacyScripts.Count
    parserErrors = 0
    prohibitedTokens = 0
    csWinRtAbiInterop = $true
    dpiFormulaCases = $dpiCases.Count
} | ConvertTo-Json -Depth 3
