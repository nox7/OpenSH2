using System;
using System.Collections.Generic;
using Assets.Code.Stronghold2;

namespace Assets.Code.Stronghold2.FormatReaders
{
  public class S2MFile
  {
    public string Source { get; set; } = string.Empty;

    public int FileSize { get; set; }

    public int HeaderEndOffset { get; set; }

    public S2MHeader Header { get; set; } = new S2MHeader();

    public S2MSegmentA SegmentA { get; set; } = new S2MSegmentA();

    public S2MWorldPayload WorldPayload { get; set; } = new S2MWorldPayload();

    public byte[] RawBytes { get; set; } = Array.Empty<byte>();
  }

  public class S2MHeader
  {
    public int EndOffset { get; set; }

    public List<S2MStringOption> StringOptions { get; } = new List<S2MStringOption>();

    public List<S2MIntOption> IntOptions { get; } = new List<S2MIntOption>();
  }

  public class S2MStringOption
  {
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
  }

  public class S2MIntOption
  {
    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }
  }

  public class S2MSegmentA
  {
    public int StartOffset { get; set; }

    public int CompressedLength { get; set; }

    public int DecompressedLength { get; set; }

    public byte ZlibHeaderByte0 { get; set; }

    public byte ZlibHeaderByte1 { get; set; }

    public byte[] CompressedBytes { get; set; } = Array.Empty<byte>();

    public byte[] DecompressedBytes { get; set; } = Array.Empty<byte>();

    public List<S2MTokenRecord> TokenRecords { get; set; } = new List<S2MTokenRecord>();

    public List<S2MScenarioEvent> ScenarioEvents { get; } = new List<S2MScenarioEvent>();

    public List<string> ParseIssues { get; } = new List<string>();
  }

  public class S2MTokenRecord
  {
    public int RecordStart { get; set; }

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Tag { get; set; }

    public int BaseNameLength { get; set; }

    public string BaseName { get; set; } = string.Empty;

    public int MetadataEnd { get; set; }

    public int PayloadStart { get; set; }

    public int PayloadLength { get; set; }

    public byte[] PayloadBytes { get; set; } = Array.Empty<byte>();

    public List<int> PayloadInt32 { get; set; } = new List<int>();
  }

  public class S2MScenarioEvent
  {
    public int EventIndex { get; set; }

    public int RecordStart { get; set; }

    public int RecordEndExclusive { get; set; }

    public int RecordId { get; set; }

    public string RecordName { get; set; } = string.Empty;

    public byte Month { get; set; }

    public byte Delay { get; set; }

    // Candidate event-level repeat control fields derived from controlled diffs.
    public int RepeatCountCode { get; set; }

    public int RepeatTimeCode { get; set; }

    public List<S2MScenarioTrigger> Triggers { get; } = new List<S2MScenarioTrigger>();

    public List<S2MScenarioAction> Actions { get; } = new List<S2MScenarioAction>();
  }

  public abstract class S2MScenarioNode
  {
    public int RecordId { get; set; }

    public int RecordStart { get; set; }

    public string RecordName { get; set; } = string.Empty;

    public int Tag { get; set; }

    public string BaseName { get; set; } = string.Empty;

    public List<int> RawPayloadInt32 { get; set; } = new List<int>();
  }

  public abstract class S2MScenarioTrigger : S2MScenarioNode
  {
    public int TriggerCode { get; set; }

    public int TriggerModeCode { get; set; }

    public int TriggerValue { get; set; }
  }

  public abstract class S2MScenarioAction : S2MScenarioNode
  {
  }

  public class S2MUnknownTrigger : S2MScenarioTrigger
  {
  }

  public class S2MUnknownAction : S2MScenarioAction
  {
  }

  public class S2MGoodsAcquiredTrigger : S2MScenarioTrigger
  {
    public int GoodsVectorStartIndex { get; set; } = -1;

    public Dictionary<GoodsAcquiredEnum, int> GoodsAmounts { get; } = new Dictionary<GoodsAcquiredEnum, int>();
  }

  public class S2MEnemyGoodsAcquiredTrigger : S2MScenarioTrigger
  {
    public int PayloadWordAlignment { get; set; }

    public int GoodsVectorStartIndex { get; set; } = -1;

    public int TargetLordSelector { get; set; }

    public Dictionary<GoodsAcquiredEnum, int> GoodsAmounts { get; } = new Dictionary<GoodsAcquiredEnum, int>();
  }

  public class S2MGoldAcquiredTrigger : S2MScenarioTrigger
  {
    public int RequiredGoldAmount { get; set; }
  }

  public class S2MEnemyGoldAcquiredTrigger : S2MScenarioTrigger
  {
    public int RequiredGoldAmount { get; set; }

    public int TargetLordSelector { get; set; }
  }

  public class S2MHonourAcquiredTrigger : S2MScenarioTrigger
  {
    public int RequiredHonourAmount { get; set; }
  }

  public class S2MEnemyHonourAcquiredTrigger : S2MScenarioTrigger
  {
    public int RequiredHonourAmount { get; set; }

    public int TargetLordSelector { get; set; }
  }

  public class S2MPopulationReachedTrigger : S2MScenarioTrigger
  {
    public int RequiredPopulation { get; set; }
  }

  public class S2MNoPeopleLeftTrigger : S2MScenarioTrigger
  {
  }

  public class S2MAnyEnemyOnMapTrigger : S2MScenarioTrigger
  {
  }

  public class S2MAnyEnemyTroopOnMapTrigger : S2MScenarioTrigger
  {
  }

  public class S2MNoEnemyOrInvasionsLeftTrigger : S2MScenarioTrigger
  {
  }

  public class S2MAllYourTroopsDeadTrigger : S2MScenarioTrigger
  {
  }

  public class S2MPercentTroopsKilledTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }

    public int PercentTroopsKilled { get; set; }
  }

  public class S2MGetXTroopsTrigger : S2MScenarioTrigger
  {
    public int RequiredTroopCount { get; set; }

    public int TroopTypeCode { get; set; }

    public int TroopClassCode { get; set; }
  }

  public class S2MLordDamagedTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }

    public int RequiredDamagePercent { get; set; }
  }

  public class S2MEnemyLordDiesTrigger : S2MScenarioTrigger
  {
  }

  public class S2MSpecificEnemyLordDiesTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MRescueLordTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MMultipleLordsDeadTrigger : S2MScenarioTrigger
  {
    public int LordSelectionMaskCandidate { get; set; }

    public byte[] LordSelectionFlagsCandidate { get; set; } = Array.Empty<byte>();

    public List<int> SelectedLordSlotsCandidate { get; } = new List<int>();
  }

  public class S2MPlayerKillsLordXTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MOtherLordsKillsLordXTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MSpecificLordKillsLordXTrigger : S2MScenarioTrigger
  {
    public int KillerLordSelector { get; set; }

    public int KilledLordSelector { get; set; }
  }

  public class S2MOutlawCampDestroyedTrigger : S2MScenarioTrigger
  {
  }

  public class S2MBreachInWallTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MEnemyTroopsOnWallsTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MSomeEnemiesCloseToKeepTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MManyEnemiesCloseToKeepTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MLiftSiegeTrigger : S2MScenarioTrigger
  {
    public int TargetLordSelector { get; set; }
  }

  public class S2MEnemyNearMarkerTrigger : S2MScenarioTrigger
  {
    public int Radius { get; set; }

    public int FlagColorType { get; set; }

    public int FlagNumber { get; set; }
  }

  public class S2MQuestCompleteTrigger : S2MScenarioTrigger
  {
    public bool QuestACompleted { get; set; }

    public bool QuestBCompleted { get; set; }

    public bool QuestCCompleted { get; set; }

    public int CompletedQuestCount { get; set; }
  }

  public class S2MQuestNotCompleteTrigger : S2MScenarioTrigger
  {
    public int QuestIndex { get; set; }
  }

  public class S2MSingleQuestCompleteTrigger : S2MScenarioTrigger
  {
    public int QuestIndex { get; set; }
  }

  public class S2MNumQuestsCompleteTrigger : S2MScenarioTrigger
  {
    public int RequiredQuestCount { get; set; }
  }

  public class S2MQuestFailedTrigger : S2MScenarioTrigger
  {
    public int QuestIndex { get; set; }
  }

  public class S2MAfterBriefingTrigger : S2MScenarioTrigger
  {
  }

  public class S2MNoMessagesPlayingTrigger : S2MScenarioTrigger
  {
  }

  public class S2MConstructedBuildingCompleteTrigger : S2MScenarioTrigger
  {
  }

  public class S2MConstructedBuildingPercentCompleteTrigger : S2MScenarioTrigger
  {
    public int RequiredPercent { get; set; }
  }

  public class S2MControlNumEstatesTrigger : S2MScenarioTrigger
  {
    public int RequiredEstateCount { get; set; }
  }

  public class S2MNoBearsOnMapTrigger : S2MScenarioTrigger
  {
  }

  public class S2MNoWolvesOnMapTrigger : S2MScenarioTrigger
  {
  }

  public class S2MNoFoodInGranaryTrigger : S2MScenarioTrigger
  {
    public int FlagColorCode { get; set; }

    public int FlagSelectionValue { get; set; }
  }

  public class S2MAlwaysTrigger : S2MScenarioTrigger
  {
  }

  public class S2MLordDiesTrigger : S2MScenarioTrigger
  {
  }

  public class S2MNoGongInYourEstatesTrigger : S2MScenarioTrigger
  {
  }

  public class S2MNoRatsInYourEstatesTrigger : S2MScenarioTrigger
  {
  }

  public class S2MNoCriminalsTrigger : S2MScenarioTrigger
  {
  }

  public class S2MWinAction : S2MScenarioAction
  {
  }

  public class S2MLoseAction : S2MScenarioAction
  {
  }

  public enum S2MStopInvasionsMode
  {
    Unknown = 0,
    StopRepeatingInvasions = 1,
    StopAllInvasions = 2,
  }

  public class S2MStopInvasionsAction : S2MScenarioAction
  {
    public int TargetLordSelector { get; set; }

    public int ModeCode { get; set; }

    public S2MStopInvasionsMode Mode { get; set; } = S2MStopInvasionsMode.Unknown;
  }

  public class S2MBearAttackAction : S2MScenarioAction
  {
    public int TargetFlagColorCode { get; set; }

    // Stored as zero-based selector in current BearAttackAction samples.
    public int TargetFlagNumberCode { get; set; }

    public int BearCount { get; set; }
  }

  public class S2MCreateCriminalsAction : S2MScenarioAction
  {
    // Control/mode field observed at payload offset +20 in current samples.
    public int ModeCode { get; set; }

    // Percent configured in editor (for example, 14, 15) at payload offset +24.
    public int CreateCriminalsPercent { get; set; }
  }

  public class S2MMaintainMinimumFoodLevelAction : S2MScenarioAction
  {
    // Minimum food threshold configured in editor, observed at payload offset +24.
    public int MinimumFoodLevelUnits { get; set; }
  }

  public class S2MPlagueOfRatsAction : S2MScenarioAction
  {
    // Number of rats configured in editor, observed at payload offset +24.
    public int RatsCount { get; set; }
  }

  public class S2MRatInvasionAction : S2MScenarioAction
  {
    // Stored as packed selector code at payload offset +24.
    public int TargetFlagColorCode { get; set; }

    // Stored as zero-based selector in current samples, at payload offset +28.
    public int TargetFlagNumberCode { get; set; }

    // Number of rats configured in editor, observed at payload offset +32.
    public int RatsCount { get; set; }
  }

  public class S2MSetWolvesToDefensiveAction : S2MScenarioAction
  {
  }

  public class S2MBadWeatherAction : S2MScenarioAction
  {
  }

  public class S2MWheatDiseaseAction : S2MScenarioAction
  {
  }

  public class S2MAppleBlightAction : S2MScenarioAction
  {
  }

  public class S2MVineRotAction : S2MScenarioAction
  {
  }

  public class S2MSwineFeverAction : S2MScenarioAction
  {
  }

  public class S2MMadCowDiseaseAction : S2MScenarioAction
  {
  }

  public class S2MLostSheepAction : S2MScenarioAction
  {
  }

  public class S2MHopWeevilAction : S2MScenarioAction
  {
  }

  public class S2MWolfInvasionAction : S2MScenarioAction
  {
    // Control/mode family code observed at payload offset +20.
    public int ControlCode { get; set; }

    public int InvasionPointFlagColorCode { get; set; }

    // Stored as zero-based selector in current WolfInvasionAction samples.
    public int InvasionPointFlagNumberCode { get; set; }

    public int TargetPointFlagColorCode { get; set; }

    // Stored as zero-based selector in current WolfInvasionAction samples.
    public int TargetPointFlagNumberCode { get; set; }

    public int WolfCount { get; set; }
  }

  public enum S2MInvasionTroopType
  {
    ArmedPeasant = 0,
    Spearman = 1,
    Archer = 2,
    Pikeman = 3,
    Maceman = 4,
    Crossbowman = 5,
    Swordsman = 6,
    Knight = 7,
    Monk = 8,
    WarriorMonk = 9,
    Ladderman = 10,
    Engineer = 11,
    Assassin = 12,
    Outlaw = 13,
    HorseArcher = 14,
    Berserker = 15,
    BoatWarrior = 16,
    HorseCavalry = 17,
    AxeThrower = 18,
    SmallSiegeTower = 19,
    LargeSiegeTower = 20,
    BatteringRam = 21,
    Cat = 22,
    Trebuchet = 23,
    Ballista = 24,
    Catapult = 25,
    Manglet = 26,
    BurningCart = 27,
  }

  public enum S2MInvasionWarningType
  {
    Unknown = -1,
    NoWarnings = 0,
    EarlyWarnings = 1,
    NormalMessages = 2,
    FullWarnings = 3,
  }

  public enum S2MInvasionArmyType
  {
    Unknown = -1,
    MovementArmy = 0,
    SiegeArmy = 1,
    DefensiveArmy = 2,
    AttackingArmy = 3,
  }

  public class S2MInvasionAction : S2MScenarioAction
  {
    public int InvasionPointFlagColorCode { get; set; }

    public int InvasionPointFlagNumber { get; set; }

    public bool InvasionPointAnyFlagNumber { get; set; }

    public int DestinationPointTypeCode { get; set; }

    public int DestinationFlagColorCode { get; set; }

    public int AttackTargetLordSelector { get; set; }

    public int OwnerLordSelectorCode { get; set; }

    public int WarningTypeCode { get; set; }

    public S2MInvasionWarningType WarningType { get; set; } = S2MInvasionWarningType.Unknown;

    public int ArmyTypeCode { get; set; }

    public S2MInvasionArmyType ArmyType { get; set; } = S2MInvasionArmyType.Unknown;

    public int RepeatCountCode { get; set; }

    public int IncludeLordInArmyCode { get; set; }

    public int LeaveMapCode { get; set; }

    public int AttackModeCode0 { get; set; }

    public int AttackModeCode1 { get; set; }

    // Keep raw contiguous slot values until all troop-slot identities are fully locked.
    public List<int> RawTroopSlotCounts { get; } = new List<int>();

    // High-confidence troop mappings from current controlled tests.
    public Dictionary<S2MInvasionTroopType, int> ConfirmedTroopCounts { get; } = new Dictionary<S2MInvasionTroopType, int>();
  }

  public class S2MWorldPayload
  {
    public int ScanStartOffset { get; set; }

    public List<S2MZlibCandidate> ZlibCandidates { get; } = new List<S2MZlibCandidate>();

    public S2MZlibCandidate DominantCandidate { get; set; } = new S2MZlibCandidate();

    public byte[] DominantDecompressedBytes { get; set; } = Array.Empty<byte>();

    public S2MHeightLayerBlock HeightLayer { get; set; } = new S2MHeightLayerBlock();
  }

  public class S2MZlibCandidate
  {
    public int Offset { get; set; }

    public byte[] ZlibHeader { get; set; } = Array.Empty<byte>();

    public int DecompressedLength { get; set; }

    public int AnchorHits { get; set; }
  }

  public class S2MHeightLayerBlock
  {
    public bool Found { get; set; }

    public int LabelOffset { get; set; } = -1;

    public int DimensionsOffset { get; set; } = -1;

    public int DataOffset { get; set; } = -1;

    public int RowByteWidth { get; set; }

    public int RowCount { get; set; }

    public int TileWidth { get; set; }

    public int TileHeight { get; set; }

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public string ParseIssue { get; set; } = string.Empty;
  }
}
