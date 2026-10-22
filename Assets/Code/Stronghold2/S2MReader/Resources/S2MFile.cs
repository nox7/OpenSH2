using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public class S2MFile
  {
    public string SourcePath { get; internal set; }
    public string Author { get; set; }
    public MapType MapType { get; set; }
    public bool Balanced { get; set; }
    public string LastSave { get; set; }
    /// <summary>Playable terrain side length; Landscape takes precedence over the header.</summary>
    public int MapSize { get; set; }
    public int MaxPlayers { get; set; }
    public int Version { get; set; }
    public IReadOnlyDictionary<string, string> StringOptions { get; internal set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, int> IntegerOptions { get; internal set; } = new Dictionary<string, int>();
    public IReadOnlyList<S2Object> MapHeaderObjects { get; internal set; } = Array.Empty<S2Object>();
    public IReadOnlyList<S2Object> RadarMapObjects { get; internal set; } = Array.Empty<S2Object>();
    public IReadOnlyList<S2Object> S2GameObjects { get; internal set; } = Array.Empty<S2Object>();
    public RadarMap RadarMap { get; internal set; }
    public EstateLayer RadarEstateLayer { get; internal set; }
    public Landscape Landscape { get; internal set; }
    public WaterLayer WaterLayer => Landscape?.Water;
    public HeightLayer HeightLayer { get; internal set; }
    public Forest Forest { get; internal set; }
    /// <summary>Complete zlib payloads in their original S2M file order.</summary>
    public IReadOnlyList<ZLibDecompressedSegment> DecompressedSegments { get; internal set; } = Array.Empty<ZLibDecompressedSegment>();

    internal void ApplyPlayableTerrainSize()
    {
      // war_chapter2 omits the header size but Landscape declares 128, despite
      // its 256-wide backing grid. Serialized capacity is not playable size.
      int size = Landscape?.LogicalMapSize ?? (MapSize > 0 ? MapSize : HeightLayer?.Width ?? 0);
      if (size <= 0) throw new InvalidDataException($"Invalid playable map size {size}.");
      if ((Landscape != null && (size > Landscape.Width || size > Landscape.Height))
        || (HeightLayer != null && (size > HeightLayer.Width || size > HeightLayer.Height)))
        throw new InvalidDataException($"Playable map size {size} exceeds the stored terrain grid.");

      if (HeightLayer != null)
      {
        int width = HeightLayer.Width;
        int height = HeightLayer.Height;
        HeightLayer.Heights = CropGrid(HeightLayer.Heights, width, height, size);
        if (HeightLayer.CornerHeightPlanes != null)
        {
          for (int plane = 0; plane < HeightLayer.CornerHeightPlanes.Length; plane++)
            HeightLayer.CornerHeightPlanes[plane] = CropGrid(
              HeightLayer.CornerHeightPlanes[plane], width, height, size, HeightLayer.CornersPerCell);
        }
        HeightLayer.Width = HeightLayer.Height = size;
      }

      if (Landscape != null)
      {
        int width = Landscape.Width;
        int height = Landscape.Height;
        Landscape.Heights = CropGrid(Landscape.Heights, width, height, size);
        Landscape.TerrainTintColors = CropGrid(Landscape.TerrainTintColors, width, height, size);
        Landscape.TerrainTextureRotationValues = CropGrid(Landscape.TerrainTextureRotationValues, width, height, size);
        WaterLayer water = Landscape.Water;
        if (water != null)
        {
          water.SurfaceMaterialIds = CropGrid(water.SurfaceMaterialIds, water.Width, water.Height, size);
          water.FlowDirections = CropGrid(water.FlowDirections, water.Width, water.Height, size);
          water.LandscapeFeatureValues = CropGrid(water.LandscapeFeatureValues, water.Width, water.Height, size);
          water.Width = water.Height = size;
        }
        Landscape.Width = Landscape.Height = size;
      }
      MapSize = size;
    }

    private static T[] CropGrid<T>(T[] source, int width, int height, int size, int valuesPerCell = 1)
    {
      if (source == null) return null;
      if (size > width || size > height || source.Length != checked(width * height * valuesPerCell))
        throw new InvalidDataException("Terrain grid dimensions do not match its stored values.");
      if (size == width && size == height) return source;

      // Keep the origin and copy rows using the original stride, including all
      // four corner values per HeightLayer cell. Raw serialized data is retained.
      var result = new T[checked(size * size * valuesPerCell)];
      int rowLength = checked(size * valuesPerCell);
      for (int z = 0; z < size; z++)
        Array.Copy(source, z * width * valuesPerCell, result, z * rowLength, rowLength);
      return result;
    }
  }
}
