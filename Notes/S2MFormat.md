# Stronghold 2 `.s2m` map format notes

Last updated: 2026-05-19
Sample used: `maps/war_chapter1.s2m` (size: 673,069 bytes)

## Current working spec (2026-05-19)

### Certainty levels

- **Certain**: validated on all 48 maps in `maps/`.
- **High confidence**: strong repeated evidence, but not full schema decode.
- **Unresolved**: still under reverse-engineering.

### 1) Primitive encodings (certain)

- Endianness: little-endian.
- `int32`: signed 4-byte integer.
- `string`: `int32 length` + ASCII bytes.
- `wstring`: `int32 length` + UTF-16LE bytes (`length * 2`).
- `list<T>`: `int32 count` + repeated `T`.

### 2) Header layout (certain)

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

Cross-file invariants:

- String keys are always: `author`, `type`.
- Int keys are either:
  - `balanced,lastsave,maxplayers,version`
  - `balanced,lastsave,mapsize,maxplayers,version`
- Header length range: 131 to 150 bytes.

### 3) Segment A immediately after header (certain location, high-confidence meaning)

- `headerEnd` always points to bytes `78 9C`.
- Decoding from `headerEnd + 2` with deflate is successful.
- Compressed span is consistently 8194 bytes from `headerEnd`.
- Decompressed size varies by map.
- Content is scenario/script oriented (`MapHeader`, `Scenario`, `Trigger`, `WinAction`, etc.).

Additional high-confidence finding inside Segment A:

- Many scenario tokens are embedded in repeatable record-like structures of the form:
  - `int32 id`
  - `int32 nameLen`
  - `nameLen bytes name` (ASCII token)
  - `int32 tag`
  - `int32 baseNameLen`
  - optional `baseNameLen bytes baseName` (ASCII)

Example matches from `war_chapter1.s2m` Segment A:

- `id=6  name=LoseAction                 tag=7  base=ScenarioAction`
- `id=19 name=WinAction                  tag=7  base=(none)`
- `id=8  name=LordDiesTrigger            tag=9  base=Trigger`
- `id=11 name=AlwaysTrigger              tag=9  base=(none)`
- `id=20 name=CustomChapter1Trigger      tag=9  base=(none)`

Interpretation:

- Tokens are not standalone markers; they carry associated metadata immediately after the token name.
- `tag` and `baseName` appear to encode type hierarchy/category information (exact semantics still unresolved).

### 4) Dominant world payload later in file (high confidence)

- Additional zlib candidates exist later at variable offsets.
- A dominant large stream is often present (~9.5 MB to ~10.6 MB decompressed in tested samples).
- Frequent anchors: `S2Game`, `Simulation`, `Floaters`, `Landscape`, `FX_ENVIRONMENT_*`.
- Interpretation: core world/simulation/environment payload.

### 5) Region-level structure inside dominant payload (high-confidence pattern)

Heuristic carving identifies recurring semantic zones:

- `core-simulation`
- `grid-or-terrain`
- `environment-fx`
- `entities-and-systems`
- plus `mixed-unknown` / `binary-opaque`

Frequent dimension-like int32 pairs:

- `255x255`, `256x256`, `128x128`, `32x32`, `512x512`, `1024x1024`

### 6) Terrain/texturing correlation findings (new)

`landtex.txt` correlation:

- Direct ASCII name matching of `landtex.txt` texture entries against the dominant payload of `war_chapter1.s2m` found **0 direct filename hits**.
- Current implication: terrain texture references in `.s2m` are likely stored as indices/enums/indirect IDs rather than literal BMP filenames.

`HeightLayer` anchor correlation (cross-map check):

- In sampled maps (`war_chapter1`, `Coastal County`, `peace_chapter1`, `Arena of Kings`), `HeightLayer` appears in dominant payload and has a nearby repeated int32 pair:
  - `1024 x 256`
- Observed examples:
  - `war_chapter1`: `HeightLayer` at `5651748`, pair `1024x256` at `5651796`
  - `Coastal County`: `HeightLayer` at `5557690`, pair `1024x256` at `5557770`
  - `peace_chapter1`: `HeightLayer` at `5908234`, pair `1024x256` at `5908346`
  - `Arena of Kings`: `HeightLayer` at `5449335`, pair `1024x256` at `5449383`

Interpretation:

- `HeightLayer` is a strong anchor for terrain-related data.
- The nearby `1024x256` pair is likely structural metadata (layout/chunking/atlas dimensions) and not yet proven to be final in-world terrain size.

### 7) What remains unresolved

- No definitive proof yet of a universal 3-contiguous-top-level-chunks model.
- Some maps do not expose a single dominant stream as cleanly under current heuristics.
- Exact field schema for most post-header binary records remains unknown.

## Unity-facing parse strategy (recommended now)

1. Parse header deterministically.
2. Parse Segment A deterministically from `headerEnd`.
3. Discover later zlib candidates; select dominant world payload by size + anchors.
4. Parse dominant payload by semantic regions (not fixed absolute offsets).
5. Preserve unknown regions as raw blobs for forward-compatible decoder updates.

Terrain-first implementation note:

1. Anchor on `HeightLayer` in dominant payload.
2. Capture nearby structural ints (including recurring `1024x256`).
3. Search forward for candidate raster blocks with `256x256` / `255x255` signatures.
4. Validate candidate height grids by continuity tests (neighbor deltas, slope sanity) before rendering.
5. Resolve texturing as index mapping first; defer filename resolution until an index->material table is identified.

## Generated analysis artifacts

- `reports/s2m_stream_locator_report.csv`
- `reports/s2m_stream_locator_summary.md`
- `reports/s2m_structure_carving_report.csv`
- `reports/s2m_structure_carving_summary.md`
- `reports/s2m_region_slices.csv`
- `reports/s2m_region_slices_summary.md`

## Appendix: representative evidence (war_chapter1)

- Header end: 135.
- Segment A compressed range: 135..8329 (8194 bytes).
- Segment A contains: `MapHeader`, `Scenario`, `ScenarioEvent`, `WinAction`, `LoseAction`, `Trigger` names.
- Dominant large stream candidate: starts at 14127, decompresses to 10397138 bytes.

Segment A token-record examples:

- `LoseAction`: `nameLen=10`, `tag=7`, `baseNameLen=14`, `baseName=ScenarioAction`
- `WinAction`: `nameLen=9`, `tag=7`, `baseNameLen=0`
- `LordDiesTrigger`: `nameLen=15`, `tag=9`, `baseNameLen=7`, `baseName=Trigger`
