using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal sealed class ForestReader : ObjectReader
  {
    private const int RecordSize = 65;

    public ForestReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      byte[] payload = ReadPayloadToTrailer(reader);
      if (payload.Length < 8) throw new InvalidDataException("Forest payload is shorter than its array header.");

      using var stream = new MemoryStream(payload, writable: false);
      using var payloadReader = new BinaryReader(stream);
      int declaredLength = payloadReader.ReadInt32();
      int count = payloadReader.ReadInt32();
      // The leading length covers everything after the length field itself.
      if (declaredLength != payload.Length - sizeof(int))
        throw new InvalidDataException(
          $"Forest declares {declaredLength} following bytes but contains {payload.Length - sizeof(int)}.");
      if (count < 0 || count > (payload.Length - 8) / RecordSize)
        throw new InvalidDataException($"Forest contains an invalid placement count {count}.");

      var instances = new List<ForestInstance>(count);
      for (int i = 0; i < count; i++)
      {
        instances.Add(new ForestInstance
        {
          RawX = payloadReader.ReadSingle(),
          RawY = payloadReader.ReadSingle(),
          RawZ = payloadReader.ReadSingle(),
          RotationDegrees = payloadReader.ReadSingle(),
          ScaleX = payloadReader.ReadSingle(),
          ScaleY = payloadReader.ReadSingle(),
          AppearanceScale = payloadReader.ReadSingle(),
          TintRed = payloadReader.ReadSingle(),
          TintGreen = payloadReader.ReadSingle(),
          TintBlue = payloadReader.ReadSingle(),
          RandomValue = payloadReader.ReadInt32(),
          CellX = payloadReader.ReadUInt16(),
          CellZ = payloadReader.ReadUInt16(),
          Family = payloadReader.ReadUInt16(),
          Variant = payloadReader.ReadUInt16(),
          UnknownTail = S2MReaderUtils.ReadExactBytes(payloadReader, 13)
        });
      }

      return new Forest
      {
        Instances = instances,
        TrailingData = S2MReaderUtils.ReadExactBytes(payloadReader, checked((int)(stream.Length - stream.Position))),
        RawPayload = payload
      };
    }
  }
}
