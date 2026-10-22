using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.TerrainRendering;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Renders all supported building families from one map-level asset load.</summary>
  public static class S2MBuildingRenderer
  {
    public static GameObject Render(S2MBuildingData data, Transform parent = null,
      S2MTerrainSettings terrainSettings = null, S2MKeepRenderSettings keepSettings = null)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));
      var root = new GameObject("S2M Buildings");
      if (parent != null) root.transform.SetParent(parent, false);
      S2MKeepRenderer.Render(data.Keeps, root.transform, terrainSettings, keepSettings);
      S2MBridgeRenderer.Render(data.Bridges, root.transform, terrainSettings);
      return root;
    }
  }
}
