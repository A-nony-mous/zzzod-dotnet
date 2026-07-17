[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path
)

$resolvedPath = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$content = [System.IO.File]::ReadAllText($resolvedPath, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($resolvedPath, $content.TrimStart([char]0xFEFF), $utf8)
