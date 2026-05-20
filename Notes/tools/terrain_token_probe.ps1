Set-Location "c:\Steam\steamapps\common\Stronghold 2"
$ErrorActionPreference = 'Stop'

$p = 'maps\war_chapter1.s2m'
$b = [IO.File]::ReadAllBytes($p)

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

function TryInflateZ([byte[]]$bytes, [int]$off) {
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
        [PSCustomObject]@{ ok = $true; decomp = $tot; payload = $o.ToArray() }
    } catch {
        [PSCustomObject]@{ ok = $false }
    }
}

$best = $null
for ($i = $segAEnd; $i -lt $b.Length - 2; $i++) {
    if ($b[$i] -ne 0x78) { continue }
    $flg = $b[$i + 1]
    if (((0x78 * 256 + $flg) % 31) -ne 0) { continue }
    $r = TryInflateZ $b $i
    if (-not $r.ok -or $r.decomp -lt 1000000) { continue }
    if ($best -eq $null -or $r.decomp -gt $best.decomp) {
        $best = [PSCustomObject]@{ off = $i; payload = $r.payload; decomp = $r.decomp }
    }
}
if ($best -eq $null) { throw 'No dominant payload' }

$u = $best.payload
$text = [Text.Encoding]::ASCII.GetString($u)
$keys = @('HeightLayer', 'ContextLayer', 'DesirabilityLayer', 'Landscape', 'TextureLayer', 'Ground', 'Tree')

"dominantOffset=$($best.off) decomp=$($best.decomp)"

foreach ($k in $keys) {
    $idx = $text.IndexOf($k, [StringComparison]::Ordinal)
    if ($idx -lt 0) { continue }

    "=== $k at $idx ==="
    $winStart = [Math]::Max(0, $idx - 256)
    $winEnd = [Math]::Min($u.Length - 1, $idx + 1024)

    for ($o = $winStart; $o -le $winEnd - 8; $o += 4) {
        $a = [BitConverter]::ToInt32($u, $o)
        $b2 = [BitConverter]::ToInt32($u, $o + 4)
        if (($a -in 32,64,128,255,256,512,1024,2048) -and ($b2 -in 32,64,128,255,256,512,1024,2048)) {
            "  pair @$o : $a x $b2"
        }
    }

    $subLen = [Math]::Min(1800, $text.Length - $winStart)
    $m = [regex]::Matches($text.Substring($winStart, $subLen), '[A-Za-z][A-Za-z0-9_]{4,}')
    $tok = $m | ForEach-Object { $_.Value } | Select-Object -Unique | Select-Object -First 30
    "  nearbyTokens: " + ($tok -join ', ')
}
