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
      settings.AmbientAnimations ??= new S2MBuildingAnimationSettings();
      var root = new GameObject("S2M Buildings");
      if (parent != null) root.transform.SetParent(parent, false);
      S2MSceneryOcclusion.GetOrCreate(parent != null ? parent : root.transform, data.Map);
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
        UnitScale = terrainSettings.HorizontalCellSize / ModelUnitsPerCell, UseFoliageMaterials = false,
        FallbackMaterial = Resources.Load<Material>("StaticOriginalModel"),
        Livery = S2MLiveryContext.Find(parent)
      };
      foreach (S2MPlacedBuildingBatch batch in data.Batches)
      {
        Transform[] buildingRoots = CreateBuildingRoots(batch.Placements, placed.transform, data.Map);
        var modelBatches = new List<GameObject>();
        var instances = new List<Granny2ModelInstance>(batch.Placements.Count);
        foreach (BuildingPlacement placement in batch.Placements) instances.Add(CreateInstance(placement, terrainSettings));
        string name = System.IO.Path.GetFileNameWithoutExtension(batch.Descriptor.RelativePath);
        GameObject closed = Granny2ModelRenderer.RenderInstances(batch.Closed, instances,
          placed.transform, modelSettings, name + " Exteriors");
        modelBatches.Add(closed);
        if (batch.Open != null)
        {
          GameObject open = Granny2ModelRenderer.RenderInstances(batch.Open, instances,
            placed.transform, modelSettings, name + " Interiors");
          modelBatches.Add(open);
          Bounds bounds = Granny2ModelRenderer.GetLocalBounds(batch.Closed, modelSettings.UnitScale);
          for (int i = 0; i < instances.Count; i++)
          {
            Granny2ModelInstance instance = instances[i];
            var view = new GameObject(instance.Name + " View");
            view.transform.SetParent(buildingRoots[i], false);
            view.transform.localPosition = instance.Position;
            view.transform.localRotation = Quaternion.Euler(instance.EulerAngles);
            view.transform.localScale = instance.Scale;
            view.AddComponent<S2MBuildingInteriorView>().Initialize(settings,
              closed.transform.GetChild(i).gameObject, open.transform.GetChild(i).gameObject, null, bounds);
          }
        }
        foreach (Granny2ModelData part in batch.StaticParts)
        {
          var partInstances=new List<Granny2ModelInstance>(instances.Count);
          var partStates=new List<Assets.Code.Stronghold2.MapLoading.S2MGatePartState>(instances.Count);
          for(int i=0;i<instances.Count;i++)
          {
            var state=Assets.Code.Stronghold2.MapLoading.S2MGatePartState.Resolve(data.Map,batch.Placements[i],part);
            var instance=instances[i];partStates.Add(state);
            partInstances.Add(new Granny2ModelInstance(instance.Name,instance.Position+Vector3.up*state.Lift*terrainSettings.HorizontalCellSize,
              instance.EulerAngles,instance.Scale,instance.Sway));
          }
          GameObject rendered = null;
          if (settings.AmbientAnimations.Enabled && batch.AmbientAnimations.TryGetValue(part.SourcePath, out var animation))
          {
            try { rendered = S2MBuildingAmbientAnimation.Render(part, animation, partInstances, batch.Placements,
              placed.transform, modelSettings, settings.AmbientAnimations); }
            catch (Exception e) when (e is NotSupportedException || e is InvalidOperationException)
            { Debug.LogWarning($"Building animation {part.SourcePath}: {e.Message}; using static geometry."); }
          }
          rendered ??= Granny2ModelRenderer.RenderInstances(part, partInstances, placed.transform, modelSettings);
          for(int i=0;i<partStates.Count;i++)if(!partStates[i].Visible)rendered.transform.GetChild(i).gameObject.SetActive(false);
          modelBatches.Add(rendered);
        }
        if (terrainSettings.GenerateMeshColliders)
        {
          if (batch.Collision != null)
          {
            GameObject collision = Granny2ModelRenderer.RenderInstances(batch.Collision, instances,
              placed.transform, modelSettings, name + " Collision");
            modelBatches.Add(collision);
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
              collision.transform.SetParent(buildingRoots[i], false);
              collision.transform.localPosition = instances[i].Position;
              collision.transform.localRotation = Quaternion.Euler(instances[i].EulerAngles);
              collision.transform.localScale = instances[i].Scale;
              foreach (MeshFilter filter in closed.transform.GetChild(i).GetComponentsInChildren<MeshFilter>(true))
                collision.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
          }
        }
        foreach (GameObject modelBatch in modelBatches)
        {
          if (modelSettings.Livery != null) modelSettings.Livery.ApplyBuildings(modelBatch, batch.Placements);
          ParentInstances(modelBatch, buildingRoots);
        }
      }
      return root;
    }

    internal static Transform[] CreateBuildingRoots(IReadOnlyList<BuildingPlacement> placements, Transform parent, S2MFile map)
    {
      var occupancy = S2MSceneryOcclusion.GetOrCreate(parent, map);
      var roots = new Transform[placements.Count];
      for (int i = 0; i < placements.Count; i++)
      {
        BuildingPlacement placement = placements[i];
        var root = new GameObject($"{placement.Type} ({placement.Id}) Building");
        root.transform.SetParent(parent, false);
        roots[i] = root.transform;
        if (S2MBuildingOccupancy.CanResolve(placement)) root.AddComponent<S2MBuildingPresence>().Initialize(occupancy, placement);
      }
      return roots;
    }

    internal static void ParentInstances(GameObject batch, Transform[] roots)
    {
      // Roots have identity transforms in their common map frame. Preserve each
      // instance's placement and shared batch resources while grouping its lifetime.
      for (int i = 0; i < roots.Length; i++) batch.transform.GetChild(0).SetParent(roots[i], false);
    }

    internal static Granny2ModelInstance CreateInstance(BuildingPlacement building, S2MTerrainSettings settings) => new(
      $"{building.Type} ({building.Id})",
      new Vector3(building.MapX * settings.HorizontalCellSize, building.RawY * settings.HeightUnitScale,
        building.MapZ * settings.HorizontalCellSize), new Vector3(0f, building.RotationDegrees, 0f),
      new Vector3(1f, settings.HeightUnitScale * 1024f / settings.HorizontalCellSize, 1f));
  }
}
