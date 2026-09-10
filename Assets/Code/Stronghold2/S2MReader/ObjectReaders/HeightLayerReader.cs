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
      int additionalCellCount = reader.ReadInt32();
      if (additionalBlockTag != 2 || additionalCellCount != valueCount || additionalBlockLength < sizeof(int))
        throw new InvalidDataException(
          $"Invalid HeightLayer additional block (tag {additionalBlockTag}, length {additionalBlockLength}, count {additionalCellCount}).");

      // The length includes the 4-byte cell count and an 8-byte footer. The
      // tested format stores twelve float32 samples per cell between them.
      const int footerByteCount = 8;
      int sampleByteCount = additionalBlockLength - sizeof(int) - footerByteCount;
      int bytesPerCell = sampleByteCount / additionalCellCount;
      if (sampleByteCount < 0 || sampleByteCount % additionalCellCount != 0 || bytesPerCell % sizeof(float) != 0)
        throw new InvalidDataException("HeightLayer additional block is not a uniform float32 cell array.");

      int samplesPerCell = bytesPerCell / sizeof(float);
      var additionalSamples = new float[additionalCellCount * samplesPerCell];
      for (int i = 0; i < additionalSamples.Length; i++) additionalSamples[i] = reader.ReadSingle();

      int side = (int)Math.Sqrt(valueCount);
      byte[] remaining = ReadPayloadToTrailer(reader);
      return new HeightLayer
      {
        Width = side * side == valueCount ? side : 0,
        Height = side * side == valueCount ? side : 0,
        Heights = heights,
        AdditionalSamplesPerCell = samplesPerCell,
        AdditionalHeightSamples = additionalSamples,
        RemainingPayload = remaining
      };
    }
  }
}
