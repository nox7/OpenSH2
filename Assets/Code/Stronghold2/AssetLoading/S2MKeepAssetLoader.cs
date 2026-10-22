using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class S2MKeepData
  {
    public S2MFile Map { get; internal set; }
    public IReadOnlyList<S2MKeepBatch> Batches { get; internal set; } = Array.Empty<S2MKeepBatch>();
  }

  public sealed class S2MKeepBatch
  {
    public KeepSize Size { get; internal set; }
    public IReadOnlyList<Keep> Keeps { get; internal set; }
    public Granny2ModelData Closed { get; internal set; }
    public Granny2ModelData Open { get; internal set; }
    public Granny2ModelData Table { get; internal set; }
    public Granny2ModelData Collision { get; internal set; }
  }

  /// <summary>Loads shared exterior, interior, furniture and collision assets for placed keeps.</summary>
  public sealed class S2MKeepAssetLoader
  {
    public S2MKeepData Load(S2MFile map, string gameInstallPath,
      string converterPath = null, string cacheDirectory = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      var groups = new Dictionary<KeepSize, List<Keep>>();
      foreach (Keep keep in map.Keeps)
      {
        if (!groups.TryGetValue(keep.Size, out List<Keep> placements))
          groups.Add(keep.Size, placements = new List<Keep>());
        placements.Add(keep);
      }
      var batches = new List<S2MKeepBatch>();
      foreach (KeyValuePair<KeepSize, List<Keep>> group in groups)
      {
        string stem = group.Key switch
        {
          KeepSize.Wooden => "Keep_wooden",
          KeepSize.Small => "Keep_small",
          KeepSize.Medium => "keep_medium",
          KeepSize.Large => "Keep_large",
          _ => throw new InvalidOperationException($"Unsupported keep size {group.Key}.")
        };
        batches.Add(new S2MKeepBatch
        {
          Size = group.Key, Keeps = group.Value,
          Closed = LoadPart("closed"), Open = LoadPart("open"),
          Table = LoadPart("table"), Collision = LoadPart("collision")
        });

        Granny2ModelData LoadPart(string part)
        {
          Granny2ModelData model = Granny2AssetConverter.LoadOrConvert(gameInstallPath,
            $"meshes/buildings/{stem}_{part}.GR2", converterPath, cacheDirectory);
          if (model.Meshes.Count == 0)
            throw new InvalidOperationException($"Keep asset {stem}_{part} contains no geometry.");
          return model;
        }
      }
      return new S2MKeepData { Map = map, Batches = batches };
    }
  }
}
