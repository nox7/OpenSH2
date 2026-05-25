# Stronghold 2 S2M Segment A

Last updated: 2026-05-24

## Scope

This document defines how to locate, decompress, and parse Segment A (mission/script graph).

## Placement and Length

- Segment A starts at `headerEnd`.
- Segment A compressed span is currently parsed as a fixed length of `8194` bytes.
- The compressed bytes are zlib (`0x78 0x9C` seen in tested files).

## Segment A Parse Order

1. Slice compressed bytes at `[headerEnd, headerEnd + 8194)` (clamped to file length).
2. Inflate zlib payload.
3. Scan decompressed bytes for token records.
4. Sort token records by `RecordStart`.
5. Compute payload range for each record using next record start.
6. Build scenario-event hierarchy:
	 - `ScenarioEvent` records define event spans.
	 - Actions/triggers are assigned to the containing event span.
7. Dispatch token records by name/tag to typed action/trigger decoders.

## Token Record Layout

Each candidate token record in decompressed Segment A is parsed as:

```text
int32 id
int32 nameLen
byte[nameLen] name (ASCII)
int32 tag
int32 baseNameLen
byte[baseNameLen] baseName (ASCII, optional)
byte[payloadLength] payload
```

Where:

- `payloadStart = metadataEnd`
- `payloadLength = nextRecordStart - payloadStart`
- `nextRecordStart` is next token record start, or end of decompressed Segment A.

## Token Scan Heuristics (Current Implementation)

When scanning byte-by-byte for token starts:

- `id` must be in `0..500000`
- `nameLen` must be in `1..128`
- `tag` must be in `0..128`
- `baseNameLen` must be in `0..128`
- `name` and `baseName` must be ASCII-safe identifiers (`A-Z`, `a-z`, `0-9`, `_`).

After scan:

- Deduplicate by `RecordStart`.
- Sort ascending by `RecordStart`.

## Scenario Event Ownership Model

`ScenarioEvent` is the parent mission container.

Event span rule:

- For event `i`: `[eventStart(i), eventStart(i+1))`
- Last event ends at decompressed Segment A length.

Assignment rule:

- A record belongs to the event whose span contains `record.RecordStart`.
- `ScenarioEvent` records themselves are not nested as children.

## ScenarioEvent Known Field Offsets

Current known fields used by parser:

- Absolute to event record start:
	- `+99` byte: month candidate
	- `+117` byte: delay candidate
- Relative to event payload bytes:
	- `+8` int32: repeat count candidate
	- `+12` int32: repeat time candidate

## Action and Trigger Dispatch

- Trigger candidates:
	- `tag == 9` OR name ends with `Trigger`
- Action candidates:
	- `tag == 7` OR name ends with `Action`
- Dispatch is by exact token name to typed parsers.

## Structural Spine (Common in Current Samples)

A typical Segment A record order includes:

1. `MapHeader`
2. `EstateMarkers`
3. `Scenario`
4. `Mission`
5. `ScenarioEvent`
6. One or more `*Action` / `*Trigger` records
7. `Trigger` container token

## Mission Payload Layout (Current Known Format)

This section is the parser-oriented summary of the `Mission` token payload only.

### Payload Scope

- Mission payload start = `Mission.metadataEnd`
- Mission payload end = first `ScenarioEvent.recordStart`
- Current 2-mission sample payload length = `1158`
- Current mission-entry split:
	- Mission 1 = payload `0..588`
	- Mission 2 = payload `589..1157`

### Split Rule (Observed)

- The payload contains two mission-entry records concatenated back-to-back.
- Mission 2 start is not fixed across edits.
- Observed Mission 2 starts:
	- `581` when Mission 1 had 1 row
	- `585` when Mission 1 had 2 rows
	- `589` when Mission 1 had 3 rows
- Mission 1 grows by `+4` bytes per added row.

### Mission Entry Leading Header

Known fields at mission-entry start:

```text
offset +0  : int32  entry_size_or_weight     ; increases by +4 per added row
offset +4  : int32  row_count                ; observed 1, 2, 3
offset +8+ : dword list / header words       ; grows as rows are added
```

Observed Mission 1 leading words across row-count growth:

```text
rows=1: +0=08 00 00 00  +4=01 00 00 00
rows=2: +0=0C 00 00 00  +4=02 00 00 00
rows=3: +0=10 00 00 00  +4=03 00 00 00
```

Interpretation:

- `+0` is a size-like field
- `+4` is a row-count field
- the remainder of the leading header is still not semantically decoded

### Mission 1 Linear Map (Current 1158-byte Payload)

```text
0..32     dynamic row header / row metadata / row-reference words
33..138   building availability table
139..231  unknown / not yet decoded
232..243  tradeability marker / preamble
244..331  tradeability table
332..467  start-values block (gold / popularity / goods / nearby metadata)
468..543  zero-filled region in current sample
544..588  mission tail / footer / unknown
```

### Mission 2 Linear Map (Current 1158-byte Payload)

```text
589..612    dynamic row header / row metadata / row-reference words
613..718    building availability table
719..812    unknown / not yet decoded
813..824    tradeability marker / preamble
825..912    tradeability table
913..1048   start-values block (gold / popularity / goods / nearby metadata)
1049..1124  zero-filled region in current sample
1125..1157  mission tail / footer / unknown
```

### Building Availability Table

Encoding:

```text
00 = No
01 = Yes
02 = 1Q
03 = 2Q
04 = 3Q
```

Current known navigation:

```text
Mission 1 early table base = 33
Mission 2 early table base = 613
```

Confirmed Mission 1 early entries:

```text
33 = Stockpile
34 = Saw Pit
35 = Wheat Farm
36 = Stone Quarry
37 = Mill
```

Later-table rule:

- Later building entries are serialized with one omitted slot before `Market`.
- Current best mapping treats the early `SallyPort = 33` enum entry as omitted from serialized indexing.
- The editor-visible Sally Port toggle matched the later duplicate slot (`SallyPort2`).

### Tradeability Marker And Table

Marker bytes:

```text
02 00 00 00 5E 00 00 00 2D 00 00 00
```

Current marker offsets:

```text
Mission 1 marker = 232
Mission 2 marker = 813
```

Current table windows:

```text
Mission 1 tradeability table = 244..331
Mission 2 tradeability table = 825..912
```

Encoding:

```text
00 00 = OFF
01 01 = ON
02 02 = 1Q
03 03 = 2Q
04 04 = 3Q
```

Known Mission 1 pair indices:

```text
pair 1  -> Wood
pair 4  -> Wheat
pair 10 -> Candles
pair 33 -> Mace
pair 36 -> Metal Armor
pair 37 -> Leather Armor
```

### Start-Values Block

Current known Mission 1 fields:

```text
342 = Gold       (500)
350 = Popularity (80)
370 = Wood       (72)
374 = Stone      (40)
378 = Iron       (confirmed writable field)
454 = Apples     (10)
458 = Bread      (10)
462 = Cheese     (10)
466 = Meat       (10)
```

Current known Mission 2 fields:

```text
923  = Gold       (500)
931  = Popularity (80)
951  = Wood       (72)
955  = Stone      (40)
1035 = Apples     (10)
1039 = Bread      (10)
1043 = Cheese     (10)
1047 = Meat       (10)
```

Current interpretation:

- gold and popularity are explicit mission-local fields
- goods are stored in mission-local quantity fields
- at least the early goods region uses 4-byte little-endian integers
- the full 29-goods ordering is not yet fully pinned
- nearby words at `334`, `338`, `354` are still unknown

### Zero Region

Current sample contains a long zero-filled span per mission entry:

```text
Mission 1 = 468..543
Mission 2 = 1049..1124
```

Meaning is unknown.

### Mission Tail / Footer

Current sample contains an undecoded tail per mission entry after the zero-filled span.

Known Mission 1 tail words:

```text
544..547 = 00 00 06 00
548..551 = 00 00 0F 00
568..571 = 00 AF 1E FF
572..575 = FF 08 66 E9
576..579 = 3F 04 00 00
580..583 = 00 04 00 00
584..588 = 00 00 00 00 00
```

Meaning is unknown.

### What Is Confirmed Versus Unknown

Confirmed:

- the `Mission` token contains two concatenated mission-entry records
- current mission-entry boundaries are `0..588` and `589..1157`
- entry size grows with Mission 1 row count by `+4` bytes per added row
- each mission entry contains:
	- leading row metadata
	- building availability
	- tradeability
	- start gold / popularity / goods fields
	- zero-filled span
	- trailing undecoded footer

Still unknown:

- exact row-record encoding and row-reference fields in the leading header
- full 29-goods ordering in the start-values block
- precise meaning of the unknown ranges between known blocks
- precise meaning of the trailing footer words

## Output of Segment A Parse

Expose at minimum:

- compressed metadata (`StartOffset`, compressed bytes, zlib header)
- decompressed bytes
- token record list with payload ranges
- scenario events with grouped child actions and triggers

