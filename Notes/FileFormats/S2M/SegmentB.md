# Stronghold 2 S2M Segment B (World Payload)

Last updated: 2026-05-24

## Scope

This document defines the current parser strategy for the post-Segment-A world payload region (commonly called Segment B).

## Start of Segment B Scan

- `segmentAEnd = headerEnd + 8194` (clamped to file length).
- Scan begins at `segmentAEnd`.

## Candidate Discovery

1. Iterate each byte offset from `scanStart` to EOF-2.
2. If two bytes at offset match a zlib header signature, attempt inflate from that offset.
3. For each successful inflate, record candidate:
	- compressed offset
	- zlib header bytes
	- decompressed length
	- anchor hit count

## Dominant Candidate Selection

Choose dominant world payload as:

1. Highest decompressed length.
2. Tie-break: highest anchor hit count.

Current anchor strings include:

- `S2Game`
- `Simulation`
- `Floaters`
- `Landscape`
- `FX_ENVIRONMENT_`
- `HeightLayer`

## HeightLayer Block Parse (Implemented)

Within dominant payload:

1. Find ASCII `HeightLayer` label.
2. Search nearby for an int32 pair `(rowByteWidth, rowCount)` where:
	- both positive
	- `rowByteWidth % 4 == 0`
	- `rowByteWidth <= 8192`
	- `rowCount <= 2048`
	- data block fits payload bounds
3. Set:
	- `dimensionsOffset = pairOffset`
	- `dataOffset = pairOffset + 8`
	- `byteLength = rowByteWidth * rowCount`
	- `tileWidth = rowByteWidth / 4`
	- `tileHeight = rowCount`

Common observed pair is `1024 x 256`, which yields:

- `tileWidth = 256`
- `tileHeight = 256`
- total data bytes `262144`

## HeightLayer Tile Record

Each tile is 4 bytes:

1. byte 0: raw elevation
2. byte 1: smoothed elevation
3. byte 2: zone/type index candidate
4. byte 3: texture/flags candidate

## Current Parse Output for Segment B

Expose:

- scan start offset
- all zlib candidates found
- dominant candidate
- dominant decompressed bytes
- parsed HeightLayer block (or parse issue)

## Known Limits (Still Unresolved)

- There is no final complete schema for all dominant payload subsystems yet.
- Region carving beyond HeightLayer is heuristic.
- Exact serialization contracts for many world-system blocks remain unresolved.

