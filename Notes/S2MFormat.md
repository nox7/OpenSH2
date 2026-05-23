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

New BinaryCheck comparison finding (2026-05-23):

- Controlled diff between `BinaryCheck-Blank.s2m` (kingmaker) and `BinaryCheck-WoodGathered10.s2m` (warcampaign; lose on lord death, win on 10 wood acquired) shows the mission logic delta is concentrated in **decompressed Segment A**.
- Both files share the base Segment A record spine:
  - `MapHeader`
  - `EstateMarkers`
  - `Scenario`
  - `Mission`
  - `ScenarioEvent`
  - `LoseAction`
  - `LordDiesTrigger`
  - `Trigger`
- The wood-goal map adds two new token records that are absent from the blank map:
  - `id=10 name=WinAction           tag=7  base=(none)`
  - `id=11 name=GoodsAcquiredTrigger tag=9  base=(none)`
- In the wood-goal map, the relevant decompressed Segment A offsets are:
  - `ScenarioEvent` at `1470`
  - `LoseAction` at `1619`
  - `LordDiesTrigger` at `1684`
  - `Trigger` at `1707`
  - `WinAction` at `1755`
  - `GoodsAcquiredTrigger` record start at `1801` (`name` starts at `1809`)
- No literal ASCII `Wood` token appears in Segment A for the wood-goal map, which is additional evidence that the resource type is serialized numerically rather than as a string.
- Immediately after the `GoodsAcquiredTrigger` metadata (`id`, `nameLen`, `name`, `tag`, `baseNameLen`), the following aligned `int32` values appear in the wood-goal map:
  - `0, 1, 1, 0xFFC1A500, 11, 1, 180, 0, 10, 0, ...`
- Current interpretation:
  - the nearby literal `10` is a strong candidate for the required goods amount;
  - `180` is a candidate goods/resource enum or related trigger field;
  - additional comparisons with the same trigger but different amounts/resources are needed to prove the exact field mapping.

Follow-up comparison: wood `10` vs stone `10` (2026-05-23):

- Comparing `BinaryCheck-WoodGathered10.s2m` against `BinaryCheck-StoneGathered10.s2m` keeps the trigger type and amount constant while changing only the acquired good.
- The `GoodsAcquiredTrigger` record is still present in both files with the same metadata (`id=11`, `nameLen=20`, `tag=9`, `baseNameLen=0`).
- The apparent `+2` drift in many later Segment A offsets is **not** a trigger-schema change; it is explained by earlier UTF-16 text content:
  - the full map name string appears in Segment A as UTF-16 at offset `71`;
  - `Wood` / `Stone` also appear only as part of that UTF-16 name string at offset `95`.
- After normalizing for that text-length drift, the strongest goods-type candidate field near `GoodsAcquiredTrigger` changes while the amount stays fixed:
  - wood map: `0x19EA9830`
  - stone map: `0x19EA9448`
  - both maps retain the nearby literal amount field `10`.
- The previous `180` candidate is weakened by this comparison because the nearby `0x0000B400` / `180` field does **not** change between wood and stone.
- Current best interpretation:
  - `GoodsAcquiredTrigger` stores the target amount as a direct integer field (`10` in both files);
  - the acquired-good identity is likely encoded in the changed 32-bit value (`0x19EA9830` for wood vs `0x19EA9448` for stone), or in a tightly-related packed field adjacent to it.

Follow-up comparison: wood `10` vs stone `10` vs iron `10` (2026-05-23):

- Adding `BinaryCheck-IronGathered10.s2m` falsifies the simple “single 32-bit goods enum” hypothesis:
  - wood vs stone changed `0x19EA9830 -> 0x19EA9448`
  - stone vs iron changed `0x19EA9448 -> 0x19EA9830`
  - wood vs iron leaves that field unchanged
- A stronger pattern appears later in the same `GoodsAcquiredTrigger` record, after the stable sequence:
  - `tag=9`
  - `baseNameLen=0`
  - `0, 1, 1, 0xFFC1A500, 0x00000BFF, 1, 180`
- Immediately after that stable prefix is a short run of zero/amount `int32` slots, and the selected good is indicated by **which slot contains the required amount**:
  - wood map window: `0, 10, 0, 0, ...`
  - stone map window: `0, 0, 10, 0, ...`
  - iron map window: `0, 0, 0, 10, ...`
- Current best interpretation:
  - `GoodsAcquiredTrigger` stores the required amount directly as an `int32` value;
  - the target resource is likely encoded by the index of the non-zero slot in a compact goods-amount vector, not by an adjacent ASCII token and probably not by a standalone enum field.
- The remaining changed 32-bit value near the start of the record is now treated as secondary/opaque until more comparisons prove its meaning.

Follow-up comparison: mixed goods (`20 wood`, `10 pigs`) (2026-05-23):

- `BinaryCheck-WoodPigGathered.s2m` confirms that a single `GoodsAcquiredTrigger` record can carry **multiple non-zero amounts** in the same payload.
- After correcting for the byte alignment in the raw payload, the stable prefix is followed by:
  - `180` (`0x000000B4`)
  - one zero `int32`
  - then a run of little-endian amount slots
- In the mixed-goods sample, two distinct slot values are populated:
  - `20` at the same slot previously used by wood
  - `10` at a later slot corresponding to pigs
- The raw bytes for the mixed-goods case show:
  - `... B4 00 00 00 | 00 00 00 00 | 14 00 00 00 | ... | 0A 00 00 00 | ...`
- The wood-to-pigs distance in the serialized slot vector is **16 `int32` slots**, while the user-reported UI order places pigs **14 goods after wood**.
- Current best interpretation:
  - the trigger payload contains a compact per-good amount vector;
  - the vector likely has **two leading slots before wood** that are not part of the visible goods order list supplied from the editor UI.
- Refined tentative mapping:
  - slot 3 => wood
  - slot 4 => stone
  - slot 5 => iron
  - slot 17 => pigs
- The identities of slots 1 and 2 remain unresolved.

Follow-up comparison: wheat `10` (2026-05-23):

- `BinaryCheck-WheatGathered10.s2m` lands the non-zero amount in the next predicted slot of the same vector.
- In the aligned `GoodsAcquiredTrigger` window, the amount field appears as:
  - `10` at the slot three positions after wood
  - raw aligned portion: `... B4 00 00 00 | 00 00 00 00 | 00 00 00 00 | 00 00 00 00 | 0A 00 00 00 | ...`
- This matches the UI order offset hypothesis exactly:
  - wood => slot 3
  - stone => slot 4
  - iron => slot 5
  - wheat => slot 6
- Current best model for `GoodsAcquiredTrigger` goods storage:
  - two unresolved leading slots
  - then the visible editor goods list in the same order reported by the Stronghold 2 trigger UI.

Follow-up comparison: wheat `10` with `month=1`, `delay=3` (2026-05-23):

- Re-checking `BinaryCheck-WheatGathered10.s2m` after setting month/delay in the editor did **not** place `1` and `3` into the two candidate pre-wood goods slots.
- The `GoodsAcquiredTrigger` goods vector still behaves as before:
  - wheat amount remains encoded in its expected slot (`10`, represented as `0x00000A00` in the current shifted int32 view),
  - candidate pre-wood slots remain `0, 0` in this sample.
- A record-aware diff against `BinaryCheck-WoodGathered10.s2m` points to the `ScenarioEvent` block as the month/delay location:
  - in `ScenarioEvent`, byte-level diffs include `rel=99: 00 -> 01` and `rel=117: 00 -> 03`.
  - these two values exactly match the editor settings (month `1`, delay `3`).
- Tentative field mapping (high-confidence candidate, pending one more confirmation pair):
  - `ScenarioEvent` byte at relative offset `+99` => month
  - `ScenarioEvent` byte at relative offset `+117` => delay
- For the current wheat sample, this corresponds to absolute Segment A offsets:
  - month candidate at `1571` (`1472 + 99`)
  - delay candidate at `1589` (`1472 + 117`)
- Current interpretation update:
  - month and delay are likely serialized in `ScenarioEvent` (or a tightly coupled event-configuration block), not in the two unresolved pre-wood goods vector slots.

Follow-up comparison: trigger-heavy single-event mission (`BinaryCheck-Triggers.s2m`) (2026-05-23):

- A single `ScenarioEvent` can serialize a mixed block of **multiple triggers** and an action (`WinAction`) inside the same event span.
- Confirmed trigger token records in this sample event:
  - `AlwaysTrigger`
  - `HonourAcquiredTrigger`
  - `NoGongInYourEstatesTrigger`
  - `NoFoodInGranaryTrigger`
  - `NoRatsInYourEstatesTrigger`
  - `NoCriminalsTrigger`
  - `EnemyGoodsAcquiredTrigger`
  - `GoodsAcquiredTrigger`
  - `GoldAcquiredTrigger`

Common trigger payload layout (high confidence for the trigger families above):

```text
int32 0
int32 1
int32 1
int32 0xFFC1A500
int32 triggerCode
[optional trigger-specific fields]
[often terminates with 0xFFFF1EAF + 4-byte float-like value]
```

Observed per-trigger parameter shape (from payload ordering, not absolute offsets):

- `GoldAcquiredTrigger`:
  - `triggerCode = 0x13`
  - `modeCode = 0x04`
  - `requiredGold = 0x000000D2` (`210`)
- `HonourAcquiredTrigger`:
  - `triggerCode = 0x0B`
  - `modeCode = 0x04`
  - `requiredHonour = 0x00000064` (`100` in this sample)
- `NoFoodInGranaryTrigger`:
  - `triggerCode = 0x0D`
  - contains extra config fields before trailer:
    - `modeOrFlagCode = 0x08`
    - `modeOrFlagValue = 0x00`
  - this block is the best candidate location for the editor’s red-flag selector.
- `NoGongInYourEstatesTrigger`, `NoRatsInYourEstatesTrigger`, `NoCriminalsTrigger`:
  - minimal payloads with distinct `triggerCode` values (`0x0C`, `0x0E`, `0x0F` respectively)
  - no additional obvious threshold field in this sample.
- `EnemyGoodsAcquiredTrigger`:
  - `triggerCode = 0x10`
  - payload uses a goods-vector style layout similar to `GoodsAcquiredTrigger`, but with a distinct marker (`0xB8` observed in this sample).

Current hierarchy interpretation:

- The parser should model `ScenarioEvent` as the parent node, with child collections of:
  - `Actions`
  - `Triggers`
- Trigger/action nodes should be polymorphic typed records (`*Trigger`, `*Action`) instead of separate top-level arrays per trigger type.

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

`HeightLayer` tile record encoding (confirmed, high confidence):

- `HeightLayer` appears in dominant payload with a nearby `int32` pair `1024 x 256`.
- **The pair is a byte-block size, not a tile dimension**: `1024 bytes/row = 256 tiles × 4 bytes/tile`, across `256 rows`.
- True logical tile grid: **256 × 256 tiles**.
- Each tile is a **4-byte record**:
  - `byte 0`: raw elevation `u8` — confirmed clean heightmap
  - `byte 1`: smoothed/interpolated elevation `u8` — softer version of same terrain
  - `byte 2`: terrain zone/type classification `u8` — coarse biome-like zones
  - `byte 3`: texture ID or per-tile flags `u8` — noisy/speckled
- Data start: `PairOffset + 8` (immediately after the two `int32` dimensions).
- Confirmed anchor offsets:
  - `war_chapter1`: pair at `5651796`, data at `5651804`
  - `Coastal County`: pair at `5557770`, data at `5557778`
  - `peace_chapter1`: pair at `5908346`, data at `5908354`
  - `Arena of Kings`: pair at `5449383`, data at `5449391`
- Visual validation: `war_chapter1_heightmap_final.png` (256×256 grayscale, byte 0) shows recognizable terrain matching in-game geography; dark = water/low, light = elevated land.

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

1. Anchor on `HeightLayer` string in dominant payload.
2. Read the two `int32` values immediately after as the byte-block dimensions (`1024`, `256`).
3. Data block starts at `PairOffset + 8`; total size = `1024 × 256 = 262144` bytes.
4. Interpret as a `256 × 256` grid of 4-byte tile records:
   - `byte 0`: elevation (use as `TerrainData.SetHeights` input, normalize `/ 255f`)
   - `byte 1`: smoothed elevation (optional secondary mesh pass)
   - `byte 2`: terrain zone (map to material index)
   - `byte 3`: texture ID / flags (map to splat layer)
5. Resolve texturing as index mapping first; defer filename resolution until an index→material table is identified.

## Generated analysis artifacts

- `reports/war_chapter1_heightmap_final.png` — confirmed 256×256 u8 heightmap (byte 0 of 4-byte tile records)
- `reports/war_chapter1_hm_256x256_b1.png` — smoothed elevation layer
- `reports/war_chapter1_hm_256x256_b2.png` — terrain zone layer
- `reports/war_chapter1_hm_256x256_b3.png` — texture/flags layer
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
