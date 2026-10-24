using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.TerrainRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Renders all supported building families from one map-level asset load.</summary>
  public static class S2MBuildingRenderer
  {
    public const float ModelUnitsPerCell = 204.8f;

    public static GameObject Render(S2MBuildingData data, Transform parent = null,
      S2MTerrainSettings terrainSettings = null, S2MBuildingRenderSettings settings = null)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));
      terrainSettings ??= new S2MTerrainSettings();
      settings ??= new S2MBuildingRenderSettings();
      var root = new GameObject("S2M Buildings");
      if (parent != null) root.transform.SetParent(parent, false);
      S2MKeepRenderer.Render(data.Keeps, root.transform, terrainSettings, settings);
      S2MBridgeRenderer.Render(data.Bridges, root.transform, terrainSettings);
      var placed = new GameObject("Placed Buildings");
      placed.transform.SetParent(root.transform, false);
      placed.transform.localPosition = new Vector3(
        terrainSettings.FlipX ? data.Map.MapSize * terrainSettings.HorizontalCellSize : 0f, 0f,
        terrainSettings.FlipZ ? data.Map.MapSize * terrainSettings.HorizontalCellSize : 0f);
      placed.transform.localScale = new Vector3(terrainSettings.FlipX ? -1f : 1f, 1f, terrainSettings.FlipZ ? -1f : 1f);
      var modelSettings = new Granny2ModelRenderSettings
      {
        UnitScale = terrainSettings.HorizontalCellSize / ModelUnitsPerCell, UseFoliageMaterials = false
      };
      foreach (S2MPlacedBuildingBatch batch in data.Batches)
      {
        var instances = new List<Granny2ModelInstance>(batch.Placements.Count);
        foreach (BuildingPlacement placement in batch.Placements) instances.Add(CreateInstance(placement, terrainSettings));
        string name = System.IO.Path.GetFileNameWithoutExtension(batch.Descriptor.RelativePath);
        GameObject closed = Granny2ModelRenderer.RenderInstances(batch.Closed, instances,
          placed.transform, modelSettings, name + " Exteriors");
        if (batch.Open != null)
        {
          GameObject open = Granny2ModelRenderer.RenderInstances(batch.Open, instances,
            placed.transform, modelSettings, name + " Interiors");
          Bounds bounds = Granny2ModelRenderer.GetLocalBounds(batch.Closed, modelSettings.UnitScale);
          for (int i = 0; i < instances.Count; i++)
          {
            Granny2ModelInstance instance = instances[i];
            var view = new GameObject(instance.Name + " View");
            view.transform.SetParent(placed.transform, false);
            view.transform.localPosition = instance.Position;
            view.transform.localRotation = Quaternion.Euler(instance.EulerAngles);
            view.transform.localScale = instance.Scale;
            view.AddComponent<S2MBuildingInteriorView>().Initialize(settings,
              closed.transform.GetChild(i).gameObject, open.transform.GetChild(i).gameObject, null, bounds);
          }
        }
        foreach (Granny2ModelData part in batch.StaticParts)
          Granny2ModelRenderer.RenderInstances(part, instances, placed.transform, modelSettings);
        if (terrainSettings.GenerateMeshColliders)
        {
          if (batch.Collision != null)
          {
            GameObject collision = Granny2ModelRenderer.RenderInstances(batch.Collision, instances,
              placed.transform, modelSettings, name + " Collision");
            foreach (MeshRenderer renderer in collision.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = false;
            foreach (MeshFilter filter in collision.GetComponentsInChildren<MeshFilter>())
              filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
          }
          else
          {
            // A few descriptors have no dedicated collider. Reuse exterior
            // geometry without tying collision lifetime to the visible view.
            for (int i = 0; i < instances.Count; i++)
            {
              var collision = new GameObject(instances[i].Name + " Collision");
              collision.transform.SetParent(placed.transform, false);
              collision.transform.localPosition = instances[i].Position;
              collision.transform.localRotation = Quaternion.Euler(instances[i].EulerAngles);
              collision.transform.localScale = instances[i].Scale;
              foreach (MeshFilter filter in closed.transform.GetChild(i).GetComponentsInChildren<MeshFilter>(true))
                collision.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
          }
        }
      }
      return root;
    }

    internal static Granny2ModelInstance CreateInstance(BuildingPlacement building, S2MTerrainSettings settings) => new(
      $"{building.Type} ({building.Id})",
      new Vector3(building.MapX * settings.HorizontalCellSize, building.RawY * settings.HeightUnitScale,
        building.MapZ * settings.HorizontalCellSize), new Vector3(0f, building.RotationDegrees, 0f),
      new Vector3(1f, settings.HeightUnitScale * 1024f / settings.HorizontalCellSize, 1f));
  }
}
