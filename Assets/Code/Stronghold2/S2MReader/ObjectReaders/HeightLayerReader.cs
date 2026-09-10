using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class HeightLayerReader : ObjectReader
  {
    public HeightLayerReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      for (int i = 0; i < 5; i++) reader.ReadInt32();
      ReadDataPayloadMarker(reader, isEmpty: false);

      int valueCount = reader.ReadInt32();
      if (valueCount <= 0 || valueCount > 16 * 1024 * 1024)
        throw new InvalidDataException($"Invalid HeightLayer primary-plane value count {valueCount}.");

      var heights = new float[valueCount];
      for (int i = 0; i < heights.Length; i++) heights[i] = reader.ReadSingle();

      int additionalBlockTag = reader.ReadInt32();
      int additionalBlockLength = reader.ReadInt32();
      if (additionalBlockTag != 2 || additionalBlockLength <= 0)
        throw new InvalidDataException(
          $"Invalid HeightLayer corner-height block (tag {additionalBlockTag}, length {additionalBlockLength}).");

      int valuesPerPlane = checked(valueCount * HeightLayer.CornersPerCell);
      int bytesPerPlane = checked(sizeof(int) + valuesPerPlane * sizeof(float));
      if (additionalBlockLength % bytesPerPlane != 0)
        throw new InvalidDataException("HeightLayer corner-height block is not a whole number of counted four-corner planes.");

      int planeCount = additionalBlockLength / bytesPerPlane;
      var cornerHeightPlanes = new float[planeCount][];
      for (int plane = 0; plane < planeCount; plane++)
      {
        int cellCount = reader.ReadInt32();
        if (cellCount != valueCount)
          throw new InvalidDataException(
            $"HeightLayer corner-height plane {plane} has {cellCount} cells; expected {valueCount}.");

        var cornerHeights = new float[valuesPerPlane];
        for (int i = 0; i < cornerHeights.Length; i++) cornerHeights[i] = reader.ReadSingle();
        cornerHeightPlanes[plane] = cornerHeights;
      }

      int side = (int)Math.Sqrt(valueCount);
      ReadObjectTrailerMarker(reader);
      return new HeightLayer
      {
        Width = side * side == valueCount ? side : 0,
        Height = side * side == valueCount ? side : 0,
        Heights = heights,
        CornerHeightPlanes = cornerHeightPlanes
      };
    }
  }
}
