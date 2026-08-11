[CmdletBinding()]
param(
    [Parameter()]
    [string]$StagingScript = (Join-Path $PSScriptRoot "Invoke-AssetStaging.ps1")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Equal {
    param([object]$Expected, [object]$Actual, [string]$Message)

    if ($Expected -ne $Actual) {
        throw "$Message Expected: $Expected; Actual: $Actual"
    }
}

function Assert-ThrowsCode {
    param([scriptblock]$Action, [string]$Code)

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message.Contains("[$Code]", [StringComparison]::Ordinal)) {
            return
        }

        throw "预期错误码 $Code，实际错误: $($_.Exception.Message)"
    }

    throw "预期错误码 $Code，但命令成功。"
}

function Write-TestFile {
    param([string]$Root, [string]$RelativePath, [string]$Content)

    $path = Join-Path $Root ($RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($false))
}

function Write-Rules {
    param([string]$Path, [object]$Rules)

    [IO.File]::WriteAllText($Path, ($Rules | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}

function New-Category {
    param([string]$Name, [string]$SourceRoot, [string[]]$Rids, [string[]]$Required = @("data.yml"), [string[]]$Include = @("data.yml"))

    return [ordered]@{
        name = $Name
        sourceRoot = $SourceRoot
        include = $Include
        exclude = @()
        required = $Required
        rids = $Rids
    }
}

function New-Rules {
    param([hashtable]$SourceRoots, [object[]]$Categories)

    return [ordered]@{
        schemaVersion = 1
        generator = "test"
        sourceRoots = $SourceRoots
        managedRoots = @("assets")
        mutablePaths = @()
        categories = $Categories
    }
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("zzzod-asset-staging-tests-" + [Guid]::NewGuid().ToString("N"))
$externalRoot = Join-Path ([IO.Path]::GetTempPath()) ("zzzod-asset-staging-external-" + [Guid]::NewGuid().ToString("N"))
try {
    [IO.Directory]::CreateDirectory($testRoot) | Out-Null
    [IO.Directory]::CreateDirectory($externalRoot) | Out-Null
    Write-TestFile $testRoot "assets/data.yml" "data"
    Write-TestFile $testRoot "assets/arm.yml" "arm"
    $rulesPath = Join-Path $testRoot "rules.json"
    $outputPath = Join-Path $testRoot "stage"

    Write-Rules $rulesPath (New-Rules @{ assets = "assets" } @(
            (New-Category "x64" "assets" @("win-x64") @("data.yml") @("data.yml")),
            (New-Category "arm" "assets" @("win-arm64") @("arm.yml") @("arm.yml"))))
    $ridPlan = & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly | ConvertFrom-Json
    Assert-Equal 1 $ridPlan.Files.Count "RID 过滤结果错误。"
    Assert-Equal "assets/data.yml" $ridPlan.Files[0].TargetPath "RID 过滤保留了错误文件。"

    Write-TestFile $testRoot "assets/中文.yml" "中文"
    Write-Rules $rulesPath (New-Rules @{ assets = "assets" } @((New-Category "ordinal" "assets" @("win-x64") @("data.yml") @("data.yml", "中文.yml"))))
    $ordinalPlan = & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly | ConvertFrom-Json
    Assert-Equal "assets/data.yml" $ordinalPlan.Files[0].TargetPath "文件计划没有按 Ordinal 排序。"
    Assert-Equal "assets/中文.yml" $ordinalPlan.Files[1].TargetPath "中文文件没有进入文件计划。"

    Write-TestFile $testRoot "assets/新增.yml" "new"
    Write-TestFile $testRoot "assets/runtime.log" "log"
    Write-TestFile $testRoot "assets/cache.cache" "cache"
    Write-TestFile $testRoot "assets/merged.yml" "merged"
    Write-TestFile $testRoot "assets/unknown.merged.yml" "merged"
    $excludedCategory = New-Category "excluded" "assets" @("win-x64") @() @("*.log", "*.cache", "merged.yml", "*.merged.yml")
    $excludedCategory.exclude = @("*.log", "*.cache", "merged.yml", "*.merged.yml")
    Write-Rules $rulesPath (New-Rules @{ assets = "assets" } @(
            (New-Category "audit" "assets" @("win-x64") @("data.yml") @("data.yml")),
            $excludedCategory))
    $auditPlan = & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly | ConvertFrom-Json
    Assert-Equal 1 @($auditPlan.Audit | Where-Object { $_.State -eq "unclassified" -and $_.Path -eq "assets/新增.yml" }).Count "新增 YAML 未被审计为未分类。"
    Assert-Equal 4 @($auditPlan.Audit | Where-Object { $_.State -eq "excluded" }).Count "日志、缓存和聚合 YAML 未被审计为排除项。"

    Write-Rules $rulesPath (New-Rules @{ assets = "assets" } @((New-Category "missing-required" "assets" @("win-x64") @("missing.yml"))))
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly } "required-pattern-missing"

    $unknownSchema = New-Rules @{ assets = "assets" } @((New-Category "schema" "assets" @("win-x64")))
    $unknownSchema.schemaVersion = 99
    Write-Rules $rulesPath $unknownSchema
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly } "unknown-schema"

    Write-Rules $rulesPath (New-Rules @{ first = "assets"; second = "assets" } @(
            (New-Category "first" "first" @("win-x64")),
            (New-Category "second" "second" @("win-x64"))))
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly } "target-path-duplicate"

    Write-Rules $rulesPath (New-Rules @{ assets = "C:\\staging-escape" } @((New-Category "absolute" "assets" @("win-x64"))))
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly } "target-path-escape"

    Write-Rules $rulesPath (New-Rules @{ assets = "../assets" } @((New-Category "parent" "assets" @("win-x64"))))
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly } "target-path-escape"

    Write-TestFile $externalRoot "outside.yml" "outside"
    $junctionPath = Join-Path $testRoot "assets\outside-link"
    New-Item -ItemType Junction -Path $junctionPath -Target $externalRoot | Out-Null
    Write-Rules $rulesPath (New-Rules @{ assets = "assets" } @((New-Category "junction" "assets" @("win-x64"))))
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly } "source-link-escape"
    [IO.Directory]::Delete($junctionPath, $false)

    Write-Rules $rulesPath (New-Rules @{ lower = "assets"; upper = "ASSETS" } @(
            (New-Category "lower" "lower" @("win-x64")),
            (New-Category "upper" "upper" @("win-x64"))))
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -PlanOnly } "target-path-duplicate"

    Write-Rules $rulesPath (New-Rules @{ assets = "assets" } @((New-Category "stage" "assets" @("win-x64"))))
    [IO.Directory]::CreateDirectory($outputPath) | Out-Null
    Write-TestFile $outputPath "obsolete.yml" "obsolete"
    $stagingOutput = & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" | ConvertFrom-Json
    Assert-Equal ([IO.Path]::GetFullPath($outputPath)) $stagingOutput.StagingPath "staging 输出路径不正确。"
    Assert-Equal (Join-Path ([IO.Path]::GetFullPath($outputPath)) "assets-manifest.json") $stagingOutput.ManifestPath "staging manifest 路径不正确。"
    Assert-Equal "win-x64" $stagingOutput.Rid "staging RID 不正确。"
    if ([string]::IsNullOrWhiteSpace($stagingOutput.SourceSummary)) {
        throw "staging 输出缺少来源摘要。"
    }
    if (Test-Path -LiteralPath (Join-Path $outputPath "obsolete.yml")) {
        throw "clean staging 保留了旧文件。"
    }

    $manifestPath = Join-Path $outputPath "assets-manifest.json"
    $before = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -FailAfterCopy 0 } "injected-copy-failure"
    $after = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    Assert-Equal $before $after "复制中断改变了上一个有效 staging。"

    foreach ($injection in @(
            [pscustomobject]@{ Stage = "Generator"; Code = "injected-generator-failure" },
            [pscustomobject]@{ Stage = "Hash"; Code = "injected-hash-failure" },
            [pscustomobject]@{ Stage = "Manifest"; Code = "injected-manifest-failure" },
            [pscustomobject]@{ Stage = "Swap"; Code = "directory-exchange-failed" })) {
        Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -InjectFailure $injection.Stage } $injection.Code
        $afterInjection = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
        Assert-Equal $before $afterInjection "$($injection.Stage) 失败改变了上一个有效 staging。"
    }

    $manifestLock = [IO.File]::Open($manifestPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        Assert-ThrowsCode { & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" } "directory-exchange-failed"
    }
    finally {
        $manifestLock.Dispose()
    }
    $afterLockedDirectory = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    Assert-Equal $before $afterLockedDirectory "目录占用改变了上一个有效 staging。"

    Assert-ThrowsCode {
        & $StagingScript -WorkspaceRoot $testRoot -Output $outputPath -RulesPath $rulesPath -Rid "win-x64" -AfterPlanAction {
            param($plan)
            [IO.File]::AppendAllText($plan[0].SourcePath, "changed", [Text.UTF8Encoding]::new($false))
        }
    } "source-changed"
    $afterSourceChange = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    Assert-Equal $before $afterSourceChange "源目录在计划冻结后变化时替换了有效 staging。"

    Write-Output "Asset staging tests passed."
}
finally {
    if ([IO.Directory]::Exists($testRoot)) {
        [IO.Directory]::Delete($testRoot, $true)
    }

    if ([IO.Directory]::Exists($externalRoot)) {
        [IO.Directory]::Delete($externalRoot, $true)
    }
}
