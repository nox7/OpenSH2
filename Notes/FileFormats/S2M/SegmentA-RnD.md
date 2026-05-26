# Stronghold 2 S2M Segment A RnD Notes

Last updated: 2026-05-25

## Purpose

This file stores experiment history, detailed byte-diff notes, and unresolved hypotheses.
`SegmentA.md` is the confirmed parser-spec reference.

## ScenarioEvent Probe History

### Trigger Count Expansion

- One event, one trigger (`Win` + `GoldAcquired=10`) baseline produced `trigger_count=1` and `trigger_value_bytes=8`.
- Adding a second trigger in the same event changed:
	- `+34`: `08 00 00 00 -> 0C 00 00 00`
	- `+38`: `01 00 00 00 -> 02 00 00 00`
	- one additional trigger dword inserted before `AF 1E FF FF`.

### Event Chain Expansion

- Adding a second event changed first-event post-marker dword from terminal-position value to next event id.
- Adding a third event repeated the same chain behavior:
	- event1 post-marker -> event2 id
	- event2 post-marker -> event3 id
	- event3 post-marker -> terminal-position scalar

### Action-Type Probes (`Win`/`Lose`)

- Event2 `Win -> Lose` with triggers unchanged:
	- Mission bytes unchanged.
	- ScenarioEvent changed only event2-local scalar `A0 DD FD 3F -> 00 DF FD 3F`.
- Event1 `Win -> Lose` (event2 already Lose):
	- Mission bytes unchanged.
	- ScenarioEvent changed event1-local early scalar `A0 E0 FD 3F -> 10 DE FD 3F`.
	- The final event post-marker terminal-position scalar also changed to `10 DE FD 3F`.

## Mission Probe History

### Entry Count and Event Id List

- Mission entry header scales with row/event count:
	- rows=1: `+0=08`, `+4=01`
	- rows=2: `+0=0C`, `+4=02`
	- rows=3: `+0=10`, `+4=03`
- Event ids occupy contiguous dwords starting at `+8`.

Observed 3-event list:

- `A8 F0 11 40`
- `08 E0 01 40`
- `F0 17 06 40`

### Cross-Type Baseline Notes

Blank/default map-type baseline (`Kingmaker`, `WarCustom`, `PeaceCustom`, `FreeBuild`) produced identical single-entry mission payloads in tested data:

- length `569`
- sha256 `32df84aeb18707f6a551bf85f9fee825c2ebafd9c0e5cefa2cf050ec8f17da95`

### FreeBuild Start-Value Edit Notes

Test edit (`Wood 72->77`, `Gold 500->510`) changed known start-value fields and also changed a mirrored float-like dword at both entry head and tail in the tested sample.

## Unconfirmed or Open Items

- Candidate `ScenarioEvent` fields from parser offsets:
	- absolute `+99` month candidate
	- absolute `+117` delay candidate
	- payload `+8` repeat count candidate
	- payload `+12` repeat time candidate
- Exact semantics of event-local float-like scalars in each event header.
- Exact semantics of the last-event post-marker terminal-position scalar.
- Full start-values goods ordering and all undecoded mission dwords outside currently mapped fields.

## Suggested Next Probes

- Reorder events without changing actions/triggers to test whether Mission id order is authoritative for chain order.
- Toggle action type on only the last event to test whether terminal-position scalar follows last-event action type.
- Keep action fixed and vary trigger type/value to separate trigger-value dwords from other event-local scalars.

