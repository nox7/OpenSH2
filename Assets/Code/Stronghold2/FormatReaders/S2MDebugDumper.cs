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
        lines.Add($"Event[{ev.EventIndex}] id={ev.RecordId} name={ev.RecordName} month={ev.Month} delay={ev.Delay} repeatCount={ev.RepeatCountCode} repeatTime={ev.RepeatTimeCode} range={ev.RecordStart}..{ev.RecordEndExclusive}");

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

      if (action is S2MMaintainMinimumFoodLevelAction maintainFood)
      {
        return prefix
          + $" minimumFoodLevelUnits={maintainFood.MinimumFoodLevelUnits}";
      }

      if (action is S2MFireAction fire)
      {
        return prefix
          + $" fireCount={fire.FireCount}";
      }

      if (action is S2MProtestAction protest)
      {
        return prefix
          + $" peasantCount={protest.PeasantCount}";
      }

      if (action is S2MTurnIndustriesOnOffAction turnIndustriesOnOff)
      {
        return prefix
          + $" scopeModeCode={turnIndustriesOnOff.ScopeModeCode}"
          + $" scopeMode={turnIndustriesOnOff.ScopeMode}"
          + $" markedEstateFlagColorCode={turnIndustriesOnOff.MarkedEstateFlagColorCode}"
          + $" markedEstateFlagNumberCode={turnIndustriesOnOff.MarkedEstateFlagNumberCode}"
          + $" lordSelectorCode={turnIndustriesOnOff.LordSelectorCode}"
          + $" control40={turnIndustriesOnOff.ControlByte40}"
          + $" control41={turnIndustriesOnOff.ControlByte41}"
          + $" control42={turnIndustriesOnOff.ControlByte42}"
          + $" geeseToggleCode={turnIndustriesOnOff.GeeseToggleCode}"
          + $" cheeseToggleCode={turnIndustriesOnOff.CheeseToggleCode}"
          + $" rawToggleSegmentLen={turnIndustriesOnOff.RawToggleSegment.Count}";
      }

      if (action is S2MCapResourcesAction capResources)
      {
        string configuredSlots = string.Join(",", capResources.ResourceCapSlots
          .Select((value, index) => new { index, value })
          .Where(x => x.value >= 0)
          .Select(x => $"{x.index}:{x.value}"));

        if (string.IsNullOrEmpty(configuredSlots))
        {
          configuredSlots = "none";
        }

        return prefix
          + $" scopeModeCode={capResources.ScopeModeCode}"
          + $" scopeMode={capResources.ScopeMode}"
          + $" markedEstateFlagColorCode={capResources.MarkedEstateFlagColorCode}"
          + $" markedEstateFlagNumberCode={capResources.MarkedEstateFlagNumberCode}"
          + $" lordSelectorCode={capResources.LordSelectorCode}"
          + $" control40={capResources.ControlByte40}"
          + $" goldCap={capResources.GoldCap}"
          + $" dateCapCode={capResources.DateCapCode}"
          + $" configuredCapSlots=[{configuredSlots}]"
          + $" resourceCapSlotCount={capResources.ResourceCapSlots.Count}";
      }

      if (action is S2MGiveResourcesAction giveResources)
      {
        string configuredSlots = string.Join(",", giveResources.ResourceGiveSlots
          .Select((value, index) => new { index, value })
          .Where(x => x.value != 0)
          .Select(x => $"{x.index}:{x.value}"));

        if (string.IsNullOrEmpty(configuredSlots))
        {
          configuredSlots = "none";
        }

        return prefix
          + $" scopeModeCode={giveResources.ScopeModeCode}"
          + $" scopeMode={giveResources.ScopeMode}"
          + $" markedEstateFlagColorCode={giveResources.MarkedEstateFlagColorCode}"
          + $" markedEstateFlagNumberCode={giveResources.MarkedEstateFlagNumberCode}"
          + $" lordSelectorCode={giveResources.LordSelectorCode}"
          + $" control40={giveResources.ControlByte40}"
          + $" goldAmount={giveResources.GoldAmount}"
          + $" dateCode={giveResources.DateCode}"
          + $" configuredGiveSlots=[{configuredSlots}]"
          + $" resourceGiveSlotCount={giveResources.ResourceGiveSlots.Count}";
      }

      if (action is S2MSetAlliesAction setAllies)
      {
        return prefix
          + $" scopeModeCode={setAllies.ScopeModeCode}"
          + $" playerRelation={setAllies.PlayerRelation}"
          + $" olafRelation={setAllies.OlafRelation}"
          + $" lordBarclayRelation={setAllies.LordBarclayRelation}"
          + $" theHawkRelation={setAllies.TheHawkRelation}"
          + $" theBullRelation={setAllies.TheBullRelation}"
          + $" ladySerenRelation={setAllies.LadySerenRelation}"
          + $" edwinRelation={setAllies.EdwinRelation}"
          + $" theKingRelation={setAllies.TheKingRelation}"
          + $" sirWilliamRelation={setAllies.SirWilliamRelation}"
          + $" sirGreyRelation={setAllies.SirGreyRelation}";
      }

      if (action is S2MMoveLordAction moveLord)
      {
        return prefix
          + $" controlCode={moveLord.ControlCode}"
          + $" lordSelectorCode={moveLord.LordSelectorCode}"
          + $" targetFlagNumberCode={moveLord.TargetFlagNumberCode}"
          + $" targetFlagColorCode={moveLord.TargetFlagColorCode}"
          + $" exitModeCode={moveLord.ExitModeCode}"
          + $" exitMode={moveLord.ExitMode}";
      }

      if (action is S2MTakeEnemysCastleAction takeEnemysCastle)
      {
        return prefix
          + $" controlCode={takeEnemysCastle.ControlCode}"
          + $" lordSelectorCode={takeEnemysCastle.LordSelectorCode}";
      }

      if (action is S2MConvertEstateToVillageAction convertEstateToVillage)
      {
        return prefix
          + $" controlCode={convertEstateToVillage.ControlCode}"
          + $" targetFlagColorCode={convertEstateToVillage.TargetFlagColorCode}"
          + $" targetFlagNumberCode={convertEstateToVillage.TargetFlagNumberCode}";
      }

      if (action is S2MQuestAction questAction)
      {
        return prefix
          + $" controlCode={questAction.ControlCode}"
          + $" questIndex={questAction.QuestIndex}"
          + $" questChoice={questAction.QuestChoice}";
      }

      if (action is S2MQuestFailedAction questFailedAction)
      {
        return prefix
          + $" controlCode={questFailedAction.ControlCode}"
          + $" questIndex={questFailedAction.QuestIndex}"
          + $" questChoice={questFailedAction.QuestChoice}";
      }

      if (action is S2MSetAvailableTroopTypesAction setAvailableTroopTypes)
      {
        string zeroOffsets = string.Join(",", setAvailableTroopTypes.ZeroToggleOffsets);
        if (string.IsNullOrEmpty(zeroOffsets))
        {
          zeroOffsets = "none";
        }

        return prefix
          + $" controlCode={setAvailableTroopTypes.ControlCode}"
          + $" rawToggleByteCount={setAvailableTroopTypes.RawToggleBytes.Count}"
          + $" zeroToggleOffsets=[{zeroOffsets}]";
      }

      if (action is S2MGongProductionAction gongProduction)
      {
        return prefix
          + $" controlCode={gongProduction.ControlCode}"
          + $" productionLevelCode={gongProduction.ProductionLevelCode}"
          + $" productionLevel={gongProduction.ProductionLevel}";
      }

      if (action is S2MRatProductionAction ratProduction)
      {
        return prefix
          + $" controlCode={ratProduction.ControlCode}"
          + $" productionLevelCode={ratProduction.ProductionLevelCode}"
          + $" productionLevel={ratProduction.ProductionLevel}";
      }

      if (action is S2MDiseaseProductionAction diseaseProduction)
      {
        return prefix
          + $" controlCode={diseaseProduction.ControlCode}"
          + $" productionLevelCode={diseaseProduction.ProductionLevelCode}"
          + $" productionLevel={diseaseProduction.ProductionLevel}";
      }

      if (action is S2MCrimeRateAction crimeRate)
      {
        return prefix
          + $" controlCode={crimeRate.ControlCode}"
          + $" productionLevelCode={crimeRate.ProductionLevelCode}"
          + $" productionLevel={crimeRate.ProductionLevel}";
      }

      if (action is S2MOutlawProductionAction outlawProduction)
      {
        return prefix
          + $" controlCode={outlawProduction.ControlCode}"
          + $" productionLevelCode={outlawProduction.ProductionLevelCode}"
          + $" productionLevel={outlawProduction.ProductionLevel}"
          + $" maxOutlaws={outlawProduction.MaxOutlaws}"
          + $" locationCode={outlawProduction.LocationCode}"
          + $" location={outlawProduction.Location}";
      }

      if (action is S2MWolfSpawnRateAction wolfSpawnRate)
      {
        return prefix
          + $" controlCode={wolfSpawnRate.ControlCode}"
          + $" productionLevelCode={wolfSpawnRate.ProductionLevelCode}"
          + $" productionLevel={wolfSpawnRate.ProductionLevel}";
      }

      if (action is S2MLimitWeaponProductionAction limitWeaponProduction)
      {
        string zeroOffsets = string.Join(",", limitWeaponProduction.ZeroToggleOffsets);
        if (string.IsNullOrEmpty(zeroOffsets))
        {
          zeroOffsets = "none";
        }

        string disabledWeaponTypes = string.Join(",", limitWeaponProduction.DisabledWeaponTypes);
        if (string.IsNullOrEmpty(disabledWeaponTypes))
        {
          disabledWeaponTypes = "none";
        }

        return prefix
          + $" controlCode={limitWeaponProduction.ControlCode}"
          + $" rawToggleByteCount={limitWeaponProduction.RawToggleBytes.Count}"
          + $" zeroToggleOffsets=[{zeroOffsets}]"
          + $" disabledWeapons=[{disabledWeaponTypes}]";
      }

      if (action is S2MSetCampfirePeasantsAction setCampfirePeasants)
      {
        return prefix
          + $" controlCode={setCampfirePeasants.ControlCode}"
          + $" peasantsCount={setCampfirePeasants.PeasantsCount}"
          + $" campfireFlagColorCode={setCampfirePeasants.CampfireFlagColorCode}"
          + $" campfireFlagNumberCode={setCampfirePeasants.CampfireFlagNumberCode}";
      }

      if (action is S2MControlConstructingBuildingsAction controlConstructingBuildings)
      {
        return prefix
          + $" controlCode={controlConstructingBuildings.ControlCode}"
          + $" buildingSitesCount={controlConstructingBuildings.BuildingSitesCount}"
          + $" buildingSitesStateCode={controlConstructingBuildings.BuildingSitesStateCode}"
          + $" buildingSitesState={controlConstructingBuildings.BuildingSitesState}";
      }

      if (action is S2MMaxOutPeasasntsAction maxOutPeasasnts)
      {
        return prefix
          + $" controlCode={maxOutPeasasnts.ControlCode}"
          + $" targetLordSelector={maxOutPeasasnts.TargetLordSelector}";
      }

      if (action is S2MKillAllLordsTroopsAction killAllLordsTroops)
      {
        return prefix
          + $" controlCode={killAllLordsTroops.ControlCode}"
          + $" targetLordSelector={killAllLordsTroops.TargetLordSelector}";
      }

      if (action is S2MRedirectVillageOutputAction redirectVillageOutput)
      {
        return prefix
          + $" sourceFlagColorCode={redirectVillageOutput.SourceFlagColorCode}"
          + $" sourceFlagNumberCode={redirectVillageOutput.SourceFlagNumberCode}"
          + $" targetEstateFlagColorCode={redirectVillageOutput.TargetEstateFlagColorCode}"
          + $" targetEstateFlagNumberCode={redirectVillageOutput.TargetEstateFlagNumberCode}";
      }

      if (action is S2MPlagueOfRatsAction plagueOfRats)
      {
        return prefix
          + $" ratsCount={plagueOfRats.RatsCount}";
      }

      if (action is S2MRatInvasionAction ratInvasion)
      {
        return prefix
          + $" targetFlagColorCode={ratInvasion.TargetFlagColorCode}"
          + $" targetFlagNumberCode={ratInvasion.TargetFlagNumberCode}"
          + $" ratsCount={ratInvasion.RatsCount}";
      }

      if (action is S2MGongInvasionAction gongInvasion)
      {
        return prefix
          + $" targetFlagColorCode={gongInvasion.TargetFlagColorCode}"
          + $" targetFlagNumberCode={gongInvasion.TargetFlagNumberCode}"
          + $" gongCount={gongInvasion.GongCount}";
      }

      if (action is S2MSetAllBuildingsOnFireAction)
      {
        return prefix;
      }

      if (action is S2MEnterBriefingAction)
      {
        return prefix;
      }

      if (action is S2MWitchcraftAction)
      {
        return prefix;
      }

      if (action is S2MBumperHarvestAction)
      {
        return prefix;
      }

      if (action is S2MSetWolvesToDefensiveAction)
      {
        return prefix;
      }

      if (action is S2MBadWeatherAction)
      {
        return prefix;
      }

      if (action is S2MWheatDiseaseAction)
      {
        return prefix;
      }

      if (action is S2MAppleBlightAction)
      {
        return prefix;
      }

      if (action is S2MVineRotAction)
      {
        return prefix;
      }

      if (action is S2MSwineFeverAction)
      {
        return prefix;
      }

      if (action is S2MMadCowDiseaseAction)
      {
        return prefix;
      }

      if (action is S2MLostSheepAction)
      {
        return prefix;
      }

      if (action is S2MHopWeevilAction)
      {
        return prefix;
      }

      if (action is S2MWolfInvasionAction wolfInvasion)
      {
        return prefix
          + $" controlCode={wolfInvasion.ControlCode}"
          + $" invasionPointFlagColorCode={wolfInvasion.InvasionPointFlagColorCode}"
          + $" invasionPointFlagNumberCode={wolfInvasion.InvasionPointFlagNumberCode}"
          + $" targetPointFlagColorCode={wolfInvasion.TargetPointFlagColorCode}"
          + $" targetPointFlagNumberCode={wolfInvasion.TargetPointFlagNumberCode}"
          + $" wolfCount={wolfInvasion.WolfCount}";
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

    private static string FormatGoodsAmounts(Dictionary<MapEditorScenarioResources, int> amounts)
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
