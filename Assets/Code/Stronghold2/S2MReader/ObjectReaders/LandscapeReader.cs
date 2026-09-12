using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal sealed class LandscapeReader : ObjectReader
  {
    private const int HeightBlockTag = 4;
    private const int SurfaceMaterialBlockTag = 27;
    private const int FlowDirectionBlockTag = 7;
    private const int TerrainTintBlockTag = 22;
    private const int TerrainTextureRotationBlockTag = 30;
    private const int LandscapeFeatureBlockTag = 19;

    public LandscapeReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      byte[] payload = ReadPayloadToTrailer(reader);
      using var stream = new MemoryStream(payload, writable: false);
      using var payloadReader = new BinaryReader(stream);

      // Most maps have a 12-byte object prefix. An older variant begins with
      // the height block immediately.
      if (payload.Length >= 16 && BitConverter.ToInt32(payload, 12) == HeightBlockTag)
        stream.Position = 12;

      int heightBlockTag = payloadReader.ReadInt32();
      int logicalMapSize = payloadReader.ReadInt32();
      payloadReader.ReadInt32();
      int heightByteCount = payloadReader.ReadInt32();
      if (heightBlockTag != HeightBlockTag || logicalMapSize <= 0 || heightByteCount <= 0
        || heightByteCount % sizeof(float) != 0)
        throw new InvalidDataException("Landscape does not begin with the expected square float height grid.");

      int cellCount = heightByteCount / sizeof(float);
      int side = (int)Math.Sqrt(cellCount);
      if (side * side != cellCount)
        throw new InvalidDataException("Landscape height data is not a square grid.");

      var serializedHeights = new float[cellCount];
      for (int i = 0; i < serializedHeights.Length; i++) serializedHeights[i] = payloadReader.ReadSingle();

      var blocks = new Dictionary<int, byte[]>();
      while (stream.Position < stream.Length)
      {
        if (stream.Length - stream.Position < sizeof(int) * 2)
          throw new InvalidDataException("Landscape ends inside a block header.");

        int tag = payloadReader.ReadInt32();
        int byteCount = payloadReader.ReadInt32();
        if (byteCount < 0 || byteCount > stream.Length - stream.Position)
          throw new InvalidDataException($"Landscape block {tag} has invalid byte length {byteCount}.");
        blocks[tag] = S2MReaderUtils.ReadExactBytes(payloadReader, byteCount);
      }

      byte[] surfaceMaterials = GetGrid(blocks, SurfaceMaterialBlockTag, cellCount);
      byte[] flowDirections = GetGrid(blocks, FlowDirectionBlockTag, cellCount);
      byte[] landscapeFeatureValues = GetGrid(blocks, LandscapeFeatureBlockTag, cellCount);
      Color32[] terrainTintColors = GetColorGrid(blocks, TerrainTintBlockTag, cellCount);
      byte[] terrainTextureRotationValues = GetGrid(blocks, TerrainTextureRotationBlockTag, cellCount);

      return new Landscape
      {
        Width = side,
        Height = side,
        LogicalMapSize = logicalMapSize,
        Heights = Transpose(serializedHeights, side),
        Water = new WaterLayer
        {
          Width = side,
          Height = side,
          SurfaceMaterialIds = Transpose(surfaceMaterials, side),
          FlowDirections = Transpose(flowDirections, side),
          LandscapeFeatureValues = Transpose(landscapeFeatureValues, side)
        },
        TerrainTintColors = terrainTintColors,
        TerrainTextureRotationValues = Transpose(terrainTextureRotationValues, side),
        RawPayload = payload
      };
    }

    private static byte[] GetGrid(Dictionary<int, byte[]> blocks, int tag, int cellCount)
    {
      if (!blocks.TryGetValue(tag, out byte[] block) || block.Length < cellCount)
        throw new InvalidDataException($"Landscape block {tag} does not contain a complete cell grid.");

      var grid = new byte[cellCount];
      Buffer.BlockCopy(block, 0, grid, 0, cellCount);
      return grid;
    }

    private static byte[] Transpose(byte[] source, int side)
    {
      var result = new byte[source.Length];
      for (int x = 0; x < side; x++)
      {
        for (int z = 0; z < side; z++) result[z * side + x] = source[x * side + z];
      }
      return result;
    }

    private static Color32[] GetColorGrid(Dictionary<int, byte[]> blocks, int tag, int cellCount)
    {
      if (!blocks.TryGetValue(tag, out byte[] block) || block.Length < cellCount * 4) return null;

      int side = (int)Math.Sqrt(cellCount);
      var result = new Color32[cellCount];
      for (int x = 0; x < side; x++)
      {
        for (int z = 0; z < side; z++)
        {
          int serializedIndex = x * side + z;
          int source = serializedIndex * 4;
          result[z * side + x] = new Color32(block[source], block[source + 1], block[source + 2], block[source + 3]);
        }
      }
      return result;
    }

    private static float[] Transpose(float[] source, int side)
    {
      var result = new float[source.Length];
      for (int x = 0; x < side; x++)
      {
        for (int z = 0; z < side; z++) result[z * side + x] = source[x * side + z];
      }
      return result;
    }
  }
}
