# Stronghold 2 `.s2m` map format notes

Last updated: 2026-05-19
Sample used: `maps/war_chapter1.s2m` (size: 673,069 bytes)

## Current best-known file structure (concise)

This is the practical model to use right now.

1. Header (certain)
- Little-endian.
- String options list: `name` (ASCII string), `value` (UTF-16LE length-prefixed string).
- Integer options list: `name` (ASCII string), `value` (int32).
- Observed across all 48 maps.

2. Segment A (certain location, high-confidence meaning)
- Starts immediately at `headerEnd` with bytes `78 9C`.
- Decoded as zlib header + deflate body.
- Compressed length is consistently 8194 bytes from `headerEnd`.
- Meaning: scenario/mission/script metadata (tokens like `MapHeader`, `Scenario`, `Trigger`, `WinAction`, `LoseAction`).

3. Dominant world payload (high-confidence)
- A later zlib stream exists in many maps at variable offsets.
- Inflates to large payloads (roughly 9.5 MB to 10.6 MB in tested files).
- Meaning: core world/simulation data (tokens like `S2Game`, `Simulation`, `Floaters`, `Landscape`, `FX_ENVIRONMENT_*`).

4. Internal world payload regions (high-confidence pattern, unresolved exact schema)
- Region carving repeatedly finds terrain/grid-like and systems/entity-like zones.
- Frequent dimension-like int32 pairs: `255x255`, `256x256`, `128x128`, `32x32`, `512x512`, `1024x1024`.
- Practical interpretation:
  - core simulation/bootstrap data
  - grid/terrain data
  - environment/fx data
  - entities/pathing/system data

5. Unresolved
- Exact global container/chunk table for all compressed parts is still not fully proven.
- "Exactly 3 sequential top-level compressed chunks" is not yet confirmed as a universal rule.
- Some maps expose dominant world payloads cleanly; others require more robust stream selection logic.

## Unity-facing parsing strategy (current recommendation)

1. Parse header deterministically.
2. Parse Segment A deterministically from `headerEnd`.
3. Discover candidate later zlib streams and select dominant world payload by decompressed size + anchor tokens.
4. Parse dominant payload by semantic regions (not fixed absolute offsets):
  - `core-simulation`
  - `grid-or-terrain`
  - `environment-fx`
  - `entities-and-systems`
5. Preserve unknown regions as raw bytes for forward-compatible decoder updates.

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

## 10) Automated locator run (C# tool)

A C# analyzer was added at `tools/S2mStreamLocator` and executed across all 48 maps.

Generated reports:

- `reports/s2m_stream_locator_report.csv`
- `reports/s2m_stream_locator_summary.md`

High-level outcomes from that run:

- Segment A classified as `scenario-like` in 48/48 maps.
- Segment B (immediately after Segment A end) did not decode as clean raw-deflate in the generalized scanner (0/48 successful direct decodes).
- Dominant large stream classified as `world-like` in 30/48 maps using token heuristics.

By map type (from generated summary):

- `freebuild`: 8 maps, Segment A scenario-like in 8/8, dominant world-like in 5/8.
- `kingmaker`: 21 maps, Segment A scenario-like in 21/21, dominant world-like in 17/21.
- `peacecampaign`: 4 maps, Segment A scenario-like in 4/4, dominant world-like in 1/4.
- `warcampaign`: 15 maps, Segment A scenario-like in 15/15, dominant world-like in 7/15.

Important interpretation note:

- "Not world-like" here means the token classifier was inconclusive, not that a large payload is absent.
- Many large-stream token sets are noisy due to binary data mixed with occasional readable identifiers.

Practical conclusion:

- The automated pass reinforces that Segment A is strongly tied to scenario scripting/configuration.
- A later, much larger compressed payload is consistently present and strongly associated with simulation/landscape/environment data.

## 11) Generic boundary strategy (works per-file, not fixed offsets)

To make sense of *any* map file, use this repeatable strategy instead of hard-coded chunk positions:

1. Parse header (already stable across files).
2. Decode Segment A from `headerEnd` (`78 9C` + deflate body), classify as scenario-like when tokens include `Scenario`, `Trigger`, `MapHeader`.
3. Scan for candidate zlib starts (`0x78 ??` with checksum mod 31) after Segment A and inflate candidates.
4. Pick dominant world payload by decompressed size (large MB-scale stream) and classify with anchor tokens.
5. Carve the dominant payload using 4 signals:
  - anchor token offsets (`S2Game`, `Simulation`, `Floaters`, `Landscape`, `FX_ENVIRONMENT_*`),
  - windowed metric jumps (printable ratio, zero-byte ratio, unique-byte count) every 4096 bytes,
  - length-prefixed ASCII record names,
  - frequent int32 pair patterns that look like dimensions (`255x255`, `256x256`, `128x128`, `32x32`, etc.).

New carving reports generated by the tool:

- `reports/s2m_structure_carving_report.csv`
- `reports/s2m_structure_carving_summary.md`

Observed stable anchors in maps with dominant payloads (30 files):

- `S2Game` appears in 30/30
- `Simulation` appears in 30/30
- `Floaters` appears in 30/30
- `Landscape` appears in 30/30

Observed useful boundary hints:

- First major feature transition commonly appears at offset `4096` in decompressed dominant payloads.
- Frequent dimension-like pairs include values such as `255x255`, `256x256`, `128x128`, `32x32`, `512x512`, `1024x1024`.

Interpretation:

- This gives a practical, map-agnostic parsing workflow: locate anchors, then split into candidate regions via metric transitions and dimension/table signatures.
- Even when top-level compressed sections differ in placement, this method still produces consistent semantic entry points.

## 12) Region-slice scaffolding for reconstruction

The analyzer now emits candidate region slices for each map with a dominant large payload.

New files:

- `reports/s2m_region_slices.csv`
- `reports/s2m_region_slices_summary.md`

What each slice record contains:

- start offset (within decompressed dominant payload)
- end offset
- byte length
- semantic label (`core-simulation`, `grid-or-terrain`, `environment-fx`, `entities-and-systems`, `mixed-unknown`, `binary-opaque`)
- confidence score
- nearby anchor tokens
- top token hints and dimension-pair hints

Latest run highlights:

- region slices produced for 30/48 maps
- average slices per covered map: 20.4
- common labels show a recurring parse order opportunity:
  - core simulation bootstrap
  - grid/terrain data blocks
  - environment/fx blocks
  - entities/systems blocks

Unity planning implication:

- Use these slices as staged import phases instead of trying to decode the entire payload at once.
- Persist unknown slices in raw form so importer revisions can decode them later without breaking compatibility.
