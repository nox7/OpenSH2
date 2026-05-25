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

## Output of Segment A Parse

Expose at minimum:

- compressed metadata (`StartOffset`, compressed bytes, zlib header)
- decompressed bytes
- token record list with payload ranges
- scenario events with grouped child actions and triggers

