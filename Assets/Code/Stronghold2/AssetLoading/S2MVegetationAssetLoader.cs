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
        foreach (ForestInstance instance in map.Forest.Instances)
        {
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
            Kind = SceneryKind.Forest, RawX = instance.RawX, RawY = instance.RawY, RawZ = instance.RawZ,
            RotationDegrees = instance.RotationDegrees, ScaleX = instance.ScaleX, ScaleY = instance.ScaleY,
            AppearanceScale = instance.AppearanceScale, Family = instance.Family, Variant = instance.Variant
          });
        }
      }

      foreach (SceneryLayer layer in new[] { map.Shrubs, map.Rocks, map.NaturalStone, map.Grass })
      {
        if (layer == null) continue;
        foreach (SceneryInstance instance in layer.Instances)
        {
          if (!instance.Enabled || !InBounds(instance.MapX, instance.MapZ)) continue;
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

      var models = Granny2AssetConverter.LoadOrConvertMany(gameInstallPath, grouped.Keys, converterPath, cacheDirectory);
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
