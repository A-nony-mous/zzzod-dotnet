[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $ProcessId,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [ValidateRange(1, 60)]
    [int] $TimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$process = Get-Process -Id $ProcessId
$root = $null
while ([DateTimeOffset]::UtcNow -lt $deadline) {
    $process.Refresh()
    if ($process.HasExited) {
        throw "GUI process exited before its window was ready: $ProcessId"
    }

    if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        if ($null -ne $root) {
            break
        }
    }

    Start-Sleep -Milliseconds 200
}

if ($null -eq $root) {
    throw "GUI window was not ready: $ProcessId"
}

$walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
$treeDeadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
while ([DateTimeOffset]::UtcNow -lt $treeDeadline -and $null -eq $walker.GetFirstChild($root)) {
    Start-Sleep -Milliseconds 200
}

function Convert-ControlNode {
    param(
        [Parameter(Mandatory = $true)]
        [System.Windows.Automation.AutomationElement] $Element,

        [Parameter(Mandatory = $true)]
        [int] $Depth
    )

    $bounds = $Element.Current.BoundingRectangle
    $node = [ordered]@{
        name = $Element.Current.Name
        automationId = $Element.Current.AutomationId
        controlType = $Element.Current.ControlType.ProgrammaticName
        enabled = $Element.Current.IsEnabled
        offscreen = $Element.Current.IsOffscreen
        bounds = [ordered]@{ x = $bounds.X; y = $bounds.Y; width = $bounds.Width; height = $bounds.Height }
        children = @()
    }

    if ($Depth -ge 32) {
        return $node
    }

    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    $child = $walker.GetFirstChild($Element)
    while ($null -ne $child) {
        $node.children += Convert-ControlNode -Element $child -Depth ($Depth + 1)
        $child = $walker.GetNextSibling($child)
    }

    return $node
}

$tree = [ordered]@{
    schema = 'zzzod-gui-control-tree.v1'
    capturedAt = [DateTimeOffset]::UtcNow.ToString('o')
    processId = $ProcessId
    root = Convert-ControlNode -Element $root -Depth 0
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) | Out-Null
$tree | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Output ([IO.Path]::GetFullPath($OutputPath))
