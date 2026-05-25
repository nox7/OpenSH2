# Stronghold 2 S2M Format Docs

Last updated: 2026-05-24

This folder contains the parser-oriented split documentation for `.s2m`.

## Read Order

1. `Header.md`
2. `SegmentA.md`
3. `SegmentB.md`

## Parse Pipeline Summary

1. Parse header key/value tables to get `headerEnd`.
2. Parse Segment A from fixed compressed span at `headerEnd`.
3. Parse Segment B by scanning after Segment A for zlib candidates and selecting dominant world payload.

