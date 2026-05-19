# Stronghold 2 `.s2m` map format notes

Last updated: 2026-05-19
Sample used: `maps/war_chapter1.s2m` (size: 673,069 bytes)

## 1) Confirmed primitive types

All observations below are consistent with little-endian encoding.

- `int32`: signed 4-byte integer (little-endian)
- `string`: `int32 length` + `length` bytes (ASCII in observed keys)
- `wstring`: `int32 length` + `length * 2` bytes (UTF-16LE in observed values)
- `list<T>`: `int32 count` + repeated `T` payload

## 2) Confirmed file header layout

This header model is validated by parsing all 48 `.s2m` files in `maps/`.

```text
int32 stringOptionCount
repeat stringOptionCount:
  string optionName
  wstring optionValue

int32 intOptionCount
repeat intOptionCount:
  string optionName
  int32 optionValue
```

### Example: `maps/war_chapter1.s2m`

Parsed header values:

- String options (count = 2)
  - `author` = `Firefly`
  - `type` = `warcampaign`
- Integer options (count = 4)
  - `balanced` = 1
  - `lastsave` = 25383421
  - `maxplayers` = 1
  - `version` = 1

Header end offset: `135` (0x87)

Bytes at offset 135 begin with `78 9C`.

## 3) Cross-file header observations (48 files)

- String-key set is always exactly: `author,type`
- Integer-key sets observed:
  - `balanced,lastsave,maxplayers,version` (29 files)
  - `balanced,lastsave,mapsize,maxplayers,version` (19 files)
- Header length range: `131` to `150` bytes
- The first 2 bytes immediately after header are always `78 9C`

This strongly supports that data immediately after the header starts with a zlib-like stream marker.

## 4) First compressed region behavior (empirical)

Using .NET `DeflateStream` from `headerEnd + 2` (skip `78 9C`) on all `.s2m` files:

- Parsing succeeds on all tested files.
- Stream cursor after decode is consistently `headerEnd + 8194`.
  - For `war_chapter1.s2m`: header end `135`, cursor `8329`.
- Decompressed size of this first region varies by map.
  - Typical values around ~1.6 KB for many skirmish/freebuild maps.
  - Much larger in some campaign maps (example: `war_chapter1.s2m` = 3,914 bytes; `peace_chapter1.s2m` = 37,973 bytes).

Interpretation so far:

- There is definitely compressed content beginning right after the header.
- The first compressed payload appears to occupy a fixed compressed byte budget of 8,194 bytes relative to header start in all tested files.
- The exact container semantics after this first region are still unknown.

## 5) Decompressed payload reconnaissance (content-focused)

### Segment A (immediately after header)

For `war_chapter1.s2m`:

- Compressed: offset `135` to `8329` (8194 bytes, starts with `78 9C`)
- Decompressed size: `3914` bytes
- Contains clear semantic tokens:
  - `MapHeader`
  - `EstateMarkers`
  - `Scenario`
  - `Mission`
  - `ScenarioEvent`
  - `LoseAction`, `WinAction`
  - Trigger/action classes like `LordDiesTrigger`, `AlwaysTrigger`, `SetAlliesAction`

Cross-map checks (war, peace, freebuild, kingmaker samples) consistently show these scenario/script-related labels in Segment A.

Interpretation: this strongly supports the hypothesis that the first compressed payload stores scenario/global mission configuration.

### Segment B (next decodable block in some files)

For `war_chapter1.s2m`:

- Raw-deflate decodable at offset `8329`
- Compressed length consumed: `8192` bytes
- Decompressed size: `2264` bytes
- No readable ASCII strings of length >= 5

For other tested maps, this block may decode to 0 bytes or fail depending on entry point assumptions.

Interpretation: likely binary table/array data, not human-readable script text.

### Large later zlib stream candidate

In multiple maps, a later offset contains `78 9C` and inflates to a very large payload (around 9.8 MB to 10.5 MB in samples).

Examples:

- `war_chapter1.s2m`: offset `14127`, decompressed `10397138`
- `Coastal County.s2m`: offset `14689`, decompressed `9807959`
- `peace_chapter1.s2m`: offset `23730`, decompressed `10533942`

Extracted identifier-like tokens include engine/system terms such as:

- `S2Game`
- `Simulation`
- `Floaters`
- `Landscape`
- `FX_ENVIRONMENT_OCEAN`
- `FX_ENVIRONMENT_BEACH`
- `FX_ENVIRONMENT_WINDLIGHT`

Interpretation: this payload likely contains core world-state data and environment/static map content, and may also contain dynamic state.

## 6) Current uncertainty on the "3 compressed parts" claim

The fandom claim says there are 3 compressed parts. Current parsing confirms:

- Header: confirmed
- Scenario/script payload near file start: strongly supported
- Additional large compressed payload later in file: strongly supported
- Exact top-level segmentation into exactly 3 canonical chunks: not yet confirmed

Reason: candidate streams overlap and not all maps expose clean, contiguous boundaries via a single inflate strategy.

## 7) Practical parser status

You can already implement a reliable **header parser** and attach a tentative **first compressed block extractor**.

Minimal header parser pseudocode:

```text
read int32 stringCount
for i in 0..stringCount-1:
  name = readString()
  value = readWString()

read int32 intCount
for i in 0..intCount-1:
  name = readString()
  value = readInt32()

headerEnd = stream.Position
```

## 8) Next reverse-engineering steps

1. Build a stream locator that records all valid inflate windows and classifies each as:
  - scenario-like (contains `Scenario`/`Trigger` tokens),
  - world-like (contains `Simulation`/`Landscape`/`FX_ENVIRONMENT_*`),
  - binary-only (no string markers).
2. Compare locator output between maps of different `type` values (`warcampaign`, `peacecampaign`, `freebuild`, `kingmaker`) to find stable offsets/patterns.
3. For the large world-like stream, begin structure carving using repeating fixed-size arrays (candidate terrain grids, object lists, marker tables).
4. Confirm whether "static vs dynamic" is:
  - split across distinct streams,
  - or interleaved records within the same large stream.

## 9) Working assumptions (subject to change)

- Endianness: little-endian
- Header schema: high confidence
- Compression family: zlib/deflate present
- Segment A likely stores scenario/global script metadata
- Large later stream likely stores world-state data (static and possibly dynamic)
- Number of top-level compressed sections: unresolved
