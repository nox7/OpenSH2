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
    public IReadOnlyList<S2MPlacedBuildingBatch> Batches { get; internal set; } = Array.Empty<S2MPlacedBuildingBatch>();
    public IReadOnlyList<S2MBuildingAssetIssue> Issues { get; internal set; } = Array.Empty<S2MBuildingAssetIssue>();
    public IReadOnlyList<S2MBuildingFootprint> Footprints { get; internal set; }
    public bool[] PrimaryHeightCells { get; internal set; }
    /// <summary>Saved cutouts authorized by successfully loaded building geometry,
    /// in unflipped row-major map order. Does not change source heights or occupancy.</summary>
    public bool[] TerrainCutoutCells { get; internal set; }
    public S2MTerrainSurfaceOverrides SurfaceOverrides { get; internal set; }
    public int GeneralBuildingCount { get; internal set; }
    public int BuildingCount => Map.Keeps.Count + Bridges.Spans.Count + GeneralBuildingCount;
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

    internal static S2MBuildingFootprint FromPlacement(BuildingPlacement building)
    {
      double turns = Math.Round(building.RotationDegrees / 90d);
      if (Math.Abs(building.RotationDegrees - turns * 90d) > 0.01d)
        throw new InvalidDataException($"Building {building.Id} has an unsupported non-quarter-turn footprint.");
      int quarter = (int)((turns % 4 + 4) % 4);
      // Width/height are already rotated in the Building record. Its model
      // origin is a different corner for each yaw; it is not always the minimum.
      double minX = building.MapX - (quarter == 2 || quarter == 3 ? building.FootprintWidth : 0);
      double minZ = building.MapZ - (quarter == 1 || quarter == 2 ? building.FootprintHeight : 0);
      return new S2MBuildingFootprint(building.Id,
        checked((int)Math.Floor(minX)), checked((int)Math.Floor(minZ)),
        checked((int)Math.Ceiling(minX + building.FootprintWidth)),
        checked((int)Math.Ceiling(minZ + building.FootprintHeight)));
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
    private readonly S2MPlacedBuildingAssetLoader placedLoader = new();
    public S2MBuildingData Load(S2MFile map, string gameInstallPath,
      string converterPath = null, string cacheDirectory = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      S2MKeepData keeps = new S2MKeepAssetLoader().Load(map, gameInstallPath, converterPath, cacheDirectory);
      S2MBridgeData bridges = new S2MBridgeAssetLoader().Load(map, gameInstallPath, converterPath, cacheDirectory);
      var issues = new List<S2MBuildingAssetIssue>();
      IReadOnlyList<S2MPlacedBuildingBatch> batches = placedLoader.Load(map, gameInstallPath, converterPath, cacheDirectory, issues);
      S2MTerrainSurfaceOverrides surfaceOverrides = S2MBuildingSurfacePatchLoader.Load(map, batches, issues);
      int generalCount = 0;
      foreach (S2MPlacedBuildingBatch batch in batches) generalCount += batch.Placements.Count;
      int width = map.HeightLayer?.Width ?? 0;
      int height = map.HeightLayer?.Height ?? 0;
      // Copy the bridge mask: building coverage must not mutate bridge-only data.
      var primaryHeights = (bool[])bridges.ReplacedTerrainCells.Clone();
      var footprints = new List<S2MBuildingFootprint>();
      // Terrain/walking separation depends on placed buildings, not on whether
      // their visual assets have been implemented. Keep all navigation data.
      foreach (BuildingPlacement building in map.BuildingPlacements)
      {
        // The rectangular footprint interpretation is only established for
        // quarter turns. Leave unsupported rotations alone rather than flattening
        // a guessed region or preventing the whole map from loading.
        if (Math.Abs(building.RotationDegrees / 90d - Math.Round(building.RotationDegrees / 90d)) > 0.0001d) continue;
        S2MBuildingFootprint footprint = S2MBuildingFootprint.FromPlacement(building);
        footprint.AddToMask(primaryHeights, width, height);
        footprints.Add(footprint);
      }
      var cutouts = new bool[checked(width * height)];
      byte[] savedCutouts = map.Landscape?.TerrainCutoutValues;
      if (savedCutouts != null && cutouts.Length > 0)
      {
        if (savedCutouts.Length != cutouts.Length)
          throw new InvalidDataException("Saved terrain cutouts do not match the terrain dimensions.");
        foreach (S2MKeepBatch batch in keeps.Batches)
          foreach (Keep keep in batch.Keeps) AddCutouts(keep);
        foreach (S2MPlacedBuildingBatch batch in batches)
          foreach (BuildingPlacement building in batch.Placements) AddCutouts(building);
      }
      return new S2MBuildingData
      {
        Map = map, Keeps = keeps, Bridges = bridges, Batches = batches,
        Issues = issues, GeneralBuildingCount = generalCount,
        Footprints = footprints, PrimaryHeightCells = primaryHeights, TerrainCutoutCells = cutouts,
        SurfaceOverrides = surfaceOverrides
      };

      void AddCutouts(BuildingPlacement building)
      {
        if (Math.Abs(building.RotationDegrees / 90d - Math.Round(building.RotationDegrees / 90d)) > 0.0001d) return;
        S2MBuildingFootprint footprint = S2MBuildingFootprint.FromPlacement(building);
        for (int z = Math.Max(0, footprint.MinZ); z < Math.Min(height, footprint.MaxZExclusive); z++)
          for (int x = Math.Max(0, footprint.MinX); x < Math.Min(width, footprint.MaxXExclusive); x++)
          {
            int index = z * width + x;
            // Tag 18 is already in map coordinates: do not rotate it again or
            // remove an entire reserved clearance rectangle. Keep bridge beds.
            if (savedCutouts[index] == 1 && !bridges.ReplacedTerrainCells[index]) cutouts[index] = true;
          }
      }
    }
  }
}
