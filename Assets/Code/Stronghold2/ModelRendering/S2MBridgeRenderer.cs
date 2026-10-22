using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  public static class S2MBridgeRenderer
  {
    // The exported bridge modules use a 409.6-unit, two-cell pitch. The middle
    // mesh is exported one pitch beyond the end mesh, rather than about (0,0).
    public const float ModelUnitsPerCell = 204.8f;

    public static GameObject Render(S2MBridgeData bridges, Transform parent = null,
      S2MTerrainSettings terrainSettings = null)
    {
      if (bridges == null) throw new ArgumentNullException(nameof(bridges));
      terrainSettings ??= new S2MTerrainSettings();
      var root = new GameObject("S2M Bridges");
      if (parent != null) root.transform.SetParent(parent, false);
      if (bridges.Spans.Count == 0) return root;

      var ends = new List<Granny2ModelInstance>();
      var middles = new List<Granny2ModelInstance>();
      foreach (BridgeSpan span in bridges.Spans)
        AddSpanInstances(span, bridges.Map.HeightLayer, terrainSettings, ends, middles);
      var modelSettings = new Granny2ModelRenderSettings
      {
        UnitScale = terrainSettings.HorizontalCellSize / ModelUnitsPerCell,
        UseFoliageMaterials = false
      };
      Granny2ModelRenderer.RenderInstances(bridges.EndModel, ends, root.transform, modelSettings, "Bridge Ends");
      if (middles.Count > 0)
        Granny2ModelRenderer.RenderInstances(bridges.MiddleModel, middles, root.transform, modelSettings, "Bridge Middles");

      // Reflect the complete assembly about the same playable bounds as terrain.
      root.transform.localPosition = new Vector3(
        terrainSettings.FlipX ? bridges.Map.HeightLayer.Width * terrainSettings.HorizontalCellSize : 0f,
        0f, terrainSettings.FlipZ ? bridges.Map.HeightLayer.Height * terrainSettings.HorizontalCellSize : 0f);
      root.transform.localScale = new Vector3(terrainSettings.FlipX ? -1f : 1f, 1f, terrainSettings.FlipZ ? -1f : 1f);
      if (terrainSettings.GenerateMeshColliders)
      {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
          filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
      }
      return root;
    }

    internal static void AddSpanInstances(BridgeSpan span, HeightLayer heights, S2MTerrainSettings settings,
      List<Granny2ModelInstance> ends, List<Granny2ModelInstance> middles)
    {
      int cellIndex = span.StartZ * heights.Width + span.StartX;
      float baseHeight = heights.Heights[cellIndex];
      if (heights.CornerHeightPlanes?.Length > 0)
      {
        int otherCorner = span.AlongZ ? 3 : 0;
        baseHeight = (heights.GetCornerHeight(0, cellIndex, 2)
          + heights.GetCornerHeight(0, cellIndex, otherCorner)) * 0.5f;
      }
      float y = baseHeight * settings.HeightUnitScale;
      float verticalScale = settings.HeightUnitScale * 1024f / settings.HorizontalCellSize;
      var scale = new Vector3(1f, verticalScale, 1f);
      string name = $"Bridge {span.StartX},{span.StartZ}";
      ends.Add(Instance(0f, 0f, 0f, "start"));
      for (int along = 2; along < span.Length - 2; along += 2)
        middles.Add(Instance(0f, along - 2f, 0f, $"middle {along}"));
      ends.Add(Instance(2f, span.Length, 180f, "end"));

      Granny2ModelInstance Instance(float across, float along, float yaw, string part)
      {
        float x = span.StartX + (span.AlongZ ? across : along);
        float z = span.StartZ + (span.AlongZ ? along : 2f - across);
        return new Granny2ModelInstance($"{name} {part}",
          new Vector3(x * settings.HorizontalCellSize, y, z * settings.HorizontalCellSize),
          new Vector3(0f, yaw + (span.AlongZ ? 0f : 90f), 0f), scale);
      }
    }
  }
}
