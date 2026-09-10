using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Loads only the distinct GR2 models referenced by one map.</summary>
  public sealed class S2MVegetationAssetLoader
  {
    private readonly Dictionary<string, Granny2ModelData> modelCache = new(StringComparer.OrdinalIgnoreCase);

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

      var batches = new List<S2MVegetationBatch>(grouped.Count);
      foreach (KeyValuePair<string, List<ForestInstance>> pair in grouped)
      {
        if (!modelCache.TryGetValue(pair.Key, out Granny2ModelData model))
        {
          model = Granny2AssetConverter.LoadOrConvert(gameInstallPath, pair.Key, converterPath, cacheDirectory);
          modelCache.Add(pair.Key, model);
        }
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
