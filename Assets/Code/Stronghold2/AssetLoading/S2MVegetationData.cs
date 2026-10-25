using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>All model data needed by one map's currently supported vegetation.</summary>
  public sealed class S2MVegetationData
  {
    public S2MFile Map { get; internal set; }
    public IReadOnlyList<S2MVegetationBatch> Batches { get; internal set; } = Array.Empty<S2MVegetationBatch>();
    public int SupportedInstanceCount { get; internal set; }
    public int UnsupportedInstanceCount { get; internal set; }
    public int InactiveInstanceCount { get; internal set; }
    public IReadOnlyList<SceneryInstance> Grass { get; internal set; } = Array.Empty<SceneryInstance>();
    public string GrassTexturePath { get; internal set; }
    public IReadOnlyList<PlantActor> Wheat { get; internal set; } = Array.Empty<PlantActor>();
    public string WheatTexturePath { get; internal set; }
    public IReadOnlyList<string> Issues { get; internal set; } = Array.Empty<string>();
  }

  public sealed class S2MVegetationBatch
  {
    public string AssetRelativePath { get; internal set; }
    public Granny2ModelData Model { get; internal set; }
    public IReadOnlyList<SceneryInstance> Instances { get; internal set; } = Array.Empty<SceneryInstance>();
  }
}
