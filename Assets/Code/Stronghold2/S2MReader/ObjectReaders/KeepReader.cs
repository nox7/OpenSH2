using Assets.Code.Stronghold2.S2MReader.Resources;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal sealed class KeepReader : ObjectReader
  {
    public KeepReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      byte[] payload = ReadPayloadToTrailer(reader);
      if (BuildingPlacementReader.TryRead(payload, out BuildingPlacement placement))
      {
        var keep = new Keep
        {
          Size = Object.Type switch
          {
            "Keep1" => KeepSize.Wooden,
            "Keep2" => KeepSize.Small,
            "Keep3" => KeepSize.Medium,
            "Keep4" => KeepSize.Large,
            _ => throw new InvalidDataException($"Unsupported keep type {Object.Type}.")
          },
          RawX = placement.RawX, RawY = placement.RawY, RawZ = placement.RawZ,
          RotationDegrees = placement.RotationDegrees, FootprintWidth = placement.FootprintWidth,
          FootprintHeight = placement.FootprintHeight, SavedBuildingReference = placement.SavedBuildingReference,
          RawPayload = payload
        };
        ReadCampfire(keep, payload);
        return keep;
      }
      throw new InvalidDataException("Keep has no supported counted Building transform block.");
    }

    private static void ReadCampfire(Keep keep, byte[] payload)
    {
      for (int offset = 0; offset <= payload.Length - 133; offset++)
      {
        if (System.BitConverter.ToInt32(payload, offset) != S2MReaderUtils.DataPayloadMarker) continue;
        int header = offset + 8;
        if (System.BitConverter.ToInt32(payload, header) == 4
          && System.Text.Encoding.ASCII.GetString(payload, header + 4, 4) == "Keep") header += 8;
        if (header + 125 > payload.Length || System.BitConverter.ToInt32(payload, header) != 0
          || System.BitConverter.ToInt32(payload, header + 4) != 117) continue;
        int position = header + 8 + 101;
        float x = System.BitConverter.ToSingle(payload, position), y = System.BitConverter.ToSingle(payload, position + 4),
          z = System.BitConverter.ToSingle(payload, position + 8);
        if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)
          || float.IsNaN(z) || float.IsInfinity(z)) throw new InvalidDataException("Non-finite keep campfire position.");
        keep.HasCampfirePosition = true; keep.CampfireRawX = x; keep.CampfireRawY = y; keep.CampfireRawZ = z;
        return;
      }
    }
  }
}
