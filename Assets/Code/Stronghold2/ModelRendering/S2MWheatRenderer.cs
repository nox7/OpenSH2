using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Static saved wheat patches. Growth/harvest simulation is separate.</summary>
  internal static class S2MWheatRenderer
  {
    // Original wheat atlas table (0xAB8820), first four floats per stage.
    private static readonly Vector4[] Atlas =
    {
      new(.353f, .1765f, .293f, .1565f), new(.686f, .01f, .293f, .323f),
      new(.02f, .343f, .293f, .323f), new(.353f, .343f, .293f, .323f),
      new(.686f, .5095f, .293f, .1565f), new(.02f, .676f, .293f, .323f)
    };

    internal static void Render(S2MVegetationData data, Transform parent, S2MVegetationRenderSettings settings, float modelScale = 1f)
    {
      if (data.Wheat.Count == 0) return;
      var root = new GameObject("Wheat Patches");
      root.transform.SetParent(parent, false);
      var resources = root.AddComponent<Granny2GeneratedModelResources>();
      var material = new Material(Shader.Find("OpenSH2/Stronghold 2 Foliage")) { name = "Wheat Atlas" };
      resources.Add(material);
      material.SetTexture("_BaseMap", resources.LoadSharedTexture(data.WheatTexturePath));
      material.SetFloat("_Cutoff", settings.Model.AlphaCutoff);
      var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var indices = new List<int>();
      foreach (PlantActor p in data.Wheat)
      {
        Vector4 uv = Atlas[p.Stage];
        float stageScale = p.Stage == 0 || p.Stage == 4 ? .5f : 1f;
        for (int i = 0; i < 4; i++)
        {
          float x = (p.RawX + p.WheatOffsets[i]) / 1024f;
          float z = (p.RawZ + p.WheatOffsets[i + 4]) / 1024f;
          if (settings.FlipX) x = data.Map.MapSize - x;
          if (settings.FlipZ) z = data.Map.MapSize - z;
          var anchor = new Vector3(x * settings.HorizontalCellSize, p.RawY * settings.HeightUnitScale, z * settings.HorizontalCellSize);
          // Crossed alpha cards retain the four saved stalk-cluster positions.
          for (int axis = 0; axis < 2; axis++)
          {
            Vector3 side = (axis == 0 ? Vector3.right : Vector3.forward) * (.425f * modelScale * settings.HorizontalCellSize);
            Vector3 top = Vector3.up * (.625f * modelScale * stageScale * settings.HorizontalCellSize);
            int n = vertices.Count;
            vertices.Add(anchor - side); vertices.Add(anchor + side); vertices.Add(anchor - side + top); vertices.Add(anchor + side + top);
            uvs.Add(new Vector2(uv.x, uv.y + uv.w)); uvs.Add(new Vector2(uv.x + uv.z, uv.y + uv.w));
            uvs.Add(new Vector2(uv.x, uv.y)); uvs.Add(new Vector2(uv.x + uv.z, uv.y));
            indices.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 });
          }
        }
      }
      var mesh = new Mesh { name = "Saved wheat", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
      mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0);
      var colours = new Color[vertices.Count];
      for (int i = 0; i < colours.Length; i++) colours[i] = Color.white;
      mesh.colors = colours; mesh.RecalculateNormals(); mesh.RecalculateBounds(); resources.Add(mesh);
      root.AddComponent<MeshFilter>().sharedMesh = mesh;
      root.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
  }
}
