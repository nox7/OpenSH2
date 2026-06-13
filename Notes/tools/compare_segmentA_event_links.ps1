param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$OldMapPath,

    [Parameter(Mandatory = $true, Position = 1)]
    [string]$NewMapPath,

    [Parameter(Mandatory = $false)]
    [string]$ReportPath,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 64)]
    [int]$WordCount = 16
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

    for ($i = 0; $i -lt $stringCount; $i++) {
        [void](Read-AsciiString -Reader $reader)
        [void](Read-Utf16String -Reader $reader)
    }

    $intCount = $reader.ReadInt32()
    if ($intCount -lt 0 -or $intCount -gt 10000) {
        throw "Invalid int option count: $intCount"
    }

    for ($i = 0; $i -lt $intCount; $i++) {
        [void](Read-AsciiString -Reader $reader)
        [void]$reader.ReadInt32()
    }

    return [int]$stream.Position
}

function Parse-SegmentA {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $headerEnd = Parse-Header -Bytes $bytes

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

    return [PSCustomObject]@{
        HeaderEnd = $headerEnd
        SegmentA = $out.ToArray()
    }
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

    $ordered = $records | Sort-Object Start -Unique

    $withPayload = [System.Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $ordered.Count; $i++) {
        $record = $ordered[$i]
        $nextOffset = if ($i + 1 -lt $ordered.Count) { $ordered[$i + 1].Start } else { $Bytes.Length }

        if ($nextOffset -le $record.MetaEnd) {
            continue
        }

        $payload = $Bytes[$record.MetaEnd..($nextOffset - 1)]
        $withPayload.Add([PSCustomObject]@{
            Start = $record.Start
            Id = $record.Id
            Name = $record.Name
            Tag = $record.Tag
            PayloadStart = $record.MetaEnd
            PayloadLength = $payload.Length
            PayloadBytes = $payload
        })
    }

    return $withPayload
}

function Get-HexDword {
    param([int]$Value)

    $bytes = [BitConverter]::GetBytes($Value)
    return ($bytes | ForEach-Object { $_.ToString('X2') }) -join ' '
}

function Get-PayloadHash {
    param([byte[]]$Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($Bytes)
        return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-LeadingAlignedWords {
    param(
        [byte[]]$Bytes,
        [int]$Count
    )

    $words = [System.Collections.Generic.List[object]]::new()
    if ($Count -le 0 -or $Bytes.Length -lt 4) {
        return $words
    }

    $limit = [Math]::Min($Count, [Math]::Floor($Bytes.Length / 4))
    for ($i = 0; $i -lt $limit; $i++) {
        $offset = $i * 4
        $raw = [BitConverter]::ToInt32($Bytes, $offset)
        $words.Add([PSCustomObject]@{
            Index = $i
            Offset = $offset
            Raw = $raw
            Hex = Get-HexDword -Value $raw
        })
    }

    return $words
}

function Format-WordVector {
    param([object[]]$Words)

    if ($null -eq $Words -or $Words.Count -eq 0) {
        return ''
    }

    return (($Words | ForEach-Object { '{0}:{1}[{2}]' -f $_.Index, $_.Raw, $_.Hex }) -join ' | ')
}

function Find-PatternOffsets {
    param(
        [byte[]]$Bytes,
        [byte[]]$Pattern
    )

    $hits = [System.Collections.Generic.List[int]]::new()
    if ($Pattern.Length -eq 0 -or $Pattern.Length -gt $Bytes.Length) {
        return $hits
    }

    for ($i = 0; $i -le ($Bytes.Length - $Pattern.Length); $i++) {
        $ok = $true
        for ($j = 0; $j -lt $Pattern.Length; $j++) {
            if ($Bytes[$i + $j] -ne $Pattern[$j]) {
                $ok = $false
                break
            }
        }

        if ($ok) {
            $hits.Add($i)
        }
    }

    return $hits
}

function Get-MissionSummary {
    param([object[]]$Records)

    $mission = $Records | Where-Object { $_.Name -eq 'Mission' } | Select-Object -First 1
    if ($null -eq $mission) {
        return $null
    }

    if ($mission.PayloadLength -lt 8) {
        return [PSCustomObject]@{
            Found = $true
            RowCount = 0
            HeaderSize = 0
            EventIds = @()
            PayloadLength = $mission.PayloadLength
        }
    }

    $headerSize = [BitConverter]::ToInt32($mission.PayloadBytes, 0)
    $rowCount = [BitConverter]::ToInt32($mission.PayloadBytes, 4)
    $eventIds = [System.Collections.Generic.List[object]]::new()

    if ($rowCount -gt 0 -and $rowCount -lt 64) {
        for ($i = 0; $i -lt $rowCount; $i++) {
            $offset = 8 + (4 * $i)
            if (($offset + 3) -ge $mission.PayloadLength) {
                break
            }

            $raw = [BitConverter]::ToInt32($mission.PayloadBytes, $offset)
            $eventIds.Add([PSCustomObject]@{
                Index = $i
                Offset = $offset
                Raw = $raw
                Hex = Get-HexDword -Value $raw
            })
        }
    }

    return [PSCustomObject]@{
        Found = $true
        HeaderSize = $headerSize
        RowCount = $rowCount
        EventIds = $eventIds
        PayloadLength = $mission.PayloadLength
    }
}

function Get-ScenarioEventSummary {
    param(
        [object[]]$Records,
        [object]$MissionSummary
    )

    $scenario = $Records | Where-Object { $_.Name -eq 'ScenarioEvent' } | Select-Object -First 1
    if ($null -eq $scenario) {
        return $null
    }

    $marker = [byte[]](0xAF, 0x1E, 0xFF, 0xFF)
    $markerOffsets = Find-PatternOffsets -Bytes $scenario.PayloadBytes -Pattern $marker

    $entries = [System.Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $markerOffsets.Count; $i++) {
        $m = $markerOffsets[$i]
        $postRaw = $null
        $postHex = ''
        if (($m + 7) -lt $scenario.PayloadLength) {
            $postRaw = [BitConverter]::ToInt32($scenario.PayloadBytes, $m + 4)
            $postHex = Get-HexDword -Value $postRaw
        }

        $entries.Add([PSCustomObject]@{
            MarkerIndex = $i
            MarkerOffset = $m
            PostMarkerRaw = $postRaw
            PostMarkerHex = $postHex
        })
    }

    $eventIdHits = [System.Collections.Generic.List[object]]::new()
    if ($null -ne $MissionSummary) {
        foreach ($eid in $MissionSummary.EventIds) {
            $pattern = [BitConverter]::GetBytes([int]$eid.Raw)
            $hits = Find-PatternOffsets -Bytes $scenario.PayloadBytes -Pattern $pattern
            $eventIdHits.Add([PSCustomObject]@{
                EventIdIndex = $eid.Index
                EventIdRaw = $eid.Raw
                EventIdHex = $eid.Hex
                Offsets = @($hits)
            })
        }
    }

    return [PSCustomObject]@{
        Found = $true
        PayloadLength = $scenario.PayloadLength
        MarkerEntries = $entries
        EventIdHits = $eventIdHits
        PayloadHash = Get-PayloadHash -Bytes $scenario.PayloadBytes
    }
}

function Get-ActionTriggerTable {
    param(
        [object[]]$Records,
        [object]$MissionSummary,
        [int]$WordCount
    )

    $table = [System.Collections.Generic.List[object]]::new()
    $nameCounts = @{}

    foreach ($r in $Records | Sort-Object Start) {
        $isTrigger = ($r.Tag -eq 9) -or $r.Name.EndsWith('Trigger')
        $isAction = ($r.Tag -eq 7) -or $r.Name.EndsWith('Action')
        if (-not ($isTrigger -or $isAction)) {
            continue
        }

        if (-not $nameCounts.ContainsKey($r.Name)) {
            $nameCounts[$r.Name] = 0
        }
        $ordinal = [int]$nameCounts[$r.Name]
        $nameCounts[$r.Name] = $ordinal + 1

        $eventHitText = ''
        if ($null -ne $MissionSummary) {
            $hits = [System.Collections.Generic.List[string]]::new()
            foreach ($eid in $MissionSummary.EventIds) {
                $pattern = [BitConverter]::GetBytes([int]$eid.Raw)
                $positions = Find-PatternOffsets -Bytes $r.PayloadBytes -Pattern $pattern
                if ($positions.Count -gt 0) {
                    $hits.Add(('id[{0}]@{1}' -f $eid.Index, ($positions -join ',')))
                }
            }
            $eventHitText = $hits -join '; '
        }

        $table.Add([PSCustomObject]@{
            Key = ('{0}#{1}' -f $r.Name, $ordinal)
            Name = $r.Name
            Ordinal = $ordinal
            Start = $r.Start
            PayloadLength = $r.PayloadLength
            PayloadHash = Get-PayloadHash -Bytes $r.PayloadBytes
            EventIdHits = $eventHitText
            LeadingWords = Get-LeadingAlignedWords -Bytes $r.PayloadBytes -Count $WordCount
        })
    }

    return $table
}

function Build-MapSummary {
    param(
        [string]$MapPath,
        [int]$WordCount
    )

    $parsed = Parse-SegmentA -Path $MapPath
    $records = Get-TokenRecords -Bytes $parsed.SegmentA
    $mission = Get-MissionSummary -Records $records
    $scenario = Get-ScenarioEventSummary -Records $records -MissionSummary $mission
    $actionTrigger = Get-ActionTriggerTable -Records $records -MissionSummary $mission -WordCount $WordCount

    return [PSCustomObject]@{
        MapPath = $MapPath
        HeaderEnd = $parsed.HeaderEnd
        SegmentALength = $parsed.SegmentA.Length
        Tokens = $records
        Mission = $mission
        ScenarioEvent = $scenario
        ActionTrigger = $actionTrigger
    }
}

function Write-MapSection {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Title,
        [object]$Summary
    )

    $Lines.Add("## $Title")
    $Lines.Add("MapPath=$($Summary.MapPath)")
    $Lines.Add("HeaderEnd=$($Summary.HeaderEnd)")
    $Lines.Add("SegmentADecompressedLength=$($Summary.SegmentALength)")
    $Lines.Add('')

    if ($null -eq $Summary.Mission) {
        $Lines.Add('Mission=not found')
    }
    else {
        $Lines.Add('Mission:')
        $Lines.Add("  payloadLen=$($Summary.Mission.PayloadLength) headerSize=$($Summary.Mission.HeaderSize) rowCount=$($Summary.Mission.RowCount)")
        foreach ($eid in $Summary.Mission.EventIds) {
            $Lines.Add(('  eventId[{0}] off={1} raw={2} hex={3}' -f $eid.Index, $eid.Offset, $eid.Raw, $eid.Hex))
        }
    }

    if ($null -eq $Summary.ScenarioEvent) {
        $Lines.Add('ScenarioEvent=not found')
    }
    else {
        $Lines.Add('ScenarioEvent:')
        $Lines.Add("  payloadLen=$($Summary.ScenarioEvent.PayloadLength) payloadHash=$($Summary.ScenarioEvent.PayloadHash)")
        foreach ($entry in $Summary.ScenarioEvent.MarkerEntries) {
            $Lines.Add(('  marker[{0}] off={1} postRaw={2} postHex={3}' -f $entry.MarkerIndex, $entry.MarkerOffset, $entry.PostMarkerRaw, $entry.PostMarkerHex))
        }

        foreach ($hit in $Summary.ScenarioEvent.EventIdHits) {
            $offsetText = if ($hit.Offsets.Count -gt 0) { $hit.Offsets -join ',' } else { 'none' }
            $Lines.Add(('  eventIdHit[{0}] raw={1} hex={2} offs={3}' -f $hit.EventIdIndex, $hit.EventIdRaw, $hit.EventIdHex, $offsetText))
        }
    }

    $Lines.Add('ActionTriggerTokens:')
    foreach ($row in $Summary.ActionTrigger) {
        $Lines.Add(('  {0} start={1} len={2} hash={3} hits={4}' -f $row.Key, $row.Start, $row.PayloadLength, $row.PayloadHash, $row.EventIdHits))
        $wordVec = Format-WordVector -Words $row.LeadingWords
        $Lines.Add(('    words0={0}' -f $wordVec))
    }

    $Lines.Add('')
}

function Write-DiffSection {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [object]$OldSummary,
        [object]$NewSummary
    )

    $Lines.Add('## Diff')

    $oldIds = @()
    if ($null -ne $OldSummary.Mission) {
        $oldIds = $OldSummary.Mission.EventIds | ForEach-Object { $_.Hex }
    }

    $newIds = @()
    if ($null -ne $NewSummary.Mission) {
        $newIds = $NewSummary.Mission.EventIds | ForEach-Object { $_.Hex }
    }

    $Lines.Add(('MissionEventIds old=[{0}] new=[{1}]' -f ($oldIds -join ' | '), ($newIds -join ' | ')))

    $oldScenarioHash = if ($null -ne $OldSummary.ScenarioEvent) { $OldSummary.ScenarioEvent.PayloadHash } else { 'none' }
    $newScenarioHash = if ($null -ne $NewSummary.ScenarioEvent) { $NewSummary.ScenarioEvent.PayloadHash } else { 'none' }
    $Lines.Add("ScenarioEventHash old=$oldScenarioHash new=$newScenarioHash")

    $oldPost = @()
    if ($null -ne $OldSummary.ScenarioEvent) {
        $oldPost = $OldSummary.ScenarioEvent.MarkerEntries | ForEach-Object { $_.PostMarkerHex }
    }
    $newPost = @()
    if ($null -ne $NewSummary.ScenarioEvent) {
        $newPost = $NewSummary.ScenarioEvent.MarkerEntries | ForEach-Object { $_.PostMarkerHex }
    }
    $Lines.Add(('ScenarioEventPostMarkerDwords old=[{0}] new=[{1}]' -f ($oldPost -join ' | '), ($newPost -join ' | ')))

    $oldMap = @{}
    foreach ($row in $OldSummary.ActionTrigger) {
        $oldMap[$row.Key] = $row
    }

    $newMap = @{}
    foreach ($row in $NewSummary.ActionTrigger) {
        $newMap[$row.Key] = $row
    }

    $allKeys = @($oldMap.Keys + $newMap.Keys) | Sort-Object -Unique
    $Lines.Add('ActionTriggerChanges:')

    foreach ($key in $allKeys) {
        $hasOld = $oldMap.ContainsKey($key)
        $hasNew = $newMap.ContainsKey($key)

        if ($hasOld -and -not $hasNew) {
            $Lines.Add("  removed $key")
            continue
        }

        if (-not $hasOld -and $hasNew) {
            $Lines.Add("  added   $key")
            continue
        }

        $old = $oldMap[$key]
        $new = $newMap[$key]

        $changed = @()
        if ($old.PayloadHash -ne $new.PayloadHash) {
            $changed += 'payloadHash'
        }
        if ($old.Start -ne $new.Start) {
            $changed += ('start {0}->{1}' -f $old.Start, $new.Start)
        }
        if ($old.PayloadLength -ne $new.PayloadLength) {
            $changed += ('len {0}->{1}' -f $old.PayloadLength, $new.PayloadLength)
        }
        if ($old.EventIdHits -ne $new.EventIdHits) {
            $changed += 'eventIdHits'
        }

        if ($changed.Count -gt 0) {
            $Lines.Add(('  changed {0} : {1}' -f $key, ($changed -join ', ')))
            $Lines.Add(('    old hits={0}' -f $old.EventIdHits))
            $Lines.Add(('    new hits={0}' -f $new.EventIdHits))
            $Lines.Add(('    old words0={0}' -f (Format-WordVector -Words $old.LeadingWords)))
            $Lines.Add(('    new words0={0}' -f (Format-WordVector -Words $new.LeadingWords)))
        }
    }

    $Lines.Add('')
}

$resolvedOld = (Resolve-Path -LiteralPath $OldMapPath).Path
$resolvedNew = (Resolve-Path -LiteralPath $NewMapPath).Path

if ([System.IO.Path]::GetExtension($resolvedOld).ToLowerInvariant() -ne '.s2m' -or
    [System.IO.Path]::GetExtension($resolvedNew).ToLowerInvariant() -ne '.s2m') {
    throw 'Both inputs must be .s2m files.'
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportsDir = Join-Path (Get-Location).Path 'Notes/reports'
    if (-not (Test-Path -LiteralPath $reportsDir)) {
        [void](New-Item -ItemType Directory -Path $reportsDir)
    }

    $oldName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedOld)
    $newName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedNew)
    $ReportPath = Join-Path $reportsDir ("segmentA_event_link_compare_{0}_vs_{1}.txt" -f $oldName, $newName)
}

$oldSummary = Build-MapSummary -MapPath $resolvedOld -WordCount $WordCount
$newSummary = Build-MapSummary -MapPath $resolvedNew -WordCount $WordCount

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# SegmentA Event Link Compare')
$lines.Add('')
Write-MapSection -Lines $lines -Title 'Old' -Summary $oldSummary
Write-MapSection -Lines $lines -Title 'New' -Summary $newSummary
Write-DiffSection -Lines $lines -OldSummary $oldSummary -NewSummary $newSummary

[System.IO.File]::WriteAllLines($ReportPath, $lines)
Write-Output $ReportPath