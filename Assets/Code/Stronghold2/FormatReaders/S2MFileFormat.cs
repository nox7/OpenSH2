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
  public static class S2MFileFormat
  {
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

      if (record.Name == "HonourAcquiredTrigger")
      {
        return ParseHonourAcquiredTrigger(record);
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

    private static S2MGoodsAcquiredTrigger ParseGoodsAcquiredTrigger(S2MTokenRecord record)
    {
      var trigger = new S2MGoodsAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      PopulateSharedTriggerFields(trigger);

      // GoodsAcquired payload layout currently resolves the goods vector from this marker sequence.
      var markerIndex = record.PayloadInt32.FindIndex(v => v == 180);
      if (markerIndex >= 0)
      {
        trigger.GoodsVectorStartIndex = markerIndex + 2;
      }

      PopulateGoodsAmounts(record.PayloadInt32, trigger.GoodsVectorStartIndex, trigger.GoodsAmounts);

      return trigger;
    }

    private static S2MEnemyGoodsAcquiredTrigger ParseEnemyGoodsAcquiredTrigger(S2MTokenRecord record)
    {
      var trigger = new S2MEnemyGoodsAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      PopulateSharedTriggerFields(trigger);

      // Enemy-goods variant uses the same vector model with a different marker (184 / 0xB8).
      var markerIndex = record.PayloadInt32.FindIndex(v => v == 184);
      if (markerIndex >= 0)
      {
        trigger.GoodsVectorStartIndex = markerIndex + 2;
      }

      PopulateGoodsAmounts(record.PayloadInt32, trigger.GoodsVectorStartIndex, trigger.GoodsAmounts);

      return trigger;
    }

    private static S2MHonourAcquiredTrigger ParseHonourAcquiredTrigger(S2MTokenRecord record)
    {
      var trigger = new S2MHonourAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      PopulateSharedTriggerFields(trigger);
      trigger.RequiredHonourAmount = trigger.TriggerValue;

      return trigger;
    }

    private static S2MNoFoodInGranaryTrigger ParseNoFoodInGranaryTrigger(S2MTokenRecord record)
    {
      var trigger = new S2MNoFoodInGranaryTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      PopulateSharedTriggerFields(trigger);
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
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      PopulateSharedTriggerFields(trigger);
      return trigger;
    }

    private static void PopulateSharedTriggerFields(S2MScenarioTrigger trigger)
    {
      if (trigger.RawPayloadInt32 == null)
      {
        return;
      }

      // Shared trigger prefix observed across multiple trigger families.
      trigger.TriggerCode = TryReadInt(trigger.RawPayloadInt32, 4);
      trigger.TriggerModeCode = TryReadInt(trigger.RawPayloadInt32, 6);
      trigger.TriggerValue = TryReadInt(trigger.RawPayloadInt32, 7);
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

    private static S2MGoldAcquiredTrigger ParseGoldAcquiredTrigger(S2MTokenRecord record)
    {
      var trigger = new S2MGoldAcquiredTrigger
      {
        RecordId = record.Id,
        RecordStart = record.RecordStart,
        RecordName = record.Name,
        Tag = record.Tag,
        BaseName = record.BaseName,
        RawPayloadInt32 = new List<int>(record.PayloadInt32),
      };

      PopulateSharedTriggerFields(trigger);
      trigger.RequiredGoldAmount = trigger.TriggerValue;

      // Fallback for unexpected payload shapes.
      if (trigger.RequiredGoldAmount <= 0)
      {
        var positiveCandidates = record.PayloadInt32.Where(v => v > 0 && v < 1000000).ToList();
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

    private static List<int> ReadInt32List(byte[] bytes)
    {
      var values = new List<int>();
      for (var i = 0; i + 3 < bytes.Length; i += 4)
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
