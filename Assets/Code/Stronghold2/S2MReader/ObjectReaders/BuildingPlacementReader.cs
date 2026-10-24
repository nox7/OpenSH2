using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal static class BuildingPlacementReader
  {
    public static bool TryRead(byte[] payload, out BuildingPlacement placement)
    {
      placement = null;
      if (payload == null) return false;
      for (int offset = 0; offset <= payload.Length - 68; offset++)
      {
        if (BitConverter.ToInt32(payload, offset) != S2MReaderUtils.DataPayloadMarker) continue;
        int header = offset + 8;
        bool named = BitConverter.ToInt32(payload, header) == 8
          && Encoding.ASCII.GetString(payload, header + 4, 8) == "Building";
        if (named) header += 12;
        // Only inspect the first inherited payload, not matching byte sequences
        // inside unrelated objects or their later nested data blocks.
        if (header > payload.Length - 60 || BitConverter.ToInt32(payload, header) != 1
          || BitConverter.ToInt32(payload, header + 4) != 52) return false;
        int data = header + 8;
        float x = BitConverter.ToSingle(payload, data + 24);
        float y = BitConverter.ToSingle(payload, data + 28);
        float z = BitConverter.ToSingle(payload, data + 32);
        float yaw = BitConverter.ToSingle(payload, data + 36);
        int width = BitConverter.ToInt32(payload, data + 40);
        int height = BitConverter.ToInt32(payload, data + 44);
        if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) || !IsFinite(yaw)
          || width <= 0 || height <= 0) return false;
        placement = new BuildingPlacement
        {
          RawX = x, RawY = y, RawZ = z, RotationDegrees = yaw,
          FootprintWidth = width, FootprintHeight = height,
          // These two uint16 fields are unaligned in the packed Building body.
          // Concrete association records store them in the opposite word order.
          SavedBuildingReference = ((uint)BitConverter.ToUInt16(payload, data + 19) << 16)
            | BitConverter.ToUInt16(payload, data + 21),
          BuildingTypeIndex = BitConverter.ToInt32(payload, offset + 4), RegistersBuildingType = named
        };
        return true;
      }
      return false;
    }

    public static IReadOnlyList<BuildingPlacement> ReadAll(IReadOnlyList<S2Object> objects)
    {
      var candidates = new List<BuildingPlacement>();
      var confirmedTypes = new HashSet<int>();
      foreach (S2Object obj in objects)
      {
        byte[] payload = obj is Keep keep ? keep.RawPayload : (obj as RawS2Object)?.Payload;
        if (!TryRead(payload, out BuildingPlacement placement)) continue;
        placement.Id = obj.Id;
        placement.Type = obj.Type;
        placement.TypeIndex = obj.TypeIndex;
        placement.LinkedBuildingReference = ReadLinkedReference(payload, obj.Type, obj.TypeIndex);
        if (obj is Keep || placement.RegistersBuildingType) confirmedTypes.Add(placement.BuildingTypeIndex);
        candidates.Add(placement);
      }
      // Type indices restart per segment/map. Require an actual Building name
      // registration or known keep; a 52-byte block alone is not identification.
      List<BuildingPlacement> placements = candidates.FindAll(p => confirmedTypes.Contains(p.BuildingTypeIndex));
      var references = new Dictionary<uint, BuildingPlacement>();
      var ambiguous = new HashSet<uint>();
      foreach (BuildingPlacement placement in placements)
      {
        if (references.ContainsKey(placement.SavedBuildingReference)) ambiguous.Add(placement.SavedBuildingReference);
        else references.Add(placement.SavedBuildingReference, placement);
      }
      foreach (BuildingPlacement placement in placements)
      {
        uint link = placement.LinkedBuildingReference;
        if (link == 0 || ambiguous.Contains(link) || ambiguous.Contains(placement.SavedBuildingReference)
          || !references.TryGetValue(link, out BuildingPlacement partner)
          || partner.LinkedBuildingReference != placement.SavedBuildingReference) continue;
        if (IsPair(placement.Type, partner.Type)) placement.LinkedBuildingId = partner.Id;
      }
      return placements;
    }

    private static bool IsPair(string first, string second) =>
      (first == "Kitchen" && second == "ServantsQuarters")
      || (first == "ServantsQuarters" && second == "Kitchen")
      || (first == "CourtHouse" && second == "Dungeon")
      || (first == "Dungeon" && second == "CourtHouse");

    private static uint ReadLinkedReference(byte[] payload, string type, int typeIndex)
    {
      int size, field;
      switch (type)
      {
        case "Kitchen": size = 66; field = 62; break;
        case "ServantsQuarters": size = 20; field = 16; break;
        case "CourtHouse": size = 26; field = 18; break;
        case "Dungeon":
          uint shortReference = ReadBodyReference(8, 4);
          return shortReference != 0 ? shortReference : ReadBodyReference(12, 4);
        default: return 0;
      }
      return ReadBodyReference(size, field);

      uint ReadBodyReference(int bodySize, int fieldOffset)
      {
        int marker = payload.Length - bodySize - 16;
        if (marker < 0 || BitConverter.ToInt32(payload, marker) != S2MReaderUtils.DataPayloadMarker
          || BitConverter.ToInt32(payload, marker + 4) != typeIndex
          || BitConverter.ToInt32(payload, marker + 8) != 0
          || BitConverter.ToInt32(payload, marker + 12) != bodySize) return 0;
        return BitConverter.ToUInt32(payload, marker + 16 + fieldOffset);
      }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
  }
}
