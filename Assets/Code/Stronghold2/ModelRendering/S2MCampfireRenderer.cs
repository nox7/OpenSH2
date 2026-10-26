using System.Collections.Generic;
using System.IO;
using Assets.Code.Simulation;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Original campfire geometry with crossed animated fire-texture cards.</summary>
  public sealed class S2MCampfireRenderer : MonoBehaviour
  {
    public SimulationClock Clock { get; set; }
    private Material material;
    private Texture2D texture;
    private Mesh mesh;
    private float time;

    public static S2MCampfireRenderer Render(S2MKeepData data, Transform parent, S2MTerrainSettings terrain)
    {
      var root = new GameObject("Keep Campfires"); root.transform.SetParent(parent, false);
      var component = root.AddComponent<S2MCampfireRenderer>();
      var instances = new List<Granny2ModelInstance>();
      var centers = new List<Vector3>();
      foreach (var camp in S2MCampfireLayout.Create(data.Map))
      {
        var position = new Vector3(camp.ModelOrigin.x * terrain.HorizontalCellSize,
          camp.RawHeight * terrain.HeightUnitScale,
          camp.ModelOrigin.y * terrain.HorizontalCellSize);
        centers.Add(position + new Vector3(.5f, .2f, .5f) * terrain.HorizontalCellSize);
        instances.Add(new Granny2ModelInstance("Campfire " + camp.Keep.Id, position, Vector3.zero, Vector3.one));
      }
      Granny2ModelRenderer.RenderInstances(data.Campfire, instances, root.transform,
        new Granny2ModelRenderSettings { UnitScale = terrain.HorizontalCellSize / S2MKeepRenderer.ModelUnitsPerCell,
          UseFoliageMaterials = false }, "Campfire Bases");
      component.texture = TGALoader.LoadTGA(Path.Combine(data.GameInstallPath, "meshes/misc/fire_orange.tga"));
      component.texture.wrapMode = TextureWrapMode.Repeat;
      component.material = new Material(Resources.Load<Shader>("Campfire"));
      component.material.mainTexture = component.texture;
      component.mesh = new Mesh { name = "Campfire flame cards" };
      component.mesh.vertices = new[] { new Vector3(-.45f,0,0), new Vector3(.45f,0,0), new Vector3(.45f,1.2f,0), new Vector3(-.45f,1.2f,0),
        new Vector3(0,0,-.45f),new Vector3(0,0,.45f),new Vector3(0,1.2f,.45f),new Vector3(0,1.2f,-.45f) };
      component.mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
      component.mesh.triangles = new[] { 0,1,2,0,2,3,4,5,6,4,6,7 }; component.mesh.RecalculateBounds();
      foreach (var center in centers)
      {
        var card = new GameObject("Animated Fire"); card.transform.SetParent(root.transform, false);
        card.transform.localPosition = center + Vector3.up * .08f * terrain.HorizontalCellSize;
        card.transform.localScale = Vector3.one * terrain.HorizontalCellSize;
        card.AddComponent<MeshFilter>().sharedMesh = component.mesh;
        var renderer = card.AddComponent<MeshRenderer>(); renderer.sharedMaterial = component.material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
      }
      return component;
    }

    private void LateUpdate()
    {
      time += Clock?.DeltaTime ?? Time.deltaTime;
      if (material != null) material.SetFloat("_FireTime", time);
    }

    private void OnDestroy()
    {
      Release(material); Release(texture); Release(mesh);
    }
    private static void Release(Object value)
    { if (value != null) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
  }
}
