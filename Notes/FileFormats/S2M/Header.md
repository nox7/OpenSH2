# Stronghold 2 S2M Header

Last updated: 2026-05-24

## Scope

This document defines the deterministic file header at the start of a `.s2m` file.

## Primitive Types

- Endianness: little-endian.
- `int32`: signed 4 bytes.
- `string` (ASCII):
	- `int32 length`
	- `length` bytes ASCII payload.
- `wstring` (UTF-16LE):
	- `int32 length` (character count)
	- `length * 2` bytes UTF-16LE payload.

## Binary Layout (In Order)

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

## Parse Algorithm

1. Read `stringOptionCount` (`int32`).
2. For each string option:
	 - Read ASCII `optionName`.
	 - Read UTF-16LE `optionValue`.
3. Read `intOptionCount` (`int32`).
4. For each int option:
	 - Read ASCII `optionName`.
	 - Read `optionValue` (`int32`).
5. Record `headerEnd` as the current byte position.

## Validation Rules Used by Parser

- `stringOptionCount` and `intOptionCount` must be in range `0..2048`.
- ASCII and UTF-16 string lengths must be in range `0..1,048,576`.
- A short read while reading any string is an EOF error.

## Known Invariants

- Header immediately precedes Segment A.
- In tested maps, these key sets are stable:
	- string keys: `author`, `type`
	- int keys:
		- `balanced,lastsave,maxplayers,version`
		- or `balanced,lastsave,mapsize,maxplayers,version`

## Output of Header Parse

The parser should expose:

- `StringOptions` list
- `IntOptions` list
- `EndOffset` (`headerEnd`)

