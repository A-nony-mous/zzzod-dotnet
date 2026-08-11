[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$WorkspaceRoot,

    [Parameter(Mandatory)]
    [string]$Output,

    [Parameter()]
    [string]$Rid = "win-x64",

    [Parameter()]
    [string]$RulesPath = (Join-Path $PSScriptRoot "asset-staging.json"),

    [Parameter()]
    [switch]$Audit,

    [Parameter()]
    [switch]$PlanOnly,

    [Parameter()]
    [int]$FailAfterCopy = -1,

    [Parameter()]
    [scriptblock]$AfterPlanAction,

    [Parameter()]
    [ValidateSet("None", "Hash", "Generator", "Manifest", "Swap")]
    [string]$InjectFailure = "None"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Throw-StagingError {
    param([string]$Code, [string]$Message)
    throw "[$Code] $Message"
}

function Get-NormalizedRelativePath {
    param([string]$Root, [string]$Path)

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $pathFull = [IO.Path]::GetFullPath($Path)
    $prefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        Throw-StagingError "source-path-escape" "源文件不在允许根目录内: $pathFull"
    }

    return $pathFull.Substring($prefix.Length).Replace([char]92, [char]47)
}

function ConvertTo-GlobRegex {
    param([string]$Pattern)

    $normalized = $Pattern.Replace([char]92, [char]47)
    $escaped = [Regex]::Escape($normalized)
    $escaped = $escaped.Replace("\*\*/", "(?:.*/)?")
    $escaped = $escaped.Replace("\*\*", ".*")
    $escaped = $escaped.Replace("\*", "[^/]*")
    $escaped = $escaped.Replace("\?", "[^/]")
    return "^$escaped$"
}

function Test-GlobMatch {
    param([string]$Path, [string[]]$Patterns)

    foreach ($pattern in @($Patterns)) {
        if ($Path -match (ConvertTo-GlobRegex ([string]$pattern))) {
            return $true
        }
    }

    return $false
}

function Test-ReparsePointInPath {
    param([string]$Root, [string]$Path)

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $current = [IO.Path]::GetFullPath($Path)
    while ($true) {
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Throw-StagingError "source-link-escape" "源文件位于符号链接或 junction 中: $Path"
        }

        if ([string]::Equals($current, $rootFull, [StringComparison]::OrdinalIgnoreCase)) {
            return
        }

        $parent = [IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrWhiteSpace($parent)) {
            Throw-StagingError "source-path-escape" "源文件不在允许根目录内: $Path"
        }

        $current = $parent
    }
}

function Test-SourceTreeNoLinks {
    param([string]$Root)

    foreach ($directory in @(Get-ChildItem -LiteralPath $Root -Directory -Recurse -Force)) {
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Throw-StagingError "source-link-escape" "源根目录包含符号链接或 junction: $($directory.FullName)"
        }
    }
}

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-TextSha256 {
    param([string]$Text)

    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash)
}

function Get-RuleProperty {
    param([object]$Object, [string]$Name, [string]$Code)

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        Throw-StagingError $Code "规则缺少 $Name。"
    }

    return $property.Value
}

function New-AssetFilePlan {
    param([object]$Rules, [string]$Workspace, [string]$TargetRid)

    if ([int](Get-RuleProperty $Rules "schemaVersion" "unknown-schema") -ne 1) {
        Throw-StagingError "unknown-schema" "不支持的规则 schemaVersion。"
    }

    $sourceRoots = Get-RuleProperty $Rules "sourceRoots" "invalid-rules"
    $categories = @(Get-RuleProperty $Rules "categories" "invalid-rules")
    $plan = [Collections.Generic.List[object]]::new()
    $auditEntries = [Collections.Generic.List[object]]::new()
    $targetPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $auditRulesByRoot = @{}

    foreach ($category in $categories) {
        $categoryName = [string](Get-RuleProperty $category "name" "invalid-rules")
        $sourceRootName = [string](Get-RuleProperty $category "sourceRoot" "invalid-rules")
        $sourceRootRelative = [string](Get-RuleProperty $sourceRoots $sourceRootName "invalid-rules")
        if ([IO.Path]::IsPathRooted($sourceRootRelative) -or $sourceRootRelative.Split([char]47, [char]92) -contains "..") {
            Throw-StagingError "target-path-escape" "源根目录必须是工作区内相对路径: $sourceRootRelative"
        }

        $sourceRootPath = [IO.Path]::GetFullPath((Join-Path $Workspace $sourceRootRelative))
        $workspacePrefix = $Workspace.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $sourceRootPath.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            Throw-StagingError "target-path-escape" "源根目录越出工作区: $sourceRootRelative"
        }

        if (-not (Test-Path -LiteralPath $sourceRootPath -PathType Container)) {
            Throw-StagingError "source-root-missing" "源根目录不存在: $sourceRootRelative"
        }

        Test-SourceTreeNoLinks $sourceRootPath

        $rids = @((Get-RuleProperty $category "rids" "invalid-rules") | ForEach-Object { [string]$_ })
        if ($rids -notcontains $TargetRid) {
            continue
        }

        $includes = @((Get-RuleProperty $category "include" "invalid-rules") | ForEach-Object { [string]$_ })
        $excludes = @((Get-RuleProperty $category "exclude" "invalid-rules") | ForEach-Object { [string]$_ })
        $requiredPatterns = @((Get-RuleProperty $category "required" "invalid-rules") | ForEach-Object { [string]$_ })
        if (-not $auditRulesByRoot.ContainsKey($sourceRootRelative)) {
            $auditRulesByRoot[$sourceRootRelative] = [Collections.Generic.List[object]]::new()
        }
        $auditRulesByRoot[$sourceRootRelative].Add([pscustomobject]@{ Includes = $includes; Excludes = $excludes; Category = $categoryName })
        $requiredMatches = @{}
        foreach ($pattern in $requiredPatterns) {
            $requiredMatches[$pattern] = 0
        }

        $allFiles = @(Get-ChildItem -LiteralPath $sourceRootPath -File -Recurse -Force)
        foreach ($file in $allFiles) {
            Test-ReparsePointInPath $sourceRootPath $file.FullName
            $relative = Get-NormalizedRelativePath $sourceRootPath $file.FullName
            $included = Test-GlobMatch $relative $includes
            $excluded = Test-GlobMatch $relative $excludes
            if (-not $included) {
                continue
            }

            if ($excluded) {
                continue
            }

            $isRequired = $false
            foreach ($pattern in $requiredPatterns) {
                if (Test-GlobMatch $relative @($pattern)) {
                    $requiredMatches[$pattern]++
                    $isRequired = $true
                }
            }

            $targetPath = "$sourceRootRelative/$relative"
            if ([IO.Path]::IsPathRooted($targetPath) -or $targetPath.Split("/") -contains "..") {
                Throw-StagingError "target-path-escape" "目标路径越界: $targetPath"
            }

            if (-not $targetPaths.Add($targetPath)) {
                Throw-StagingError "target-path-duplicate" "目标路径重复或仅大小写不同: $targetPath"
            }

            $plan.Add([pscustomobject]@{
                    SourcePath = [IO.Path]::GetFullPath($file.FullName)
                    TargetPath = $targetPath
                    Category = $categoryName
                    Required = $isRequired
                    Rids = $rids
                    Size = [int64]$file.Length
                    Sha256 = Get-Sha256 $file.FullName
                })
        }

        foreach ($pattern in $requiredPatterns) {
            if ($requiredMatches[$pattern] -eq 0) {
                Throw-StagingError "required-pattern-missing" "必需模式没有匹配文件: $sourceRootRelative/$pattern"
            }
        }
    }

    foreach ($sourceRootRelative in $auditRulesByRoot.Keys) {
        $sourceRootPath = [IO.Path]::GetFullPath((Join-Path $Workspace $sourceRootRelative))
        foreach ($file in @(Get-ChildItem -LiteralPath $sourceRootPath -File -Recurse -Force)) {
            Test-ReparsePointInPath $sourceRootPath $file.FullName
            $relative = Get-NormalizedRelativePath $sourceRootPath $file.FullName
            $included = $false
            $excluded = $false
            foreach ($auditRule in $auditRulesByRoot[$sourceRootRelative]) {
                if (-not (Test-GlobMatch $relative $auditRule.Includes)) {
                    continue
                }

                if (Test-GlobMatch $relative $auditRule.Excludes) {
                    $excluded = $true
                }
                else {
                    $included = $true
                }
            }

            if (-not $included) {
                $auditEntries.Add([pscustomobject]@{
                        Path = "$sourceRootRelative/$relative"
                        State = if ($excluded) { "excluded" } else { "unclassified" }
                        Category = ""
                    })
            }
        }
    }

    if ($plan.Count -eq 0) {
        Throw-StagingError "rid-empty" "RID $TargetRid 没有任何可发布文件。"
    }

    $planByPath = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $plan) {
        $planByPath.Add($entry.TargetPath, $entry)
    }

    [string[]]$sortedPaths = @($planByPath.Keys)
    [Array]::Sort($sortedPaths, [StringComparer]::Ordinal)
    $sortedPlan = @($sortedPaths | ForEach-Object { $planByPath[$_] })
    return [pscustomobject]@{ Plan = $sortedPlan; Audit = @($auditEntries | Sort-Object -Property Path, State) }
}

function Test-StagingOutput {
    param([string]$StagingRoot, [object[]]$Plan)

    $expectedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Plan) {
        $expectedPaths.Add($entry.TargetPath) | Out-Null
        $path = Join-Path $StagingRoot ($entry.TargetPath.Replace("/", [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            Throw-StagingError "staging-file-missing" "临时 staging 缺少文件: $($entry.TargetPath)"
        }

        $item = Get-Item -LiteralPath $path -Force
        if ($item.Length -ne $entry.Size -or (Get-Sha256 $path) -ne $entry.Sha256) {
            Throw-StagingError "staging-file-mismatch" "临时 staging 文件校验失败: $($entry.TargetPath)"
        }
    }

    $actualPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($managedRoot in @("assets", "config")) {
        $managedRootPath = Join-Path $StagingRoot $managedRoot
        if (-not (Test-Path -LiteralPath $managedRootPath -PathType Container)) {
            continue
        }

        foreach ($file in @(Get-ChildItem -LiteralPath $managedRootPath -File -Recurse -Force)) {
            $relative = Get-NormalizedRelativePath $StagingRoot $file.FullName
            if (-not $actualPaths.Add($relative)) {
                Throw-StagingError "staging-path-duplicate" "临时 staging 存在重复路径: $relative"
            }

            if (-not $expectedPaths.Contains($relative)) {
                Throw-StagingError "staging-extra-file" "临时 staging 存在未计划文件: $relative"
            }
        }
    }

    if ($actualPaths.Count -ne $expectedPaths.Count) {
        Throw-StagingError "staging-file-set-mismatch" "临时 staging 文件集合与文件计划不一致。"
    }
}

function Write-Manifest {
    param([string]$StagingRoot, [object[]]$Plan, [object]$Rules, [string]$TargetRid)

    $files = @($Plan | ForEach-Object {
            [ordered]@{
                path = $_.TargetPath
                category = $_.Category
                required = [bool]$_.Required
                rids = @($_.Rids)
                size = [int64]$_.Size
                sha256 = $_.Sha256
                generatedSource = "workspace-root"
            }
        })
    $sourceSummary = Get-TextSha256 (($files | ForEach-Object { "$($_.path)`n$($_.sha256)`n$($_.size)" }) -join "`n")
    $manifest = [ordered]@{
        schemaVersion = 1
        rid = $TargetRid
        generatedSource = "workspace-root"
        sourceSummary = $sourceSummary
        managedRoots = @($Rules.managedRoots)
        mutablePaths = @($Rules.mutablePaths)
        files = $files
    }
    $manifestPath = Join-Path $StagingRoot "assets-manifest.json"
    $json = $manifest | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    return $manifestPath
}

$workspaceFull = [IO.Path]::GetFullPath($WorkspaceRoot)
$outputFull = [IO.Path]::GetFullPath($Output)
$rulesFull = [IO.Path]::GetFullPath($RulesPath)
if ([string]::Equals($workspaceFull.TrimEnd([char]92, [char]47), $outputFull.TrimEnd([char]92, [char]47), [StringComparison]::OrdinalIgnoreCase)) {
    Throw-StagingError "invalid-output" "staging 输出目录不能是工作区根目录。"
}

if (-not (Test-Path -LiteralPath $rulesFull -PathType Leaf)) {
    Throw-StagingError "rules-missing" "规则文件不存在: $rulesFull"
}

$rules = Get-Content -LiteralPath $rulesFull -Raw | ConvertFrom-Json
$planResult = New-AssetFilePlan $rules $workspaceFull $Rid
if ($null -ne $AfterPlanAction) {
    & $AfterPlanAction $planResult.Plan
}

if ($InjectFailure -eq "Generator") {
    Throw-StagingError "injected-generator-failure" "已注入生成器异常。"
}

if ($PlanOnly) {
    [pscustomobject]@{
        Rid = $Rid
        Files = $planResult.Plan
        Audit = $planResult.Audit
    } | ConvertTo-Json -Depth 10
    return
}

$outputParent = [IO.Path]::GetDirectoryName($outputFull)
if ([string]::IsNullOrWhiteSpace($outputParent)) {
    Throw-StagingError "invalid-output" "输出目录没有父目录: $outputFull"
}

[IO.Directory]::CreateDirectory($outputParent) | Out-Null
$buildId = [Guid]::NewGuid().ToString("N")
$temporaryOutput = "$outputFull.$buildId.tmp"
$oldOutput = "$outputFull.$buildId.old"
$oldMoved = $false
$copied = 0

try {
    [IO.Directory]::CreateDirectory($temporaryOutput) | Out-Null
    foreach ($entry in $planResult.Plan) {
        $sourceItem = Get-Item -LiteralPath $entry.SourcePath -Force
        if ($InjectFailure -eq "Hash") {
            Throw-StagingError "injected-hash-failure" "已注入哈希读取失败。"
        }

        if ($sourceItem.Length -ne $entry.Size -or (Get-Sha256 $entry.SourcePath) -ne $entry.Sha256) {
            Throw-StagingError "source-changed" "文件计划冻结后源文件发生变化: $($entry.TargetPath)"
        }

        $destination = Join-Path $temporaryOutput ($entry.TargetPath.Replace("/", [IO.Path]::DirectorySeparatorChar))
        $destinationFull = [IO.Path]::GetFullPath($destination)
        $temporaryPrefix = $temporaryOutput.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $destinationFull.StartsWith($temporaryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            Throw-StagingError "target-path-escape" "目标路径越界: $($entry.TargetPath)"
        }

        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destinationFull)) | Out-Null
        [IO.File]::Copy($entry.SourcePath, $destinationFull, $false)
        if ((Get-Sha256 $destinationFull) -ne $entry.Sha256) {
            Throw-StagingError "copy-hash-mismatch" "复制后哈希不一致: $($entry.TargetPath)"
        }

        $copied++
        if ($FailAfterCopy -ge 0 -and $copied -gt $FailAfterCopy) {
            Throw-StagingError "injected-copy-failure" "已注入复制中断。"
        }
    }

    if ($InjectFailure -eq "Manifest") {
        Throw-StagingError "injected-manifest-failure" "已注入 manifest 写入失败。"
    }

    $manifestPath = Write-Manifest $temporaryOutput $planResult.Plan $rules $Rid
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Throw-StagingError "manifest-write-failed" "manifest 写入失败。"
    }

    Test-StagingOutput $temporaryOutput $planResult.Plan

    try {
        if ($InjectFailure -eq "Swap") {
            Throw-StagingError "injected-swap-failure" "已注入目录交换失败。"
        }

        if (Test-Path -LiteralPath $outputFull) {
            Move-Item -LiteralPath $outputFull -Destination $oldOutput -ErrorAction Stop
            $oldMoved = $true
        }

        Move-Item -LiteralPath $temporaryOutput -Destination $outputFull -ErrorAction Stop
    }
    catch {
        Throw-StagingError "directory-exchange-failed" "目录交换失败: $($_.Exception.Message)"
    }

    if ($oldMoved -and (Test-Path -LiteralPath $oldOutput)) {
        Remove-Item -LiteralPath $oldOutput -Recurse -Force -ErrorAction Stop
    }

    [pscustomobject]@{
        StagingPath = $outputFull
        ManifestPath = Join-Path $outputFull "assets-manifest.json"
        Rid = $Rid
        FileCount = $planResult.Plan.Count
        SourceSummary = (Get-Content -LiteralPath (Join-Path $outputFull "assets-manifest.json") -Raw | ConvertFrom-Json).sourceSummary
        Audit = $planResult.Audit
    } | ConvertTo-Json -Depth 8
}
catch {
    if (Test-Path -LiteralPath $temporaryOutput) {
        Remove-Item -LiteralPath $temporaryOutput -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($oldMoved -and -not (Test-Path -LiteralPath $outputFull) -and (Test-Path -LiteralPath $oldOutput)) {
        Move-Item -LiteralPath $oldOutput -Destination $outputFull -ErrorAction SilentlyContinue
    }

    throw
}
