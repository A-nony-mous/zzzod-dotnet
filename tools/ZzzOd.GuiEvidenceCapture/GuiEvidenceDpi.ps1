function ConvertTo-GuiEvidenceNativePixelSize {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidatePattern('^\d+x\d+$')]
        [string] $LogicalSize,

        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 960)]
        [int] $WindowDpi
    )

    $logicalParts = $LogicalSize -split 'x'
    $logicalWidth = [int] $logicalParts[0]
    $logicalHeight = [int] $logicalParts[1]
    $scale = $WindowDpi / 96.0
    $width = [int] [Math]::Round($logicalWidth * $scale, [MidpointRounding]::AwayFromZero)
    $height = [int] [Math]::Round($logicalHeight * $scale, [MidpointRounding]::AwayFromZero)

    [pscustomobject]@{
        LogicalWidth = $logicalWidth
        LogicalHeight = $logicalHeight
        WindowDpi = $WindowDpi
        Scale = $scale
        Width = $width
        Height = $height
        Text = "$($width)x$($height)"
    }
}
