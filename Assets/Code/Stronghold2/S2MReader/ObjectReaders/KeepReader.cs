using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal sealed class KeepReader : ObjectReader
  {
    public KeepReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      byte[] payload = ReadPayloadToTrailer(reader);
      // The first Building block may register its name inline. Type indices are
      // segment-local. Locate its counted data, not the preceding actor position.
      for (int offset = 0; offset <= payload.Length - 68; offset++)
      {
        if (BitConverter.ToInt32(payload, offset) != S2MReaderUtils.DataPayloadMarker) continue;
        int header = offset + 8;
        if (BitConverter.ToInt32(payload, header) == 8
          && Encoding.ASCII.GetString(payload, header + 4, 8) == "Building") header += 12;
        if (header > payload.Length - 60
          || BitConverter.ToInt32(payload, header) != 1
          || BitConverter.ToInt32(payload, header + 4) != 52) continue;
        int data = header + 8;

        float x = BitConverter.ToSingle(payload, data + 24);
        float y = BitConverter.ToSingle(payload, data + 28);
        float z = BitConverter.ToSingle(payload, data + 32);
        float yaw = BitConverter.ToSingle(payload, data + 36);
        int width = BitConverter.ToInt32(payload, data + 40);
        int height = BitConverter.ToInt32(payload, data + 44);
        if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) || !IsFinite(yaw))
          throw new InvalidDataException("Keep Building transform contains non-finite values.");
        if (width <= 0 || height <= 0)
          throw new InvalidDataException("Keep Building footprint must have positive dimensions.");
        return new Keep
        {
          Size = Object.Type switch
          {
            "Keep1" => KeepSize.Wooden,
            "Keep2" => KeepSize.Small,
            "Keep3" => KeepSize.Medium,
            "Keep4" => KeepSize.Large,
            _ => throw new InvalidDataException($"Unsupported keep type {Object.Type}.")
          },
          RawX = x, RawY = y, RawZ = z, RotationDegrees = yaw,
          FootprintWidth = width, FootprintHeight = height, RawPayload = payload
        };
      }
      throw new InvalidDataException("Keep has no supported counted Building transform block.");
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
  }
}
