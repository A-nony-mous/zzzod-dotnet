[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string] $ConfigRoot,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [int] $DurationSeconds = 30,

    [string[]] $RelativePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($DurationSeconds -lt 1) {
    throw 'DurationSeconds must be at least 1.'
}

$root = (Resolve-Path -LiteralPath $ConfigRoot).Path
$output = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [IO.Path]::GetDirectoryName($output)
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

$selectedFiles = if ($RelativePath -and $RelativePath.Count -gt 0) {
    foreach ($relative in $RelativePath) {
        $candidate = Join-Path $root $relative
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "YAML file does not exist: $relative"
        }
        (Resolve-Path -LiteralPath $candidate).Path
    }
} else {
    Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.yml' | Select-Object -ExpandProperty FullName
}

$selectedFiles = @($selectedFiles | Sort-Object -Unique)
if ($selectedFiles.Count -eq 0) {
    throw "No YAML files found under $root."
}

function Get-FileDigest {
    param([Parameter(Mandatory = $true)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string] $Path)
    [IO.Path]::GetRelativePath($root, $Path).Replace([IO.Path]::DirectorySeparatorChar, '/')
}

$state = @{}
foreach ($file in $selectedFiles) {
    $state[$file] = Get-FileDigest -Path $file
}

$startedAt = [DateTimeOffset]::Now
$changes = [Collections.Generic.List[object]]::new()
$deadline = [DateTime]::UtcNow.AddSeconds($DurationSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    foreach ($file in $selectedFiles) {
        $before = $state[$file]
        $after = Get-FileDigest -Path $file
        if ($before -ne $after) {
            $changes.Add([ordered]@{
                observedAt = [DateTimeOffset]::Now.ToString('O')
                relativePath = Get-RelativePath -Path $file
                oldSha256 = $before
                newSha256 = $after
            })
            $state[$file] = $after
        }
    }
    Start-Sleep -Milliseconds 100
}

$finishedAt = [DateTimeOffset]::Now
$result = [ordered]@{
    schema = 'zzzod-gui-yaml-watch.v1'
    status = 'captured'
    startedAt = $startedAt.ToString('O')
    finishedAt = $finishedAt.ToString('O')
    durationSeconds = $DurationSeconds
    configRoot = $root
    watchedFiles = @($selectedFiles | ForEach-Object { Get-RelativePath -Path $_ })
    contentChangeCount = $changes.Count
    contentChanges = @($changes)
    note = 'Only content hash changes are counted. The watcher does not write configuration or inject data.'
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $output -Encoding utf8
$result | ConvertTo-Json -Depth 8
