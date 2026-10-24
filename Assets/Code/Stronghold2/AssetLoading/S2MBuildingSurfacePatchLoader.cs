using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Associates saved cultivated-ground components with their farm, independent of plot dimensions.</summary>
  internal static class S2MBuildingSurfacePatchLoader
  {
    public static S2MTerrainSurfaceOverrides Load(S2MFile map,
      IReadOnlyList<S2MPlacedBuildingBatch> batches, List<S2MBuildingAssetIssue> issues)
    {
      int width = map.Landscape?.Width ?? 0, height = map.Landscape?.Height ?? 0;
      if (width <= 0 || height <= 0) return null;
      var patches = new List<S2MBuildingSurfacePatch>();
      byte[] features = map.WaterLayer?.LandscapeFeatureValues;
      if (features == null) return new S2MTerrainSurfaceOverrides(width, height, patches);
      if (features.Length != checked(width * height))
        throw new InvalidDataException("Farm surface markers do not match the map dimensions.");

      var loaded = new HashSet<int>();
      foreach (S2MPlacedBuildingBatch batch in batches)
        foreach (BuildingPlacement building in batch.Placements) loaded.Add(building.Id);
      var farms = new Dictionary<int, BuildingPlacement>();
      var seeds = new Dictionary<int, List<int>>();
      // Establish ownership from the original placements, including unsupported
      // farms. A surviving neighbor must not take over a missing farm's patch.
      foreach (BuildingPlacement building in map.BuildingPlacements)
      {
        if (!IsFarm(building.Type)
          || Math.Abs(building.RotationDegrees / 90d - Math.Round(building.RotationDegrees / 90d)) > 0.0001d) continue;
        farms.Add(building.Id, building);
        S2MBuildingFootprint footprint = S2MBuildingFootprint.FromPlacement(building);
        for (int z = Math.Max(0, footprint.MinZ); z < Math.Min(height, footprint.MaxZExclusive); z++)
          for (int x = Math.Max(0, footprint.MinX); x < Math.Min(width, footprint.MaxXExclusive); x++)
          {
            int index = z * width + x;
            if (!IsSoil(features[index])) continue;
            if (!seeds.TryGetValue(index, out List<int> owners)) seeds.Add(index, owners = new List<int>());
            owners.Add(building.Id);
          }
      }

      var visited = new bool[features.Length];
      var queue = new Queue<int>();
      for (int start = 0; start < features.Length; start++)
      {
        if (visited[start] || !IsSoil(features[start])) continue;
        byte material = features[start];
        var cells = new List<int>();
        var owners = new HashSet<int>();
        visited[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
          int index = queue.Dequeue();
          cells.Add(index);
          if (seeds.TryGetValue(index, out List<int> atCell)) owners.UnionWith(atCell);
          int x = index % width, z = index / width;
          if (x > 0) Visit(index - 1);
          if (x + 1 < width) Visit(index + 1);
          if (z > 0) Visit(index - width);
          if (z + 1 < height) Visit(index + width);
        }
        if (owners.Count != 1)
        {
          // All 283 sampled plots have one owner. Retain base terrain rather
          // than guess ownership if a future map has touching/shared plots.
          if (owners.Count > 1)
            foreach (int id in owners)
              issues.Add(new S2MBuildingAssetIssue { ObjectId = id, ObjectType = farms[id].Type,
                Reason = "Saved soil patch touches multiple farms; surface ownership is ambiguous." });
          continue;
        }
        foreach (int id in owners)
        {
          if (!loaded.Contains(id)) continue;
          int quarter = (int)((Math.Round(farms[id].RotationDegrees / 90d) % 4 + 4) % 4);
          patches.Add(new S2MBuildingSurfacePatch(id, material, (byte)quarter, cells));
        }

        void Visit(int index)
        {
          if (visited[index] || features[index] != material) return;
          visited[index] = true;
          queue.Enqueue(index);
        }
      }
      return new S2MTerrainSurfaceOverrides(width, height, patches);
    }

    private static bool IsSoil(byte value) => value == 0x25 || value == 0x26;
    private static bool IsFarm(string type) => type == "WheatFarm" || type == "HopFarm"
      || type == "AppleFarm" || type == "PigFarm" || type == "DairyFarm" || type == "SheepFarm";
  }
}
