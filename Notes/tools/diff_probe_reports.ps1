param(
    [Parameter(Mandatory = $true)]
    [string]$OldProbePath,

    [Parameter(Mandatory = $true)]
    [string]$NewProbePath,

    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

function Get-PayloadHexLine {
    param([string]$Path)

    $line = Get-Content $Path | Where-Object { $_ -like 'payloadHex=*' } | Select-Object -First 1
    if ([string]::IsNullOrEmpty($line)) {
        throw "payloadHex line not found in $Path"
    }

    return $line.Substring(11)
}

function HexToBytes {
    param([string]$Hex)

    $parts = $Hex.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    $bytes = [byte[]]::new($parts.Length)

    for ($i = 0; $i -lt $parts.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($parts[$i], 16)
    }

    return $bytes
}

function Normalize-PackedValue {
    param([int]$Value)

    if ($Value -gt 255 -and ($Value % 256) -eq 0) {
        return [int]($Value / 256)
    }

    if ((($Value -band 0xFF) -eq 0xFF) -and ($Value -gt 255)) {
        return ($Value -shr 8)
    }

    return $Value
}

$oldHex = Get-PayloadHexLine -Path $OldProbePath
$newHex = Get-PayloadHexLine -Path $NewProbePath

$oldBytes = HexToBytes -Hex $oldHex
$newBytes = HexToBytes -Hex $newHex

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('byteOffset oldHex newHex')

$max = [Math]::Min($oldBytes.Length, $newBytes.Length)
for ($i = 0; $i -lt $max; $i++) {
    if ($oldBytes[$i] -ne $newBytes[$i]) {
        $lines.Add("$i $($oldBytes[$i].ToString('X2')) $($newBytes[$i].ToString('X2'))")
    }
}

$lines.Add('--int32-shift0--')
$index = 0
for ($offset = 0; $offset + 3 -lt $max; $offset += 4) {
    $oldRaw = [BitConverter]::ToInt32($oldBytes, $offset)
    $newRaw = [BitConverter]::ToInt32($newBytes, $offset)

    if ($oldRaw -ne $newRaw) {
        $lines.Add(
            "idx=$index off=$offset oldRaw=$oldRaw oldNorm=$(Normalize-PackedValue -Value $oldRaw) newRaw=$newRaw newNorm=$(Normalize-PackedValue -Value $newRaw)"
        )
    }

    $index++
}

[System.IO.File]::WriteAllLines($ReportPath, $lines)
Write-Output $ReportPath
