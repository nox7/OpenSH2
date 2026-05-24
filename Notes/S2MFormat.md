# Stronghold 2 `.s2m` map format notes

Last updated: 2026-05-24
Sample used: `maps/war_chapter1.s2m` (size: 673,069 bytes)

## Current working spec (2026-05-19)

### Certainty levels

- **Certain**: validated on all 48 maps in `maps/`.
- **High confidence**: strong repeated evidence, but not full schema decode.
- **Unresolved**: still under reverse-engineering.

### 0) Format vocabulary draft (linear)

This is a working glossary for a future fully linear spec document.

- File header:
  - deterministic key/value prelude at file start (string options + int options).
- Segment A:
  - first zlib block immediately after header (`78 9C` at `headerEnd`), currently observed compressed length `8194` in tested maps.
  - stores mission/script record graph (`Scenario`, `Mission`, `ScenarioEvent`, `*Action`, `*Trigger`, etc.).
- Token record:
  - record-like structure in Segment A with fields `id`, `nameLen`, `name`, `tag`, `baseNameLen`, optional `baseName`, then payload until next record.
- Scenario event span:
  - byte range from one `ScenarioEvent` token start to the next `ScenarioEvent` token start (or end of Segment A).
  - parser currently treats this span as the ownership boundary for child actions/triggers.
- Trigger payload words:
  - `int32` view of token payload, usually interpreted as an aligned sequence with a common prefix and trigger-family-specific fields.
- Dominant world payload:
  - larger later zlib stream containing simulation/world/environment data (outside Segment A mission logic).

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

#### Segment A ordering and grouping invariants (high confidence)

Validated behavior from current corpus and latest checks:

- `ScenarioEvent` tokens define parent spans in record order.
- `*Action` and `*Trigger` records are grouped under the nearest enclosing `ScenarioEvent` span.
- In tested maps, mission actions/triggers do not appear before the first `ScenarioEvent`.
- In tested maps, mission actions/triggers do not appear outside any `ScenarioEvent` span.

Current parser rule (deterministic and intentional):

1. Find all `ScenarioEvent` records sorted by `RecordStart`.
2. Define each event range as `[thisEventStart, nextEventStart)` (last event ends at Segment A decompressed end).
3. Assign every non-`ScenarioEvent` `*Action`/`*Trigger` token to the event whose range contains that token's start offset.

Evidence snapshot:

- Current `BinaryCheck-Triggers.s2m` has one `ScenarioEvent` span containing `LoseAction`, `LordDiesTrigger`, `WinAction`, and all current test triggers in order.
- Cross-map check (`23` maps in local set):
  - `0` cases with action/trigger records before first `ScenarioEvent`.
  - `0` cases with action/trigger records outside any `ScenarioEvent` range.

Confidence note:

- Treat this as **high confidence**, not absolute certainty. Keep parser behavior stable with this rule, but continue flagging any future counterexample map as a format-variant candidate.

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

Note: trigger payload words in this block are currently best decoded with a +1 byte alignment shift from raw payload start.

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
  - payload uses a goods-vector style layout related to `GoodsAcquiredTrigger`, but with a distinct marker (`0xB8` observed in this sample).
  - in the current `BinaryCheck-Triggers.s2m` sample:
    - selected good/value (`Cheese = 10`) is confirmed in the enemy goods vector;
    - a non-zero selector field appears near the end of the trigger block;
    - when target lord is `Lord Barclay` (second UI entry), selector value is `3`;
    - after changing target lord to `Olaf` (first UI entry), selector value changes to `2`;
    - after changing target lord to `SirGrey` (ninth UI entry), selector value changes to `10`;
    - this selector is the best current candidate for the chosen target-lord entry (`Lord Barclay` in editor).
    - provisional mapping now looks 1-based-with-reserved-first-entry (`1` potentially player/self, `2` first enemy UI entry).
  - current parser interpretation:
    - enemy goods vector begins after an additional 4-int enemy-specific block compared with normal `GoodsAcquiredTrigger`;
    - target-lord selector is read from the int immediately before the `0xFFFF1EAF` trailer marker.

Latest trigger expansion in `BinaryCheck-Triggers.s2m` (2026-05-23):

- New trigger tokens confirmed in Segment A:
  - `EnemyGoldAcquiredTrigger`
  - `EnemyHonourAcquiredTrigger`
  - `PopulationReachedTrigger`
  - `NoPeopleLeftTrigger`
  - `AnyEnemyOnMapTrigger`
  - `AnyEnemyTroopOnMapTrigger`
  - `NoEnemyOrInvasionsLeftTrigger`
  - `AllYourTroopsDeadTrigger`
  - `PercentTroopsKilledTrigger`
  - `GetXTroopsTrigger`
  - `LordDiesTrigger`
  - `LordDamagedTrigger`
  - `EnemyLordDiesTrigger`
  - `SpecificEnemyLordDiesTrigger`
  - `RescueLordTrigger`
  - `MultipleLordsDeadTrigger`
  - `PlayerKillsLordXTrigger`
  - `OtherLordsKillsLordXTrigger`
  - `SpecificLordKillsLordXTrigger`
  - `OutlawCampDestroyedTrigger`
  - `BreachInWallTrigger`
  - `EnemyTroopsOnWallsTrigger`
  - `SomeEnemiesCloseToKeepTrigger`
  - `ManyEnemiesCloseToKeepTrigger`
  - `LiftSiegeTrigger`
  - `EnemyNearMarkerTrigger`
  - `QuestCompleteTrigger`
  - `QuestNotCompleteTrigger`
  - `SingleQuestCompleteTrigger`
  - `NumQuestsCompleteTrigger`
  - `QuestFailedTrigger`
  - `AfterBriefingTrigger`
  - `NoMessagesPlayingTrigger`
  - `ConstructedBuildingCompleteTrigger`
  - `ConstructedBuildingPercentCompleteTrigger`
  - `ControlNumEstatesTrigger`
  - `NoBearsOnMapTrigger`
  - `NoWolvesOnMapTrigger`

Observed payload field mapping (aligned words, then normalized by parser):

### Trigger encoding families (cleanup view)

This is a structural grouping of trigger payload layouts so the long trigger list can be read as a set of reusable schemas.

1. **Simple state / no-threshold family**
   - Typical shape: common trigger prefix + trailer, no meaningful configurable value field.
   - Usually modeled as simple typed triggers in parser.
   - Examples:
     - `LordDiesTrigger`
     - `EnemyLordDiesTrigger`
     - `OutlawCampDestroyedTrigger`
     - `AfterBriefingTrigger`
     - `NoMessagesPlayingTrigger`
     - `NoWolvesOnMapTrigger`
     - `NoPeopleLeftTrigger`
     - `AllYourTroopsDeadTrigger`

2. **Single scalar threshold/count family**
   - Typical shape: mode code + one logical scalar value.
   - Scalar location is usually aligned word `7`, but not universal.
   - Examples:
     - `GoldAcquiredTrigger` / `HonourAcquiredTrigger` / `PopulationReachedTrigger`
     - `NumQuestsCompleteTrigger` (`RequiredQuestCount` at word 7)
     - `ControlNumEstatesTrigger` (`RequiredEstateCount` at word 7)
     - `ConstructedBuildingPercentCompleteTrigger` (`RequiredPercent` at word 8)

3. **Single selector family (one lord or quest selector)**
   - Typical shape: selector at aligned word `7` (or equivalent pre-trailer slot in some enemy-* variants).
   - Used by multiple distinct selector domains:
     - lord target selector (`SpecificEnemyLordDiesTrigger`, `PlayerKillsLordXTrigger`, `LiftSiegeTrigger`, wall/keep families)
     - quest index selector (`QuestNotCompleteTrigger`, `SingleQuestCompleteTrigger`, `QuestFailedTrigger`)
   - Important: selector indexing is **family-specific** (for example, one family may be 0-based while another is offset by reserved entries).

4. **Dual-selector family**
   - Two selector fields in consecutive words.
   - Current confirmed example:
     - `SpecificLordKillsLordXTrigger`
       - word `7` = killed lord selector
       - word `8` = killer lord selector

5. **Multi-target bit/flag-set family**
   - Multiple selected lords encoded as a compact mask/flag run, not one scalar selector.
   - Current example:
     - `MultipleLordsDeadTrigger`
       - candidate 4-byte selection flags before trailer marker
       - candidate mask and selected slot list derived from those bytes

6. **Marker tuple family**
   - Multi-parameter payload tuple for marker-driven spatial checks.
   - Current example:
     - `EnemyNearMarkerTrigger`
       - word `7` = `radius`
       - word `8` = `flagColorType`
       - word `9` = `flagNumber` (currently appears 0-based in storage)

7. **Quest bitfield/status-byte family**
   - Quest completion state packed as bytes near trailer marker rather than a single scalar.
   - Current example:
     - `QuestCompleteTrigger`
       - three quest status bytes packed immediately before trailer marker
       - decoded into `QuestACompleted`, `QuestBCompleted`, `QuestCCompleted`

Practical parser rule:

- Dispatch by token name first.
- Then decode fields according to the token's encoding family.
- Do not rely on `triggerCode` as global enum identity (order-dependent in this dataset).

- Trigger code behavior update (important):
  - the `triggerCode` field is currently behaving like a **scenario-local instance/order id** (tracks record id/order), not a stable global trigger-type enum.
  - evidence: after deselecting/reselecting `AllYourTroopsDead` and `PercentTroopsKilled`, their codes swapped again with ordering:
    - current run: `AllYourTroopsDeadTrigger = 0x1B`, `PercentTroopsKilledTrigger = 0x1C`
    - previous run had the opposite assignment.
  - parser should continue dispatching by token name, not by this code.

- `EnemyGoldAcquiredTrigger`:
  - trigger code: `0x14`
  - mode code: `0x08`
  - required gold: `60`
  - target lord selector: `2` (Olaf test)
- `EnemyHonourAcquiredTrigger`:
  - trigger code: `0x15`
  - mode code: `0x08`
  - required honour: `10`
  - target lord selector: `4` (The Hawk test)
- `PopulationReachedTrigger`:
  - trigger code: `0x16`
  - mode code: `0x04`
  - required population: `90`
- `NoPeopleLeftTrigger`:
  - trigger code: `0x17`
  - minimal payload (no extra threshold field observed)
- `AnyEnemyOnMapTrigger` (editor setting: no enemy on map):
  - trigger code: `0x18`
  - mode code: `0x04`
  - value field observed as `0`
- `AnyEnemyTroopOnMapTrigger` (editor setting: no enemy troops on map):
  - trigger code: `0x19`
  - mode code: `0x04`
  - value field observed as `0`
- `NoEnemyOrInvasionsLeftTrigger`:
  - trigger code: `0x1A`
  - mode code: `0x04`
  - value field observed as `0`
- `AllYourTroopsDeadTrigger`:
  - trigger code is order-dependent (currently `0x1B` in latest run)
  - minimal payload (no extra threshold field observed)
- `PercentTroopsKilledTrigger`:
  - trigger code is order-dependent (currently `0x1C` in latest run)
  - mode code: `0x0C`
  - observed field ordering in this sample:
    - selector field at aligned word index 7
    - percentage threshold at aligned word index 8
  - sample values from latest file revision:
    - `PercentTroopsKilled = 18`
    - `TargetLordSelector = 1` (Olaf selection)
    - `TargetLordSelector = 0` (Player selection)
    - `TargetLordSelector = -1` (All lords selection)
  - packed encoding note for this trigger family:
    - observed values use both `0x??00` and `0x??FF` low-byte forms;
    - parser normalization now decodes both forms to the same logical value (for example, `18` from `0x1200` or `0x12FF`).
  - interpretation note:
    - this trigger family appears to use a different selector/value layout than `EnemyGoldAcquiredTrigger` and `EnemyHonourAcquiredTrigger`, so selector semantics should be re-confirmed with one or two controlled variations (`All lords`, `Player lord`, and another enemy lord).
- `GetXTroopsTrigger` (editor setting: `RecruitArchers = 70`):
  - trigger code is order-dependent (currently `0x1D` in latest run)
  - mode code: `0x04`
  - required troop count: `70`
  - additional payload words after the main trailer appear to encode troop identity metadata.
  - current candidate parse fields:
    - `troopTypeCode = 8`
    - `troopClassCode = 9`
  - these candidate type/class fields need one more controlled comparison with a different troop type to confirm exact semantics.
- `LordDiesTrigger` (editor setting: `Your Lord Dies`):
  - trigger token is `LordDiesTrigger`
  - payload length is `0` in this sample (no extra threshold/config fields observed).
- `LordDamagedTrigger` (editor setting: `Olaf`, `12%`):
  - trigger code is order-dependent (currently `0x1E` in latest run)
  - mode code: `0x08`
  - field ordering (aligned words):
    - target lord selector at index 7
    - damage percent threshold at index 8
  - sample values from latest run:
    - `TargetLordSelector = 1`
    - `RequiredDamagePercent = 12`
  - note: selector semantics in this trigger family appear closer to `PercentTroopsKilledTrigger` than to `EnemyGoldAcquiredTrigger` / `EnemyHonourAcquiredTrigger`.
- `EnemyLordDiesTrigger` (editor setting: `All the enemy lords are dead`):
  - trigger code is order-dependent (currently `0x1F` in latest run)
  - minimal payload shape (same compact/no-parameter pattern seen in other boolean state triggers).
- `SpecificEnemyLordDiesTrigger` (editor setting: `Lord Barclay`):
  - trigger code is order-dependent (currently `0x20` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 2` (matches current Barclay expectation)
- `RescueLordTrigger` (editor setting: `Olaf`):
  - trigger code is order-dependent (currently `0x21` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample values from latest runs:
    - `Olaf -> TargetLordSelector = 0`
    - `TheHawk -> TargetLordSelector = 2`
  - current interpretation:
    - this trigger family appears to use a **0-based enemy-lord selector** (for example, third UI lord entry => selector `2`).
    - selector semantics are trigger-family-specific and should not be assumed to match `LordDamagedTrigger` / `PercentTroopsKilledTrigger`.
- `MultipleLordsDeadTrigger` (editor setting: `Olaf + TheHawk`):
  - trigger code is order-dependent (currently `0x22` in latest run)
  - mode code candidate: `0x0B`
  - payload shape differs from single-lord selector triggers and appears to carry a compact multi-select target block.
  - current sample evidence near trailer marker `AF 1E FF FF`:
    - four-byte candidate flag run: `01 00 01 00`
    - candidate mask from those four bytes: `0x00010001` (`65537`)
    - candidate selected slots: `0` and `2`
  - current interpretation:
    - this is likely a multi-target lord selection encoding (not a single selector scalar).
    - with current test (`Olaf + TheHawk`), the candidate selected slots `0` and `2` match expected 0-based enemy-lord positions.
  - confidence note:
    - treat field semantics as provisional until one more controlled comparison is captured (for example, only Olaf, only TheHawk, and Olaf+Barclay).
- `PlayerKillsLordXTrigger` (editor setting: `Lord Barclay`):
  - trigger code is order-dependent (currently `0x23` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 3` (Barclay)
- `OtherLordsKillsLordXTrigger` (editor setting selected as `Olaf`, UI text displayed as `Player`):
  - trigger code is order-dependent (currently `0x24` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 2`
  - interpretation note:
    - selector `2` matches the same lord-selector family used by `PlayerKillsLordXTrigger` / enemy-* selectors where Olaf is `2` and Barclay is `3` (with `1` likely reserved for player/self).
    - this supports your suspicion that the editor label rendering for this case is wrong (string shows `Player` while stored selector corresponds to Olaf).
- `SpecificLordKillsLordXTrigger` (editor selection attempted: killer `Olaf`, killed `Player`; UI renders killer `Barclay`, killed `Olaf`):
  - trigger code is order-dependent (currently `0x25` in latest run)
  - mode code: `0x08`
  - field ordering (aligned words):
    - selector field at index 7 = `2`
    - selector field at index 8 = `3`
  - confirmed interpretation (from two controlled runs):
    - field index `7` is **killed-lord selector**
    - field index `8` is **killer-lord selector**
  - confirmation pair:
    - run A decoded `(w7,w8)=(2,3)` and UI rendered `(killed,killer)=(Olaf,Barclay)`
    - run B decoded `(w7,w8)=(2,4)` with editor set to killer `TheHawk`, killed `Olaf`
  - practical conclusion:
    - this trigger stores two lord-selector indices from the same selector family (`Olaf=2`, `Barclay=3`, `TheHawk=4` observed), and the UI chooser text/mapping can be inconsistent with user intent.
- `OutlawCampDestroyedTrigger`:
  - trigger code is order-dependent (currently `0x26` in latest run)
  - minimal payload shape (same no-parameter boolean pattern as other simple state triggers).
- `BreachInWallTrigger` (editor setting: `Lord Barclay`):
  - trigger code is order-dependent (currently `0x27` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 2`
- `EnemyTroopsOnWallsTrigger` (editor setting: `Lord Barclay`):
  - trigger code is order-dependent (currently `0x28` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 2`
- `SomeEnemiesCloseToKeepTrigger` (editor setting: `The Hawk`):
  - trigger code is order-dependent (currently `0x29` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 3`
- `ManyEnemiesCloseToKeepTrigger` (editor setting: `The Hawk`):
  - trigger code is order-dependent (currently `0x2A` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 3`
- `LiftSiegeTrigger` (editor setting: `Olaf`):
  - trigger code is order-dependent (currently `0x2B` in latest run)
  - mode code: `0x04`
  - field ordering (aligned words):
    - target lord selector at index 7
  - sample value from latest run:
    - `TargetLordSelector = 1`
- selector-family note for the four triggers above:
  - observed values indicate a selector family that is offset by `-1` relative to the `PlayerKillsLordX` / `EnemyGoldAcquired` family.
  - current observed mapping in this family:
    - `Olaf -> 1`
    - `Lord Barclay -> 2`
    - `The Hawk -> 3`
  - this now has direct confirmation from `LiftSiegeTrigger` (`Olaf -> 1`).
- `EnemyNearMarkerTrigger`:
  - trigger code is order-dependent (currently `0x2C` in latest run)
  - mode code: `0x0C`
  - confirmed payload fields:
    - `radius` at aligned word index 7
    - `flagColorType` at aligned word index 8
    - `flagNumber` at aligned word index 9
  - confirmation samples:
    - sample A: `radius=24`, `flagColorType=0`, `flagNumber=1`
    - sample B: `radius=20`, `flagColorType=1`, `flagNumber=2`
  - interpretation note:
    - changing flag color in editor from prior value to green changed `flagColorType` from `0 -> 1`.
    - setting flag number to `3` in editor decoded as `2`, so stored `flagNumber` currently appears 0-based.
- `QuestCompleteTrigger` (editor setting: `Quest A = completed`, `Quest C = completed`):
  - trigger code is order-dependent (currently `0x2D` in latest run)
  - mode code: `0x03`
  - quest completion statuses are packed as three bytes immediately before trailer marker `AF 1E FF FF`.
  - sample bytes decode to:
    - `QuestACompleted = true`
    - `QuestBCompleted = false`
    - `QuestCCompleted = true`
  - `CompletedQuestCount = 2`
- `QuestNotCompleteTrigger` (editor setting: `Quest C`):
  - trigger code is order-dependent (currently `0x2E` in latest run)
  - mode code: `0x04`
  - `QuestIndex = 2` (Quest C)
- `SingleQuestCompleteTrigger` (editor setting: `Quest A`):
  - trigger code is order-dependent (currently `0x2F` in latest run)
  - mode code: `0x04`
  - `QuestIndex = 0` (Quest A)
- `NumQuestsCompleteTrigger` (editor setting: `2`):
  - trigger code is order-dependent (currently `0x30` in latest run)
  - mode code: `0x04`
  - `RequiredQuestCount = 2`
- `QuestFailedTrigger` (editor setting: `Quest B`):
  - trigger code is order-dependent (currently `0x31` in latest run)
  - mode code: `0x04`
  - `QuestIndex = 1` (Quest B)
- `AfterBriefingTrigger`:
  - trigger code is order-dependent (currently `0x32` in latest run)
  - minimal payload shape (no configurable threshold field observed).
- `NoMessagesPlayingTrigger`:
  - trigger code is order-dependent (currently `0x33` in latest run)
  - minimal payload shape (no configurable threshold field observed).
- `ConstructedBuildingCompleteTrigger`:
  - trigger code is order-dependent (currently `0x34` in latest run)
  - mode code: `0x08`
  - no user-configurable threshold field is exposed in this sample.
- `ConstructedBuildingPercentCompleteTrigger` (editor setting: `21%`):
  - trigger code is order-dependent (currently `0x35` in latest run)
  - mode code: `0x08`
  - percent threshold is stored at aligned word index `8`
  - sample value from latest run:
    - `RequiredPercent = 21`
- `ControlNumEstatesTrigger` (editor setting: `7`):
  - trigger code is order-dependent (currently `0x36` in latest run)
  - mode code: `0x04`
  - required estate count is stored at aligned word index `7`
  - sample value from latest run:
    - `RequiredEstateCount = 7`
- `NoBearsOnMapTrigger`:
  - trigger code is order-dependent (currently `0x37` in latest run)
  - mode code: `0x04`
  - minimal/no-threshold payload shape in this sample.
- `NoWolvesOnMapTrigger`:
  - trigger code is order-dependent (currently `0x38` in latest run)
  - minimal payload shape (no configurable threshold field observed).
- quest index mapping (confirmed from current set):
  - `Quest A -> 0`
  - `Quest B -> 1`
  - `Quest C -> 2`

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
