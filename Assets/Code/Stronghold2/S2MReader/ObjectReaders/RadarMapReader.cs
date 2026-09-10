using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class RadarMapReader : ObjectReader
  {
    public RadarMapReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      int byteCount = reader.ReadInt32();
      if (byteCount < 0) throw new InvalidDataException("RadarMap has a negative pixel byte count.");

      byte[] pixels = S2MReaderUtils.ReadExactBytes(reader, byteCount);
      int pixelCount = byteCount / 4;
      int side = (int)Math.Sqrt(pixelCount);

      var result = new RadarMap
      {
        Width = side * side == pixelCount ? side : 0,
        Height = side * side == pixelCount ? side : 0,
        PixelBytes = pixels,
        UnknownAfterPixels1 = reader.ReadInt32(),
        UnknownAfterPixels2 = reader.ReadInt32(),
        EstateLayerObjectId = reader.ReadInt32()
      };
      ReadObjectTrailerMarker(reader);
      return result;
    }
  }
}
