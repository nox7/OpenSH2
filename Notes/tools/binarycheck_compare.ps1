param(
    [Parameter(Mandatory = $true)]
    [string]$BlankPath,

    [Parameter(Mandatory = $true)]
    [string]$ChangedPath,

    [string]$ReportPath = "c:\Users\garet\Documents\GitHub\OpenSH2\Notes\reports\binarycheck_compare_report.txt"
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

function Parse-S2m {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $stream = [System.IO.MemoryStream]::new($bytes, $false)
    $reader = [System.IO.BinaryReader]::new($stream)

    $stringOptions = [ordered]@{}
    $stringCount = $reader.ReadInt32()
    for ($i = 0; $i -lt $stringCount; $i++) {
        $key = Read-AsciiString -Reader $reader
        $value = Read-Utf16String -Reader $reader
        $stringOptions[$key] = $value
    }

    $intOptions = [ordered]@{}
    $intCount = $reader.ReadInt32()
    for ($i = 0; $i -lt $intCount; $i++) {
        $key = Read-AsciiString -Reader $reader
        $value = $reader.ReadInt32()
        $intOptions[$key] = $value
    }

    $headerEnd = [int]$stream.Position
    $deflateStream = [System.IO.MemoryStream]::new($bytes, $false)
    $deflateStream.Position = $headerEnd + 2
    $inflater = [System.IO.Compression.DeflateStream]::new($deflateStream, [System.IO.Compression.CompressionMode]::Decompress, $true)
    $output = [System.IO.MemoryStream]::new()
    $buffer = New-Object byte[] 16384

    while (($read = $inflater.Read($buffer, 0, $buffer.Length)) -gt 0) {
        $output.Write($buffer, 0, $read)
    }

    $segmentABytes = $output.ToArray()

    return [PSCustomObject]@{
        Path = $Path
        FileSize = $bytes.Length
        HeaderEnd = $headerEnd
        StringOptions = $stringOptions
        IntOptions = $intOptions
        SegmentACompressedLength = ([int]$deflateStream.Position - $headerEnd)
        SegmentABytes = $segmentABytes
        SegmentADecompressedLength = $segmentABytes.Length
        SegmentAText = [System.Text.Encoding]::ASCII.GetString($segmentABytes)
        RawBytes = $bytes
    }
}

function Find-AsciiOffsets {
    param(
        [byte[]]$Bytes,
        [string]$Needle
    )

    $needleBytes = [System.Text.Encoding]::ASCII.GetBytes($Needle)
    $hits = [System.Collections.Generic.List[int]]::new()
    for ($i = 0; $i -le $Bytes.Length - $needleBytes.Length; $i++) {
        $match = $true
        for ($j = 0; $j -lt $needleBytes.Length; $j++) {
            if ($Bytes[$i + $j] -ne $needleBytes[$j]) {
                $match = $false
                break
            }
        }

        if ($match) {
            $hits.Add($i)
        }
    }

    return $hits
}

function Get-I32 {
    param(
        [byte[]]$Bytes,
        [int]$Offset
    )

    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) {
        return $null
    }

    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Get-TokenRecords {
    param([byte[]]$Bytes)

    $records = [System.Collections.Generic.List[object]]::new()
    for ($offset = 8; $offset -le $Bytes.Length - 20; $offset++) {
        $nameLength = Get-I32 -Bytes $Bytes -Offset $offset
        if ($null -eq $nameLength -or $nameLength -lt 4 -or $nameLength -gt 80) {
            continue
        }

        $nameStart = $offset + 4
        $nameEnd = $nameStart + $nameLength
        if ($nameEnd + 8 -gt $Bytes.Length) {
            continue
        }

        $ascii = $true
        for ($i = $nameStart; $i -lt $nameEnd; $i++) {
            $value = $Bytes[$i]
            if ($value -lt 0x20 -or $value -gt 0x7E) {
                $ascii = $false
                break
            }
        }

        if (-not $ascii) {
            continue
        }

        $name = [System.Text.Encoding]::ASCII.GetString($Bytes, $nameStart, $nameLength)
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]+$') {
            continue
        }

        $id = Get-I32 -Bytes $Bytes -Offset ($offset - 4)
        $tag = Get-I32 -Bytes $Bytes -Offset $nameEnd
        $baseLength = Get-I32 -Bytes $Bytes -Offset ($nameEnd + 4)
        if ($null -eq $id -or $null -eq $tag -or $null -eq $baseLength -or $baseLength -lt 0 -or $baseLength -gt 80) {
            continue
        }

        $baseName = ''
        if ($baseLength -gt 0) {
            $baseStart = $nameEnd + 8
            $baseEnd = $baseStart + $baseLength
            if ($baseEnd -gt $Bytes.Length) {
                continue
            }

            $baseAscii = $true
            for ($i = $baseStart; $i -lt $baseEnd; $i++) {
                $value = $Bytes[$i]
                if ($value -lt 0x20 -or $value -gt 0x7E) {
                    $baseAscii = $false
                    break
                }
            }

            if (-not $baseAscii) {
                continue
            }

            $baseName = [System.Text.Encoding]::ASCII.GetString($Bytes, $baseStart, $baseLength)
            if ($baseName -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
                continue
            }
        }

        $records.Add([PSCustomObject]@{
            Offset = $offset - 4
            Id = $id
            NameLength = $nameLength
            Name = $name
            Tag = $tag
            BaseLength = $baseLength
            BaseName = $baseName
        })
    }

    return $records | Sort-Object Offset -Unique
}

function Get-DiffRuns {
    param(
        [byte[]]$Left,
        [byte[]]$Right
    )

    $minLength = [Math]::Min($Left.Length, $Right.Length)
    $diffs = [System.Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt $minLength; $i++) {
        if ($Left[$i] -ne $Right[$i]) {
            $diffs.Add($i)
        }
    }

    $runs = [System.Collections.Generic.List[object]]::new()
    if ($diffs.Count -eq 0) {
        return $runs
    }

    $start = $diffs[0]
    $previous = $diffs[0]
    for ($i = 1; $i -lt $diffs.Count; $i++) {
        $current = $diffs[$i]
        if ($current -eq ($previous + 1)) {
            $previous = $current
            continue
        }

        $runs.Add([PSCustomObject]@{
            Start = $start
            End = $previous
            Length = $previous - $start + 1
        })

        $start = $current
        $previous = $current
    }

    $runs.Add([PSCustomObject]@{
        Start = $start
        End = $previous
        Length = $previous - $start + 1
    })

    return $runs
}

function Get-WindowText {
    param(
        [byte[]]$Bytes,
        [int]$Offset,
        [int]$Radius = 96
    )

    $start = [Math]::Max(0, $Offset - $Radius)
    $length = [Math]::Min($Radius * 2 + 160, $Bytes.Length - $start)
    return ([System.Text.Encoding]::ASCII.GetString($Bytes, $start, $length) -replace '[^ -~]', '.')
}

$blankInfo = Parse-S2m -Path $BlankPath
$changedInfo = Parse-S2m -Path $ChangedPath

$blankRecords = Get-TokenRecords -Bytes $blankInfo.SegmentABytes
$changedRecords = Get-TokenRecords -Bytes $changedInfo.SegmentABytes
$diffRuns = Get-DiffRuns -Left $blankInfo.SegmentABytes -Right $changedInfo.SegmentABytes

$lines = [System.Collections.Generic.List[string]]::new()

$lines.Add('=== HEADER COMPARISON ===')
foreach ($info in @($blankInfo, $changedInfo)) {
    $lines.Add("Path=$($info.Path)")
    $lines.Add("FileSize=$($info.FileSize)")
    $lines.Add("HeaderEnd=$($info.HeaderEnd)")
    $lines.Add("SegmentACompressedLength=$($info.SegmentACompressedLength)")
    $lines.Add("SegmentADecompressedLength=$($info.SegmentADecompressedLength)")
    foreach ($entry in $info.StringOptions.GetEnumerator()) {
        $lines.Add("StringOption:$($entry.Key)=$($entry.Value)")
    }
    foreach ($entry in $info.IntOptions.GetEnumerator()) {
        $lines.Add("IntOption:$($entry.Key)=$($entry.Value)")
    }
    $lines.Add('')
}

$lines.Add('=== SEGMENT A KEYWORD OFFSETS ===')
foreach ($keyword in @('MapHeader', 'Scenario', 'ScenarioEvent', 'WinAction', 'LoseAction', 'Trigger', 'LordDiesTrigger', 'AlwaysTrigger', 'Mission', 'Wood', 'Resource', 'Condition', 'Goal')) {
    $blankHits = Find-AsciiOffsets -Bytes $blankInfo.SegmentABytes -Needle $keyword
    $changedHits = Find-AsciiOffsets -Bytes $changedInfo.SegmentABytes -Needle $keyword
    if ($blankHits.Count -gt 0 -or $changedHits.Count -gt 0) {
        $lines.Add("$keyword|Blank=$($blankHits -join ',')|Changed=$($changedHits -join ',')")
    }
}
$lines.Add('')

$lines.Add('=== SEGMENT A TOKEN RECORDS (Blank) ===')
foreach ($record in $blankRecords | Select-Object -First 80) {
    $lines.Add(("Offset={0} Id={1} Name={2} Tag={3} Base={4}" -f $record.Offset, $record.Id, $record.Name, $record.Tag, $record.BaseName))
}
$lines.Add('')

$lines.Add('=== SEGMENT A TOKEN RECORDS (Changed) ===')
foreach ($record in $changedRecords | Select-Object -First 120) {
    $lines.Add(("Offset={0} Id={1} Name={2} Tag={3} Base={4}" -f $record.Offset, $record.Id, $record.Name, $record.Tag, $record.BaseName))
}
$lines.Add('')

$lines.Add('=== SEGMENT A DIFF RUNS ===')
$lines.Add("RunCount=$($diffRuns.Count)")
foreach ($run in ($diffRuns | Sort-Object @{ Expression = 'Length'; Descending = $true }, @{ Expression = 'Start'; Descending = $false } | Select-Object -First 80)) {
    $lines.Add("Run:$($run.Start)-$($run.End) len=$($run.Length)")
}
$lines.Add('')

$interestingNames = @('MapHeader', 'Scenario', 'ScenarioEvent', 'LoseAction', 'WinAction', 'Trigger', 'LordDiesTrigger', 'AlwaysTrigger', 'Mission')
$lines.Add('=== CHANGED TOKEN WINDOWS ===')
foreach ($record in $changedRecords | Where-Object { $_.Name -in $interestingNames } | Select-Object -First 40) {
    $lines.Add("Token:$($record.Name) Offset=$($record.Offset) Id=$($record.Id) Tag=$($record.Tag) Base=$($record.BaseName)")
    $lines.Add((Get-WindowText -Bytes $changedInfo.SegmentABytes -Offset $record.Offset))
    $lines.Add('')
}

$lines.Add('=== CHANGED DIFF WINDOWS ===')
foreach ($run in ($diffRuns | Sort-Object @{ Expression = 'Length'; Descending = $true }, @{ Expression = 'Start'; Descending = $false } | Select-Object -First 20)) {
    $lines.Add("DiffRun:$($run.Start)-$($run.End) len=$($run.Length)")
    $lines.Add((Get-WindowText -Bytes $changedInfo.SegmentABytes -Offset $run.Start -Radius 120))
    $lines.Add('')
}

[System.IO.File]::WriteAllLines($ReportPath, $lines)
Write-Output $ReportPath