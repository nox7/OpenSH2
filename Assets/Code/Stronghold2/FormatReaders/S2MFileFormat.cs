using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Assets.Code.Stronghold2;

namespace Assets.Code.Stronghold2.FormatReaders
{
  /// <summary>
  /// The format (we think) is in
  /// - Header
  /// - SegmentA
  /// - SegmentB
  /// 
  /// Where the A and B segments are in compressed zlib blocks.
  /// </summary>
  public static class S2MFileFormat
  {
    /// <summary>
    /// I think it is always this length. This is consistent across all the .s2m maps in the game so far.
    /// </summary>
    private const int SegmentACompressedLength = 8194;

    public static S2MFile ParseFile(string filePath)
    {
      if (string.IsNullOrWhiteSpace(filePath))
      {
        throw new ArgumentException("File path is required.", nameof(filePath));
      }

      return ParseBytes(File.ReadAllBytes(filePath), filePath);
    }

    public static S2MFile ParseStream(Stream stream, string sourceName = "<stream>", bool leaveOpen = false)
    {
      if (stream == null)
      {
        throw new ArgumentNullException(nameof(stream));
      }

      using (var ms = new MemoryStream())
      {
        stream.CopyTo(ms);
        if (!leaveOpen)
        {
          stream.Dispose();
        }

        return ParseBytes(ms.ToArray(), sourceName);
      }
    }

    public static S2MFile ParseBytes(byte[] fileBytes, string sourceName = "<bytes>")
    {
      if (fileBytes == null)
      {
        throw new ArgumentNullException(nameof(fileBytes));
      }

      var result = new S2MFile
      {
        Source = sourceName,
        FileSize = fileBytes.Length,
        RawBytes = fileBytes,
      };

      using (var ms = new MemoryStream(fileBytes, writable: false))
      using (var br = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true))
      {
        result.Header = ParseHeader(br);
      }

      result.HeaderEndOffset = result.Header.EndOffset;
      result.SegmentA = ParseSegmentA(fileBytes, result.HeaderEndOffset);

      var segmentAEnd = Math.Min(fileBytes.Length, result.HeaderEndOffset + SegmentACompressedLength);
      result.WorldPayload = ParseWorldPayload(fileBytes, segmentAEnd);

      return result;
    }

    private static S2MHeader ParseHeader(BinaryReader br)
    {
      var header = new S2MHeader();

      var stringOptionCount = br.ReadInt32();
      if (stringOptionCount < 0 || stringOptionCount > 2048)
      {
        throw new InvalidDataException($"Invalid string option count: {stringOptionCount}");
      }

      for (var i = 0; i < stringOptionCount; i++)
      {
        var key = ReadAsciiString(br);
        var value = ReadUtf16String(br);
        header.StringOptions.Add(new S2MStringOption { Name = key, Value = value });
      }

      var intOptionCount = br.ReadInt32();
      if (intOptionCount < 0 || intOptionCount > 2048)
      {
        throw new InvalidDataException($"Invalid int option count: {intOptionCount}");
      }

      for (var i = 0; i < intOptionCount; i++)
      {
        var key = ReadAsciiString(br);
        var value = br.ReadInt32();
        header.IntOptions.Add(new S2MIntOption { Name = key, Value = value });
      }

      header.EndOffset = checked((int)br.BaseStream.Position);
      return header;
    }

    private static S2MSegmentA ParseSegmentA(byte[] fileBytes, int headerEnd)
    {
      var segmentA = new S2MSegmentA
      {
        StartOffset = headerEnd,
      };

      if (headerEnd < 0 || headerEnd >= fileBytes.Length)
      {
        segmentA.ParseIssues.Add("Header end offset is outside file bounds.");
        return segmentA;
      }

      var maxLength = Math.Min(SegmentACompressedLength, fileBytes.Length - headerEnd);
      segmentA.CompressedLength = maxLength;
      segmentA.CompressedBytes = new byte[maxLength];
      Buffer.BlockCopy(fileBytes, headerEnd, segmentA.CompressedBytes, 0, maxLength);

      if (maxLength >= 2)
      {
        segmentA.ZlibHeaderByte0 = segmentA.CompressedBytes[0];
        segmentA.ZlibHeaderByte1 = segmentA.CompressedBytes[1];
      }

      byte[] decompressed;
      string inflateIssue;
      if (!TryInflateZlibBlock(segmentA.CompressedBytes, out decompressed, out inflateIssue))
      {
        segmentA.ParseIssues.Add(inflateIssue ?? "Unable to inflate Segment A.");
        return segmentA;
      }

      segmentA.DecompressedBytes = decompressed;
      segmentA.DecompressedLength = decompressed.Length;
      segmentA.TokenRecords = ParseTokenRecords(decompressed);

      PopulateRecordPayloadRanges(segmentA);
      ParseKnownSegmentABlocks(segmentA);

      return segmentA;
    }

    private static void PopulateRecordPayloadRanges(S2MSegmentA segmentA)
    {
      for (var i = 0; i < segmentA.TokenRecords.Count; i++)
      {
        var record = segmentA.TokenRecords[i];
        var nextRecordStart = i + 1 < segmentA.TokenRecords.Count
          ? segmentA.TokenRecords[i + 1].RecordStart
          : segmentA.DecompressedLength;

        var payloadStart = record.MetadataEnd;
        var payloadLength = Math.Max(0, nextRecordStart - payloadStart);

        record.PayloadStart = payloadStart;
        record.PayloadLength = payloadLength;

        if (payloadLength > 0)
        {
          record.PayloadBytes = new byte[payloadLength];
          Buffer.BlockCopy(segmentA.DecompressedBytes, payloadStart, record.PayloadBytes, 0, payloadLength);
          record.PayloadInt32 = ReadInt32List(record.PayloadBytes);
        }
      }
    }

    private static void ParseKnownSegmentABlocks(S2MSegmentA segmentA)
    {
      var scenarioEventRecords = segmentA.TokenRecords
        .Where(r => r.Name == "ScenarioEvent")
        .OrderBy(r => r.RecordStart)
        .ToList();

      for (var i = 0; i < scenarioEventRecords.Count; i++)
      {
        var record = scenarioEventRecords[i];
        var scenarioEvent = new S2MScenarioEvent
        {
          EventIndex = i,
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordEndExclusive = i + 1 < scenarioEventRecords.Count
            ? scenarioEventRecords[i + 1].RecordStart
            : segmentA.DecompressedBytes.Length,
          RecordName = record.Name,
        };

        if (record.RecordStart + 99 < segmentA.DecompressedBytes.Length)
        {
          scenarioEvent.Month = segmentA.DecompressedBytes[record.RecordStart + 99];
        }

        if (record.RecordStart + 117 < segmentA.DecompressedBytes.Length)
        {
          scenarioEvent.Delay = segmentA.DecompressedBytes[record.RecordStart + 117];
        }

        // Candidate event-level repeat fields observed in controlled wolf repeat diffs:
        // - payload +8  : repeat count candidate
        // - payload +12 : repeat interval candidate
        if (record.PayloadBytes != null && record.PayloadBytes.Length >= 16)
        {
          scenarioEvent.RepeatCountCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 8));
          scenarioEvent.RepeatTimeCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 12));
        }

        segmentA.ScenarioEvents.Add(scenarioEvent);
      }

      foreach (var record in segmentA.TokenRecords)
      {
        if (record.Name == "ScenarioEvent")
        {
          continue;
        }

        var parentEvent = FindParentScenarioEvent(segmentA.ScenarioEvents, record.RecordStart);
        if (parentEvent == null)
        {
          continue;
        }

        var trigger = ParseTrigger(record);
        if (trigger != null)
        {
          parentEvent.Triggers.Add(trigger);
          continue;
        }

        var action = ParseAction(record);
        if (action != null)
        {
          parentEvent.Actions.Add(action);
        }
      }
    }

    private static S2MScenarioEvent FindParentScenarioEvent(List<S2MScenarioEvent> scenarioEvents, int recordStart)
    {
      foreach (var scenarioEvent in scenarioEvents)
      {
        if (recordStart >= scenarioEvent.RecordStart && recordStart < scenarioEvent.RecordEndExclusive)
        {
          return scenarioEvent;
        }
      }

      return null;
    }

    private static S2MScenarioTrigger ParseTrigger(S2MTokenRecord record)
    {
      if (record.Tag != 9 && !record.Name.EndsWith("Trigger", StringComparison.Ordinal))
      {
        return null;
      }

      if (record.Name == "AlwaysTrigger")
      {
        return CreateSimpleTrigger<S2MAlwaysTrigger>(record);
      }

      if (record.Name == "GoodsAcquiredTrigger")
      {
        return ParseGoodsAcquiredTrigger(record);
      }

      if (record.Name == "EnemyGoodsAcquiredTrigger")
      {
        return ParseEnemyGoodsAcquiredTrigger(record);
      }

      if (record.Name == "GoldAcquiredTrigger")
      {
        return ParseGoldAcquiredTrigger(record);
      }

      if (record.Name == "EnemyGoldAcquiredTrigger")
      {
        return ParseEnemyGoldAcquiredTrigger(record);
      }

      if (record.Name == "HonourAcquiredTrigger")
      {
        return ParseHonourAcquiredTrigger(record);
      }

      if (record.Name == "EnemyHonourAcquiredTrigger")
      {
        return ParseEnemyHonourAcquiredTrigger(record);
      }

      if (record.Name == "PopulationReachedTrigger")
      {
        return ParsePopulationReachedTrigger(record);
      }

      if (record.Name == "NoPeopleLeftTrigger")
      {
        return CreateSimpleTrigger<S2MNoPeopleLeftTrigger>(record);
      }

      if (record.Name == "AnyEnemyOnMapTrigger")
      {
        return CreateSimpleTrigger<S2MAnyEnemyOnMapTrigger>(record);
      }

      if (record.Name == "AnyEnemyTroopOnMapTrigger")
      {
        return CreateSimpleTrigger<S2MAnyEnemyTroopOnMapTrigger>(record);
      }

      if (record.Name == "NoEnemyOrInvasionsLeftTrigger")
      {
        return CreateSimpleTrigger<S2MNoEnemyOrInvasionsLeftTrigger>(record);
      }

      if (record.Name == "AllYourTroopsDeadTrigger")
      {
        return CreateSimpleTrigger<S2MAllYourTroopsDeadTrigger>(record);
      }

      if (record.Name == "LordDiesTrigger")
      {
        return CreateSimpleTrigger<S2MLordDiesTrigger>(record);
      }

      if (record.Name == "PercentTroopsKilledTrigger")
      {
        return ParsePercentTroopsKilledTrigger(record);
      }

      if (record.Name == "GetXTroopsTrigger")
      {
        return ParseGetXTroopsTrigger(record);
      }

      if (record.Name == "LordDamagedTrigger")
      {
        return ParseLordDamagedTrigger(record);
      }

      if (record.Name == "EnemyLordDiesTrigger")
      {
        return CreateSimpleTrigger<S2MEnemyLordDiesTrigger>(record);
      }

      if (record.Name == "SpecificEnemyLordDiesTrigger")
      {
        return ParseSpecificEnemyLordDiesTrigger(record);
      }

      if (record.Name == "RescueLordTrigger")
      {
        return ParseRescueLordTrigger(record);
      }

      if (record.Name == "MultipleLordsDeadTrigger")
      {
        return ParseMultipleLordsDeadTrigger(record);
      }

      if (record.Name == "PlayerKillsLordXTrigger")
      {
        return ParsePlayerKillsLordXTrigger(record);
      }

      if (record.Name == "OtherLordsKillsLordXTrigger")
      {
        return ParseOtherLordsKillsLordXTrigger(record);
      }

      if (record.Name == "SpecificLordKillsLordXTrigger")
      {
        return ParseSpecificLordKillsLordXTrigger(record);
      }

      if (record.Name == "OutlawCampDestroyedTrigger")
      {
        return CreateSimpleTrigger<S2MOutlawCampDestroyedTrigger>(record);
      }

      if (record.Name == "BreachInWallTrigger")
      {
        return ParseBreachInWallTrigger(record);
      }

      if (record.Name == "EnemyTroopsOnWallsTrigger")
      {
        return ParseEnemyTroopsOnWallsTrigger(record);
      }

      if (record.Name == "SomeEnemiesCloseToKeepTrigger")
      {
        return ParseSomeEnemiesCloseToKeepTrigger(record);
      }

      if (record.Name == "ManyEnemiesCloseToKeepTrigger")
      {
        return ParseManyEnemiesCloseToKeepTrigger(record);
      }

      if (record.Name == "LiftSiegeTrigger")
      {
        return ParseLiftSiegeTrigger(record);
      }

      if (record.Name == "EnemyNearMarkerTrigger")
      {
        return ParseEnemyNearMarkerTrigger(record);
      }

      if (record.Name == "QuestCompleteTrigger")
      {
        return ParseQuestCompleteTrigger(record);
      }

      if (record.Name == "QuestNotCompleteTrigger")
      {
        return ParseQuestNotCompleteTrigger(record);
      }

      if (record.Name == "SingleQuestCompleteTrigger")
      {
        return ParseSingleQuestCompleteTrigger(record);
      }

      if (record.Name == "NumQuestsCompleteTrigger")
      {
        return ParseNumQuestsCompleteTrigger(record);
      }

      if (record.Name == "QuestFailedTrigger")
      {
        return ParseQuestFailedTrigger(record);
      }

      if (record.Name == "AfterBriefingTrigger")
      {
        return CreateSimpleTrigger<S2MAfterBriefingTrigger>(record);
      }

      if (record.Name == "NoMessagesPlayingTrigger")
      {
        return CreateSimpleTrigger<S2MNoMessagesPlayingTrigger>(record);
      }

      if (record.Name == "ConstructedBuildingCompleteTrigger")
      {
        return CreateSimpleTrigger<S2MConstructedBuildingCompleteTrigger>(record);
      }

      if (record.Name == "ConstructedBuildingPercentCompleteTrigger")
      {
        return ParseConstructedBuildingPercentCompleteTrigger(record);
      }

      if (record.Name == "ControlNumEstatesTrigger")
      {
        return ParseControlNumEstatesTrigger(record);
      }

      if (record.Name == "NoBearsOnMapTrigger")
      {
        return CreateSimpleTrigger<S2MNoBearsOnMapTrigger>(record);
      }

      if (record.Name == "NoWolvesOnMapTrigger")
      {
        return CreateSimpleTrigger<S2MNoWolvesOnMapTrigger>(record);
      }

      if (record.Name == "NoFoodInGranaryTrigger")
      {
        return ParseNoFoodInGranaryTrigger(record);
      }

      if (record.Name == "NoGongInYourEstatesTrigger")
      {
        return CreateSimpleTrigger<S2MNoGongInYourEstatesTrigger>(record);
      }

      if (record.Name == "NoRatsInYourEstatesTrigger")
      {
        return CreateSimpleTrigger<S2MNoRatsInYourEstatesTrigger>(record);
      }

      if (record.Name == "NoCriminalsTrigger")
      {
        return CreateSimpleTrigger<S2MNoCriminalsTrigger>(record);
      }

      return new S2MUnknownTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };
    }

    private static S2MScenarioAction ParseAction(S2MTokenRecord record)
    {
      if (record.Tag != 7 && !record.Name.EndsWith("Action", StringComparison.Ordinal))
      {
        return null;
      }

      if (record.Name == "WinAction")
      {
        return new S2MWinAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "LoseAction")
      {
        return new S2MLoseAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "StopInvasionsAction")
      {
        return ParseStopInvasionsAction(record);
      }

      if (record.Name == "InvasionAction")
      {
        return ParseInvasionAction(record);
      }

      if (record.Name == "BearAttackAction")
      {
        return ParseBearAttackAction(record);
      }

      if (record.Name == "CreateCriminalsAction")
      {
        return ParseCreateCriminalsAction(record);
      }

      if (record.Name == "MaintainMinimumFoodLevelAction")
      {
        return ParseMaintainMinimumFoodLevelAction(record);
      }

      if (record.Name == "PlagueOfRatsAction")
      {
        return ParsePlagueOfRatsAction(record);
      }

      if (record.Name == "RatInvasionAction")
      {
        return ParseRatInvasionAction(record);
      }

      if (record.Name == "SetWolvesToDefensiveAction")
      {
        return new S2MSetWolvesToDefensiveAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "BadWeatherAction")
      {
        return new S2MBadWeatherAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "WheatDiseaseAction")
      {
        return new S2MWheatDiseaseAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "AppleBlightAction")
      {
        return new S2MAppleBlightAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "VineRotAction")
      {
        return new S2MVineRotAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "SwineFeverAction")
      {
        return new S2MSwineFeverAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "MadCowDiseaseAction")
      {
        return new S2MMadCowDiseaseAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "LostSheepAction")
      {
        return new S2MLostSheepAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "HopWeevilAction")
      {
        return new S2MHopWeevilAction
        {
          RecordId = record.Id,
          RecordStart = record.RecordStart,
          RecordName = record.Name,
          Tag = record.Tag,
          BaseName = record.BaseName,
          RawPayloadInt32 = new List<int>(record.PayloadInt32),
        };
      }

      if (record.Name == "WolfInvasionAction")
      {
        return ParseWolfInvasionAction(record);
      }

      return new S2MUnknownAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };
    }

    private static S2MStopInvasionsAction ParseStopInvasionsAction(S2MTokenRecord record)
    {
      var action = new S2MStopInvasionsAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // Current mapping from BinaryCheck-Triggers.s2m (single-action isolation):
      // - byte @ payload offset +29 toggles with editor mode change
      //   (0 = StopRepeatingInvasions, 1 = StopAllInvasions).
      // - int32 @ payload offset +24 is packed (value*256) and tracks selected target lord.
      action.ModeCode = ReadByteAt(record.PayloadBytes, 29);
      action.TargetLordSelector = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));

      if (action.ModeCode == 1)
      {
        action.Mode = S2MStopInvasionsMode.StopAllInvasions;
      }
      else if (action.ModeCode == 0)
      {
        action.Mode = S2MStopInvasionsMode.StopRepeatingInvasions;
      }

      return action;
    }

    private static S2MBearAttackAction ParseBearAttackAction(S2MTokenRecord record)
    {
      var action = new S2MBearAttackAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // Deterministic offsets from BearAttackAction controlled toggles:
      // - +24: target flag color selector
      // - +28: target flag number selector (zero-based)
      // - +32: bear count
      action.TargetFlagColorCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));
      action.TargetFlagNumberCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 28));
      action.BearCount = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 32));

      return action;
    }

    private static S2MCreateCriminalsAction ParseCreateCriminalsAction(S2MTokenRecord record)
    {
      var action = new S2MCreateCriminalsAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // Current deterministic offsets from CreateCriminalsAction controlled toggles:
      // - +20: control/mode code
      // - +24: create-criminals percent value
      action.ModeCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 20));
      action.CreateCriminalsPercent = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));

      return action;
    }

    private static S2MMaintainMinimumFoodLevelAction ParseMaintainMinimumFoodLevelAction(S2MTokenRecord record)
    {
      var action = new S2MMaintainMinimumFoodLevelAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // The probe shows a fixed structural word at +20 and the editable food threshold at +24.
      action.MinimumFoodLevelUnits = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));

      return action;
    }

    private static S2MPlagueOfRatsAction ParsePlagueOfRatsAction(S2MTokenRecord record)
    {
      var action = new S2MPlagueOfRatsAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // The probe shows a fixed structural word at +20 and the editable rat count at +24.
      action.RatsCount = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));

      return action;
    }

    private static S2MRatInvasionAction ParseRatInvasionAction(S2MTokenRecord record)
    {
      var action = new S2MRatInvasionAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // The probe shows a fixed structural word at +20, then the three editable fields:
      // - +24: target flag color selector
      // - +28: target flag number selector (zero-based)
      // - +32: rats count
      action.TargetFlagColorCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));
      action.TargetFlagNumberCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 28));
      action.RatsCount = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 32));

      return action;
    }

    private static S2MWolfInvasionAction ParseWolfInvasionAction(S2MTokenRecord record)
    {
      var action = new S2MWolfInvasionAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // Current deterministic offsets from WolfInvasionAction initial probe:
      // - +20: control/mode code
      // - +24: invasion point flag color selector
      // - +28: invasion point flag number selector (zero-based)
      // - +32: target point flag color selector
      // - +36: target point flag number selector (zero-based)
      // - +40: wolf count
      action.ControlCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 20));
      action.InvasionPointFlagColorCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));
      action.InvasionPointFlagNumberCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 28));
      action.TargetPointFlagColorCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 32));
      action.TargetPointFlagNumberCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 36));
      action.WolfCount = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 40));

      return action;
    }

    private static S2MInvasionAction ParseInvasionAction(S2MTokenRecord record)
    {
      var action = new S2MInvasionAction
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      // Current deterministic offsets from BinaryCheck-Triggers controlled edits.
      action.InvasionPointFlagColorCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 24));

      var invasionPointRaw = ReadInt32At(record.PayloadBytes, 28);
      action.InvasionPointAnyFlagNumber = invasionPointRaw == -256 || invasionPointRaw == -1;
      action.InvasionPointFlagNumber = action.InvasionPointAnyFlagNumber
        ? -1
        : NormalizePackedValue(invasionPointRaw);

      action.DestinationPointTypeCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 32));
      action.DestinationFlagColorCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 36));

      // Keep raw contiguous troop slots visible while full slot->troop identity map is finalized.
      for (var i = 0; i < 29; i++)
      {
        var raw = ReadInt32At(record.PayloadBytes, 40 + (i * 4));
        action.RawTroopSlotCounts.Add(NormalizePackedValue(raw));
      }

      PopulateConfirmedInvasionTroopCounts(action, record.PayloadBytes);

      action.AttackTargetLordSelector = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 176));
      action.ArmyTypeCode = ReadByteAt(record.PayloadBytes, 181);

      // Warning mode is encoded as a 2-bit field split across two bytes in current samples:
      // bit1 = byte 183, bit0 = byte 187.
      var warningBit1 = ReadByteAt(record.PayloadBytes, 183) & 0x01;
      var warningBit0 = ReadByteAt(record.PayloadBytes, 187) & 0x01;
      action.WarningTypeCode = (warningBit1 << 1) | warningBit0;

      action.RepeatCountCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 184));

      action.OwnerLordSelectorCode = NormalizePackedValue(ReadInt32At(record.PayloadBytes, 164));
      action.IncludeLordInArmyCode = ReadByteAt(record.PayloadBytes, 157);
      action.LeaveMapCode = ReadByteAt(record.PayloadBytes, 160);
      action.AttackModeCode0 = ReadByteAt(record.PayloadBytes, 186);
      action.AttackModeCode1 = ReadByteAt(record.PayloadBytes, 187);

      action.WarningType = ParseInvasionWarningType(action.WarningTypeCode);
      action.ArmyType = ParseInvasionArmyType(action.ArmyTypeCode);

      return action;
    }

    private static void PopulateConfirmedInvasionTroopCounts(S2MInvasionAction action, byte[] payloadBytes)
    {
      if (action == null)
      {
        return;
      }

      action.ConfirmedTroopCounts[S2MInvasionTroopType.ArmedPeasant] = NormalizePackedValue(ReadInt32At(payloadBytes, 40));
      action.ConfirmedTroopCounts[S2MInvasionTroopType.Archer] = NormalizePackedValue(ReadInt32At(payloadBytes, 48));
      action.ConfirmedTroopCounts[S2MInvasionTroopType.Knight] = NormalizePackedValue(ReadInt32At(payloadBytes, 68));
      action.ConfirmedTroopCounts[S2MInvasionTroopType.WarriorMonk] = NormalizePackedValue(ReadInt32At(payloadBytes, 76));
      action.ConfirmedTroopCounts[S2MInvasionTroopType.Catapult] = NormalizePackedValue(ReadInt32At(payloadBytes, 148));
      action.ConfirmedTroopCounts[S2MInvasionTroopType.Manglet] = NormalizePackedValue(ReadInt32At(payloadBytes, 152));

      // Latest controlled edit strongly suggests this slot maps to HorseCavalry in this action family.
      action.ConfirmedTroopCounts[S2MInvasionTroopType.HorseCavalry] = NormalizePackedValue(ReadInt32At(payloadBytes, 112));
    }

    private static S2MInvasionWarningType ParseInvasionWarningType(int code)
    {
      if (code == 0)
      {
        return S2MInvasionWarningType.NoWarnings;
      }

      if (code == 1)
      {
        return S2MInvasionWarningType.EarlyWarnings;
      }

      if (code == 2)
      {
        return S2MInvasionWarningType.NormalMessages;
      }

      if (code == 3)
      {
        return S2MInvasionWarningType.FullWarnings;
      }

      return S2MInvasionWarningType.Unknown;
    }

    private static S2MInvasionArmyType ParseInvasionArmyType(int code)
    {
      if (code == 0)
      {
        return S2MInvasionArmyType.MovementArmy;
      }

      if (code == 1)
      {
        return S2MInvasionArmyType.SiegeArmy;
      }

      if (code == 2)
      {
        return S2MInvasionArmyType.DefensiveArmy;
      }

      if (code == 3)
      {
        return S2MInvasionArmyType.AttackingArmy;
      }

      if (code == 4)
      {
        return S2MInvasionArmyType.MovementArmy;
      }

      return S2MInvasionArmyType.Unknown;
    }

    private static S2MGoodsAcquiredTrigger ParseGoodsAcquiredTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MGoodsAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // For current payload shape, vector starts at marker index in aligned trigger words.
      var markerIndex = triggerWords.FindIndex(v => v == 180);
      if (markerIndex >= 0)
      {
        trigger.GoodsVectorStartIndex = markerIndex;
      }

      PopulateGoodsAmounts(triggerWords, trigger.GoodsVectorStartIndex, trigger.GoodsAmounts);

      return trigger;
    }

    private static S2MEnemyGoodsAcquiredTrigger ParseEnemyGoodsAcquiredTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MEnemyGoodsAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
        PayloadWordAlignment = alignment,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // Enemy-goods variant uses marker 184 (0xB8) and an additional 4-int block before goods vector.
      var markerIndex = triggerWords.FindIndex(v => v == 184);
      if (markerIndex < 0)
      {
        // Some payloads appear byte-shifted and expose marker as 0x0000B800 in word view.
        markerIndex = triggerWords.FindIndex(v => v == 47104);
      }

      if (markerIndex >= 0)
      {
        trigger.GoodsVectorStartIndex = markerIndex + 4;
      }

      PopulateGoodsAmounts(triggerWords, trigger.GoodsVectorStartIndex, trigger.GoodsAmounts);
      NormalizeGoodsAmountDictionary(trigger.GoodsAmounts);

      var trailerIndex = triggerWords.FindIndex(v => v == -14766336);
      if (trailerIndex > 0)
      {
        var selector = triggerWords[trailerIndex - 1];
        selector = NormalizePackedValue(selector);
        if (selector >= 0 && selector <= 256)
        {
          trigger.TargetLordSelector = selector;
        }
      }

      return trigger;
    }

    private static S2MHonourAcquiredTrigger ParseHonourAcquiredTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MHonourAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.RequiredHonourAmount = trigger.TriggerValue;

      return trigger;
    }

    private static S2MEnemyGoldAcquiredTrigger ParseEnemyGoldAcquiredTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MEnemyGoldAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.RequiredGoldAmount = trigger.TriggerValue;
      trigger.TargetLordSelector = ExtractTargetLordSelector(triggerWords);

      return trigger;
    }

    private static S2MEnemyHonourAcquiredTrigger ParseEnemyHonourAcquiredTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MEnemyHonourAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.RequiredHonourAmount = trigger.TriggerValue;
      trigger.TargetLordSelector = ExtractTargetLordSelector(triggerWords);

      return trigger;
    }

    private static S2MPopulationReachedTrigger ParsePopulationReachedTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MPopulationReachedTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.RequiredPopulation = trigger.TriggerValue;

      return trigger;
    }

    private static S2MPercentTroopsKilledTrigger ParsePercentTroopsKilledTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MPercentTroopsKilledTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // This trigger family stores selector/percent in dedicated consecutive fields.
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.PercentTroopsKilled = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 8));

      // Present the threshold percentage as the logical trigger value for this type.
      trigger.TriggerValue = trigger.PercentTroopsKilled;

      return trigger;
    }

    private static S2MGetXTroopsTrigger ParseGetXTroopsTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MGetXTroopsTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // GetXTroops uses trigger value for required count; extra words likely encode troop identity.
      trigger.RequiredTroopCount = trigger.TriggerValue;
      trigger.TroopTypeCode = NormalizePackedValue(TryReadInt(triggerWords, 10));
      trigger.TroopClassCode = NormalizePackedValue(TryReadInt(triggerWords, 11));

      return trigger;
    }

    private static S2MLordDamagedTrigger ParseLordDamagedTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MLordDamagedTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // Similar to PercentTroopsKilled shape but uses mode 0x08 and selector at word 7.
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.RequiredDamagePercent = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 8));

      // Expose threshold as logical trigger value for this family.
      trigger.TriggerValue = trigger.RequiredDamagePercent;

      return trigger;
    }

    private static S2MSpecificEnemyLordDiesTrigger ParseSpecificEnemyLordDiesTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MSpecificEnemyLordDiesTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MRescueLordTrigger ParseRescueLordTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MRescueLordTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MMultipleLordsDeadTrigger ParseMultipleLordsDeadTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MMultipleLordsDeadTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // Candidate multi-target encoding in current sample is exposed as a compact 4-byte flag run
      // immediately before the trailer marker (0xAF 0x1E 0xFF 0xFF).
      var trailerOffset = FindTrailerMarkerOffset(record.PayloadBytes);
      if (trailerOffset >= 9)
      {
        var flags = new byte[4];
        Buffer.BlockCopy(record.PayloadBytes, trailerOffset - 9, flags, 0, flags.Length);
        trigger.LordSelectionFlagsCandidate = flags;
        trigger.LordSelectionMaskCandidate =
          flags[0]
          | (flags[1] << 8)
          | (flags[2] << 16)
          | (flags[3] << 24);

        for (var i = 0; i < flags.Length; i++)
        {
          if (flags[i] != 0)
          {
            trigger.SelectedLordSlotsCandidate.Add(i);
          }
        }
      }
      else
      {
        // Fallback candidate reconstruction from packed int fields if trailer probing fails.
        var low = NormalizePackedValue(TryReadInt(triggerWords, 7));
        var high = NormalizePackedValue(TryReadInt(triggerWords, 8));
        trigger.LordSelectionMaskCandidate = low | high;
      }

      return trigger;
    }

    private static int FindTrailerMarkerOffset(byte[] payloadBytes)
    {
      if (payloadBytes == null || payloadBytes.Length < 4)
      {
        return -1;
      }

      for (var i = 0; i <= payloadBytes.Length - 4; i++)
      {
        if (payloadBytes[i] == 0xAF
            && payloadBytes[i + 1] == 0x1E
            && payloadBytes[i + 2] == 0xFF
            && payloadBytes[i + 3] == 0xFF)
        {
          return i;
        }
      }

      return -1;
    }

    private static S2MPlayerKillsLordXTrigger ParsePlayerKillsLordXTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MPlayerKillsLordXTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MOtherLordsKillsLordXTrigger ParseOtherLordsKillsLordXTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MOtherLordsKillsLordXTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MSpecificLordKillsLordXTrigger ParseSpecificLordKillsLordXTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MSpecificLordKillsLordXTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // Current sample has two selector fields at words 7 and 8.
      // Empirically, interpreting word 8 as killer and word 7 as killed matches editor-rendered text.
      trigger.KilledLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.KillerLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 8));

      trigger.TriggerValue = trigger.KilledLordSelector;

      return trigger;
    }

    private static S2MBreachInWallTrigger ParseBreachInWallTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MBreachInWallTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MEnemyTroopsOnWallsTrigger ParseEnemyTroopsOnWallsTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MEnemyTroopsOnWallsTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MSomeEnemiesCloseToKeepTrigger ParseSomeEnemiesCloseToKeepTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MSomeEnemiesCloseToKeepTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MManyEnemiesCloseToKeepTrigger ParseManyEnemiesCloseToKeepTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MManyEnemiesCloseToKeepTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MLiftSiegeTrigger ParseLiftSiegeTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MLiftSiegeTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.TargetLordSelector = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.TargetLordSelector;

      return trigger;
    }

    private static S2MEnemyNearMarkerTrigger ParseEnemyNearMarkerTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MEnemyNearMarkerTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // Current sample layout: mode=word6, radius=word7, then marker tuple (color type, flag number).
      trigger.Radius = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.FlagColorType = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 8));
      trigger.FlagNumber = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 9));
      trigger.TriggerValue = trigger.Radius;

      return trigger;
    }

    private static S2MQuestCompleteTrigger ParseQuestCompleteTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MQuestCompleteTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      var trailerOffset = FindTrailerMarkerOffset(record.PayloadBytes);
      if (trailerOffset >= 3)
      {
        // Three quest status bytes are packed immediately before the trailer marker.
        trigger.QuestACompleted = record.PayloadBytes[trailerOffset - 3] != 0;
        trigger.QuestBCompleted = record.PayloadBytes[trailerOffset - 2] != 0;
        trigger.QuestCCompleted = record.PayloadBytes[trailerOffset - 1] != 0;
      }

      trigger.CompletedQuestCount =
        (trigger.QuestACompleted ? 1 : 0)
        + (trigger.QuestBCompleted ? 1 : 0)
        + (trigger.QuestCCompleted ? 1 : 0);

      return trigger;
    }

    private static S2MQuestNotCompleteTrigger ParseQuestNotCompleteTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MQuestNotCompleteTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.QuestIndex = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.QuestIndex;

      return trigger;
    }

    private static S2MSingleQuestCompleteTrigger ParseSingleQuestCompleteTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MSingleQuestCompleteTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.QuestIndex = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.QuestIndex;

      return trigger;
    }

    private static S2MNumQuestsCompleteTrigger ParseNumQuestsCompleteTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MNumQuestsCompleteTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.RequiredQuestCount = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.RequiredQuestCount;

      return trigger;
    }

    private static S2MQuestFailedTrigger ParseQuestFailedTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MQuestFailedTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.QuestIndex = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.QuestIndex;

      return trigger;
    }

    private static S2MConstructedBuildingPercentCompleteTrigger ParseConstructedBuildingPercentCompleteTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MConstructedBuildingPercentCompleteTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);

      // Current sample stores percent threshold in word 8 (e.g., 21 => 0x1500).
      trigger.RequiredPercent = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 8));
      trigger.TriggerValue = trigger.RequiredPercent;

      return trigger;
    }

    private static S2MControlNumEstatesTrigger ParseControlNumEstatesTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MControlNumEstatesTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.RequiredEstateCount = NormalizePercentTroopsPackedField(TryReadInt(triggerWords, 7));
      trigger.TriggerValue = trigger.RequiredEstateCount;

      return trigger;
    }

    private static int NormalizePercentTroopsPackedField(int value)
    {
      // Seen patterns: 0x??00 (e.g., 18 => 0x1200) and 0x??FF (e.g., 18 => 0x12FF).
      if ((value & 0xFF) == 0x00)
      {
        return value / 256;
      }

      if ((value & 0xFF) == 0xFF)
      {
        return value >> 8;
      }

      return value;
    }

    private static S2MNoFoodInGranaryTrigger ParseNoFoodInGranaryTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MNoFoodInGranaryTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.FlagColorCode = trigger.TriggerModeCode;
      trigger.FlagSelectionValue = trigger.TriggerValue;

      return trigger;
    }

    private static TTrigger CreateSimpleTrigger<TTrigger>(S2MTokenRecord record)
      where TTrigger : S2MScenarioTrigger, new()
    {
      var trigger = new TTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = GetBestAlignedTriggerWords(record.PayloadBytes, out _),
      };

      PopulateSharedTriggerFields(trigger, trigger.RawPayloadInt32);
      return trigger;
    }

    private static void PopulateSharedTriggerFields(S2MScenarioTrigger trigger, List<int> triggerWords)
    {
      if (triggerWords == null)
      {
        return;
      }

      // Shared trigger words observed across multiple trigger families.
      trigger.TriggerCode = TryReadInt(triggerWords, 4);
      trigger.TriggerModeCode = TryReadInt(triggerWords, 5);
      trigger.TriggerValue = TryReadInt(triggerWords, 7);

      trigger.TriggerCode = NormalizeTriggerCode(trigger.TriggerCode);
      trigger.TriggerModeCode = NormalizePackedValue(trigger.TriggerModeCode);
      trigger.TriggerValue = NormalizePackedValue(trigger.TriggerValue);
    }

    private static int ExtractTargetLordSelector(List<int> triggerWords)
    {
      if (triggerWords == null || triggerWords.Count == 0)
      {
        return 0;
      }

      var trailerIndex = triggerWords.FindIndex(v => v == -14766336);
      if (trailerIndex > 0)
      {
        var selector = NormalizePackedValue(triggerWords[trailerIndex - 1]);
        if (selector >= 0 && selector <= 256)
        {
          return selector;
        }
      }

      return 0;
    }

    private static void NormalizeGoodsAmountDictionary(Dictionary<GoodsAcquiredEnum, int> goodsAmounts)
    {
      if (goodsAmounts == null || goodsAmounts.Count == 0)
      {
        return;
      }

      var keys = goodsAmounts.Keys.ToList();
      foreach (var key in keys)
      {
        goodsAmounts[key] = NormalizePackedValue(goodsAmounts[key]);
      }
    }

    private static int NormalizePackedValue(int value)
    {
      if (value > 255 && value % 256 == 0)
      {
        return value / 256;
      }

      return value;
    }

    private static int NormalizeTriggerCode(int value)
    {
      // For many trigger payloads, code is packed as 0x0000CCFF where CC is the logical code.
      if ((value & 0xFF) == 0xFF && value > 0xFF)
      {
        return (value >> 8) & 0xFF;
      }

      return NormalizePackedValue(value);
    }

    private static List<int> GetBestAlignedTriggerWords(byte[] payloadBytes, out int alignment)
    {
      alignment = 0;
      if (payloadBytes == null || payloadBytes.Length < 16)
      {
        return new List<int>();
      }

      var bestScore = int.MinValue;
      List<int> best = null;

      for (var a = 0; a < 4; a++)
      {
        var words = ReadInt32List(payloadBytes, a);
        var score = 0;

        if (words.Count > 0 && words[0] == 0) score += 2;
        if (words.Count > 1 && words[1] == 1) score += 2;
        if (words.Count > 2 && words[2] == 1) score += 2;
        if (words.Count > 3 && words[3] == -4086528) score += 3;
        if (words.Count > 4 && words[4] >= 0 && words[4] <= 512) score += 1;

        if (score > bestScore)
        {
          bestScore = score;
          best = words;
          alignment = a;
        }
      }

      return best ?? new List<int>();
    }

    private static void PopulateGoodsAmounts(List<int> payloadInt32, int goodsVectorStartIndex, Dictionary<GoodsAcquiredEnum, int> destination)
    {
      if (goodsVectorStartIndex < 0)
      {
        return;
      }

      var goods = Enum.GetValues(typeof(GoodsAcquiredEnum)).Cast<GoodsAcquiredEnum>().ToArray();

      // Two leading unknown slots are observed before visible goods list starts.
      const int leadingUnknownSlots = 2;
      for (var i = 0; i < goods.Length; i++)
      {
        var vectorIndex = goodsVectorStartIndex + leadingUnknownSlots + i;
        if (vectorIndex >= 0 && vectorIndex < payloadInt32.Count)
        {
          destination[goods[i]] = payloadInt32[vectorIndex];
        }
      }
    }

    private static int TryReadInt(List<int> values, int index)
    {
      if (index < 0 || index >= values.Count)
      {
        return 0;
      }

      return values[index];
    }

    private static int ReadInt32At(byte[] bytes, int offset)
    {
      if (bytes == null || offset < 0 || offset + 4 > bytes.Length)
      {
        return 0;
      }

      return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static int ReadByteAt(byte[] bytes, int offset)
    {
      if (bytes == null || offset < 0 || offset >= bytes.Length)
      {
        return 0;
      }

      return bytes[offset];
    }

    private static S2MGoldAcquiredTrigger ParseGoldAcquiredTrigger(S2MTokenRecord record)
    {
      int alignment;
      var triggerWords = GetBestAlignedTriggerWords(record.PayloadBytes, out alignment);

      var trigger = new S2MGoldAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = triggerWords,
      };

      PopulateSharedTriggerFields(trigger, triggerWords);
      trigger.RequiredGoldAmount = trigger.TriggerValue;

      // Fallback for unexpected payload shapes.
      if (trigger.RequiredGoldAmount <= 0)
      {
        var positiveCandidates = triggerWords.Where(v => v > 0 && v < 1000000).ToList();
        if (positiveCandidates.Count > 0)
        {
          trigger.RequiredGoldAmount = positiveCandidates.Max();
        }
      }

      return trigger;
    }

    private static S2MWorldPayload ParseWorldPayload(byte[] fileBytes, int scanStart)
    {
      var world = new S2MWorldPayload
      {
        ScanStartOffset = Math.Max(0, scanStart),
      };

      byte[] bestDecompressed = Array.Empty<byte>();
      S2MZlibCandidate bestCandidate = null;

      for (var i = world.ScanStartOffset; i + 1 < fileBytes.Length; i++)
      {
        if (!LooksLikeZlibHeader(fileBytes[i], fileBytes[i + 1]))
        {
          continue;
        }

        byte[] decompressed;
        if (!TryInflateFromRemainder(fileBytes, i, out decompressed))
        {
          continue;
        }

        var candidate = new S2MZlibCandidate
        {
          Offset = i,
          ZlibHeader = new[] { fileBytes[i], fileBytes[i + 1] },
          DecompressedLength = decompressed.Length,
          AnchorHits = CountAnchorHits(decompressed),
        };

        world.ZlibCandidates.Add(candidate);

        if (bestCandidate == null ||
            candidate.DecompressedLength > bestCandidate.DecompressedLength ||
            (candidate.DecompressedLength == bestCandidate.DecompressedLength && candidate.AnchorHits > bestCandidate.AnchorHits))
        {
          bestCandidate = candidate;
          bestDecompressed = decompressed;
        }
      }

      if (bestCandidate != null)
      {
        world.DominantCandidate = bestCandidate;
        world.DominantDecompressedBytes = bestDecompressed;
        world.HeightLayer = ParseHeightLayer(bestDecompressed);
      }

      return world;
    }

    private static S2MHeightLayerBlock ParseHeightLayer(byte[] payload)
    {
      var heightLayer = new S2MHeightLayerBlock();
      if (payload == null || payload.Length == 0)
      {
        heightLayer.ParseIssue = "No dominant payload bytes available.";
        return heightLayer;
      }

      var labelBytes = Encoding.ASCII.GetBytes("HeightLayer");
      var labelOffset = IndexOf(payload, labelBytes);
      if (labelOffset < 0)
      {
        heightLayer.ParseIssue = "HeightLayer label was not found in dominant payload.";
        return heightLayer;
      }

      heightLayer.Found = true;
      heightLayer.LabelOffset = labelOffset;

      var searchStart = Math.Max(0, labelOffset);
      var searchEnd = Math.Min(payload.Length - 8, labelOffset + 1024);
      for (var i = searchStart; i <= searchEnd; i++)
      {
        var rowByteWidth = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(i, 4));
        var rowCount = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(i + 4, 4));
        if (rowByteWidth <= 0 || rowCount <= 0)
        {
          continue;
        }

        if (rowByteWidth % 4 != 0)
        {
          continue;
        }

        // Known high-confidence pair in notes is usually 1024 x 256.
        if (rowByteWidth > 8192 || rowCount > 2048)
        {
          continue;
        }

        var dataOffset = i + 8;
        var byteLength = rowByteWidth * rowCount;
        if (dataOffset + byteLength > payload.Length)
        {
          continue;
        }

        heightLayer.DimensionsOffset = i;
        heightLayer.DataOffset = dataOffset;
        heightLayer.RowByteWidth = rowByteWidth;
        heightLayer.RowCount = rowCount;
        heightLayer.TileWidth = rowByteWidth / 4;
        heightLayer.TileHeight = rowCount;
        heightLayer.Data = new byte[byteLength];
        Buffer.BlockCopy(payload, dataOffset, heightLayer.Data, 0, byteLength);
        return heightLayer;
      }

      heightLayer.ParseIssue = "HeightLayer label found but dimensions pair could not be resolved.";
      return heightLayer;
    }

    private static List<S2MTokenRecord> ParseTokenRecords(byte[] segmentABytes)
    {
      var records = new List<S2MTokenRecord>();

      for (var i = 0; i + 20 < segmentABytes.Length; i++)
      {
        var id = BinaryPrimitives.ReadInt32LittleEndian(segmentABytes.AsSpan(i, 4));
        if (id < 0 || id > 500000)
        {
          continue;
        }

        var nameLen = BinaryPrimitives.ReadInt32LittleEndian(segmentABytes.AsSpan(i + 4, 4));
        if (nameLen <= 0 || nameLen > 128)
        {
          continue;
        }

        var nameStart = i + 8;
        var nameEnd = nameStart + nameLen;
        if (nameEnd + 8 > segmentABytes.Length)
        {
          continue;
        }

        if (!IsReasonableAscii(segmentABytes, nameStart, nameLen))
        {
          continue;
        }

        var name = Encoding.ASCII.GetString(segmentABytes, nameStart, nameLen);
        var tag = BinaryPrimitives.ReadInt32LittleEndian(segmentABytes.AsSpan(nameEnd, 4));
        if (tag < 0 || tag > 128)
        {
          continue;
        }

        var baseNameLen = BinaryPrimitives.ReadInt32LittleEndian(segmentABytes.AsSpan(nameEnd + 4, 4));
        if (baseNameLen < 0 || baseNameLen > 128)
        {
          continue;
        }

        var baseNameStart = nameEnd + 8;
        var metadataEnd = baseNameStart + baseNameLen;
        if (metadataEnd > segmentABytes.Length)
        {
          continue;
        }

        if (baseNameLen > 0 && !IsReasonableAscii(segmentABytes, baseNameStart, baseNameLen))
        {
          continue;
        }

        var baseName = baseNameLen > 0
          ? Encoding.ASCII.GetString(segmentABytes, baseNameStart, baseNameLen)
          : string.Empty;

        records.Add(new S2MTokenRecord
        {
          RecordStart = i,
          Id = id,
          Name = name,
          Tag = tag,
          BaseNameLength = baseNameLen,
          BaseName = baseName,
          MetadataEnd = metadataEnd,
        });
      }

      return records
        .GroupBy(r => r.RecordStart)
        .Select(g => g.First())
        .OrderBy(r => r.RecordStart)
        .ToList();
    }

    private static List<int> ReadInt32List(byte[] bytes, int startOffset = 0)
    {
      var values = new List<int>();
      for (var i = Math.Max(0, startOffset); i + 3 < bytes.Length; i += 4)
      {
        values.Add(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i, 4)));
      }

      return values;
    }

    private static bool TryInflateZlibBlock(byte[] zlibBytes, out byte[] decompressed, out string issue)
    {
      decompressed = Array.Empty<byte>();
      issue = string.Empty;

      if (zlibBytes.Length < 6)
      {
        issue = "Zlib block is too small.";
        return false;
      }

      if (!LooksLikeZlibHeader(zlibBytes[0], zlibBytes[1]))
      {
        issue = "Zlib header marker not found.";
        return false;
      }

      try
      {
        using (var rawDeflate = new MemoryStream(zlibBytes, 2, zlibBytes.Length - 6, writable: false))
        using (var deflate = new DeflateStream(rawDeflate, CompressionMode.Decompress))
        using (var outMs = new MemoryStream())
        {
          deflate.CopyTo(outMs);
          decompressed = outMs.ToArray();
          return true;
        }
      }
      catch (Exception ex)
      {
        issue = $"Segment inflate failed: {ex.Message}";
        return false;
      }
    }

    private static bool TryInflateFromRemainder(byte[] fileBytes, int zlibStartOffset, out byte[] decompressed)
    {
      decompressed = Array.Empty<byte>();
      if (zlibStartOffset + 2 >= fileBytes.Length)
      {
        return false;
      }

      try
      {
        using (var compressed = new MemoryStream(fileBytes, zlibStartOffset + 2, fileBytes.Length - (zlibStartOffset + 2), writable: false))
        using (var deflate = new DeflateStream(compressed, CompressionMode.Decompress))
        using (var outMs = new MemoryStream())
        {
          var buffer = new byte[8192];
          while (true)
          {
            int read;
            try
            {
              read = deflate.Read(buffer, 0, buffer.Length);
            }
            catch
            {
              break;
            }

            if (read <= 0)
            {
              break;
            }

            outMs.Write(buffer, 0, read);

            // Guardrail: avoid allocating extreme buffers while probing candidates.
            if (outMs.Length > 20 * 1024 * 1024)
            {
              break;
            }
          }

          decompressed = outMs.ToArray();
          return decompressed.Length > 0;
        }
      }
      catch
      {
        return false;
      }
    }

    private static bool LooksLikeZlibHeader(byte b0, byte b1)
    {
      if (b0 != 0x78)
      {
        return false;
      }

      return b1 == 0x01 || b1 == 0x5E || b1 == 0x9C || b1 == 0xDA;
    }

    private static int CountAnchorHits(byte[] bytes)
    {
      var ascii = Encoding.ASCII.GetString(bytes);
      var anchors = new[]
      {
        "S2Game",
        "Simulation",
        "Floaters",
        "Landscape",
        "HeightLayer",
      };

      var hits = 0;
      foreach (var anchor in anchors)
      {
        if (ascii.IndexOf(anchor, StringComparison.Ordinal) >= 0)
        {
          hits++;
        }
      }

      return hits;
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
      if (data == null || pattern == null || data.Length == 0 || pattern.Length == 0 || pattern.Length > data.Length)
      {
        return -1;
      }

      for (var i = 0; i <= data.Length - pattern.Length; i++)
      {
        var matched = true;
        for (var j = 0; j < pattern.Length; j++)
        {
          if (data[i + j] != pattern[j])
          {
            matched = false;
            break;
          }
        }

        if (matched)
        {
          return i;
        }
      }

      return -1;
    }

    private static string ReadAsciiString(BinaryReader br)
    {
      var length = br.ReadInt32();
      if (length < 0 || length > 1024 * 1024)
      {
        throw new InvalidDataException($"Invalid ASCII string length: {length}");
      }

      var data = br.ReadBytes(length);
      if (data.Length != length)
      {
        throw new EndOfStreamException("Unexpected EOF while reading ASCII string.");
      }

      return Encoding.ASCII.GetString(data);
    }

    private static string ReadUtf16String(BinaryReader br)
    {
      var length = br.ReadInt32();
      if (length < 0 || length > 1024 * 1024)
      {
        throw new InvalidDataException($"Invalid UTF-16 string length: {length}");
      }

      var byteCount = checked(length * 2);
      var data = br.ReadBytes(byteCount);
      if (data.Length != byteCount)
      {
        throw new EndOfStreamException("Unexpected EOF while reading UTF-16 string.");
      }

      return Encoding.Unicode.GetString(data);
    }

    private static bool IsReasonableAscii(byte[] bytes, int start, int length)
    {
      for (var i = 0; i < length; i++)
      {
        var c = bytes[start + i];
        var isUpper = c >= (byte)'A' && c <= (byte)'Z';
        var isLower = c >= (byte)'a' && c <= (byte)'z';
        var isDigit = c >= (byte)'0' && c <= (byte)'9';
        var isUnderscore = c == (byte)'_';
        if (!(isUpper || isLower || isDigit || isUnderscore))
        {
          return false;
        }
      }

      return true;
    }
  }
}
