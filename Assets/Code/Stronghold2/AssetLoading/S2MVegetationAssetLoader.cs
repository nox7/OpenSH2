using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Loads only the distinct GR2 models referenced by one map.</summary>
  public sealed class S2MVegetationAssetLoader
  {
    public S2MVegetationData Load(
      S2MFile map,
      string gameInstallPath,
      string converterPath = null,
      string cacheDirectory = null,
      bool lowDetail = false)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      var grouped = new Dictionary<string, List<SceneryInstance>>(StringComparer.OrdinalIgnoreCase);
      var grass = new List<SceneryInstance>();
      var wheat = new List<PlantActor>();
      var issues = new HashSet<string>();
      var savedIronCells = new HashSet<(int x, int z)>();
      int unsupported = 0;
      int inactive = 0;

      bool InBounds(float x, float z) => map.MapSize <= 0
        || (x >= 0f && z >= 0f && x < map.MapSize && z < map.MapSize);
      void Add(string path, SceneryInstance instance)
      {
        if (!grouped.TryGetValue(path, out List<SceneryInstance> placements))
          grouped.Add(path, placements = new List<SceneryInstance>());
        placements.Add(instance);
      }

      if (map.Forest != null)
      {
        int forestId=0;
        foreach (ForestInstance instance in map.Forest.Instances)
        {
          int resourceId=++forestId;
          if (instance.IsInactive) { inactive++; continue; }
          if (!InBounds(instance.MapX, instance.MapZ)) continue;
          if (!S2MVegetationAssetCatalog.TryGetTreeAsset(instance.Family, instance.Variant, lowDetail, out string path))
          {
            unsupported++;
            issues.Add($"Forest family {instance.Family}, variant {instance.Variant}: unresolved model/state.");
            continue;
          }
          Add(path, new SceneryInstance
          {
            ResourceId=resourceId,ForestSource=instance,
            Kind = SceneryKind.Forest, RawX = instance.RawX, RawY = instance.RawY, RawZ = instance.RawZ,
            RotationDegrees = instance.RotationDegrees, ScaleX = instance.ScaleX, ScaleY = instance.ScaleY,
            AppearanceScale = instance.AppearanceScale, Family = instance.Family, Variant = instance.Variant
          });
        }
      }

      foreach (SceneryLayer layer in new[] { map.Shrubs, map.Rocks, map.NaturalStone, map.Grass })
      {
        if (layer == null) continue;
        int sceneryId=0;
        foreach (SceneryInstance instance in layer.Instances)
        {
          sceneryId++;
          if(layer.Kind==SceneryKind.Shrubs)instance.ResourceId=1000000+sceneryId;
          if (!instance.Enabled || !InBounds(instance.MapX, instance.MapZ)) continue;
          if (instance.Kind == SceneryKind.Rocks && instance.Family == 8 && instance.Variant < 12)
            savedIronCells.Add(((int)Math.Floor(instance.MapX), (int)Math.Floor(instance.MapZ)));
          if (instance.Kind == SceneryKind.Grass && instance.Family < 16 && instance.Variant == 0)
          {
            grass.Add(instance);
            continue;
          }
          if (S2MVegetationAssetCatalog.TryGetSceneryAsset(instance.Kind, instance.Family, instance.Variant, out string path))
            Add(path, instance);
          else
          {
            unsupported++;
            issues.Add($"{instance.Kind} family {instance.Family}, variant {instance.Variant}: unresolved asset.");
          }
        }
      }

      // Iron deposits are tag-19 terrain cells, not serialized decorative Rocks.
      WaterLayer ground = map.WaterLayer;
      if (ground?.LandscapeFeatureValues != null)
        for (int z = 0; z < ground.Height; z++)
          for (int x = 0; x < ground.Width; x++)
          {
            if (ground.LandscapeFeatureValues[z * ground.Width + x] != S2MTerrainSurfaceMaterialGrid.IronOreMaterialId) continue;
            if (savedIronCells.Contains((x, z))) continue;
            // Stable coordinate variation; never consume the simulation's RNG.
            uint seed = unchecked((uint)x * 73856093u ^ (uint)z * 19349663u);
            seed ^= seed >> 16; seed *= 0x7feb352du; seed ^= seed >> 15;
            ushort variant = (ushort)(seed % 12u);
            S2MVegetationAssetCatalog.TryGetSceneryAsset(SceneryKind.IronOre, 8, variant, out string path);
            Add(path, new SceneryInstance
            {
              Kind = SceneryKind.IronOre, Family = 8, Variant = variant,
              RawX = (x + .5f) * 1024f, RawZ = (z + .5f) * 1024f,
              RotationDegrees = ((seed >> 16) & 3u) * 90f, SampleTerrainHeight = true
            });
          }

      foreach (PlantActor plant in map.PlantActors)
      {
        if (plant.Stage < 0) { inactive++; continue; }
        if (!InBounds(plant.RawX / 1024f, plant.RawZ / 1024f)) continue;
        if (plant.Kind == SceneryKind.Wheat && plant.Stage < 6) { wheat.Add(plant); continue; }
        string path = null;
        if (plant.Kind == SceneryKind.AppleTree)
        {
          int stage = plant.Stage == 5 ? 1 : Math.Min(plant.Stage, 4);
          path = "meshes/buildings/" + new[] { "apple_tree_sapling", "apple_tree_bare", "apple_tree_blossom", "apple_tree", "apple_tree_apples" }[stage] + ".gr2";
        }
        else if (plant.Kind == SceneryKind.Hops && plant.Stage < 7)
          path = "meshes/buildings/hops_stage_" + (plant.Stage + 1) + ".gr2";
        if (path == null) { unsupported++; issues.Add($"{plant.Kind} stage {plant.Stage}: unresolved plant asset."); continue; }
        Add(path, new SceneryInstance
        {
          Kind = plant.Kind, RawX = plant.RawX + (plant.Kind == SceneryKind.AppleTree ? 1024f : 512f),
          RawY = plant.RawY, RawZ = plant.RawZ + (plant.Kind == SceneryKind.Hops ? 512f : 0f),
          RotationDegrees = plant.RotationDegrees, Family = (ushort)plant.Stage
        });
      }

      var paths=new List<string>(grouped.Keys);
      for(int i=1;i<=6;i++)paths.Add($"meshes/landscape/tree_chopped_{i}.GR2");
      var models = Granny2AssetConverter.LoadOrConvertMany(gameInstallPath, paths, converterPath, cacheDirectory);
      var logs=new List<Granny2ModelData>();
      for(int i=1;i<=6;i++)
      {
        var log=models[$"meshes/landscape/tree_chopped_{i}.GR2"];
        if(log.Error!=null)throw log.Error;logs.Add(log.Model);
      }
      var batches = new List<S2MVegetationBatch>(grouped.Count);
      foreach (KeyValuePair<string, List<SceneryInstance>> pair in grouped)
      {
        Granny2AssetLoadResult result = models[pair.Key];
        if (result.Error != null) throw result.Error;
        Granny2ModelData model = result.Model;
        batches.Add(new S2MVegetationBatch
        {
          AssetRelativePath = pair.Key,
          Model = model,
          Instances = pair.Value
        });
      }

      int supported = grass.Count + wheat.Count;
      foreach (S2MVegetationBatch batch in batches) supported += batch.Instances.Count;
      return new S2MVegetationData
      {
        Map = map,
        LogModels = logs,
        Batches = batches,
        SupportedInstanceCount = supported,
        UnsupportedInstanceCount = unsupported,
        InactiveInstanceCount = inactive,
        Grass = grass,
        GrassTexturePath = grass.Count == 0 ? null : Path.Combine(gameInstallPath, "terrain", "grass_bb.dds"),
        Wheat = wheat,
        WheatTexturePath = wheat.Count == 0 ? null : Path.Combine(gameInstallPath, "meshes", "buildings", "farm_plants.transparent.dds"),
        Issues = new List<string>(issues)
      };
    }
  }
}
