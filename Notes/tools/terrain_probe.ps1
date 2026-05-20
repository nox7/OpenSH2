Set-Location "c:\Steam\steamapps\common\Stronghold 2"
$ErrorActionPreference = 'Stop'

$mapPath = 'maps\war_chapter1.s2m'
$landtexPath = 'landtex.txt'

$landLines = Get-Content $landtexPath
$landNames = New-Object System.Collections.Generic.List[string]
foreach ($ln in $landLines) {
    $t = $ln.Trim()
    if ($t -eq '' -or $t -match '^(GROUND1|GROUND2|CLIFF1|WATER|TREES)$') { continue }
    $t = ($t -split '//')[0].Trim()
    if ($t -eq '') { continue }
    $name = ($t -split '\s+')[0].Trim()
    if ($name -ne '') { $landNames.Add($name) }
}
$landNames = $landNames | Select-Object -Unique

$b = [IO.File]::ReadAllBytes($mapPath)
$ms = New-Object IO.MemoryStream(,$b)
$br = New-Object IO.BinaryReader($ms)
$sc = $br.ReadInt32()
for ($i = 0; $i -lt $sc; $i++) {
    $k = $br.ReadInt32(); [void]$br.ReadBytes($k)
    $v = $br.ReadInt32(); [void]$br.ReadBytes($v * 2)
}
$ic = $br.ReadInt32()
for ($i = 0; $i -lt $ic; $i++) {
    $k = $br.ReadInt32(); [void]$br.ReadBytes($k)
    [void]$br.ReadInt32()
}
$headerEnd = [int]$ms.Position
$segAEnd = $headerEnd + 8194

function Try-InflateZ([byte[]]$bytes, [int]$off) {
    try {
        $m = New-Object IO.MemoryStream(,$bytes)
        $m.Position = $off + 2
        $d = New-Object IO.Compression.DeflateStream($m, [IO.Compression.CompressionMode]::Decompress, $true)
        $o = New-Object IO.MemoryStream
        $buf = New-Object byte[] 16384
        $tot = 0
        while (($r = $d.Read($buf, 0, $buf.Length)) -gt 0) {
            $o.Write($buf, 0, $r)
            $tot += $r
            if ($tot -gt 50000000) { break }
        }
        [PSCustomObject]@{ ok = $true; end = [int]$m.Position; decomp = $tot; payload = $o.ToArray() }
    } catch {
        [PSCustomObject]@{ ok = $false; err = $_.Exception.Message }
    }
}

$best = $null
for ($i = $segAEnd; $i -lt $b.Length - 2; $i++) {
    if ($b[$i] -ne 0x78) { continue }
    $flg = $b[$i + 1]
    if (((0x78 * 256 + $flg) % 31) -ne 0) { continue }
    $r = Try-InflateZ $b $i
    if (-not $r.ok) { continue }
    if ($r.decomp -lt 1000000) { continue }
    if ($best -eq $null -or $r.decomp -gt $best.decomp) {
        $best = [PSCustomObject]@{ off = $i; end = $r.end; decomp = $r.decomp; payload = $r.payload }
    }
}
if ($best -eq $null) { throw 'No dominant large stream found' }

$u = $best.payload
$text = [Text.Encoding]::ASCII.GetString($u)

$hits = New-Object System.Collections.Generic.List[object]
foreach ($n in $landNames) {
    $idx = $text.IndexOf($n, [StringComparison]::OrdinalIgnoreCase)
    if ($idx -ge 0) { $hits.Add([PSCustomObject]@{ name = $n; offset = $idx }) }
}

$terrainTokens = @('HeightLayer', 'Landscape', 'Land', 'Texture', 'Tile', 'Ground', 'Water', 'Cliff', 'Tree', 'DesirabilityLayer', 'ContextLayer')
$tokenHits = @()
foreach ($t in $terrainTokens) {
    $idx = 0
    while ($true) {
        $idx = $text.IndexOf($t, $idx, [StringComparison]::OrdinalIgnoreCase)
        if ($idx -lt 0) { break }
        $tokenHits += [PSCustomObject]@{ token = $t; offset = $idx }
        $idx += [Math]::Max(1, $t.Length)
    }
}
$tokenHits = $tokenHits | Sort-Object offset

function Get-I32([byte[]]$arr, [int]$o) {
    if ($o -lt 0 -or $o + 4 -gt $arr.Length) { return $null }
    return [BitConverter]::ToInt32($arr, $o)
}

$dimCandidates = New-Object System.Collections.Generic.List[object]
$expected = @('255x255', '256x256', '128x128', '32x32', '512x512', '1024x1024')
foreach ($h in $tokenHits) {
    $start = [Math]::Max(0, $h.offset - 4096)
    $end = [Math]::Min($u.Length - 8, $h.offset + 4096)
    for ($o = $start; $o -le $end; $o += 4) {
        $w = Get-I32 $u $o
        $hgt = Get-I32 $u ($o + 4)
        if ($w -eq $null -or $hgt -eq $null) { continue }
        if ($w -lt 16 -or $w -gt 4096 -or $hgt -lt 16 -or $hgt -gt 4096) { continue }
        $pair = "$w`x$hgt"
        if ($pair -in $expected) {
            $dimCandidates.Add([PSCustomObject]@{ token = $h.token; tokenOffset = $h.offset; pair = $pair; pairOffset = $o })
        }
    }
}

$arrayHeur = New-Object System.Collections.Generic.List[object]
$uniquePairs = $dimCandidates | Sort-Object pairOffset -Unique
foreach ($d in $uniquePairs | Select-Object -First 150) {
    $parts = $d.pair.Split('x')
    $w = [int]$parts[0]
    $hgt = [int]$parts[1]
    foreach ($bpp in 1, 2, 4) {
        $need = $w * $hgt * $bpp
        $lenField = Get-I32 $u ($d.pairOffset + 8)
        if ($lenField -ne $null -and ($lenField -eq $need -or $lenField -eq ($w * $hgt))) {
            $arrayHeur.Add([PSCustomObject]@{
                pair = $d.pair
                pairOffset = $d.pairOffset
                bpp = $bpp
                lenField = $lenField
                token = $d.token
                tokenOffset = $d.tokenOffset
            })
        }
    }
}

"map=$mapPath"
"dominantOffset=$($best.off) dominantDecomp=$($best.decomp)"
"landtexNames=$($landNames.Count) landtexMatched=$($hits.Count)"
if ($hits.Count -gt 0) {
    'landtexMatches:'
    $hits | Select-Object -First 30 | Format-Table -AutoSize
}
"terrainTokenHits=$($tokenHits.Count)"
$tokenHits | Group-Object token | Sort-Object Count -Descending | Select-Object -First 12 | Format-Table -AutoSize
"dimCandidateCount=$($dimCandidates.Count) uniquePairOffsets=$($uniquePairs.Count)"
$dimCandidates | Select-Object -First 30 | Format-Table -AutoSize
"arrayHeuristicMatches=$($arrayHeur.Count)"
$arrayHeur | Select-Object -First 40 | Format-Table -AutoSize
