param(
    [Parameter(Mandatory = $true)]
    [string]$MapPath,

    [Parameter(Mandatory = $true)]
    [string]$ActionName,

    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

function Read-AsciiString {
    param([System.IO.BinaryReader]$Reader)

    $length = $Reader.ReadInt32()
    if ($length -lt 0 -or $length -gt 1000000) {
        throw "Invalid ASCII string length: $length"
    }

    $bytes = $Reader.ReadBytes($length)
    if ($bytes.Length -ne $length) {
        throw 'Unexpected EOF while reading ASCII string.'
    }

    return [System.Text.Encoding]::ASCII.GetString($bytes)
}

function Read-Utf16String {
    param([System.IO.BinaryReader]$Reader)

    $length = $Reader.ReadInt32()
    if ($length -lt 0 -or $length -gt 1000000) {
        throw "Invalid UTF-16 string length: $length"
    }

    $byteLength = $length * 2
    $bytes = $Reader.ReadBytes($byteLength)
    if ($bytes.Length -ne $byteLength) {
        throw 'Unexpected EOF while reading UTF-16 string.'
    }

    return [System.Text.Encoding]::Unicode.GetString($bytes)
}

function Is-ReasonableAscii {
    param(
        [byte[]]$Bytes,
        [int]$Start,
        [int]$Length
    )

    if ($Start -lt 0 -or $Length -lt 0 -or ($Start + $Length) -gt $Bytes.Length) {
        return $false
    }

    for ($i = 0; $i -lt $Length; $i++) {
        $value = $Bytes[$Start + $i]
        if ($value -lt 0x20 -or $value -gt 0x7E) {
            return $false
        }
    }

    return $true
}

function Parse-SegmentA {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $stream = [System.IO.MemoryStream]::new($bytes, $false)
    $reader = [System.IO.BinaryReader]::new($stream)

    $stringCount = $reader.ReadInt32()
    for ($i = 0; $i -lt $stringCount; $i++) {
        [void](Read-AsciiString -Reader $reader)
        [void](Read-Utf16String -Reader $reader)
    }

    $intCount = $reader.ReadInt32()
    for ($i = 0; $i -lt $intCount; $i++) {
        [void](Read-AsciiString -Reader $reader)
        [void]$reader.ReadInt32()
    }

    $headerEnd = [int]$stream.Position

    $segmentProbe = [byte[]]::new(8194)
    [Array]::Copy($bytes, $headerEnd, $segmentProbe, 0, [Math]::Min(8194, $bytes.Length - $headerEnd))

    $deflateStream = [System.IO.Compression.DeflateStream]::new(
        [System.IO.MemoryStream]::new($segmentProbe, 2, $segmentProbe.Length - 6, $false),
        [System.IO.Compression.CompressionMode]::Decompress
    )

    $out = [System.IO.MemoryStream]::new()
    $buffer = New-Object byte[] 16384

    while (($read = $deflateStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
        $out.Write($buffer, 0, $read)
    }

    return $out.ToArray()
}

function Get-TokenRecords {
    param([byte[]]$Bytes)

    $records = [System.Collections.Generic.List[object]]::new()

    for ($offset = 0; $offset + 20 -lt $Bytes.Length; $offset++) {
        $id = [BitConverter]::ToInt32($Bytes, $offset)
        if ($id -lt 0 -or $id -gt 500000) {
            continue
        }

        $nameLength = [BitConverter]::ToInt32($Bytes, $offset + 4)
        if ($nameLength -le 0 -or $nameLength -gt 128) {
            continue
        }

        $nameStart = $offset + 8
        $nameEnd = $nameStart + $nameLength
        if ($nameEnd + 8 -gt $Bytes.Length) {
            continue
        }

        if (-not (Is-ReasonableAscii -Bytes $Bytes -Start $nameStart -Length $nameLength)) {
            continue
        }

        $name = [System.Text.Encoding]::ASCII.GetString($Bytes, $nameStart, $nameLength)
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]+$') {
            continue
        }

        $tag = [BitConverter]::ToInt32($Bytes, $nameEnd)
        if ($tag -lt 0 -or $tag -gt 128) {
            continue
        }

        $baseLength = [BitConverter]::ToInt32($Bytes, $nameEnd + 4)
        if ($baseLength -lt 0 -or $baseLength -gt 128) {
            continue
        }

        $baseStart = $nameEnd + 8
        $metaEnd = $baseStart + $baseLength
        if ($metaEnd -gt $Bytes.Length) {
            continue
        }

        if ($baseLength -gt 0 -and -not (Is-ReasonableAscii -Bytes $Bytes -Start $baseStart -Length $baseLength)) {
            continue
        }

        $records.Add([PSCustomObject]@{
            Start = $offset
            Id = $id
            Name = $name
            Tag = $tag
            MetaEnd = $metaEnd
        })
    }

    return $records | Sort-Object Start -Unique
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

$segmentA = Parse-SegmentA -Path $MapPath
$records = Get-TokenRecords -Bytes $segmentA

$record = $records | Where-Object { $_.Name -eq $ActionName } | Select-Object -First 1
if ($null -eq $record) {
    throw "Action not found: $ActionName"
}

$nextRecord = $records | Where-Object { $_.Start -gt $record.Start } | Select-Object -First 1
$nextOffset = if ($null -ne $nextRecord) { $nextRecord.Start } else { $segmentA.Length }

if ($nextOffset -le $record.MetaEnd) {
    throw "Invalid payload bounds for ${ActionName}: metaEnd=$($record.MetaEnd), next=$nextOffset"
}

$payload = $segmentA[$record.MetaEnd..($nextOffset - 1)]
$hex = ($payload | ForEach-Object { $_.ToString('X2') }) -join ' '

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("start=$($record.Start) id=$($record.Id) payloadLen=$($payload.Length)")
$lines.Add("payloadHex=$hex")
$lines.Add('nonZeroBytes:')

for ($i = 0; $i -lt $payload.Length; $i++) {
    if ($payload[$i] -ne 0) {
        $lines.Add("b$i=$($payload[$i])")
    }
}

$lines.Add('shift0 index off raw norm (non-zero norm only)')
$index = 0
for ($offset = 0; $offset + 3 -lt $payload.Length; $offset += 4) {
    $raw = [BitConverter]::ToInt32($payload, $offset)
    $norm = Normalize-PackedValue -Value $raw
    if ($norm -ne 0) {
        $lines.Add("$index $offset $raw $norm")
    }

    $index++
}

[System.IO.File]::WriteAllLines($ReportPath, $lines)
Write-Output $ReportPath
