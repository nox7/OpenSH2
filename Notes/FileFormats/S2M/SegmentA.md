# Stronghold 2 S2M Segment A

Last updated: 2026-05-29

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

## Parser-First Read Flow

If starting from decompressed Segment A byte `0`, parse in this order:

1. Scan all token records.
2. Sort by `RecordStart`.
3. For each token, compute payload as `[MetadataEnd, NextRecordStart)`.
4. Read first `Mission` token payload:
	- parse mission block header at `+0`, `+4`
	- parse mission event-id list at `+8 + 4*i` for `i in [0..eventCount-1]`
5. Read first `ScenarioEvent` token payload:
	- split into event blocks (first starts at `0`; next starts at `previousMarkerOffset + 20` in current samples)
	- parse each event block fields (`+34`, `+38`, marker, post-marker)
6. Continue tokens in record order:
	- action tokens (`tag==7` or name ends with `Action`)
	- trigger tokens (`tag==9` or name ends with `Trigger`)
7. For each action/trigger payload, read shift-0 word view and marker/post-marker linkage candidate.
8. Validate linkage consistency against Mission event-id order and ScenarioEvent chain.

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

## ScenarioEvent Confirmed Format

Confirmed per-event structure in `ScenarioEvent` payload:

```text
event +34 : int32 trigger_value_bytes
event +38 : int32 trigger_count
event +42 : trigger value dword list
...       : AF 1E FF FF marker dword
...       : post-marker dword
```

Confirmed rules:

- `trigger_count = 1` uses `+34 = 08 00 00 00` and `+38 = 01 00 00 00`.
- `trigger_count = 2` uses `+34 = 0C 00 00 00` and `+38 = 02 00 00 00`.
- The dword immediately after `AF 1E FF FF` is the event-chain field:
	- non-last event: equals the next mission event id
	- last event: terminal-position scalar (observed values include `A0 E0 FD 3F` and `10 DE FD 3F`)
- Mission event-id order and ScenarioEvent chain order match.
- Action-type edits (`Win`/`Lose`) do not change Mission event ids or chain links.
- Event deletion can regenerate the remaining Mission event ids; ScenarioEvent post-marker chain values are rewritten to the regenerated ids.

Detailed probe logs and unresolved hypotheses are in `SegmentA-RnD.md`.

## Action and Trigger Dispatch

- Trigger candidates:
	- `tag == 9` OR name ends with `Trigger`
- Action candidates:
	- `tag == 7` OR name ends with `Action`
- Dispatch is by exact token name to typed parsers.

## Action/Trigger Payload Read Contract

For each action or trigger token payload (in record order):

1. Preserve raw bytes.
2. Build shift-0 int32 word view (`word0` at payload `+0`).
3. Search for marker bytes `AF 1E FF FF`.
4. If marker exists, read dword immediately after marker as linkage candidate.
5. Compare linkage candidate against ScenarioEvent post-marker chain and Mission event ids.

## Structural Spine (Common in Current Samples)

A typical Segment A record order includes:

1. `MapHeader`
2. `EstateMarkers`
3. `Scenario`
4. `Mission`
5. `ScenarioEvent`
6. One or more `*Action` / `*Trigger` records
7. `Trigger` container token

## Mission Payload Confirmed Format

Mission payload scope:

- payload start = `Mission.metadataEnd`
- payload end = first `ScenarioEvent.recordStart`
- current two-entry sample length = `1158`

Mission entry boundaries in current two-entry sample:

- entry 1 = `0..588`
- entry 2 = `589..1157`

Mission entry leading header:

```text
offset +0  : int32 entry_header_size  ; 08, 0C, 10 for row_count 1,2,3
offset +4  : int32 row_count          ; equals event-id count
offset +8  : dword event_id[0]
offset +12 : dword event_id[1]        ; present when row_count >= 2
offset +16 : dword event_id[2]        ; present when row_count >= 3
```

Event-id list and ScenarioEvent chain are aligned in order.

Event ids are not guaranteed stable across mission-event list edits (for example, deleting one event can rewrite the ids of surviving events).

Building availability table:

- entry 1 base = `33`
- entry 2 base = `613`
- encoding: `00=No`, `01=Yes`, `02=1Q`, `03=2Q`, `04=3Q`

Tradeability table:

- marker bytes: `02 00 00 00 5E 00 00 00 2D 00 00 00`
- entry 1 marker at `232`, table window `244..331`
- entry 2 marker at `813`, table window `825..912`
- encoding: `00 00=OFF`, `01 01=ON`, `02 02=1Q`, `03 03=2Q`, `04 04=3Q`

Start-values block (confirmed writable fields):

- entry 1:
	- `342` Gold
	- `350` Popularity
	- `370` Wood
	- `374` Stone
	- `378` Iron
	- `454` Apples
	- `458` Bread
	- `462` Cheese
	- `466` Meat
- entry 2:
	- `923` Gold
	- `931` Popularity
	- `951` Wood
	- `955` Stone
	- `1035` Apples
	- `1039` Bread
	- `1043` Cheese
	- `1047` Meat

Detailed change logs, probe history, and unresolved hypotheses are in `SegmentA-RnD.md`.

## Output of Segment A Parse

Expose at minimum:

- compressed metadata (`StartOffset`, compressed bytes, zlib header)
- decompressed bytes
- token record list with payload ranges
- scenario events with grouped child actions and triggers

