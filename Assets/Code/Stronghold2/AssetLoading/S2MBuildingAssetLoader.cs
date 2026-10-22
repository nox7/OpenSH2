using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>All currently supported building assets and their terrain requirements for one map.</summary>
  public sealed class S2MBuildingData
  {
    public S2MFile Map { get; internal set; }
    public S2MKeepData Keeps { get; internal set; }
    public S2MBridgeData Bridges { get; internal set; }
    public IReadOnlyList<S2MBuildingFootprint> Footprints { get; internal set; }
    public bool[] PrimaryHeightCells { get; internal set; }
    public int BuildingCount => Map.Keeps.Count + Bridges.Spans.Count;
  }

  /// <summary>An axis-aligned occupied rectangle in map cells, before renderer axis flips.</summary>
  public readonly struct S2MBuildingFootprint
  {
    public int ObjectId { get; }
    public int MinX { get; }
    public int MinZ { get; }
    public int MaxXExclusive { get; }
    public int MaxZExclusive { get; }

    private S2MBuildingFootprint(int objectId, int minX, int minZ, int maxX, int maxZ)
    {
      ObjectId = objectId; MinX = minX; MinZ = minZ;
      MaxXExclusive = maxX; MaxZExclusive = maxZ;
    }

    internal static S2MBuildingFootprint FromKeep(Keep keep)
    {
      double turns = Math.Round(keep.RotationDegrees / 90d);
      if (Math.Abs(keep.RotationDegrees - turns * 90d) > 0.01d)
        throw new InvalidDataException($"Keep {keep.Id} has an unsupported non-quarter-turn footprint.");
      int quarter = (int)((turns % 4 + 4) % 4);
      // Width/height are already rotated in the Building record. Its model
      // origin is a different corner for each yaw; it is not always the minimum.
      double minX = keep.MapX - (quarter == 2 || quarter == 3 ? keep.FootprintWidth : 0);
      double minZ = keep.MapZ - (quarter == 1 || quarter == 2 ? keep.FootprintHeight : 0);
      return new S2MBuildingFootprint(keep.Id,
        checked((int)Math.Floor(minX)), checked((int)Math.Floor(minZ)),
        checked((int)Math.Ceiling(minX + keep.FootprintWidth)),
        checked((int)Math.Ceiling(minZ + keep.FootprintHeight)));
    }

    internal void AddToMask(bool[] mask, int width, int height)
    {
      for (int z = Math.Max(0, MinZ); z < Math.Min(height, MaxZExclusive); z++)
        for (int x = Math.Max(0, MinX); x < Math.Min(width, MaxXExclusive); x++)
          mask[z * width + x] = true;
    }
  }

  /// <summary>
  /// Map-level entry point for building asset loading. Add support for further
  /// building families here, without adding per-family loaders to the game loop.
  /// </summary>
  public sealed class S2MBuildingAssetLoader
  {
    public S2MBuildingData Load(S2MFile map, string gameInstallPath,
      string converterPath = null, string cacheDirectory = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      S2MKeepData keeps = new S2MKeepAssetLoader().Load(map, gameInstallPath, converterPath, cacheDirectory);
      S2MBridgeData bridges = new S2MBridgeAssetLoader().Load(map, gameInstallPath, converterPath, cacheDirectory);
      int width = map.HeightLayer?.Width ?? 0;
      int height = map.HeightLayer?.Height ?? 0;
      // Copy the bridge mask: building coverage must not mutate bridge-only data.
      var primaryHeights = (bool[])bridges.ReplacedTerrainCells.Clone();
      var footprints = new List<S2MBuildingFootprint>();
      // Draw base ground beneath loaded models, retaining exterior/partial cells.
      // Raised walking floors stay in HeightLayer for future unit navigation.
      foreach (S2MKeepBatch batch in keeps.Batches)
      {
        foreach (Keep keep in batch.Keeps)
        {
          S2MBuildingFootprint footprint = S2MBuildingFootprint.FromKeep(keep);
          footprint.AddToMask(primaryHeights, width, height);
          footprints.Add(footprint);
        }
      }
      return new S2MBuildingData
      {
        Map = map, Keeps = keeps, Bridges = bridges,
        Footprints = footprints, PrimaryHeightCells = primaryHeights
      };
    }
  }
}
