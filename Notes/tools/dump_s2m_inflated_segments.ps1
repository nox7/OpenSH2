param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$MapPath
)

$ErrorActionPreference = 'Stop'

# DeflateStream buffers beyond a DEFLATE endpoint, so its source position cannot
# identify a member boundary. This helper finds the Adler-32 trailer and verifies
# the candidate again using a stream bounded to the exact compressed body.
if ($null -eq ('S2mZlibChecksumV2' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.IO.Compression;

public static class S2mZlibChecksumV2
{
    public static uint Adler32(byte[] bytes)
    {
        const uint modulus = 65521;
        uint a = 1;
        uint b = 0;

        foreach (byte value in bytes)
        {
            a += value;
            if (a >= modulus) a -= modulus;
            b += a;
            if (b >= modulus) b -= modulus;
        }

        return (b << 16) | a;
    }

    public static int FindVerifiedTrailerOffset(
        byte[] source,
        int bodyOffset,
        byte[] expectedBytes,
        uint adler32)
    {
        for (int offset = bodyOffset; offset <= source.Length - 4; offset++)
        {
            if (ReadUInt32BigEndian(source, offset) != adler32) continue;
            if (InflatesTo(source, bodyOffset, offset - bodyOffset, expectedBytes)) return offset;
        }

        return -1;
    }

    private static uint ReadUInt32BigEndian(byte[] source, int offset)
    {
        return ((uint)source[offset] << 24)
            | ((uint)source[offset + 1] << 16)
            | ((uint)source[offset + 2] << 8)
            | source[offset + 3];
    }

    private static bool InflatesTo(byte[] source, int offset, int count, byte[] expectedBytes)
    {
        try
        {
            byte[] compressedBytes = new byte[count];
            Buffer.BlockCopy(source, offset, compressedBytes, 0, count);

            using (MemoryStream compressed = new MemoryStream(compressedBytes, false))
            using (DeflateStream inflater = new DeflateStream(compressed, CompressionMode.Decompress, false))
            using (MemoryStream output = new MemoryStream())
            {
                inflater.CopyTo(output);
                if (output.Length != expectedBytes.Length) return false;

                byte[] actualBytes = output.ToArray();
                for (int i = 0; i < actualBytes.Length; i++)
                {
                    if (actualBytes[i] != expectedBytes[i]) return false;
                }

                return true;
            }
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
'@
}

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

    $cmf = [int]$Bytes[$Offset]
    $flg = [int]$Bytes[$Offset + 1]
    $usesDeflate = ($cmf -band 0x0F) -eq 8
    $validWindowSize = ($cmf -shr 4) -le 7
    $validHeaderChecksum = ((($cmf -shl 8) + $flg) % 31) -eq 0
    $requiresPresetDictionary = ($flg -band 0x20) -ne 0

    return $usesDeflate -and $validWindowSize -and $validHeaderChecksum -and -not $requiresPresetDictionary
}

function Try-Inflate {
    param(
        [byte[]]$Bytes,
        [int]$StartOffset
    )

    try {
        if (-not (Test-ZlibHeader -Bytes $Bytes -Offset $StartOffset)) {
            return [PSCustomObject]@{
                Success = $false
                Error = 'Invalid or unsupported zlib header.'
            }
        }

        $payload = [System.IO.MemoryStream]::new()
        $bodyStart = $StartOffset + 2
        if ($bodyStart -lt 0 -or $bodyStart -ge $Bytes.Length) {
            return [PSCustomObject]@{
                Success = $false
                Error = 'Start offset out of bounds.'
            }
        }

        $stream = [System.IO.MemoryStream]::new($Bytes, $false)
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
        $stream.Dispose()

        $decompressedBytes = $payload.ToArray()
        $actualAdler32 = [S2mZlibChecksumV2]::Adler32($decompressedBytes)
        $trailerOffset = [S2mZlibChecksumV2]::FindVerifiedTrailerOffset(
            $Bytes,
            $bodyStart,
            $decompressedBytes,
            $actualAdler32
        )

        if ($trailerOffset -lt 0) {
            return [PSCustomObject]@{
                Success = $false
                Error = ('No valid Adler-32 trailer was found for checksum {0:X8}.' -f $actualAdler32)
            }
        }

        $endOffset = $trailerOffset + 4

        return [PSCustomObject]@{
            Success = $true
            EndOffset = $endOffset
            DecompressedBytes = $decompressedBytes
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

# Remove only files generated by earlier runs of this tool. Without this, stale
# false-positive .bin files remain beside the corrected output.
$generatedFilePattern = '^(?:\d{2}-(?:header|segment-a|segment-b|zlib-\d+)\.bin|manifest\.txt)$'
Get-ChildItem -LiteralPath $outputDir -File | Where-Object {
    $_.Name -match $generatedFilePattern
} | Remove-Item -Force

$records = [System.Collections.Generic.List[object]]::new()

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

$offset = $header.HeaderEnd
$segmentIndex = 1
while (Test-ZlibHeader -Bytes $bytes -Offset $offset) {
    $inflated = Try-Inflate -Bytes $bytes -StartOffset $offset
    if (-not $inflated.Success) {
        throw "Failed to inflate zlib segment $segmentIndex at offset ${offset}: $($inflated.Error)"
    }

    $label = if ($segmentIndex -eq 1) { 'SegmentA' } else { 'ZlibStream' + $segmentIndex.ToString('00') }
    $fileName = if ($segmentIndex -eq 1) {
        '01-segment-a.bin'
    }
    else {
        ('{0:D2}-zlib-{1}.bin' -f $segmentIndex, $offset)
    }
    $filePath = Join-Path $outputDir $fileName
    Add-SegmentRecord -Records $records -Label $label -StartOffset $offset -EndOffset $inflated.EndOffset -DecompressedBytes $inflated.DecompressedBytes -FilePath $filePath -Kind 'zlib'

    $offset = $inflated.EndOffset
    $segmentIndex++
}

$manifestPath = Join-Path $outputDir 'manifest.txt'
$manifest = [System.Collections.Generic.List[string]]::new()
$manifest.Add("SourceFile=$resolvedMapPath")
$manifest.Add("HeaderEnd=$($header.HeaderEnd)")
$manifest.Add("ZlibDataEnd=$offset")
$manifest.Add("TrailingBytes=$($bytes.Length - $offset)")
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
