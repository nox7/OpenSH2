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

### Action decoding notes (initial)

`StopInvasionsAction` (token `tag=7`, `baseName=ScenarioAction`) has now been isolated in a single-trigger/single-action test map.

Current payload mapping from `BinaryCheck-Triggers.s2m` (user setting: `target lord = Lord Barclay`, mode = `Stop All invasions`):

- `byte @ payload +29` -> `modeCode` (`0 = StopRepeatingInvasions`, `1 = StopAllInvasions`)
- `int32 @ payload +24` -> packed selector (`value * 256`) for `targetLordSelector`
  - All sample: `0x00000000` -> `0`
  - Olaf sample: `0x00000200` -> `2`
  - Barclay sample: `0x00000300` -> `3`
  - observed delta from Barclay -> Olaf: `-1`
  - observed delta from Olaf -> All: `-2`

Confidence:

- `targetLordSelector` mapping is high confidence (direct controlled delta on lord change only).
- mode mapping is high confidence for the observed pair (`StopRepeatingInvasions` vs `StopAllInvasions`) because only this mode byte toggled between the two controlled map versions.

### `InvasionAction` decoding notes (initial single-sample map)

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=InvasionAction`, `tag=7`, `baseName=ScenarioAction`
- payload length in this sample: `196` bytes

Configured editor values for this sample:

- Troops: `5` Armed Peasant, `5` Warrior Monk, `2` Knight, `1` Manglet
- Invasion point: Green flag `#2`
- Destination: Siege point, Red flag `#1`
- Include lord: Yes
- Leave map: No
- Warning type: Early warnings
- Army type: Defensive army
- Repeat: Always, repeat time `3`
- Owner: Lord Barclay
- Objective: Attack Player lord

Observed normalized int32 view (shift 0) shows these strong matches:

- At payload int index `10` (offset `40`): `5` -> matches Armed Peasant count
- At payload int index `17` (offset `68`): `2` -> matches Knight count
- At payload int index `19` (offset `76`): `5` -> matches Warrior Monk count

Interpretation (current confidence):

- High confidence: there is a troop-count block beginning near payload offset `40`; at least the three non-siege troop values above map correctly.
- Medium confidence: invasion point selector fields are near payload indices `6` and `7` (both normalized `1` in this sample), likely color/index style fields with one index potentially zero-based.
- Medium confidence: the final control/settings fields appear in the tail region around offsets `152..185`.
- Low confidence (needs dedicated toggle tests): exact slot for Manglet count and exact enums for warning type, army type, owner/target-lord selectors, repeat semantics, include-lord, and leave-map flags.

Follow-up toggle test (same map, changed only: Manglet `1->2`, warnings `Early->Full`, army type `Defensive/Siege setting->Attacking`):

- Changed bytes vs previous sample:
  - payload byte `153`: `01 -> 02`
  - payload byte `181`: `02 -> 03`
  - payload byte `183`: `00 -> 01`
- Changed shift-0 int slots:
  - int index `38` (offset `152`): normalized `1 -> 2`
  - int index `45` (offset `180`): composite changed because two independent bytes changed in the same 4-byte word

Updated confidence after this toggle:

- High confidence: Manglet troop count is stored at/intimately tied to offset `152` (int index `38`, packed as `count * 256`).
- Medium-high confidence: army type enum includes byte at offset `181` (`2 -> 3` for this edit).
- Medium-high confidence: warning type enum includes byte at offset `183` (`0 -> 1` for this edit).
- Remaining ambiguity: exact canonical enum labels still need one-at-a-time toggles for all warning and army type values.

Third toggle test (same base action; changed only: remove Manglets, set Catapult `=1`, warnings `No warnings`, attacked lord `Player -> Olaf`):

- Changed bytes vs probe 2:
  - payload byte `149`: `00 -> 01`
  - payload byte `153`: `02 -> 00`
  - payload byte `177`: `15 -> 16`
  - payload byte `183`: `01 -> 00`
  - payload bytes `186..187`: `01 01 -> 00 00`
- Changed shift-0 int slots:
  - int index `37` (offset `148`): normalized `0 -> 1`
  - int index `38` (offset `152`): normalized `2 -> 0`
  - int index `44` (offset `176`): normalized `21 -> 22`
  - int index `45` (offset `180`): composite word changed due to byte edits (not a single enum scalar)
  - int index `46` (offset `184`): composite/flag word changed to `0`

Updated confidence after third toggle:

- High confidence: Catapult count is at/intimately tied to offset `148` (int index `37`, packed as `count * 256`).
- High confidence: Manglet count remains at/intimately tied to offset `152` (int index `38`, packed as `count * 256`).
- High confidence: warning enum byte at offset `183` supports `No warnings = 0` (with prior `Full = 1` sample).
- Medium confidence: attacked-lord selector likely includes offset `176` (int index `44`) where `21 -> 22` when switching `Player -> Olaf`.
- Medium confidence: bytes `186..187` are related to attack-target mode/flags (player/lord semantics), changed alongside target-lord selection.

Fourth toggle test (changed only attacked lord: `Olaf -> The Hawk`):

- Changed byte vs probe 3:
  - payload byte `177`: `16 -> 18`
- Changed shift-0 int slot:
  - int index `44` (offset `176`): normalized `22 -> 24`

Updated confidence after fourth toggle:

- High confidence: attacked-lord selector is carried in offset `176` (int index `44`, packed as `value * 256`).
- Selector movement seen so far in this family:
  - `Player = 21`
  - `Olaf = 22`
  - `The Hawk = 24`
- Note: gaps in selector values indicate reserved/non-lord entries in this selector domain.

Fifth toggle test (notable mixed change set):

- Config highlights:
  - Armed Peasant `10`, Archer `15`, Knight `2`, Warrior Monk `5`, Horse cavalry/"horse warrior" `5`, Catapult `1`, Burning Cart `1`
  - Invasion point Green + `Any` flag number
  - Target point Red flag `#1`
  - No lord in army, do not leave map, no warnings, attacking army
  - Repeat always with `2`
  - Owner Olaf, target The Hawk
- Key payload deltas vs probe 4:
  - byte `29..31`: `01 00 00 -> FF FF FF` (invasion point Any sentinel behavior)
  - byte `41`: `05 -> 0A` (Armed Peasant `5 -> 10`)
  - byte `49`: `00 -> 0F` (Archer `0 -> 15`)
  - byte `113`: `00 -> 05` (Horse-cavalry slot candidate `0 -> 5`)
  - byte `137`: `00 -> 01` (new 1-count troop/control slot)
  - byte `157`: `00 -> 01` (include-lord flag changed with "No lord in army")
  - byte `185`: `00 -> 01` (repeat count/control changed with repeat=2)

Current action enum mapping status:

- Warning type split bits (offsets `183` and `187`):
  - effective warning code = `(byte183 & 1) * 2 + (byte187 & 1)`
  - observed high confidence:
    - `0` (`00`) = `NoWarnings`
    - `1` (`01`) = `EarlyWarnings`
    - `2` (`10`) = `NormalMessages`
    - `3` (`11`) = `FullWarnings`
- Army type byte (offset `181`):
  - observed high confidence:
    - `0 = MovementArmy`
    - `1 = SiegeArmy`
    - `2 = DefensiveArmy`
    - `3 = AttackingArmy`

Sixth toggle test (changed to `Early warnings` and `Siege army`):

- Changed bytes vs probe 5:
  - payload byte `181`: `03 -> 01` (confirms `SiegeArmy = 1`)
  - payload byte `187`: `00 -> 01` (warning bit0 set)
- Byte `183` remained `00`, giving warning bits `01` => effective warning code `1` => `EarlyWarnings`.

Seventh toggle test (changed to `Normal warnings` and `Defensive army`):

- Changed bytes vs probe 6:
  - payload byte `181`: `01 -> 02` (confirms `DefensiveArmy = 2`)
  - payload byte `183`: `00 -> 01`
  - payload byte `187`: `01 -> 00`
- Warning bits become `10` => effective warning code `2` => `NormalMessages`.

Eighth toggle test (changed only army type to `Movement`):

- Changed byte vs probe 7:
  - payload byte `181`: `02 -> 00`
- Confirms `MovementArmy = 0` in this action family.

Evidence artifacts:

- `Notes/reports/invasion_action_probe_1.txt`
- `Notes/reports/invasion_action_shift0_index_table.txt`
- `Notes/reports/invasion_action_probe_2.txt`
- `Notes/reports/invasion_action_probe1_vs_probe2_diff.txt`
- `Notes/reports/invasion_action_probe_3.txt`
- `Notes/reports/invasion_action_probe2_vs_probe3_diff.txt`
- `Notes/reports/invasion_action_probe_4.txt`
- `Notes/reports/invasion_action_probe3_vs_probe4_diff.txt`
- `Notes/reports/invasion_action_probe_5.txt`
- `Notes/reports/invasion_action_probe4_vs_probe5_diff.txt`
- `Notes/reports/invasion_action_probe_6.txt`
- `Notes/reports/invasion_action_probe5_vs_probe6_diff.txt`
- `Notes/reports/invasion_action_tail_bytes_probe2to6.txt`
- `Notes/reports/invasion_action_probe_7.txt`
- `Notes/reports/invasion_action_probe6_vs_probe7_diff.txt`
- `Notes/reports/invasion_action_probe_8.txt`
- `Notes/reports/invasion_action_probe7_vs_probe8_diff.txt`

### `BearAttackAction` decoding notes (finalized)

Token identity from latest `BinaryCheck-Triggers.s2m` edits:

- `name=BearAttackAction`, `tag=7`, `baseName=ScenarioAction`
- payload length in current samples: `45` bytes

Final field mapping (high confidence):

- `int32 @ payload +24` (packed) => `TargetFlagColorCode`
  - observed: `2 = Blue`, `3 = Yellow`
- `int32 @ payload +28` (packed) => `TargetFlagNumberCode` (zero-based)
  - observed: `1 => flag #2`, `2 => flag #3`
- `int32 @ payload +32` (packed) => `BearCount`
  - observed: `9`

Validation summary from controlled toggles:

- Color-only change (`Blue -> Yellow`) updated only offset `24` (`2 -> 3`).
- Flag-number-only change (`#2 -> #3`) updated only offset `28` (`1 -> 2`).
- Bear count remained stable in color/flag-only toggles.

Implementation status:

- Parser now dispatches `BearAttackAction` and populates the three typed fields above.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_bear_run.txt`
- `Notes/reports/bear_attack_action_probe_1.txt`
- `Notes/reports/bear_attack_action_probe_2.txt`
- `Notes/reports/bear_attack_action_probe1_vs_probe2_diff.txt`
- `Notes/reports/bear_attack_action_probe_3.txt`
- `Notes/reports/bear_attack_action_probe2_vs_probe3_diff.txt`

### `CreateCriminalsAction` decoding notes (initial)

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=CreateCriminalsAction`, `tag=7`, `baseName=ScenarioAction`
- payload length in this sample: `37` bytes

Configured editor value for this sample:

- Create criminals percent: `14%`

Observed payload highlights (`Notes/reports/create_criminals_action_probe_1.txt`):

- normalized int at payload offset `24` (int index `6`) is `14`
  - high confidence candidate for `CreateCriminalsPercent`
- normalized int at payload offset `20` (int index `5`) is `4`
  - medium confidence candidate for action mode/family code (currently constant in this sample)

Second toggle test (changed only percent: `14 -> 15`):

- Changed bytes vs probe 1:
  - payload byte `25`: `0E -> 0F`
- Changed normalized int slot:
  - offset `24` (int index `6`): `14 -> 15`
- All other observed non-zero slots remained unchanged.

Current confidence:

- High confidence: payload offset `24` stores the configured percent value for `CreateCriminalsAction` (confirmed by direct `14 -> 15` toggle).
- Medium confidence: payload offset `20` is a control/mode field; needs one comparison pair to confirm semantics.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_createcriminals_run.txt`
- `Notes/reports/create_criminals_action_probe_1.txt`
- `Notes/reports/create_criminals_action_probe_2.txt`
- `Notes/reports/create_criminals_action_probe1_vs_probe2_diff.txt`

### `WolfInvasionAction` decoding notes (initial)

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=WolfInvasionAction`, `tag=7`, `baseName=ScenarioAction`
- payload length in this sample: `53` bytes

Configured editor values for this sample:

- Wolf count: `7`
- Invasion point: Red flag `#3`
- Target point: Blue flag `#2`
- Repeat: Always, every `3`

Observed payload highlights (`Notes/reports/wolf_invasion_action_probe_1.txt`):

- normalized int at payload offset `40` (int index `10`) is `7`
  - high confidence: `WolfCount`
- normalized int at payload offsets `24`, `28`, `32`, `36` are `0`, `2`, `2`, `1`
  - high confidence candidate point mapping:
    - offset `24` => invasion point color selector (`0` for Red in current sample)
    - offset `28` => invasion point flag number selector (`2` => flag `#3`, zero-based)
    - offset `32` => target point color selector (`2` => Blue)
    - offset `36` => target point flag number selector (`1` => flag `#2`, zero-based)
- normalized int at payload offset `20` (int index `5`) is `20`
  - medium confidence candidate for action control/repeat-family code

Second toggle test (changed only repeat interval: `every 3 -> every 4`):

- `WolfInvasionAction` payload was byte-identical vs probe 1.
- No changed byte offsets in `probe1_vs_probe2` diff.
- Current interpretation:
  - repeat interval is either:
    - not serialized in this action payload when repeat mode is `Always`, or
    - serialized in a different owning record (for example `ScenarioEvent`-level config), not inside `WolfInvasionAction` payload.

Third toggle test (repeat mode changed from `Always` to non-always with repeat count `2`, interval back to `every 3`):

- `WolfInvasionAction` payload remained byte-identical vs both probe 1 and probe 2.
- No changed byte offsets in either diff:
  - `probe1_vs_probe3`
  - `probe2_vs_probe3`
- Updated interpretation:
  - repeat mode, repeat interval, and repeat count are not serialized in the current `WolfInvasionAction` payload bytes.
  - these settings are likely stored outside this action payload (most likely event-level or another linked control record).

Cross-file repeat-interval isolation test (`BinaryCheck-Triggers.s2m` vs `BinaryCheck-Triggers_Wolf2.s2m`):

- `WolfInvasionAction` payload remained byte-identical across files.
  - diff report: `wolf_invasion_action_crossfile_baseline_vs_every4_diff.txt` (no changes)
- `ScenarioEvent` payload changed at one normalized int field:
  - payload offset `12` (int index `3`): `3 -> 4`
  - diff report: `scenario_event_crossfile_baseline_vs_every4_diff.txt`
- Updated interpretation:
  - repeat time is serialized outside `WolfInvasionAction`, with strong current evidence for an event-level field at `ScenarioEvent payload +12`.
  - event-level `ScenarioEvent payload +8` is the repeat-count field (stable as `2` in the current non-always sample).

Repeat-count toggle test (`BinaryCheck-Triggers.s2m` changed from repeat count `2` to `3`):

- `WolfInvasionAction` payload remained unchanged.
- `ScenarioEvent` payload changed at two packed fields:
  - payload offset `8` (int index `2`): `2 -> 3`
  - payload offset `12` (int index `3`): `3 -> 4`
- Updated interpretation:
  - repeat count is at `ScenarioEvent payload +8`.
  - repeat time is at `ScenarioEvent payload +12`, and in the current editor interaction it moved in lockstep with repeat count.

Current confidence:

- High confidence: point selectors at offsets `24/28/32/36` and wolf count at offset `40`.
- High confidence: repeat semantics are external to `WolfInvasionAction` payload in current map variants.
- High confidence: repeat time is at `ScenarioEvent payload +12`.
- High confidence: repeat count is at `ScenarioEvent payload +8`.

Implementation status:

- Parser now dispatches `WolfInvasionAction` and populates:
  - `ControlCode`
  - `InvasionPointFlagColorCode`
  - `InvasionPointFlagNumberCode`
  - `TargetPointFlagColorCode`
  - `TargetPointFlagNumberCode`
  - `WolfCount`
- Parser also now exposes event-level repeat data blocks on `ScenarioEvent`:
  - `RepeatCountCode` (payload +8 candidate)
  - `RepeatTimeCode` (payload +12 candidate)

Minimal follow-up tests to isolate repeat fields:

- If the editor allows independent control, change only repeat count while explicitly holding interval fixed.
- If the editor does not allow independent control, treat count and interval as a coupled event-level repeat configuration.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_wolf_run.txt`
- `Notes/reports/wolf_invasion_action_probe_1.txt`
- `Notes/reports/wolf_invasion_action_probe_2.txt`
- `Notes/reports/wolf_invasion_action_probe1_vs_probe2_diff.txt`
- `Notes/reports/wolf_invasion_action_probe_3.txt`
- `Notes/reports/wolf_invasion_action_probe1_vs_probe3_diff.txt`
- `Notes/reports/wolf_invasion_action_probe2_vs_probe3_diff.txt`
- `Notes/reports/binarycheck_compare_wolf_repeat_crossfile.txt`
- `Notes/reports/wolf_invasion_action_baseline_crossfile.txt`
- `Notes/reports/wolf_invasion_action_every4_crossfile.txt`
- `Notes/reports/wolf_invasion_action_crossfile_baseline_vs_every4_diff.txt`
- `Notes/reports/scenario_event_baseline_crossfile.txt`
- `Notes/reports/scenario_event_every4_crossfile.txt`
- `Notes/reports/scenario_event_crossfile_baseline_vs_every4_diff.txt`
- `Notes/reports/scenario_event_repeatcount3.txt`
- `Notes/reports/scenario_event_baseline_vs_repeatcount3_diff.txt`

### `SetWolvesToDefensiveAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=SetWolvesToDefensiveAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented.

Implementation status:

- Parser now recognizes `SetWolvesToDefensiveAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_wolf_defensive_run.txt`

### `BadWeatherAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=BadWeatherAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `BadWeatherAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_badweather_run.txt`
- `Notes/reports/bad_weather_action_probe_1.txt`

### `WheatDiseaseAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=WheatDiseaseAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `WheatDiseaseAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_wheatdisease_run.txt`
- `Notes/reports/wheat_disease_action_probe_1.txt`

### `AppleBlightAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=AppleBlightAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `AppleBlightAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_appleblight_run.txt`
- `Notes/reports/apple_blight_action_probe_1.txt`

### `HopWeevilAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=HopWeevilAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `HopWeevilAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_hopweevil_run.txt`
- `Notes/reports/hop_weevil_action_probe_1.txt`

### `VineRotAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=VineRotAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `VineRotAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_vinerot_run.txt`
- `Notes/reports/vine_rot_action_probe_1.txt`

### `SwineFeverAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=SwineFeverAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `SwineFeverAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_swinefever_run.txt`
- `Notes/reports/swine_fever_action_probe_1.txt`

### `MadCowDiseaseAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=MadCowDiseaseAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `MadCowDiseaseAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_madcow_run.txt`
- `Notes/reports/mad_cow_disease_action_probe_1.txt`

### `LostSheepAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=LostSheepAction`, `tag=7`, `baseName=ScenarioAction`

Current interpretation:

- This action appears to be existence-only in current samples.
- No additional payload fields have been isolated or documented beyond the raw action payload.

Implementation status:

- Parser now recognizes `LostSheepAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_lostsheep_run.txt`
- `Notes/reports/lost_sheep_action_probe_1.txt`

### `MaintainMinimumFoodLevelAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=MaintainMinimumFoodLevelAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload now includes a single editable food threshold value.
- The probe shows a fixed structural word at payload offset `+20` and the food threshold at payload offset `+24`.
- With the editor set to `17` units, the decoded threshold at `+24` reads `17`.

Current interpretation:

- This action is modeled as a typed scenario action with one decoded value: minimum food level in units.
- The payload is preserved in raw form as well.

Implementation status:

- Parser now recognizes `MaintainMinimumFoodLevelAction` and decodes `minimumFoodLevelUnits`.
- Debug output prints the threshold value.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_maintainfood_run.txt`
- `Notes/reports/maintain_minimum_food_level_action_probe_1.txt`

### `PlagueOfRatsAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=PlagueOfRatsAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload now includes a single editable rats count value.
- The probe shows a fixed structural word at payload offset `+20` and the rats count at payload offset `+24`.
- With the editor set to `13` rats, the decoded count at `+24` reads `13`.

Current interpretation:

- This action is modeled as a typed scenario action with one decoded value: number of rats.
- The payload is preserved in raw form as well.

Implementation status:

- Parser now recognizes `PlagueOfRatsAction` and decodes `ratsCount`.
- Debug output prints the rats count.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_plagueofrats_run.txt`
- `Notes/reports/plague_of_rats_action_probe_1.txt`

### `RatInvasionAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=RatInvasionAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload includes three editable fields after a fixed structural word at `+20`.
- `+24` holds the target flag color selector.
- `+28` holds the target flag number selector, stored as a zero-based selector in current samples.
- `+32` holds the rats count.
- With the editor set to `3` rats, `Any` color, and flag number `2`, the decoded values read `ratsCount=3`, `targetFlagColorCode=-1`, and `targetFlagNumberCode=1`.

Current interpretation:

- This action is modeled as a typed scenario action with three decoded values: rats count and the two flag selectors.
- The payload is preserved in raw form as well.

Implementation status:

- Parser now recognizes `RatInvasionAction` and decodes the three fields.
- Debug output prints the rats count and flag selector codes.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_ratinfestation_run.txt`
- `Notes/reports/rat_invasion_action_probe_1.txt`

### `GongInvasionAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=GongInvasionAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload includes three editable fields after a fixed structural word at `+20`.
- `+24` holds the target flag color selector.
- `+28` holds the target flag number selector, stored as a zero-based selector in current samples.
- `+32` holds the gong count.
- With the editor set to `8` gong, `green` color, and flag number `3`, the decoded values read `gongCount=8`, `targetFlagColorCode=1`, and `targetFlagNumberCode=2`.

Current interpretation:

- This action is modeled as a typed scenario action with three decoded values: gong count and the two flag selectors.
- The payload is preserved in raw form as well.

Implementation status:

- Parser now recognizes `GongInvasionAction` and decodes the three fields.
- Debug output prints the gong count and flag selector codes.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_gonginfestation_run.txt`
- `Notes/reports/gong_invasion_action_probe_1.txt`

### `FireAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=FireAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload includes a single editable field after a fixed structural word at `+20`.
- `+24` holds the fire count.
- With the editor set to `11` fires, the decoded count at `+24` reads `11`.

Current interpretation:

- This action is modeled as a typed scenario action with one decoded value: number of fires.
- The payload is preserved in raw form as well.

Implementation status:

- Parser now recognizes `FireAction` and decodes `fireCount`.
- Debug output prints the fire count.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_startfires_run.txt`
- `Notes/reports/fire_action_probe_1.txt`

### `SetAllBuildingsOnFireAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=SetAllBuildingsOnFireAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload matches the current existence-only scenario-action pattern in samples.
- No additional editable payload fields were isolated beyond the common action payload structure.

Current interpretation:

- This action is modeled as an existence-only typed scenario action.
- The payload is preserved in raw form.

Implementation status:

- Parser now recognizes `SetAllBuildingsOnFireAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_allbuildingsfire_run.txt`
- `Notes/reports/set_all_buildings_on_fire_action_probe_1.txt`

### `WitchcraftAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=WitchcraftAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload matches the current existence-only scenario-action pattern in samples.
- No additional editable payload fields were isolated beyond the common action payload structure.

Current interpretation:

- This action is modeled as an existence-only typed scenario action.
- The payload is preserved in raw form.

Implementation status:

- Parser now recognizes `WitchcraftAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_witchcraft_run.txt`
- `Notes/reports/witchcraft_action_probe_1.txt`

### `ProtestAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=ProtestAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload includes a single editable field after a fixed structural word at `+20`.
- `+24` holds the peasant count.
- With the editor set to `26` peasants, the decoded count at `+24` reads `26`.

Current interpretation:

- This action is modeled as a typed scenario action with one decoded value: number of peasants.
- The payload is preserved in raw form as well.

Implementation status:

- Parser now recognizes `ProtestAction` and decodes `peasantCount`.
- Debug output prints the peasant count.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_peasantsrevolt_run.txt`
- `Notes/reports/protest_action_probe_1.txt`

### `BumperHarvestAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=BumperHarvestAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- The payload matches the current existence-only scenario-action pattern in samples.
- No additional editable payload fields were isolated beyond the common action payload structure.

Current interpretation:

- This action is modeled as an existence-only typed scenario action.
- The payload is preserved in raw form.

Implementation status:

- Parser now recognizes `BumperHarvestAction` and returns a typed action with raw payload preserved.
- No bespoke payload decoding is currently applied.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_bumperharvest_run.txt`
- `Notes/reports/bumper_harvest_action_probe_1.txt`

### `RedirectVillageOutputAction` decoding notes

Token identity from latest `BinaryCheck-Triggers.s2m` edit:

- `name=RedirectVillageOutputAction`, `tag=7`, `baseName=ScenarioAction`

Probe result:

- Payload length is `49`, with a fixed structural word at `+20` (`16` in current sample).
- Four selector values are present and currently map as follows:
  - `+24`: source village flag color selector
  - `+28`: source village flag number selector (zero-based)
  - `+32`: target estate flag color selector
  - `+36`: target estate flag number selector (zero-based)
- With editor values set to source `red` flag `1` and target `blue` flag `4`, decoded values are:
  - `sourceFlagColorCode=0`
  - `sourceFlagNumberCode=3`
  - `targetEstateFlagColorCode=2`
  - `targetEstateFlagNumberCode=0`

Two-probe confidence update:

- A follow-up sample changed only source color (`red -> green`).
- Only offsets `+24` and `+28` changed between probes; `+32` and `+36` remained stable.
- This supports grouping `+24/+28` as the source selector pair and `+32/+36` as the target selector pair.
- The editor appears to persist/display the two flag numbers inconsistently, consistent with the reported swap behavior.

Current interpretation:

- This action is modeled as a typed scenario action with two flag selector sets (source and target).
- Flag numbers appear zero-based in current samples.
- The payload is preserved in raw form as well.

Implementation status:

- Parser now recognizes `RedirectVillageOutputAction` and decodes the four selector codes.
- Debug output prints all source/target selector fields.

Evidence artifacts:

- `Notes/reports/binarycheck_compare_redirectvillage_run.txt`
- `Notes/reports/redirect_village_output_action_probe_1.txt`
- `Notes/reports/redirect_village_output_action_probe_2.txt`
- `Notes/reports/redirect_village_output_action_probe1_vs_probe2_diff.txt`

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
