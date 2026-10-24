using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  /// <summary>Walks the Map reference block and tagged arrays; no map/segment offsets are assumed.</summary>
  public static class WallLayerReader
  {
    public static WallLayer Read(byte[] mapPayload)
    {
      if (mapPayload == null) throw new ArgumentNullException(nameof(mapPayload));
      using var stream = new MemoryStream(mapPayload, false);
      using var reader = new BinaryReader(stream);
      if (stream.Length < 4) throw new InvalidDataException("Map has no reference block.");
      int referenceBytes = reader.ReadInt32();
      Skip(reader, referenceBytes);
      WallLayer result = null;
      while (stream.Position < stream.Length)
      {
        if (stream.Length - stream.Position < 8) throw new InvalidDataException("Truncated Map block header.");
        int tag = reader.ReadInt32();
        int bytes = reader.ReadInt32();
        if (bytes < 0 || bytes > stream.Length - stream.Position)
          throw new InvalidDataException($"Invalid Map tag {tag} byte count {bytes}.");
        if (tag != 14) { Skip(reader, bytes); continue; }
        if (result != null) throw new InvalidDataException("Map repeats wall block 14.");
        const int recordBytes = 24;
        int count = bytes / recordBytes;
        int side = (int)Math.Sqrt(count);
        if (bytes == 0 || bytes % recordBytes != 0 || side * side != count)
          throw new InvalidDataException("Map wall block is not a square grid of 24-byte records.");
        var cells = new WallCell[count];
        for (int i = 0; i < count; i++)
        {
          float h0 = reader.ReadSingle(), h1 = reader.ReadSingle(), h2 = reader.ReadSingle(), h3 = reader.ReadSingle();
          if (!Finite(h0) || !Finite(h1) || !Finite(h2) || !Finite(h3))
            throw new InvalidDataException($"Non-finite wall height at cell {i}.");
          cells[i] = new WallCell(h0, h1, h2, h3, reader.ReadByte(), reader.ReadByte(),
            reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
        result = new WallLayer { Width = side, Height = side, Cells = cells };
      }
      return result; // Older/partial maps can omit the layer.
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void Skip(BinaryReader reader, int count)
    {
      if (count < 0 || count > reader.BaseStream.Length - reader.BaseStream.Position)
        throw new InvalidDataException("Truncated Map block.");
      reader.BaseStream.Position += count;
    }
  }
}
