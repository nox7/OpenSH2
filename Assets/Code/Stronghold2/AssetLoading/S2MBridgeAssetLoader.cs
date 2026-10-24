using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class S2MBridgeData
  {
    public S2MFile Map { get; internal set; }
    public IReadOnlyList<BridgeSpan> Spans { get; internal set; } = Array.Empty<BridgeSpan>();
    public Granny2ModelData EndModel { get; internal set; }
    public Granny2ModelData MiddleModel { get; internal set; }
    /// <summary>Cells whose raised walking deck is replaced; the base terrain remains visible.</summary>
    public bool[] ReplacedTerrainCells { get; internal set; }
    public int UnsupportedSpanCount { get; internal set; }
  }

  /// <summary>Loads the completed modular bridge meshes before authorizing terrain replacement.</summary>
  public sealed class S2MBridgeAssetLoader
  {
    public S2MBridgeData Load(S2MFile map, string gameInstallPath,
      string converterPath = null, string cacheDirectory = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      int width = map.HeightLayer?.Width ?? 0;
      int height = map.HeightLayer?.Height ?? 0;
      var spans = new List<BridgeSpan>();
      var result = new S2MBridgeData
      {
        Map = map, Spans = spans,
        ReplacedTerrainCells = new bool[checked(width * height)]
      };
      if (map.Bridges == null) return result;
      foreach (BridgeSpan span in map.Bridges.Spans)
      {
        if (span.CanRender(width, height)) spans.Add(span);
        else result.UnsupportedSpanCount++;
      }
      if (spans.Count == 0) return result;

      string[] paths = { "meshes/buildings/bridge_end.GR2", "meshes/buildings/bridge_middle.GR2" };
      var models = Granny2AssetConverter.LoadOrConvertMany(gameInstallPath, paths, converterPath, cacheDirectory);
      foreach (string path in paths) if (models[path].Error != null) throw models[path].Error;
      result.EndModel = models[paths[0]].Model;
      result.MiddleModel = models[paths[1]].Model;
      if (result.EndModel.Meshes.Count == 0 || result.MiddleModel.Meshes.Count == 0)
        throw new InvalidOperationException("Bridge replacement meshes contain no geometry.");

      foreach (BridgeSpan span in spans)
      {
        for (int along = 0; along < span.Length; along++)
        {
          for (int across = 0; across < 2; across++)
          {
            int x = span.StartX + (span.AlongZ ? across : along);
            int z = span.StartZ + (span.AlongZ ? along : across);
            result.ReplacedTerrainCells[z * width + x] = true;
          }
        }
      }
      return result;
    }
  }
}
