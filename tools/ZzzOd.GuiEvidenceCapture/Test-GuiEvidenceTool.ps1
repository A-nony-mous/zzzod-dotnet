[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$toolPath = Join-Path $PSScriptRoot 'Capture-GuiEvidence.ps1'
$captureProgramPath = Join-Path $PSScriptRoot 'Program.cs'
$dpiHelperPath = Join-Path $PSScriptRoot 'GuiEvidenceDpi.ps1'
$legacyEntryRoots = @(
    Join-Path $PSScriptRoot '..\..\..\openspec\changes\fluentdesign-avalonia-260709\evidence'
    Join-Path $PSScriptRoot '..\..\..\openspec\evidence\gui-parity-fluent-baseline\scripts'
) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }

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
    "sourceAssetsUnchanged",
    "-WorkspaceRoot `$resolvedSourceRunRoot",
    "-ExistingRunRoot",
    "cannot be used together",
    "cannot use -ExistingRunRoot",
    "assets-manifest.json",
    "Set-GuiHealthApiConfig",
    "Wait-GuiHealth",
    "manifestSourceSummary",
    "GUI health run-root does not match staging",
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
    'instance_list:',
    'AssetSourceRunRoot',
    'asset-snapshot-cache',
    'New-Item -ItemType Junction'
)
foreach ($forbiddenToken in $forbiddenTokens) {
    if ($toolText.Contains($forbiddenToken, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Capture tool contains prohibited data injection text: $forbiddenToken"
    }
}

$negativeTestRoot = Join-Path ([IO.Path]::GetTempPath()) ("zzzod-gui-evidence-negative-" + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force -Path $negativeTestRoot | Out-Null
    $invalidRulesPath = Join-Path $negativeTestRoot 'asset-staging.invalid.json'
    $markerPath = Join-Path $negativeTestRoot 'gui-started.marker'
    $markerCommandPath = Join-Path $negativeTestRoot 'gui-started.cmd'
    $metadataPath = Join-Path $negativeTestRoot 'capture-manifest.json'
    $workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
    Set-Content -LiteralPath $invalidRulesPath -Value '{ invalid json' -Encoding utf8NoBOM
    Set-Content -LiteralPath $markerCommandPath -Value "@echo launched>$markerPath" -Encoding ascii

    $failure = $null
    try {
        & $toolPath `
            -State empty `
            -ExePath $markerCommandPath `
            -Page home `
            -OutputPath (Join-Path $negativeTestRoot 'unexpected.png') `
            -MetadataPath $metadataPath `
            -SourceRunRoot $workspaceRoot `
            -StagingRulesPath $invalidRulesPath `
            -WorkRoot (Join-Path $negativeTestRoot 'work') `
            -SettleSeconds 0
    }
    catch {
        $failure = $_
    }

    if ($null -eq $failure) {
        throw 'Invalid staging rules unexpectedly allowed GUI evidence capture to continue.'
    }
    if (Test-Path -LiteralPath $markerPath) {
        throw 'GUI process started after staging failed.'
    }
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
        throw 'Failed staging did not write capture metadata.'
    }
    $negativeManifest = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if ($negativeManifest.status -ne 'failed' -or $null -ne $negativeManifest.guiHealth) {
        throw 'Failed staging capture metadata has an unexpected status or GUI health result.'
    }
}
finally {
    if (Test-Path -LiteralPath $negativeTestRoot -PathType Container) {
        Remove-Item -LiteralPath $negativeTestRoot -Recurse -Force
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

$legacyScripts = @($legacyEntryRoots | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -File -Filter '*.ps1'
}) | Where-Object { $_.Name -ne 'Test-GuiEvidenceTool.ps1' }

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
    failedStagingBlockedGui = $true
} | ConvertTo-Json -Depth 3
