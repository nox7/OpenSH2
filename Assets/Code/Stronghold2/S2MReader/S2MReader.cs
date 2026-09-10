using Assets.Code.Stronghold2.S2MReader.ObjectReaders;
using Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders;
using Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.S2MReader
{
  internal class S2MReader
  {
    private string FilePath { get; set; }
    private S2MFile MapFile { get; set; }
    /// <summary>
    /// Map the object Id to its S2Object
    /// </summary>
    private readonly Dictionary<int, S2Object> Objects = new();
    /// <summary>
    /// Map the object type index (not Id) to the type name.
    /// </summary>
    private readonly Dictionary<int, string> Types = new();

    public S2MReader(string filePath)
    {
      FilePath = filePath;
      MapFile = new S2MFile();
    }

    public S2MFile ReadS2MFile()
    {
      MapFile.SourcePath = Path.GetFullPath(FilePath);
      using var stream = File.OpenRead(FilePath);
      using var reader = new BinaryReader(stream);

      // Read the map header, not compressed
      ReadHeader(reader);

      // Next, decompress the rest of the file (which has three zlib compressed segments)
      MapFile.DecompressedSegments = new ZLibDecompressor().DecompressAll(reader);

      Debug.Log("Number of decompressed segments: " + MapFile.DecompressedSegments.Count);

      if (MapFile.DecompressedSegments.Count != 3)
      {
        throw new InvalidDataException($"Expected 3 decompressed segments, but found {MapFile.DecompressedSegments.Count}.");
      }

      for (int i = 0; i < MapFile.DecompressedSegments.Count; i++)
      {
        // Registration indices restart in every compressed segment.
        Types.Clear();
        using var chunkStream = new MemoryStream(MapFile.DecompressedSegments[i].Bytes, writable: false);
        using var chunkReader = new BinaryReader(chunkStream);
        // All three segments use the same 8-byte segment prefix and object stream.
        chunkReader.ReadInt32();
        chunkReader.ReadInt32();

        var segmentObjects = new List<S2Object>();
        while (true)
        {
          var obj = ReadObjectHeader(chunkReader);
          if (obj == null) break;

          S2Object parsed = ReadObject(chunkReader, obj);
          segmentObjects.Add(parsed);

          if (parsed is RadarMap radarMap) MapFile.RadarMap = radarMap;
          if (i == 1 && parsed is EstateLayer radarEstateLayer) MapFile.RadarEstateLayer = radarEstateLayer;
          if (parsed is HeightLayer heightLayer)
          {
            MapFile.HeightLayer = heightLayer;
            if (MapFile.MapSize == 0) MapFile.MapSize = heightLayer.Width;
          }
        }

        if (i == 0) MapFile.MapHeaderObjects = segmentObjects;
        else if (i == 1) MapFile.RadarMapObjects = segmentObjects;
        else MapFile.S2GameObjects = segmentObjects;

        Debug.Log($"Read {segmentObjects.Count} objects from S2M segment {i}.");
      }

      return MapFile;
    }

    private void ReadHeader(BinaryReader reader)
    {
      int stringOptionCount = reader.ReadInt32();
      if (stringOptionCount < 0 || stringOptionCount > 10000)
        throw new InvalidDataException($"Invalid S2M string option count {stringOptionCount}.");

      var stringOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < stringOptionCount; i++)
      {
        string name = S2MReaderUtils.ReadASCIIString(reader);
        stringOptions[name] = S2MReaderUtils.ReadUtf16String(reader);
      }
      MapFile.StringOptions = stringOptions;
      stringOptions.TryGetValue("author", out string author);
      MapFile.Author = author;

      if (!stringOptions.TryGetValue("type", out string mapTypeString))
        throw new InvalidDataException("S2M header does not contain a 'type' string option.");

      if (mapTypeString == "warcampaign")
      {
        MapFile.MapType = Enums.MapType.WarCampaign;
      }
      else if (mapTypeString == "kingmaker")
      {
        MapFile.MapType = Enums.MapType.Kingmaker;
      }
      else if (mapTypeString == "peacecampaign")
      {
        MapFile.MapType = Enums.MapType.PeaceCampaign;
      }
      else if (mapTypeString == "freebuild")
      {
        MapFile.MapType = Enums.MapType.FreeBuild;
      }
      else
      {
        throw new InvalidDataException($"Unknown S2M map type '{mapTypeString}'.");
      }

      int integerOptionCount = reader.ReadInt32();
      if (integerOptionCount < 0 || integerOptionCount > 10000)
        throw new InvalidDataException($"Invalid S2M integer option count {integerOptionCount}.");

      var integerOptions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < integerOptionCount; i++)
      {
        string name = S2MReaderUtils.ReadASCIIString(reader);
        integerOptions[name] = reader.ReadInt32();
      }
      MapFile.IntegerOptions = integerOptions;

      if (integerOptions.TryGetValue("balanced", out int balanced)) MapFile.Balanced = balanced == 1;
      if (integerOptions.TryGetValue("lastsave", out int lastSave)) MapFile.LastSave = lastSave.ToString();
      if (integerOptions.TryGetValue("mapsize", out int mapSize)) MapFile.MapSize = mapSize;
      if (integerOptions.TryGetValue("maxplayers", out int maxPlayers)) MapFile.MaxPlayers = maxPlayers;
      if (integerOptions.TryGetValue("version", out int version)) MapFile.Version = version;
    }

    /// <summary>
    /// Reads the header of an S2Object and returns that object.
    /// Other functions should read the rest of the object until the object-end trailer marker.
    /// 
    /// Returns null if the object header is the end of segment marker AD DE FF FF
    /// </summary>
    /// <returns></returns>
    private S2Object ReadObjectHeader(BinaryReader reader)
    {
      S2Object obj = new();

      // Read the object Id
      obj.Id = reader.ReadInt32();

      // End of this segment
      if (obj.Id == S2MReaderUtils.EndOfDataSegmentMarker)
      {
        return null;
      }

      // Read the type index
      int typeIndex = reader.ReadInt32();
      obj.TypeIndex = typeIndex;

      // Now, check if that type index has been mapped in a dictionary yet
      Types.TryGetValue(typeIndex, out string typeName);
      if (typeName == null)
      {
        // If it's null, then the next bytes we will read will be the length of the type name
        // followed by the type name itself
        typeName = S2MReaderUtils.ReadASCIIString(reader);

        // Register the typeName
        Types.Add(typeIndex, typeName);

        Debug.Log("Registering unknown type: " + typeName + " with index " + typeIndex);
      }

      // Read the parent type index
      int parentTypeIndex = reader.ReadInt32();

      if (parentTypeIndex != typeIndex)
      {
        // When the parent type index is not equal to the type index, then this object is a child of another object type.

        if (parentTypeIndex != 0)
        {
          Types.TryGetValue(parentTypeIndex, out string parentTypeName);
          if (parentTypeName == null)
          {
            parentTypeName = S2MReaderUtils.ReadASCIIString(reader);
            Debug.Log($"Registering unknown parent type: {parentTypeName} with index {parentTypeIndex} that is parent to {typeName}");
            Types.Add(parentTypeIndex, parentTypeName);
          }
        }
      }
      else
      {
        // When it's the same, read a ... blank something? I've no idea; but there is always a 00 00 00 00 when the parent type index is the same as the type index.
        reader.ReadInt32();
      }


      obj.Type = typeName;

      return obj;
    }

    /// <summary>
    /// Takes an object-header-parsed S2Object and, using its Type, determines which reader to use to read the object.
    /// Once read, adds the object to the Objects dictionary by its Id.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="obj"></param>
    private S2Object ReadObject(BinaryReader reader, S2Object obj)
    {
      // Bound each type-specific reader to exactly one object. Several legacy
      // readers are still incomplete; a failure must not desynchronise the rest
      // of the segment.
      byte[] serializedPayload = ReadSerializedPayload(reader, obj);
      using var payloadStream = new MemoryStream(serializedPayload, writable: false);
      using var payloadReader = new BinaryReader(payloadStream);

      ObjectReader objReader = obj.Type switch
      {
        // MapHeader segment
        "MapHeader" => new MapHeaderReader(obj),
        "EstateMarkers" => new EstateMarkersReader(obj),
        "Scenario" => new ScenarioReader(obj),
        "Mission" => new MissionReader(obj),
        "ScenarioEvent" => new ScenarioEventReader(obj),

        // RadarMap segment and terrain layers
        "RadarMap" => new RadarMapReader(obj),
        "EstateLayer" => new EstateLayerReader(obj),
        "HeightLayer" => new HeightLayerReader(obj),

        // Actions
        "AITroopRetreatAction" => new AITroopRetreatActionReader(obj),
        "AppleBlightAction" => new AppleBlightActionReader(obj),
        "BadWeatherAction" => new BadWeatherActionReader(obj),
        "BearAttackAction" => new BearAttackActionReader(obj),
        "BumperHarvestAction" => new BumperHarvestActionReader(obj),
        "CapResourcesAction" => new CapResourcesActionReader(obj),
        "ControlConstructingBuildingsAction" => new ControlConstructingBuildingsActionReader(obj),
        "ControlLordsAIAction" => new ControlLordsAIActionReader(obj),
        "ControlGateHousesAction" => new ControlGateHousesActionReader(obj),
        "ConvertEstateToVillageAction" => new ConvertEstateToVillageActionReader(obj),
        "CreateCriminalsAction" => new CreateCriminalsActionReader(obj),
        "CrimeRateAction" => new CrimeRateActionReader(obj),
        "CustomActionTakeWilliamsTroops" => new CustomActionTakeWilliamsTroopsReader(obj),
        "DiseaseProductionAction" => new DiseaseProductionActionReader(obj),
        "EnterBriefingAction" => new EnterBriefingActionReader(obj),
        "FireAction" => new FireActionReader(obj),
        "GiveGoldAction" => new GiveGoldActionReader(obj),
        "GiveHonourAction" => new GiveHonourActionReader(obj),
        "GiveResourcesAction" => new GiveResourcesActionReader(obj),
        "GongInfestationAction" => new GongInfestationActionReader(obj),
        "GongProductionAction" => new GongProductionActionReader(obj),
        "HopWeevilAction" => new HopWeevilActionReader(obj),
        "InvasionAction" => new InvasionActionReader(obj),
        "KillAllLordsTroopsAction" => new KillAllLordsTroopsActionReader(obj),
        "KillAllWolvesAction" => new KillAllWolvesActionReader(obj),
        "LimitWeaponProductionAction" => new LimitWeaponProductionActionReader(obj),
        "LoseAction" => new LoseActionReader(obj),
        "LostSheepAction" => new LostSheepActionReader(obj),
        "MadCowDiseaseAction" => new MadCowDiseaseActionReader(obj),
        "MaintainMinimumFoodLevelAction" => new MaintainMinimumFoodLevelActionReader(obj),
        // Stronghold 2 misspells "peasants" as "peasasnts"
        "MaxOutPeasasntsAction" => new MaxOutPeasantsActionReader(obj),
        "MoveLordAction" => new MoveLordActionReader(obj),
        "MoveShipAction" => new MoveShipActionReader(obj),
        "OutlawProductionAction" => new OutlawProductionActionReader(obj),
        "OverlordMessageAction" => new OverlordMessageActionReader(obj),
        "PauseSiegesAction" => new PauseSiegesActionReader(obj),
        "PlagueOfRatsAction" => new PlagueOfRatsActionReader(obj),
        "ProtestAction" => new ProtestActionReader(obj),
        "QuestAction" => new QuestActionReader(obj),
        "QuestFailedAction" => new QuestFailedActionReader(obj),
        "RatInvasionAction" => new RatInvasionActionReader(obj),
        "RatProductionAction" => new RatProductionActionReader(obj),
        "RedirectVillageOutputAction" => new RedirectVillageOutputActionReader(obj),
        "RushTroopsAction" => new RushTroopsActionReader(obj),
        "SetAllBuildingsOnFireAction" => new SetAllBuildingsOnFireActionReader(obj),
        "SetAlliesAction" => new SetAlliesActionReader(obj),
        "SetAvailableTroopTypesAction" => new SetAvailableTroopTypesActionReader(obj),
        "SetCampfirePeasantsAction" => new SetCampfirePeasantsActionReader(obj),
        "SetHonourAction" => new SetHonourActionReader(obj),
        "SetRankAction" => new SetRankActionReader(obj),
        "SetWolvesToDefensiveAction" => new SetWolvesToDefensiveActionReader(obj),
        "StopInvasionsAction" => new StopInvasionsActionReader(obj),
        "SuperAggressiveTroopsAction" => new SuperAggressiveTroopsActionReader(obj),
        "SwineFeverAction" => new SwineFeverActionReader(obj),
        "TakeEnemyCastleAction" => new TakeEnemyCastleActionReader(obj),
        "TimeUntilFinalInvasionAction" => new TimeUntilFinalInvasionActionReader(obj),
        "TurnIndustriesOnOffAction" => new TurnIndustriesOnOffActionReader(obj),
        "VineRotAction" => new VineRotActionReader(obj),
        "WheatDiseaseAction" => new WheatDiseaseActionReader(obj),
        "WinAction" => new WinActionReader(obj),
        "WitchcraftAction" => new WitchcraftActionReader(obj),
        "WolfInvasionAction" => new WolfInvasionActionReader(obj),
        "WolfSpawnRateAction" => new WolfSpawnRateActionReader(obj),

        // Triggers
        "AllYourTroopsDeadTrigger" => new AllYourTroopsDeadTriggerReader(obj),
        "AlwaysTrigger" => new AlwaysTriggerReader(obj),
        "AnyEnemyOnMapTrigger" => new AnyEnemyOnMapTriggerReader(obj),
        "AnyEnemyTroopOnMapTrigger" => new AnyEnemyTroopOnMapTriggerReader(obj),
        "BreachInWallTrigger" => new BreachInWallTriggerReader(obj),
        "CustomTriggerWilliamReachKeep" => new CustomTriggerWilliamReachKeepReader(obj),
        "EnemyGoldAcquiredTrigger" => new EnemyGoldAcquiredTriggerReader(obj),
        "EnemyGoodsAcquiredTrigger" => new EnemyGoodsAcquiredTriggerReader(obj),
        "EnemyHonourAcquiredTrigger" => new EnemyHonourAcquiredTriggerReader(obj),
        "EnemyLordDiesTrigger" => new EnemyLordDiesTriggerReader(obj),
        "GetXTroopsTrigger" => new GetXTroopsTriggerReader(obj),
        "GoldAcquiredTrigger" => new GoldAcquiredTriggerReader(obj),
        "GoodsAcquiredTrigger" => new GoodsAcquiredTriggerReader(obj),
        "HonourAcquiredTrigger" => new HonourAcquiredTriggerReader(obj),
        "LordDamagedTrigger" => new LordDamagedTriggerReader(obj),
        "LordDiesTrigger" => new LordDiesTriggerReader(obj),
        "MultipleLordsDeadTrigger" => new MultipleLordsDeadTriggerReader(obj),
        "NoCriminalsTrigger" => new NoCriminalsTriggerReader(obj),
        "NoEnemyOrInvasionsLeftTrigger" => new NoEnemyOrInvasionsLeftTriggerReader(obj),
        "NoFoodInGranaryTrigger" => new NoFoodInGranaryTriggerReader(obj),
        "NoGongInYourEstatesTrigger" => new NoGongInYourEstatesTriggerReader(obj),
        "NoMessagesPlayingTrigger" => new NoMessagesPlayingTriggerReader(obj),
        "NoPeopleLeftTrigger" => new NoPeopleLeftTriggerReader(obj),
        "NoRatsInYourEstatesTrigger" => new NoRatsInYourEstatesTriggerReader(obj),
        "OtherLordsKillsLordXTrigger" => new OtherLordsKillsLordXTriggerReader(obj),
        "OutlawCampDestroyedTrigger" => new OutlawCampDestroyedTriggerReader(obj),
        "PercentTroopsKilledTrigger" => new PercentTroopsKilledTriggerReader(obj),
        "PlayerKillsLordXTrigger" => new PlayerKillsLordXTriggerReader(obj),
        "PopulationReachedTrigger" => new PopulationReachedTriggerReader(obj),
        "RescueLordTrigger" => new RescueLordTriggerReader(obj),
        "SpecificEnemyLordDiesTrigger" => new SpecificEnemyLordDiesTriggerReader(obj),
        "SpecificLordKillsLordXTrigger" => new SpecificLordKillsLordXTriggerReader(obj),
        // S2Game has many object types that are not decoded yet. Retaining their
        // complete payloads lets the map load and supports incremental research.
        _ => new RawObjectReader(obj)
      };

      S2Object parsedObject;
      try
      {
        parsedObject = objReader.Read(payloadReader);
      }
      catch (Exception exception) when (
        exception is InvalidDataException
        || exception is EndOfStreamException
        || exception is NullReferenceException
        || exception is ArgumentException
        || exception is IndexOutOfRangeException)
      {
        Debug.Log($"Could not decode {obj.Type} ({obj.Id}); preserving its raw payload: {exception.Message}");
        var rawPayload = new byte[serializedPayload.Length - sizeof(int)];
        Buffer.BlockCopy(serializedPayload, 0, rawPayload, 0, rawPayload.Length);
        parsedObject = new RawS2Object { Payload = rawPayload };
      }
      parsedObject.Id = obj.Id;
      parsedObject.TypeIndex = obj.TypeIndex;
      parsedObject.Type = obj.Type;
      Objects[obj.Id] = parsedObject;
      return parsedObject;
    }

    private static byte[] ReadSerializedPayload(BinaryReader reader, S2Object obj)
    {
      var bytes = new List<byte>();
      uint markerWindow = 0;

      while (reader.BaseStream.Position < reader.BaseStream.Length)
      {
        byte value = reader.ReadByte();
        bytes.Add(value);
        markerWindow = (markerWindow >> 8) | ((uint)value << 24);
        if (markerWindow == unchecked((uint)S2MReaderUtils.TrailerMarker)) return bytes.ToArray();
      }

      throw new EndOfStreamException($"Object '{obj.Type}' with Id {obj.Id} has no AF 1E FF FF trailer.");
    }
  }
}
