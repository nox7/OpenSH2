using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Code.Stronghold2;

namespace Assets.Code.Stronghold2.FormatReaders
{
  public static class S2MDebugDumper
  {
    public static string DumpSummary(S2MFile file)
    {
      if (file == null)
      {
        return "S2M file is null.";
      }

      var lines = new List<string>
      {
        "=== S2M Summary ===",
        $"Source: {file.Source}",
        $"FileSize: {file.FileSize}",
        $"HeaderEndOffset: {file.HeaderEndOffset}",
        $"SegmentA: start={file.SegmentA.StartOffset} compressed={file.SegmentA.CompressedLength} decompressed={file.SegmentA.DecompressedLength}",
        $"SegmentA TokenRecords: {file.SegmentA.TokenRecords.Count}",
        $"ScenarioEvents: {file.SegmentA.ScenarioEvents.Count}",
        string.Empty,
        "=== Scenario Events ===",
      };

      foreach (var ev in file.SegmentA.ScenarioEvents)
      {
        lines.Add($"Event[{ev.EventIndex}] id={ev.RecordId} name={ev.RecordName} month={ev.Month} delay={ev.Delay} range={ev.RecordStart}..{ev.RecordEndExclusive}");

        if (ev.Actions.Count == 0)
        {
          lines.Add("  Actions: (none)");
        }
        else
        {
          lines.Add("  Actions:");
          foreach (var action in ev.Actions)
          {
            lines.Add("    - " + FormatActionLine(action));
          }
        }

        if (ev.Triggers.Count == 0)
        {
          lines.Add("  Triggers: (none)");
        }
        else
        {
          lines.Add("  Triggers:");
          foreach (var trigger in ev.Triggers)
          {
            lines.Add("    - " + FormatTriggerLine(trigger));
          }
        }

        lines.Add(string.Empty);
      }

      if (file.SegmentA.ParseIssues.Count > 0)
      {
        lines.Add("=== Segment A Parse Issues ===");
        foreach (var issue in file.SegmentA.ParseIssues)
        {
          lines.Add("- " + issue);
        }

        lines.Add(string.Empty);
      }

      lines.Add("=== World Payload ===");
      lines.Add($"ZlibCandidates: {file.WorldPayload.ZlibCandidates.Count}");
      lines.Add($"Dominant: offset={file.WorldPayload.DominantCandidate.Offset} decompressed={file.WorldPayload.DominantCandidate.DecompressedLength} anchors={file.WorldPayload.DominantCandidate.AnchorHits}");
      lines.Add($"HeightLayer: found={file.WorldPayload.HeightLayer.Found} tiles={file.WorldPayload.HeightLayer.TileWidth}x{file.WorldPayload.HeightLayer.TileHeight}");
      if (!string.IsNullOrEmpty(file.WorldPayload.HeightLayer.ParseIssue))
      {
        lines.Add($"HeightLayerIssue: {file.WorldPayload.HeightLayer.ParseIssue}");
      }

      return string.Join(Environment.NewLine, lines);
    }

    private static string FormatActionLine(S2MScenarioAction action)
    {
      var prefix = $"{action.GetType().Name} name={action.RecordName} id={action.RecordId} tag={action.Tag}";

      if (action is S2MStopInvasionsAction stopInvasions)
      {
        return prefix + $" targetLordSelector={stopInvasions.TargetLordSelector} modeCode={stopInvasions.ModeCode} mode={stopInvasions.Mode}";
      }

      if (action is S2MInvasionAction invasion)
      {
        var troopSummary = invasion.ConfirmedTroopCounts.Count == 0
          ? "(none)"
          : string.Join(",", invasion.ConfirmedTroopCounts
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}:{kvp.Value}"));

        return prefix
          + $" invasionPointColor={invasion.InvasionPointFlagColorCode} invasionPointFlagNumber={invasion.InvasionPointFlagNumber} invasionPointAny={invasion.InvasionPointAnyFlagNumber}"
          + $" destinationTypeCode={invasion.DestinationPointTypeCode} destinationColor={invasion.DestinationFlagColorCode}"
          + $" attackTargetLordSelector={invasion.AttackTargetLordSelector} ownerLordSelectorCode={invasion.OwnerLordSelectorCode}"
          + $" warningTypeCode={invasion.WarningTypeCode} warningType={invasion.WarningType} armyTypeCode={invasion.ArmyTypeCode} armyType={invasion.ArmyType}"
          + $" repeatCountCode={invasion.RepeatCountCode} includeLordCode={invasion.IncludeLordInArmyCode} leaveMapCode={invasion.LeaveMapCode}"
          + $" attackModeCode0={invasion.AttackModeCode0} attackModeCode1={invasion.AttackModeCode1}"
          + $" confirmedTroops=[{troopSummary}]";
      }

      if (action is S2MBearAttackAction bearAttack)
      {
        return prefix
          + $" targetFlagColorCode={bearAttack.TargetFlagColorCode}"
          + $" targetFlagNumberCode={bearAttack.TargetFlagNumberCode}"
          + $" bearCount={bearAttack.BearCount}";
      }

      if (action is S2MCreateCriminalsAction createCriminals)
      {
        return prefix
          + $" modeCode={createCriminals.ModeCode}"
          + $" createCriminalsPercent={createCriminals.CreateCriminalsPercent}";
      }

      return prefix;
    }

    private static string FormatTriggerLine(S2MScenarioTrigger trigger)
    {
      var prefix = $"{trigger.GetType().Name} name={trigger.RecordName} id={trigger.RecordId} code={trigger.TriggerCode} mode={trigger.TriggerModeCode} value={trigger.TriggerValue}";

      if (trigger is S2MGoodsAcquiredTrigger goods)
      {
        return prefix + " " + FormatGoodsAmounts(goods.GoodsAmounts);
      }

      if (trigger is S2MEnemyGoodsAcquiredTrigger enemyGoods)
      {
        return prefix + " " + FormatGoodsAmounts(enemyGoods.GoodsAmounts) +
          $" targetLordSelector={enemyGoods.TargetLordSelector} payloadAlignment={enemyGoods.PayloadWordAlignment}";
      }

      if (trigger is S2MGoldAcquiredTrigger gold)
      {
        return prefix + $" requiredGold={gold.RequiredGoldAmount}";
      }

      if (trigger is S2MEnemyGoldAcquiredTrigger enemyGold)
      {
        return prefix + $" requiredGold={enemyGold.RequiredGoldAmount} targetLordSelector={enemyGold.TargetLordSelector}";
      }

      if (trigger is S2MHonourAcquiredTrigger honour)
      {
        return prefix + $" requiredHonour={honour.RequiredHonourAmount}";
      }

      if (trigger is S2MEnemyHonourAcquiredTrigger enemyHonour)
      {
        return prefix + $" requiredHonour={enemyHonour.RequiredHonourAmount} targetLordSelector={enemyHonour.TargetLordSelector}";
      }

      if (trigger is S2MPopulationReachedTrigger population)
      {
        return prefix + $" requiredPopulation={population.RequiredPopulation}";
      }

      if (trigger is S2MPercentTroopsKilledTrigger troopsKilled)
      {
        return prefix + $" targetLordSelector={troopsKilled.TargetLordSelector} percentTroopsKilled={troopsKilled.PercentTroopsKilled}";
      }

      if (trigger is S2MGetXTroopsTrigger getXTroops)
      {
        return prefix + $" requiredTroopCount={getXTroops.RequiredTroopCount} troopTypeCode={getXTroops.TroopTypeCode} troopClassCode={getXTroops.TroopClassCode}";
      }

      if (trigger is S2MLordDamagedTrigger lordDamaged)
      {
        return prefix + $" targetLordSelector={lordDamaged.TargetLordSelector} requiredDamagePercent={lordDamaged.RequiredDamagePercent}";
      }

      if (trigger is S2MSpecificEnemyLordDiesTrigger specificEnemyLordDies)
      {
        return prefix + $" targetLordSelector={specificEnemyLordDies.TargetLordSelector}";
      }

      if (trigger is S2MRescueLordTrigger rescueLord)
      {
        return prefix + $" targetLordSelector={rescueLord.TargetLordSelector}";
      }

      if (trigger is S2MMultipleLordsDeadTrigger multipleLordsDead)
      {
        var selectedSlots = multipleLordsDead.SelectedLordSlotsCandidate.Count == 0
          ? "(none)"
          : string.Join(",", multipleLordsDead.SelectedLordSlotsCandidate);
        var flags = multipleLordsDead.LordSelectionFlagsCandidate == null || multipleLordsDead.LordSelectionFlagsCandidate.Length == 0
          ? "(none)"
          : string.Join(",", multipleLordsDead.LordSelectionFlagsCandidate.Select(v => v.ToString()));
        return prefix + $" lordSelectionMaskCandidate={multipleLordsDead.LordSelectionMaskCandidate} selectedLordSlotsCandidate={selectedSlots} lordSelectionFlagsCandidate=[{flags}]";
      }

      if (trigger is S2MPlayerKillsLordXTrigger playerKillsLordX)
      {
        return prefix + $" targetLordSelector={playerKillsLordX.TargetLordSelector}";
      }

      if (trigger is S2MOtherLordsKillsLordXTrigger otherLordsKillsLordX)
      {
        return prefix + $" targetLordSelector={otherLordsKillsLordX.TargetLordSelector}";
      }

      if (trigger is S2MSpecificLordKillsLordXTrigger specificLordKillsLordX)
      {
        return prefix + $" killerLordSelector={specificLordKillsLordX.KillerLordSelector} killedLordSelector={specificLordKillsLordX.KilledLordSelector}";
      }

      if (trigger is S2MBreachInWallTrigger breachInWall)
      {
        return prefix + $" targetLordSelector={breachInWall.TargetLordSelector}";
      }

      if (trigger is S2MEnemyTroopsOnWallsTrigger enemyTroopsOnWalls)
      {
        return prefix + $" targetLordSelector={enemyTroopsOnWalls.TargetLordSelector}";
      }

      if (trigger is S2MSomeEnemiesCloseToKeepTrigger someEnemiesCloseToKeep)
      {
        return prefix + $" targetLordSelector={someEnemiesCloseToKeep.TargetLordSelector}";
      }

      if (trigger is S2MManyEnemiesCloseToKeepTrigger manyEnemiesCloseToKeep)
      {
        return prefix + $" targetLordSelector={manyEnemiesCloseToKeep.TargetLordSelector}";
      }

      if (trigger is S2MLiftSiegeTrigger liftSiege)
      {
        return prefix + $" targetLordSelector={liftSiege.TargetLordSelector}";
      }

      if (trigger is S2MEnemyNearMarkerTrigger enemyNearMarker)
      {
        return prefix + $" radius={enemyNearMarker.Radius} flagColorType={enemyNearMarker.FlagColorType} flagNumber={enemyNearMarker.FlagNumber}";
      }

      if (trigger is S2MQuestCompleteTrigger questComplete)
      {
        return prefix + $" questACompleted={questComplete.QuestACompleted} questBCompleted={questComplete.QuestBCompleted} questCCompleted={questComplete.QuestCCompleted} completedQuestCount={questComplete.CompletedQuestCount}";
      }

      if (trigger is S2MQuestNotCompleteTrigger questNotComplete)
      {
        return prefix + $" questIndex={questNotComplete.QuestIndex}";
      }

      if (trigger is S2MSingleQuestCompleteTrigger singleQuestComplete)
      {
        return prefix + $" questIndex={singleQuestComplete.QuestIndex}";
      }

      if (trigger is S2MNumQuestsCompleteTrigger numQuestsComplete)
      {
        return prefix + $" requiredQuestCount={numQuestsComplete.RequiredQuestCount}";
      }

      if (trigger is S2MQuestFailedTrigger questFailed)
      {
        return prefix + $" questIndex={questFailed.QuestIndex}";
      }

      if (trigger is S2MConstructedBuildingPercentCompleteTrigger buildingPercent)
      {
        return prefix + $" requiredPercent={buildingPercent.RequiredPercent}";
      }

      if (trigger is S2MControlNumEstatesTrigger controlEstates)
      {
        return prefix + $" requiredEstateCount={controlEstates.RequiredEstateCount}";
      }

      if (trigger is S2MNoFoodInGranaryTrigger noFood)
      {
        return prefix + $" flagColorCode={noFood.FlagColorCode} flagSelectionValue={noFood.FlagSelectionValue}";
      }

      return prefix;
    }

    private static string FormatGoodsAmounts(Dictionary<GoodsAcquiredEnum, int> amounts)
    {
      if (amounts == null || amounts.Count == 0)
      {
        return "goods=(none)";
      }

      var nonZero = amounts
        .Where(kvp => kvp.Value != 0)
        .OrderBy(kvp => (int)kvp.Key)
        .Select(kvp => $"{kvp.Key}:{kvp.Value}")
        .ToList();

      if (nonZero.Count == 0)
      {
        return "goods=(all-zero)";
      }

      return "goods=" + string.Join(", ", nonZero);
    }
  }
}
