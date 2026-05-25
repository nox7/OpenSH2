# Stronghold 2 S2M Scenario Actions

Last updated: 2026-05-24

## Scope

This file is the parser contract for investigated Segment A action tokens.
It reflects the current implemented decode behavior.

## Action Record Contract

- Token gate: `tag == 7` or token name ends with `Action`.
- Common metadata: `RecordId`, `RecordStart`, `RecordName`, `Tag`, `BaseName`.
- Raw payload always preserved as `RawPayloadInt32`.

## Normalization Rules

- `i32_packed`: if value is positive and divisible by 256, decode as `value / 256`; otherwise use raw value.
- `byte`: direct `u8` value from payload offset.
- `move_ship_selector`: special decode for `MoveShipAction` selector fields:
	- `255 -> 0`
	- if divisible by 256, use `value / 256`
	- otherwise use raw value

## Existence-Only Actions

These tokens are recognized and typed, but no bespoke payload field decoding is applied.

- `WinAction`
- `LoseAction`
- `TimeUntilFinalInvasionAction`
- `SetAllBuildingsOnFireAction`
- `EnterBriefingAction`
- `WitchcraftAction`
- `BumperHarvestAction`
- `SetWolvesToDefensiveAction`
- `KillAllWolvesAction`
- `BadWeatherAction`
- `WheatDiseaseAction`
- `AppleBlightAction`
- `VineRotAction`
- `SwineFeverAction`
- `MadCowDiseaseAction`
- `LostSheepAction`
- `HopWeevilAction`

## Decoded Actions

Format per field:

- `name | offset | encoding | notes`

### StopInvasionsAction

- `modeCode | +29 | byte | 0=StopRepeatingInvasions, 1=StopAllInvasions`
- `targetLordSelector | +24 | i32_packed | target lord selector`

### InvasionAction

- `invasionPointFlagColorCode | +24 | i32_packed | flag color selector`
- `invasionPointFlagNumberRaw | +28 | int32 | -256 or -1 means Any`
- `invasionPointFlagNumber | +28 | i32_packed | when not Any`
- `destinationPointTypeCode | +32 | i32_packed | destination type`
- `destinationFlagColorCode | +36 | i32_packed | destination flag color`
- `rawTroopSlotCounts[0..28] | +40..+152 | i32_packed | 29 contiguous troop slots`
- `confirmedTroop.ArmedPeasant | +40 | i32_packed | confirmed`
- `confirmedTroop.Archer | +48 | i32_packed | confirmed`
- `confirmedTroop.Knight | +68 | i32_packed | confirmed`
- `confirmedTroop.WarriorMonk | +76 | i32_packed | confirmed`
- `confirmedTroop.HorseCavalry | +112 | i32_packed | strong candidate`
- `confirmedTroop.Catapult | +148 | i32_packed | confirmed`
- `confirmedTroop.Manglet | +152 | i32_packed | confirmed`
- `ownerLordSelectorCode | +164 | i32_packed | owner selector`
- `attackTargetLordSelector | +176 | i32_packed | attacked lord selector`
- `armyTypeCode | +181 | byte | 0=Movement,1=Siege,2=Defensive,3=Attacking,4=Movement`
- `warningBit1 | +183 | byte | bit 1 of warning code`
- `warningBit0 | +187 | byte | bit 0 of warning code`
- `warningTypeCode | +183/+187 | derived | (bit1<<1)|bit0`
- `repeatCountCode | +184 | i32_packed | repeat/count control`
- `includeLordInArmyCode | +157 | byte | include-lord flag`
- `leaveMapCode | +160 | byte | leave-map flag`
- `attackModeCode0 | +186 | byte | attack mode/control`
- `attackModeCode1 | +187 | byte | attack mode/control`

### BearAttackAction

- `targetFlagColorCode | +24 | i32_packed | target flag color`
- `targetFlagNumberCode | +28 | i32_packed | zero-based flag number`
- `bearCount | +32 | i32_packed | bear count`

### CreateCriminalsAction

- `modeCode | +20 | i32_packed | control/mode`
- `createCriminalsPercent | +24 | i32_packed | percent value`

### MaintainMinimumFoodLevelAction

- `minimumFoodLevelUnits | +24 | i32_packed | minimum food threshold`

### FireAction

- `fireCount | +24 | i32_packed | fire count`

### ProtestAction

- `peasantCount | +24 | i32_packed | peasant count`

### TurnIndustriesOnOffAction

- `scopeModeCode | +24 | i32_packed | 0=AllEstates,1=MarkedEstate,2=LordsEstate`
- `markedEstateFlagColorCode | +28 | i32_packed | marked estate color`
- `markedEstateFlagNumberCode | +32 | i32_packed | marked estate number`
- `lordSelectorCode | +36 | i32_packed | lord selector`
- `controlByte40 | +40 | byte | control`
- `controlByte41 | +41 | byte | control`
- `controlByte42 | +42 | byte | control`
- `geeseToggleCode | +56 | byte | confirmed geese toggle`
- `cheeseToggleCode | +65 | byte | confirmed cheese toggle`
- `rawToggleSegment | +44..(payloadLen-8) | bytes | packed per-resource toggles`

### CapResourcesAction

- `scopeModeCode | +24 | i32_packed | 0=AllEstates,1=MarkedEstate,2=LordsEstate`
- `markedEstateFlagColorCode | +28 | i32_packed | marked estate color`
- `markedEstateFlagNumberCode | +32 | i32_packed | marked estate number`
- `lordSelectorCode | +36 | i32_packed | lord selector`
- `controlByte40 | +40 | byte | control`
- `resourceCapSlots | +45..(payloadLen-16), stride 4 | int32 | unaligned slot vector`
- `goldCap | payloadLen-16 | int32 | gold cap`
- `dateCapCode | payloadLen-12 | int32 | date code`

### GiveResourcesAction

- `scopeModeCode | +24 | i32_packed | 0=AllEstates,1=MarkedEstate,2=LordsEstate`
- `markedEstateFlagColorCode | +28 | i32_packed | marked estate color`
- `markedEstateFlagNumberCode | +32 | i32_packed | marked estate number`
- `lordSelectorCode | +36 | i32_packed | lord selector`
- `controlByte40 | +40 | byte | control`
- `resourceGiveSlots | +45..(payloadLen-16), stride 4 | int32 | unaligned slot vector`
- `goldAmount | payloadLen-16 | int32 | gold amount`
- `dateCode | payloadLen-12 | int32 | date code`

### SetAlliesAction

- `scopeModeCode | +24 | i32_packed | structural control`
- `playerRelationCode | +28 | i32_packed | relation`
- `olafRelationCode | +32 | i32_packed | relation`
- `lordBarclayRelationCode | +36 | i32_packed | relation`
- `theHawkRelationCode | +40 | i32_packed | relation`
- `theBullRelationCode | +44 | i32_packed | relation`
- `ladySerenRelationCode | +48 | i32_packed | relation`
- `edwinRelationCode | +52 | i32_packed | relation`
- `theKingRelationCode | +56 | i32_packed | relation`
- `sirWilliamRelationCode | +60 | i32_packed | relation`
- `sirGreyRelationCode | +64 | i32_packed | relation`
- `relation enum | derived | 0=Neutral,1=Friend,2=Enemy`

### MoveLordAction

- `controlCode | +20 | i32_packed | control`
- `lordSelectorCode | +24 | i32_packed | lord selector`
- `targetFlagNumberCode | +28 | i32_packed | zero-based flag number`
- `targetFlagColorCode | +32 | i32_packed | flag color`
- `exitModeCode | +36 | byte | 0=DontLeaveMap,1=LeaveMap`

### TakeEnemysCastleAction

- `controlCode | +20 | i32_packed | control`
- `lordSelectorCode | +24 | i32_packed | target lord`

### ConvertEstateToVillageAction

- `controlCode | +20 | i32_packed | control`
- `targetFlagColorCode | +24 | i32_packed | flag color`
- `targetFlagNumberCode | +28 | i32_packed | zero-based flag number`

### QuestAction

- `controlCode | +20 | i32_packed | control`
- `questIndex | +24 | i32_packed | 0=A,1=B,2=C`

### QuestFailedAction

- `controlCode | +20 | i32_packed | control`
- `questIndex | +24 | i32_packed | 0=A,1=B,2=C`

### SetAvailableTroopTypesAction

- `controlCode | +20 | i32_packed | control`
- `rawToggleBytes | +24..(payloadLen-8) | bytes | dense troop toggles`
- `zeroToggleOffsets | derived | indexes of disabled entries`
- `knightToggleConfirmedOffset | +96 | byte | segment-relative toggle index 72`

### Production-Level Actions

These use the same decode contract:

- `controlCode | +20 | i32_packed | control`
- `productionLevelCode | +24 | i32_packed | 0=Off,1=VeryLow,2=Low,3=Normal,4=High,5=VeryHigh`

Applies to:

- `GongProductionAction`
- `RatProductionAction`
- `DiseaseProductionAction`
- `CrimeRateAction`
- `WolfSpawnRateAction`

### OutlawProductionAction

- `controlCode | +20 | i32_packed | control`
- `productionLevelCode | +24 | i32_packed | 0..5 production level`
- `maxOutlaws | +28 | i32_packed | outlaw cap`
- `locationCode | +32 | i32_packed | 0=AllMap,1=OwnEstate,2=NeighboringEstates,3=OwnAndNeighboringEstates,4=HumanEstate`

### LimitWeaponProductionAction

- `controlCode | +20 | i32_packed | control`
- `rawToggleBytes | +24..(payloadLen-8) | bytes | compact weapon toggles`
- `zeroToggleOffsets | derived | disabled offsets`
- `disabledWeaponTypes provisional | first 6 bytes | enum | Bow,Crossbow,Spear,Pike,Mace,Sword`

### SetCampfirePeasantsAction

- `controlCode | +20 | i32_packed | control`
- `peasantsCount | +24 | i32_packed | count`
- `campfireFlagColorCode | +28 | i32_packed | flag color`
- `campfireFlagNumberCode | +32 | i32_packed | flag number`

### ControlConstructingBuildingsAction

- `controlCode | +20 | i32_packed | control`
- `buildingSitesCount | +24 | i32_packed | count`
- `buildingSitesStateCode | +28 | byte | 0=Inactive,1=Active`

### MaxOutPeasasntsAction

- `controlCode | +20 | i32_packed | control`
- `targetLordSelector | +24 | i32_packed | lord selector`

### KillAllLordsTroopsAction

- `controlCode | +20 | i32_packed | control`
- `targetLordSelector | +24 | i32_packed | lord selector`

### ControlLordsAIAction

- `controlCode | +20 | i32_packed | control`
- `targetLordSelector | +24 | i32_packed | lord selector`
- `enabledCode | +29 | byte | 0=false,1=true`

### ControlGateHousesAction

- `controlCode | +20 | i32_packed | control`
- `targetLordSelector | +24 | i32_packed | lord selector`
- `gateHouseStateCode | +29 | byte | 0=Closed,1=Open`
- `flagColorCode | +30 | byte | flag color`
- `flagNumberCode | +34 | byte | zero-based flag number`

### RushTroopsAction

- `controlCode | +20 | i32_packed | control`
- `targetLordSelector | +24 | i32_packed | lord selector`

### AITroopRetreatAction

- `controlCode | +20 | i32_packed | control`
- `retreatFlagColorCode | +24 | i32_packed | flag color`
- `retreatFlagNumberCode | +28 | i32_packed | zero-based flag number`
- `targetLordSelector | +32 | i32_packed | lord selector`
- `retreatControlCode | +40 | i32_packed | additional control`
- `leaveMapCode | +45 | byte | leave map mode`

### PauseSiegesAction

- `controlCode | +20 | i32_packed | control`
- `siegeFlagColorCode | +24 | i32_packed | flag color`
- `siegeFlagNumberCode | +28 | i32_packed | zero-based flag number`
- `targetLordSelector | +32 | i32_packed | lord selector`
- `pauseStateCode | +36 | byte | 0=Resume,1=Pause`

### SuperAggressiveTroopsAction

- `controlCode | +20 | i32_packed | control`
- `aggressionFlagColorCode | +24 | i32_packed | flag color`
- `aggressionFlagNumberCode | +28 | i32_packed | zero-based flag number`
- `targetLordSelector | +32 | i32_packed | lord selector`

### SetRankAction

- `controlCode | +20 | i32_packed | control`
- `rankCode | +24 | i32_packed | rank enum code`
- `rank enum range | derived | Freeman..Duke`

### SetHonourAction

- `controlCode | +20 | i32_packed | control`
- `honour | +24 | i32_packed | absolute honour value`

### GiveHonourAction

- `controlCode | +20 | i32_packed | control`
- `honourAmount | +24 | i32_packed | honour delta`

### GiveGoldAction

- `controlCode | +20 | i32_packed | control`
- `goldAmount | +24 | i32_packed | gold delta`

### MoveShipAction

- `controlCode | +20 | i32_packed | control`
- `waypoint1FlagColorCode | +24 | move_ship_selector | selector`
- `waypoint1FlagNumberCode | +28 | move_ship_selector | selector`
- `waypoint1Value | +32 | i32_packed | waypoint value`
- `waypoint2FlagColorCode | +36 | move_ship_selector | selector`
- `waypoint2FlagNumberCode | +40 | move_ship_selector | selector`
- `waypoint2Value | +44 | i32_packed | waypoint value`
- `waypoint3FlagColorCode | +48 | move_ship_selector | selector`
- `waypoint3FlagNumberCode | +52 | move_ship_selector | selector`
- `waypoint3Value | +56 | i32_packed | waypoint value`
- `waypoint4FlagColorCode | +60 | move_ship_selector | selector`
- `waypoint4FlagNumberCode | +64 | move_ship_selector | selector`
- `waypoint4Value | +68 | i32_packed | waypoint value`
- `destinationFlagColorCode | +72 | move_ship_selector | selector`
- `destinationFlagNumberCode | +76 | move_ship_selector | selector`
- `optionControlByte0 | +84 | byte | trailing control`
- `shipTypeCode | +85 | byte | 0=VikingShip,1=TradeShip`
- `exitModeCode | +86 | byte | 0=LeaveMap,1=TurnToWreck`

### RedirectVillageOutputAction

- `sourceFlagColorCode | +24 | i32_packed | source flag color`
- `sourceFlagNumberCode | +28 | i32_packed | zero-based source flag number`
- `targetEstateFlagColorCode | +32 | i32_packed | target flag color`
- `targetEstateFlagNumberCode | +36 | i32_packed | zero-based target flag number`

### PlagueOfRatsAction

- `ratsCount | +24 | i32_packed | rats count`

### RatInvasionAction

- `targetFlagColorCode | +24 | i32_packed | target flag color`
- `targetFlagNumberCode | +28 | i32_packed | zero-based flag number`
- `ratsCount | +32 | i32_packed | rats count`

### GongInvasionAction

- `targetFlagColorCode | +24 | i32_packed | target flag color`
- `targetFlagNumberCode | +28 | i32_packed | zero-based flag number`
- `gongCount | +32 | i32_packed | gong count`

### WolfInvasionAction

- `controlCode | +20 | i32_packed | control`
- `invasionPointFlagColorCode | +24 | i32_packed | invasion flag color`
- `invasionPointFlagNumberCode | +28 | i32_packed | invasion flag number`
- `targetPointFlagColorCode | +32 | i32_packed | target flag color`
- `targetPointFlagNumberCode | +36 | i32_packed | target flag number`
- `wolfCount | +40 | i32_packed | wolf count`

## Unknown Action Fallback

- Any action token not matched above is represented as `UnknownAction` with raw payload preserved.

