using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal sealed class SceneryLayerReader : ObjectReader
  {
    public SceneryLayerReader(S2Object obj) : base(obj) { }
    public override S2Object Read(BinaryReader reader) => Decode(Object.Type, ReadPayloadToTrailer(reader));

    internal static SceneryLayer Decode(string type, byte[] payload)
    {
      SceneryKind kind = type switch
      {
        "Grass" => SceneryKind.Grass, "Shrubs" => SceneryKind.Shrubs,
        "Rocks" => SceneryKind.Rocks, "NaturalStone" => SceneryKind.NaturalStone,
        _ => throw new ArgumentException("Unknown scenery layer", nameof(type))
      };
      int size = kind == SceneryKind.Rocks ? 31 : kind == SceneryKind.NaturalStone ? 23 : 27;
      if (payload == null || payload.Length < 8) throw new InvalidDataException($"{type} array header is truncated.");
      int count = BitConverter.ToInt32(payload, 4);
      if (BitConverter.ToInt32(payload, 0) != payload.Length - 4 || count < 0
        || (long)count * size != payload.Length - 8)
        throw new InvalidDataException($"{type} array length/count does not match its {size}-byte records.");
      var instances = new List<SceneryInstance>(count);
      using var stream = new MemoryStream(payload, writable: false);
      using var r = new BinaryReader(stream);
      stream.Position = 8;
      for (int i = 0; i < count; i++)
      {
        byte[] record = r.ReadBytes(size);
        var p = new SceneryInstance { Kind = kind, RawRecord = record };
        p.RawX = BitConverter.ToSingle(record, 0);
        if (kind == SceneryKind.NaturalStone)
        {
          p.RawZ = BitConverter.ToSingle(record, 4);
          p.RotationDegrees = BitConverter.ToSingle(record, 8);
          p.Family = BitConverter.ToUInt16(record, 12);
          p.SampleTerrainHeight = true;
        }
        else
        {
          p.RawY = BitConverter.ToSingle(record, 4);
          p.RawZ = BitConverter.ToSingle(record, 8);
          // Grass offset 12 is an auxiliary integer, not a yaw or atlas index.
          if (kind != SceneryKind.Grass) p.RotationDegrees = BitConverter.ToSingle(record, 12);
          p.AppearanceScale = BitConverter.ToSingle(record, 16);
          int selector = kind == SceneryKind.Rocks ? 28 : 24;
          p.Family = record[selector];
          p.Variant = record[selector + 1];
        }
        p.Enabled = record[size - 1] != 0;
        if (!Finite(p.RawX) || !Finite(p.RawY) || !Finite(p.RawZ) || !Finite(p.RotationDegrees)
          || !Finite(p.AppearanceScale) || p.AppearanceScale <= 0f)
          throw new InvalidDataException($"{type} record {i} has an invalid transform.");
        instances.Add(p);
      }
      return new SceneryLayer { Kind = kind, Instances = instances, RawPayload = payload };
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
  }
}
