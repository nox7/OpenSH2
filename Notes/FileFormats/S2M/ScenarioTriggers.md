# Stronghold 2 S2M Scenario Triggers

Last updated: 2026-05-24

## Scope

This file is the parser contract for investigated Segment A trigger tokens.
It reflects the current implemented decode behavior.

## Trigger Record Contract

- Token gate: `tag == 9` or token name ends with `Trigger`.
- Common metadata: `RecordId`, `RecordStart`, `RecordName`, `Tag`, `BaseName`.
- Raw payload always preserved as aligned trigger words in `RawPayloadInt32`.

## Trigger Word Alignment

Trigger payload is interpreted as int32 words using alignment `0..3`.
Best alignment is selected by scoring these expected prefix words:

- `word0 == 0`
- `word1 == 1`
- `word2 == 1`
- `word3 == -4086528`

## Shared Trigger Fields

After alignment, common fields are decoded as:

- `triggerCode = word4`
- `triggerModeCode = word5`
- `triggerValue = word7`

Normalization rules:

- `normalizePackedValue`: positive multiples of 256 are divided by 256.
- `normalizeTriggerCode`: if low byte is `0xFF` and value > 255, use `(value >> 8) & 0xFF`; else apply packed normalization.
- `normalizePercentField`:
	- if low byte is `0x00`, divide by 256
	- if low byte is `0xFF`, arithmetic right-shift by 8
	- else keep raw

## Trigger Families

## Simple Trigger Family

These tokens only use shared fields and have no extra decoded payload fields.

- `AlwaysTrigger`
- `NoPeopleLeftTrigger`
- `AnyEnemyOnMapTrigger`
- `AnyEnemyTroopOnMapTrigger`
- `NoEnemyOrInvasionsLeftTrigger`
- `AllYourTroopsDeadTrigger`
- `LordDiesTrigger`
- `EnemyLordDiesTrigger`
- `OutlawCampDestroyedTrigger`
- `AfterBriefingTrigger`
- `NoMessagesPlayingTrigger`
- `ConstructedBuildingCompleteTrigger`
- `NoBearsOnMapTrigger`
- `NoWolvesOnMapTrigger`
- `NoGongInYourEstatesTrigger`
- `NoRatsInYourEstatesTrigger`
- `NoCriminalsTrigger`

## Scalar Threshold Family

### GoldAcquiredTrigger

- `requiredGoldAmount = triggerValue`
- fallback: highest positive candidate in payload words when primary decode is non-positive

### HonourAcquiredTrigger

- `requiredHonourAmount = triggerValue`

### PopulationReachedTrigger

- `requiredPopulation = triggerValue`

### ControlNumEstatesTrigger

- `requiredEstateCount = normalizePercentField(word7)`
- `triggerValue = requiredEstateCount`

### NumQuestsCompleteTrigger

- `requiredQuestCount = normalizePercentField(word7)`
- `triggerValue = requiredQuestCount`

### ConstructedBuildingPercentCompleteTrigger

- `requiredPercent = normalizePercentField(word8)`
- `triggerValue = requiredPercent`

## Goods Vector Family

### GoodsAcquiredTrigger

- marker index: first word equal to `180`
- `goodsVectorStartIndex = marker index`
- goods amounts are read from vector with 2 leading unknown slots before first editor goods enum
- amounts are mapped by enum order and then normalized

### EnemyGoodsAcquiredTrigger

- marker index: first word equal to `184` or `47104`
- `goodsVectorStartIndex = marker index + 4`
- goods amounts decoded with same 2-leading-slot rule
- target lord selector extraction:
	- find trailer marker word `-14766336`
	- selector is preceding word, packed-normalized, accepted in range `0..256`

## Enemy Resource Scalar Family

### EnemyGoldAcquiredTrigger

- `requiredGoldAmount = triggerValue`
- `targetLordSelector` from trailer-preceding selector rule

### EnemyHonourAcquiredTrigger

- `requiredHonourAmount = triggerValue`
- `targetLordSelector` from trailer-preceding selector rule

## Lord Selector Family

### SpecificEnemyLordDiesTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### RescueLordTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### PlayerKillsLordXTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### OtherLordsKillsLordXTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### BreachInWallTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### EnemyTroopsOnWallsTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### SomeEnemiesCloseToKeepTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### ManyEnemiesCloseToKeepTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

### LiftSiegeTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `triggerValue = targetLordSelector`

## Dual-Selector Family

### SpecificLordKillsLordXTrigger

- `killedLordSelector = normalizePercentField(word7)`
- `killerLordSelector = normalizePercentField(word8)`
- `triggerValue = killedLordSelector`

## Percent-and-Selector Family

### PercentTroopsKilledTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `percentTroopsKilled = normalizePercentField(word8)`
- `triggerValue = percentTroopsKilled`

### LordDamagedTrigger

- `targetLordSelector = normalizePercentField(word7)`
- `requiredDamagePercent = normalizePercentField(word8)`
- `triggerValue = requiredDamagePercent`

## Troop Requirement Family

### GetXTroopsTrigger

- `requiredTroopCount = triggerValue`
- `troopTypeCode = normalizePackedValue(word10)`
- `troopClassCode = normalizePackedValue(word11)`

## Marker Tuple Family

### EnemyNearMarkerTrigger

- `radius = normalizePercentField(word7)`
- `flagColorType = normalizePercentField(word8)`
- `flagNumber = normalizePercentField(word9)`
- `triggerValue = radius`

## Quest State Families

### QuestCompleteTrigger

- trailer marker bytes in raw payload: `AF 1E FF FF`
- bytes immediately before trailer:
	- `questACompleted = payload[trailer-3] != 0`
	- `questBCompleted = payload[trailer-2] != 0`
	- `questCCompleted = payload[trailer-1] != 0`
- `completedQuestCount = sum(true flags)`

### QuestNotCompleteTrigger

- `questIndex = normalizePercentField(word7)`
- `triggerValue = questIndex`

### SingleQuestCompleteTrigger

- `questIndex = normalizePercentField(word7)`
- `triggerValue = questIndex`

### QuestFailedTrigger

- `questIndex = normalizePercentField(word7)`
- `triggerValue = questIndex`

## Multi-Lord Mask Family

### MultipleLordsDeadTrigger

- preferred decode uses trailer bytes:
	- find raw trailer marker `AF 1E FF FF`
	- read 4 bytes at `trailer-9 .. trailer-6` as selection flag bytes
	- build `lordSelectionMaskCandidate` from those 4 bytes
	- collect non-zero byte indexes as `selectedLordSlotsCandidate`
- fallback decode when trailer is unavailable:
	- `maskCandidate = normalizePackedValue(word7) | normalizePackedValue(word8)`

## Special Trigger

### NoFoodInGranaryTrigger

- shared fields are decoded normally
- parser aliases:
	- `flagColorCode = triggerModeCode`
	- `flagSelectionValue = triggerValue`

## Unknown Trigger Fallback

- Any trigger token not matched above is represented as `UnknownTrigger` with raw aligned payload preserved.

