using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  public static class S2MKeepRenderer
  {
    // Building meshes use the same exported cell pitch as the modular bridges.
    public const float ModelUnitsPerCell = 204.8f;

    public static GameObject Render(S2MKeepData data, Transform parent = null,
      S2MTerrainSettings terrainSettings = null, S2MBuildingRenderSettings settings = null)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));
      terrainSettings ??= new S2MTerrainSettings();
      settings ??= new S2MBuildingRenderSettings();
      var root = new GameObject("S2M Keeps");
      if (parent != null) root.transform.SetParent(parent, false);
      S2MSceneryOcclusion.GetOrCreate(parent != null ? parent : root.transform, data.Map);
      root.transform.localPosition = new Vector3(
        terrainSettings.FlipX ? data.Map.MapSize * terrainSettings.HorizontalCellSize : 0f, 0f,
        terrainSettings.FlipZ ? data.Map.MapSize * terrainSettings.HorizontalCellSize : 0f);
      root.transform.localScale = new Vector3(terrainSettings.FlipX ? -1f : 1f, 1f, terrainSettings.FlipZ ? -1f : 1f);
      var modelSettings = new Granny2ModelRenderSettings
      {
        UnitScale = terrainSettings.HorizontalCellSize / ModelUnitsPerCell,
        UseFoliageMaterials = false, Livery = S2MLiveryContext.Find(parent)
      };
      foreach (S2MKeepBatch batch in data.Batches)
      {
        Transform[] buildingRoots = S2MBuildingRenderer.CreateBuildingRoots(batch.Keeps, root.transform, data.Map);
        var instances = new List<Granny2ModelInstance>();
        foreach (Keep keep in batch.Keeps) instances.Add(S2MBuildingRenderer.CreateInstance(keep, terrainSettings));
        GameObject closed = Granny2ModelRenderer.RenderInstances(batch.Closed, instances,
          root.transform, modelSettings, $"{batch.Size} Keep Exteriors");
        GameObject open = Granny2ModelRenderer.RenderInstances(batch.Open, instances,
          root.transform, modelSettings, $"{batch.Size} Keep Interiors");
        GameObject tables = Granny2ModelRenderer.RenderInstances(batch.Table, instances,
          root.transform, modelSettings, $"{batch.Size} Keep Tables");
        Bounds bounds = Granny2ModelRenderer.GetLocalBounds(batch.Closed, modelSettings.UnitScale);
        for (int i = 0; i < instances.Count; i++)
        {
          Granny2ModelInstance placement = instances[i];
          var view = new GameObject($"{placement.Name} View");
          view.transform.SetParent(buildingRoots[i], false);
          view.transform.localPosition = placement.Position;
          view.transform.localRotation = Quaternion.Euler(placement.EulerAngles);
          view.transform.localScale = placement.Scale;
          view.AddComponent<S2MBuildingInteriorView>().Initialize(settings,
            closed.transform.GetChild(i).gameObject, open.transform.GetChild(i).gameObject,
            tables.transform.GetChild(i).gameObject, bounds);
        }
        if (terrainSettings.GenerateMeshColliders)
        {
          GameObject collision = Granny2ModelRenderer.RenderInstances(batch.Collision, instances,
            root.transform, modelSettings, $"{batch.Size} Keep Collision");
          foreach (MeshRenderer renderer in collision.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = false;
          foreach (MeshFilter filter in collision.GetComponentsInChildren<MeshFilter>())
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
          S2MBuildingRenderer.ParentInstances(collision, buildingRoots);
        }
        if (modelSettings.Livery != null)
        {
          modelSettings.Livery.ApplyBuildings(closed, batch.Keeps);
          modelSettings.Livery.ApplyBuildings(open, batch.Keeps);
          modelSettings.Livery.ApplyBuildings(tables, batch.Keeps);
        }
        S2MBuildingRenderer.ParentInstances(closed, buildingRoots);
        S2MBuildingRenderer.ParentInstances(open, buildingRoots);
        S2MBuildingRenderer.ParentInstances(tables, buildingRoots);
      }
      if (data.Campfire != null) S2MCampfireRenderer.Render(data, root.transform, terrainSettings);
      return root;
    }

  }
}
