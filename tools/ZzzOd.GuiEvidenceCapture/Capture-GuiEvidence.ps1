[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('empty', 'data')]
    [string] $State,

    [Parameter(Mandatory = $true)]
    [string] $ExePath,

    [string] $WindowTitle = '',

    [Parameter(Mandatory = $true)]
    [string] $Page,

    [string] $Tab = '',

    [ValidateSet('Light', 'Dark', 'Default')]
    [string] $Theme = 'Light',

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [string] $MetadataPath,

    [string] $ControlTreePath,

    [string] $SourceRunRoot,

    [string] $AssetSourceRunRoot,

    [string] $CaptureProject = (Join-Path $PSScriptRoot 'ZzzOd.GuiEvidenceCapture.csproj'),

    [string] $WorkRoot = (Join-Path ([System.IO.Path]::GetTempPath()) 'zzzod-gui-evidence'),

    [ValidatePattern('^\d+x\d+$')]
    [string] $LogicalSize = '1140x760',

    [ValidateScript({ [string]::IsNullOrWhiteSpace($_) -or $_ -match '^\d+x\d+$' })]
    [string] $ExpectedPixelSize = '',

    [ValidateRange(0, 30)]
    [int] $SettleSeconds = 5,

    [switch] $PrepareOnly,

    [switch] $KeepSession
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GuiEvidenceDpi.ps1')

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [switch] $MustExist
    )

    $expanded = [Environment]::ExpandEnvironmentVariables($Path)
    if ($MustExist) {
        return (Resolve-Path -LiteralPath $expanded).Path
    }

    return [System.IO.Path]::GetFullPath($expanded)
}

function Test-PathInside {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Candidate,

        [Parameter(Mandatory = $true)]
        [string] $Parent
    )

    $candidatePath = [System.IO.Path]::GetFullPath($Candidate).TrimEnd('\') + '\'
    $parentPath = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    return $candidatePath.StartsWith($parentPath, [StringComparison]::OrdinalIgnoreCase)
}

function Get-FileInventory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return @()
    }

    return @(
        Get-ChildItem -LiteralPath $Root -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    relativePath = [System.IO.Path]::GetRelativePath($Root, $_.FullName)
                    length = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                }
            }
    )
}

function Compare-FileInventory {
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $Before,

        [Parameter(Mandatory = $true)]
        [object[]] $After
    )

    $beforeJson = ConvertTo-Json $Before -Depth 5 -Compress
    $afterJson = ConvertTo-Json $After -Depth 5 -Compress
    return $beforeJson -ceq $afterJson
}

function Get-InventoryDigest {
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $Inventory
    )

    $json = ConvertTo-Json $Inventory -Depth 5 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

function Stop-GuiProcess {
    param([System.Diagnostics.Process] $Process)

    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    try {
        $Process.CloseMainWindow() | Out-Null
        if (-not $Process.WaitForExit(4000)) {
            $Process.Kill()
            $Process.WaitForExit(4000) | Out-Null
        }
    }
    catch {
        if (-not $Process.HasExited) {
            $Process.Kill()
        }
    }
}

function Set-EvidenceEnvironment {
    $env:ZZZOD_GUI_EVIDENCE_SIZE = $LogicalSize
    $env:ZZZOD_GUI_EVIDENCE_PANE = 'expanded'
    $env:ZZZOD_GUI_EVIDENCE_PAGE = $Page
    $env:ZZZOD_GUI_EVIDENCE_TAB = $Tab
    $env:ZZZOD_GUI_THEME = if ($Theme -eq 'Default') { '' } else { $Theme }
    $env:ZZZOD_GUI_DEV_MODE = if ($Page -eq 'devtools') { '1' } else { '' }
    $env:ZZZOD_GUI_ENABLE_DIAGNOSTICS = ''
    $env:ZZZOD_GUI_EVIDENCE_CONTROL_TREE_PATH = $resolvedControlTreePath

    Remove-Item Env:\ZZZOD_GUI_EVIDENCE_RUN_STATE -ErrorAction SilentlyContinue
    Remove-Item Env:\ZZZOD_GUI_EVIDENCE_RUN_APP -ErrorAction SilentlyContinue
}

function Clear-EvidenceEnvironment {
    @(
        'ZZZOD_GUI_EVIDENCE_SIZE',
        'ZZZOD_GUI_EVIDENCE_PANE',
        'ZZZOD_GUI_EVIDENCE_PAGE',
        'ZZZOD_GUI_EVIDENCE_TAB',
        'ZZZOD_GUI_THEME',
        'ZZZOD_GUI_DEV_MODE',
        'ZZZOD_GUI_ENABLE_DIAGNOSTICS',
        'ZZZOD_GUI_EVIDENCE_CONTROL_TREE_PATH',
        'ZZZOD_GUI_EVIDENCE_RUN_STATE',
        'ZZZOD_GUI_EVIDENCE_RUN_APP'
    ) | ForEach-Object { Remove-Item "Env:\$_" -ErrorAction SilentlyContinue }
}

$resolvedExePath = Resolve-AbsolutePath -Path $ExePath -MustExist
$resolvedOutputPath = Resolve-AbsolutePath -Path $OutputPath
$resolvedCaptureProject = Resolve-AbsolutePath -Path $CaptureProject -MustExist
$resolvedWorkRoot = Resolve-AbsolutePath -Path $WorkRoot
$resolvedSystemTemp = Resolve-AbsolutePath -Path ([System.IO.Path]::GetTempPath())

if (-not (Test-PathInside -Candidate $resolvedWorkRoot -Parent $resolvedSystemTemp)) {
    throw "WorkRoot must stay under the system temporary directory: $resolvedSystemTemp"
}

if ([string]::IsNullOrWhiteSpace($MetadataPath)) {
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    $outputName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedOutputPath)
    $MetadataPath = Join-Path $outputDirectory "$outputName.capture-manifest.json"
}
$resolvedMetadataPath = Resolve-AbsolutePath -Path $MetadataPath
$resolvedControlTreePath = if ([string]::IsNullOrWhiteSpace($ControlTreePath)) { $null } else { Resolve-AbsolutePath -Path $ControlTreePath }

$resolvedSourceRunRoot = $null
$sourceConfigPath = $null
$sourceInventoryBefore = @()
if ($State -eq 'data') {
    if ([string]::IsNullOrWhiteSpace($SourceRunRoot)) {
        throw '-SourceRunRoot is required when -State data is used.'
    }

    $resolvedSourceRunRoot = Resolve-AbsolutePath -Path $SourceRunRoot -MustExist
    $sourceConfigPath = Join-Path $resolvedSourceRunRoot 'config'
    if (-not (Test-Path -LiteralPath $sourceConfigPath -PathType Container)) {
        throw "The data source does not contain a config directory: $sourceConfigPath"
    }

    if (Test-PathInside -Candidate $resolvedWorkRoot -Parent $resolvedSourceRunRoot) {
        throw 'WorkRoot must stay outside SourceRunRoot.'
    }

    $sourceInventoryBefore = @(Get-FileInventory -Root $sourceConfigPath)
}

$resolvedAssetSourceRunRoot = $null
$sourceAssetsPath = $null
$sourceAssetsInventoryBefore = @()
$sourceAssetsDigest = $null
if (-not [string]::IsNullOrWhiteSpace($AssetSourceRunRoot)) {
    $resolvedAssetSourceRunRoot = Resolve-AbsolutePath -Path $AssetSourceRunRoot -MustExist
    $sourceAssetsPath = Join-Path $resolvedAssetSourceRunRoot 'assets'
    if (-not (Test-Path -LiteralPath $sourceAssetsPath -PathType Container)) {
        throw "The asset source does not contain an assets directory: $sourceAssetsPath"
    }
    $sourceAssetsInventoryBefore = @(Get-FileInventory -Root $sourceAssetsPath)
    $sourceAssetsDigest = Get-InventoryDigest -Inventory $sourceAssetsInventoryBefore
}

$sessionId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([Guid]::NewGuid().ToString('N'))
$sessionRoot = Join-Path $resolvedWorkRoot $sessionId
$snapshotRoot = Join-Path $sessionRoot 'source-snapshot'
$snapshotConfigPath = Join-Path $snapshotRoot 'config'
$runRoot = Join-Path $sessionRoot 'run-root'
$runConfigPath = Join-Path $runRoot 'config'
$runAssetsPath = Join-Path $runRoot 'assets'
$assetSnapshotPath = $null
$assetSnapshotInventoryBefore = @()
$process = $null
$captureResult = $null
$actualPixelSize = $null
$dynamicExpectedPixelSize = $null
$dpiScale = $null
$status = if ($PrepareOnly) { 'prepared' } else { 'pending' }
$initializedInventory = @()
$failureMessage = $null

try {
    New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

    if ($State -eq 'data') {
        New-Item -ItemType Directory -Force -Path $snapshotRoot | Out-Null
        Copy-Item -LiteralPath $sourceConfigPath -Destination $snapshotConfigPath -Recurse
        Get-ChildItem -LiteralPath $snapshotConfigPath -Recurse -File | ForEach-Object { $_.IsReadOnly = $true }

        $snapshotInventory = @(Get-FileInventory -Root $snapshotConfigPath)
        if (-not (Compare-FileInventory -Before $sourceInventoryBefore -After $snapshotInventory)) {
            throw 'The copied config snapshot does not match the source inventory.'
        }

        Copy-Item -LiteralPath $snapshotConfigPath -Destination $runConfigPath -Recurse
        Get-ChildItem -LiteralPath $runConfigPath -Recurse -File | ForEach-Object { $_.IsReadOnly = $false }

        $runInventory = @(Get-FileInventory -Root $runConfigPath)
        if (-not (Compare-FileInventory -Before $snapshotInventory -After $runInventory)) {
            throw 'The temporary run root does not match the read-only config snapshot.'
        }
    }

    if ($null -ne $sourceAssetsPath) {
        $assetCacheRoot = Join-Path $resolvedWorkRoot 'asset-snapshot-cache'
        $assetSnapshotPath = Join-Path $assetCacheRoot $sourceAssetsDigest
        if (-not (Test-Path -LiteralPath $assetSnapshotPath -PathType Container)) {
            New-Item -ItemType Directory -Force -Path $assetCacheRoot | Out-Null
            $candidatePath = "$assetSnapshotPath.$([Guid]::NewGuid().ToString('N')).tmp"
            try {
                Copy-Item -LiteralPath $sourceAssetsPath -Destination $candidatePath -Recurse
                $candidateInventory = @(Get-FileInventory -Root $candidatePath)
                if ((Get-InventoryDigest -Inventory $candidateInventory) -ne $sourceAssetsDigest) {
                    throw 'The copied assets snapshot does not match the source inventory.'
                }
                Move-Item -LiteralPath $candidatePath -Destination $assetSnapshotPath
            }
            finally {
                if (Test-Path -LiteralPath $candidatePath) {
                    Remove-Item -LiteralPath $candidatePath -Recurse -Force
                }
            }
        }

        $assetSnapshotInventoryBefore = @(Get-FileInventory -Root $assetSnapshotPath)
        if ((Get-InventoryDigest -Inventory $assetSnapshotInventoryBefore) -ne $sourceAssetsDigest) {
            Remove-Item -LiteralPath $assetSnapshotPath -Recurse -Force
            throw 'The cached assets snapshot does not match the source inventory.'
        }

        New-Item -ItemType Junction -Path $runAssetsPath -Target $assetSnapshotPath | Out-Null
    }

    if (-not $PrepareOnly) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedOutputPath) | Out-Null
        Set-EvidenceEnvironment

        $process = Start-Process `
            -FilePath $resolvedExePath `
            -ArgumentList @('--run-root', $runRoot) `
            -WorkingDirectory $runRoot `
            -PassThru

        if ($SettleSeconds -gt 0) {
            Start-Sleep -Seconds $SettleSeconds
        }

        $captureArguments = @(
            'run',
            '--project',
            $resolvedCaptureProject,
            '--',
            '--output',
            $resolvedOutputPath,
            '--timeout-seconds',
            '30'
        )
        if (-not [string]::IsNullOrWhiteSpace($ExpectedPixelSize)) {
            $captureArguments += @('--expected-size', $ExpectedPixelSize)
        }
        if ([string]::IsNullOrWhiteSpace($WindowTitle)) {
            $captureArguments += @('--process-id', $process.Id)
        }
        else {
            $captureArguments += @('--title', $WindowTitle)
        }

        $captureOutput = & dotnet @captureArguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "GUI capture failed with exit code $LASTEXITCODE.`n$($captureOutput -join [Environment]::NewLine)"
        }

        $captureResult = ($captureOutput -join [Environment]::NewLine) | ConvertFrom-Json
        $windowDpi = [int] $captureResult.windowDpi
        $nativePixelSize = ConvertTo-GuiEvidenceNativePixelSize -LogicalSize $LogicalSize -WindowDpi $windowDpi
        $dpiScale = $nativePixelSize.Scale
        $dynamicExpectedWidth = $nativePixelSize.Width
        $dynamicExpectedHeight = $nativePixelSize.Height
        $dynamicExpectedPixelSize = $nativePixelSize.Text
        $actualPixelSize = "$($captureResult.width)x$($captureResult.height)"
        if ($captureResult.width -ne $dynamicExpectedWidth -or $captureResult.height -ne $dynamicExpectedHeight) {
            Remove-Item -LiteralPath $resolvedOutputPath -Force -ErrorAction SilentlyContinue
            throw "Captured frame does not match logical size and window DPI: logical=$LogicalSize, dpi=$windowDpi, expected=$dynamicExpectedPixelSize, actual=$actualPixelSize."
        }
        $status = 'captured'
    }

    if ($State -eq 'empty') {
        $initializedInventory = @(Get-FileInventory -Root $runConfigPath)
    }
}
catch {
    $status = 'failed'
    $failureMessage = $_.Exception.Message
    throw
}
finally {
    Stop-GuiProcess -Process $process
    Clear-EvidenceEnvironment

    $sourceInventoryAfter = if ($State -eq 'data') { @(Get-FileInventory -Root $sourceConfigPath) } else { @() }
    $sourceUnchanged = if ($State -eq 'data') {
        Compare-FileInventory -Before $sourceInventoryBefore -After $sourceInventoryAfter
    }
    else {
        $true
    }
    $sourceAssetsInventoryAfter = if ($null -ne $sourceAssetsPath) { @(Get-FileInventory -Root $sourceAssetsPath) } else { @() }
    $sourceAssetsUnchanged = if ($null -ne $sourceAssetsPath) {
        Compare-FileInventory -Before $sourceAssetsInventoryBefore -After $sourceAssetsInventoryAfter
    }
    else {
        $true
    }
    $assetSnapshotInventoryAfter = if ($null -ne $assetSnapshotPath -and (Test-Path -LiteralPath $assetSnapshotPath)) {
        @(Get-FileInventory -Root $assetSnapshotPath)
    }
    else {
        @()
    }
    $assetSnapshotUnchanged = if ($null -ne $assetSnapshotPath) {
        Compare-FileInventory -Before $assetSnapshotInventoryBefore -After $assetSnapshotInventoryAfter
    }
    else {
        $true
    }

    $manifest = [ordered]@{
        schema = 'zzzod-gui-evidence-capture.v1'
        status = $status
        capturedAt = (Get-Date).ToString('o')
        state = $State
        page = $Page
        tab = $Tab
        theme = $Theme
        logicalSize = $LogicalSize
        expectedPixelSize = if ([string]::IsNullOrWhiteSpace($ExpectedPixelSize)) { $null } else { $ExpectedPixelSize }
        dynamicExpectedPixelSize = $dynamicExpectedPixelSize
        actualPixelSize = $actualPixelSize
        dpiScale = $dpiScale
        settleSeconds = $SettleSeconds
        output = $resolvedOutputPath
        controlTree = if ([string]::IsNullOrWhiteSpace($ControlTreePath)) { $null } else { $resolvedControlTreePath }
        capture = $captureResult
        error = $failureMessage
        runRoot = [ordered]@{
            path = $runRoot
            temporary = $true
            sourceRunRoot = $resolvedSourceRunRoot
            sourceConfigPath = $sourceConfigPath
            readOnlySnapshotPath = if ($State -eq 'data') { $snapshotConfigPath } else { $null }
            sourceConfigReadOnlyCopy = ($State -eq 'data')
            sourceConfigUnchanged = $sourceUnchanged
            sourceFiles = $sourceInventoryBefore
            initializedFiles = $initializedInventory
        }
        assets = [ordered]@{
            sourceRunRoot = $resolvedAssetSourceRunRoot
            sourcePath = $sourceAssetsPath
            sourceFileCount = $sourceAssetsInventoryBefore.Count
            sourceDigest = $sourceAssetsDigest
            sourceUnchanged = $sourceAssetsUnchanged
            snapshotPath = $assetSnapshotPath
            snapshotFileCount = $assetSnapshotInventoryBefore.Count
            snapshotUnchanged = $assetSnapshotUnchanged
            runRootJunction = if ($null -ne $assetSnapshotPath) { $runAssetsPath } else { $null }
        }
        prohibitedDataInjection = [ordered]@{
            exampleApplications = $false
            examplePlans = $false
            exampleAccounts = $false
            exampleConfiguration = $false
            simulatedRunState = $false
        }
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedMetadataPath) | Out-Null
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedMetadataPath -Encoding utf8NoBOM

    if ($State -eq 'data' -and -not $sourceUnchanged) {
        throw "Source config changed during capture: $sourceConfigPath"
    }
    if (-not $sourceAssetsUnchanged) {
        throw "Source assets changed during capture: $sourceAssetsPath"
    }
    if (-not $assetSnapshotUnchanged) {
        if ($null -ne $assetSnapshotPath -and (Test-Path -LiteralPath $assetSnapshotPath)) {
            Remove-Item -LiteralPath $assetSnapshotPath -Recurse -Force
        }
        throw "Cached assets snapshot changed during capture: $assetSnapshotPath"
    }

    if (-not $KeepSession -and (Test-Path -LiteralPath $sessionRoot)) {
        if (-not [string]::IsNullOrWhiteSpace($runAssetsPath) -and (Test-Path -LiteralPath $runAssetsPath)) {
            $assetsItem = Get-Item -LiteralPath $runAssetsPath -Force
            if (($assetsItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                & cmd.exe /d /c rmdir $runAssetsPath
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to remove temporary assets junction: $runAssetsPath"
                }
            }
            else {
                Remove-Item -LiteralPath $runAssetsPath -Recurse -Force
            }
        }
        Get-ChildItem -LiteralPath $sessionRoot -Recurse -File -ErrorAction SilentlyContinue |
            ForEach-Object { $_.IsReadOnly = $false }
        Remove-Item -LiteralPath $sessionRoot -Recurse -Force
    }
}

Write-Output $resolvedMetadataPath
