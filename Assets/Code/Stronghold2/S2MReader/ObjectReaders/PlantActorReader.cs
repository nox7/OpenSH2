using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal static class PlantActorReader
  {
    internal static IReadOnlyList<PlantActor> ReadAll(IReadOnlyList<S2Object> objects)
    {
      var result = new List<PlantActor>();
      foreach (S2Object obj in objects)
      {
        if (obj.Type != "AppleTree" && obj.Type != "Hops" && obj.Type != "Wheat") continue;
        if (!(obj is RawS2Object raw) || raw.Payload.Length < 45) continue;
        byte[] b = raw.Payload;
        int expectedLength = obj.Type == "AppleTree" ? 20 : obj.Type == "Hops" ? 16 : 52;
        // The final inherited payload can register its type name. Require its
        // declared extent to end at the object's boundary, not a random marker.
        int body = -1;
        for (int p = b.Length - 16; p >= 45; p--)
        {
          if (BitConverter.ToInt32(b, p) != S2MReaderUtils.DataPayloadMarker) continue;
          int nameLength = BitConverter.ToInt32(b, p + 8);
          if (nameLength < 0 || nameLength > 64) continue;
          int lengthOffset = p + 12 + nameLength;
          if (lengthOffset > b.Length - 4) continue;
          if (BitConverter.ToInt32(b, lengthOffset) != expectedLength
            || lengthOffset + 4 + expectedLength != b.Length) continue;
          body = lengthOffset + 4;
          break;
        }
        if (body < 0) throw new InvalidDataException($"Unrecognized {obj.Type} ({obj.Id}) plant-state payload.");
        var plant = new PlantActor
        {
          Id = obj.Id, Kind = obj.Type == "AppleTree" ? SceneryKind.AppleTree : obj.Type == "Hops" ? SceneryKind.Hops : SceneryKind.Wheat,
          RawX = BitConverter.ToSingle(b, 29), RawY = BitConverter.ToSingle(b, 33), RawZ = BitConverter.ToSingle(b, 37),
          RotationDegrees = BitConverter.ToSingle(b, 41), Stage = BitConverter.ToInt32(b, body + 4), RawPayload = b
        };
        if (plant.Kind == SceneryKind.Wheat)
        {
          plant.WheatOffsets = new float[8];
          for (int i = 0; i < 8; i++) plant.WheatOffsets[i] = BitConverter.ToSingle(b, body + 16 + i * 4);
        }
        result.Add(plant);
      }
      return result;
    }
  }
}
