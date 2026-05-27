param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$MapPath
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

function Parse-Header {
    param([byte[]]$Bytes)

    $stream = [System.IO.MemoryStream]::new($Bytes, $false)
    $reader = [System.IO.BinaryReader]::new($stream)

    $stringCount = $reader.ReadInt32()
    if ($stringCount -lt 0 -or $stringCount -gt 10000) {
        throw "Invalid string option count: $stringCount"
    }

    $stringOptions = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    for ($i = 0; $i -lt $stringCount; $i++) {
        $key = Read-AsciiString -Reader $reader
        $value = Read-Utf16String -Reader $reader
        $stringOptions[$key] = $value
    }

    $intCount = $reader.ReadInt32()
    if ($intCount -lt 0 -or $intCount -gt 10000) {
        throw "Invalid int option count: $intCount"
    }

    $intOptions = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::OrdinalIgnoreCase)
    for ($i = 0; $i -lt $intCount; $i++) {
        $key = Read-AsciiString -Reader $reader
        $intOptions[$key] = $reader.ReadInt32()
    }

    return [PSCustomObject]@{
        HeaderEnd = [int]$stream.Position
        StringOptions = $stringOptions
        IntOptions = $intOptions
    }
}

function Test-ZlibHeader {
    param(
        [byte[]]$Bytes,
        [int]$Offset
    )

    if ($Offset -lt 0 -or ($Offset + 1) -ge $Bytes.Length) {
        return $false
    }

    if ($Bytes[$Offset] -ne 0x78) {
        return $false
    }

    $cmf = [int]$Bytes[$Offset]
    $flg = [int]$Bytes[$Offset + 1]
    return ((($cmf -shl 8) + $flg) % 31) -eq 0
}

function Try-Inflate {
    param(
        [byte[]]$Bytes,
        [int]$StartOffset,
        [bool]$SkipTwoZlibHeaderBytes
    )

    try {
        $payload = [System.IO.MemoryStream]::new()
        $stream = [System.IO.MemoryStream]::new($Bytes, $false)

        $bodyStart = $StartOffset + $(if ($SkipTwoZlibHeaderBytes) { 2 } else { 0 })
        if ($bodyStart -lt 0 -or $bodyStart -ge $Bytes.Length) {
            return [PSCustomObject]@{
                Success = $false
                Error = 'Start offset out of bounds.'
            }
        }

        $stream.Position = $bodyStart
        $deflate = [System.IO.Compression.DeflateStream]::new(
            $stream,
            [System.IO.Compression.CompressionMode]::Decompress,
            $true
        )

        $buffer = New-Object byte[] 16384
        while (($read = $deflate.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $payload.Write($buffer, 0, $read)
        }

        $deflate.Dispose()
        $endOffset = [int]$stream.Position
        $stream.Dispose()

        return [PSCustomObject]@{
            Success = $true
            EndOffset = $endOffset
            DecompressedBytes = $payload.ToArray()
            DecompressedLength = [int]$payload.Length
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

function Add-SegmentRecord {
    param(
        [System.Collections.Generic.List[object]]$Records,
        [string]$Label,
        [int]$StartOffset,
        [int]$EndOffset,
        [byte[]]$DecompressedBytes,
        [string]$FilePath,
        [string]$Kind
    )

    [System.IO.File]::WriteAllBytes($FilePath, $DecompressedBytes)

    $Records.Add([PSCustomObject]@{
        Label = $Label
        Kind = $Kind
        StartOffset = $StartOffset
        EndOffset = $EndOffset
        CompressedLength = $EndOffset - $StartOffset
        DecompressedLength = $DecompressedBytes.Length
        OutputFile = [System.IO.Path]::GetFileName($FilePath)
    })
}

$resolvedMapPath = (Resolve-Path -LiteralPath $MapPath).Path
if ([System.IO.Path]::GetExtension($resolvedMapPath) -ne '.s2m') {
    throw 'Input file must be an .s2m file.'
}

$bytes = [System.IO.File]::ReadAllBytes($resolvedMapPath)
$header = Parse-Header -Bytes $bytes

$baseName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedMapPath)
$outputDir = Join-Path ([System.IO.Path]::GetDirectoryName($resolvedMapPath)) ($baseName + '-inflated')
[System.IO.Directory]::CreateDirectory($outputDir) | Out-Null

$records = [System.Collections.Generic.List[object]]::new()
$seenStarts = [System.Collections.Generic.HashSet[int]]::new()

$headerPath = Join-Path $outputDir '00-header.bin'
[System.IO.File]::WriteAllBytes($headerPath, $bytes[0..($header.HeaderEnd - 1)])
$records.Add([PSCustomObject]@{
    Label = 'Header'
    Kind = 'raw'
    StartOffset = 0
    EndOffset = $header.HeaderEnd
    CompressedLength = $header.HeaderEnd
    DecompressedLength = $header.HeaderEnd
    OutputFile = [System.IO.Path]::GetFileName($headerPath)
})

if (Test-ZlibHeader -Bytes $bytes -Offset $header.HeaderEnd) {
    $segmentA = Try-Inflate -Bytes $bytes -StartOffset $header.HeaderEnd -SkipTwoZlibHeaderBytes $true
    if (-not $segmentA.Success) {
        throw "Failed to inflate Segment A: $($segmentA.Error)"
    }

    $segmentAPath = Join-Path $outputDir '01-segment-a.bin'
    Add-SegmentRecord -Records $records -Label 'SegmentA' -StartOffset $header.HeaderEnd -EndOffset $segmentA.EndOffset -DecompressedBytes $segmentA.DecompressedBytes -FilePath $segmentAPath -Kind 'zlib'
    [void]$seenStarts.Add($header.HeaderEnd)

    if ($segmentA.EndOffset -lt $bytes.Length) {
        $segmentB = Try-Inflate -Bytes $bytes -StartOffset $segmentA.EndOffset -SkipTwoZlibHeaderBytes $false
        if ($segmentB.Success -and $segmentB.DecompressedLength -gt 0) {
            $segmentBPath = Join-Path $outputDir '02-segment-b.bin'
            Add-SegmentRecord -Records $records -Label 'SegmentB' -StartOffset $segmentA.EndOffset -EndOffset $segmentB.EndOffset -DecompressedBytes $segmentB.DecompressedBytes -FilePath $segmentBPath -Kind 'raw-deflate'
            [void]$seenStarts.Add($segmentA.EndOffset)
        }
    }
}

$scanIndex = 1
$searchStart = [Math]::Max($header.HeaderEnd, 0)
for ($offset = $searchStart; $offset -lt ($bytes.Length - 1); $offset++) {
    if (-not (Test-ZlibHeader -Bytes $bytes -Offset $offset)) {
        continue
    }

    if ($seenStarts.Contains($offset)) {
        continue
    }

    $inflated = Try-Inflate -Bytes $bytes -StartOffset $offset -SkipTwoZlibHeaderBytes $true
    if (-not $inflated.Success -or $inflated.DecompressedLength -lt 64) {
        continue
    }

    $label = 'ZlibStream' + $scanIndex.ToString('00')
    $fileName = ('{0:D2}-zlib-{1}.bin' -f ($records.Count), $offset)
    $filePath = Join-Path $outputDir $fileName
    Add-SegmentRecord -Records $records -Label $label -StartOffset $offset -EndOffset $inflated.EndOffset -DecompressedBytes $inflated.DecompressedBytes -FilePath $filePath -Kind 'zlib-scan'
    [void]$seenStarts.Add($offset)
    $scanIndex++
}

$manifestPath = Join-Path $outputDir 'manifest.txt'
$manifest = [System.Collections.Generic.List[string]]::new()
$manifest.Add("SourceFile=$resolvedMapPath")
$manifest.Add("HeaderEnd=$($header.HeaderEnd)")
if ($header.StringOptions.ContainsKey('type')) {
    $manifest.Add("MapType=$($header.StringOptions['type'])")
}
$manifest.Add('')
$manifest.Add('Segments:')
foreach ($record in $records | Sort-Object StartOffset, Label) {
    $manifest.Add(
        ('{0} kind={1} start={2} end={3} compressedLen={4} decompressedLen={5} file={6}' -f
            $record.Label,
            $record.Kind,
            $record.StartOffset,
            $record.EndOffset,
            $record.CompressedLength,
            $record.DecompressedLength,
            $record.OutputFile)
    )
}
[System.IO.File]::WriteAllLines($manifestPath, $manifest)

Write-Output "Wrote inflated segments to: $outputDir"
Write-Output "Manifest: $manifestPath"