using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;

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
      var grouped = new Dictionary<string, List<ForestInstance>>(StringComparer.OrdinalIgnoreCase);
      int unsupported = 0;

      if (map.Forest != null)
      {
        foreach (ForestInstance instance in map.Forest.Instances)
        {
          if (map.MapSize > 0 && (instance.MapX < 0f || instance.MapZ < 0f
            || instance.MapX >= map.MapSize || instance.MapZ >= map.MapSize)) continue;
          if (!S2MVegetationAssetCatalog.TryGetTreeAsset(instance.Family, instance.Variant, lowDetail, out string path))
          {
            unsupported++;
            continue;
          }
          if (!grouped.TryGetValue(path, out List<ForestInstance> placements))
            grouped.Add(path, placements = new List<ForestInstance>());
          placements.Add(instance);
        }
      }

      var models = Granny2AssetConverter.LoadOrConvertMany(gameInstallPath, grouped.Keys, converterPath, cacheDirectory);
      var batches = new List<S2MVegetationBatch>(grouped.Count);
      foreach (KeyValuePair<string, List<ForestInstance>> pair in grouped)
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

      int supported = 0;
      foreach (S2MVegetationBatch batch in batches) supported += batch.Instances.Count;
      return new S2MVegetationData
      {
        Map = map,
        Batches = batches,
        SupportedInstanceCount = supported,
        UnsupportedInstanceCount = unsupported
      };
    }
  }
}
