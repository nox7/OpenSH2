using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Chunked grass cards expanded toward the active camera by the shader.</summary>
  internal static class S2MGrassRenderer
  {
    internal static void Render(S2MVegetationData data, Transform parent, S2MVegetationRenderSettings settings, float modelScale = 1f)
    {
      if (data.Grass.Count == 0) return;
      if (!(settings.GrassSize > 0f) || float.IsInfinity(settings.GrassSize))
        throw new ArgumentOutOfRangeException(nameof(settings.GrassSize));
      Shader shader = Shader.Find("OpenSH2/Stronghold 2 Grass");
      if (shader == null) throw new InvalidOperationException("Stronghold 2 grass shader is missing.");
      var root = new GameObject("Grass Billboards");
      root.transform.SetParent(parent, false);
      var resources = root.AddComponent<Granny2GeneratedModelResources>();
      var material = new Material(shader) { name = "S2M Grass Atlas" };
      resources.Add(material);
      material.SetTexture("_BaseMap", resources.LoadSharedTexture(data.GrassTexturePath));
      material.SetFloat("_Cutoff", settings.GrassAlphaCutoff);
      var chunks = new Dictionary<Vector2Int, List<SceneryInstance>>();
      foreach (SceneryInstance p in data.Grass)
      {
        var key = new Vector2Int(Mathf.FloorToInt(p.MapX / 16f), Mathf.FloorToInt(p.MapZ / 16f));
        if (!chunks.TryGetValue(key, out var list)) chunks.Add(key, list = new List<SceneryInstance>());
        list.Add(p);
      }
      foreach (var pair in chunks)
      {
        int count = pair.Value.Count;
        var vertices = new Vector3[count * 4];
        var offsets = new Vector2[count * 4];
        var uvs = new Vector2[count * 4];
        var indices = new int[count * 6];
        Bounds bounds = new();
        for (int i = 0; i < count; i++)
        {
          SceneryInstance p = pair.Value[i];
          float x = settings.FlipX ? data.Map.MapSize - p.MapX : p.MapX;
          float z = settings.FlipZ ? data.Map.MapSize - p.MapZ : p.MapZ;
          var anchor = new Vector3(x * settings.HorizontalCellSize, p.RawY * settings.HeightUnitScale,
            z * settings.HorizontalCellSize);
          float size = settings.GrassSize * modelScale * settings.HorizontalCellSize * (settings.ApplyAppearanceScale ? p.AppearanceScale : 1f);
          // Original submission carries origin, extent and a bottom-center UV.
          // The +1/8 field is the center of a 1/4-wide tile, not its right edge.
          float u = (p.Family % 4) * 0.25f, v = (p.Family / 4) * 0.25f;
          int n = i * 4;
          for (int c = 0; c < 4; c++) vertices[n + c] = anchor;
          offsets[n] = new Vector2(-size * 0.5f, 0f);
          offsets[n + 1] = new Vector2(size * 0.5f, 0f);
          offsets[n + 2] = new Vector2(-size * 0.5f, size);
          offsets[n + 3] = new Vector2(size * 0.5f, size);
          // DDS row zero remains at V=0 in DdsTextureLoader, as for the GR2 meshes.
          uvs[n] = new Vector2(u, v + 0.25f); uvs[n + 1] = new Vector2(u + 0.25f, v + 0.25f);
          uvs[n + 2] = new Vector2(u, v); uvs[n + 3] = new Vector2(u + 0.25f, v);
          int t = i * 6;
          indices[t] = n; indices[t + 1] = n + 2; indices[t + 2] = n + 1;
          indices[t + 3] = n + 1; indices[t + 4] = n + 2; indices[t + 5] = n + 3;
          var cardBounds = new Bounds(anchor + Vector3.up * size * 0.5f, new Vector3(size, size, size));
          if (i == 0) bounds = cardBounds; else bounds.Encapsulate(cardBounds);
        }
        var mesh = new Mesh { name = $"Grass {pair.Key}", indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.vertices = vertices; mesh.uv = uvs; mesh.uv2 = offsets; mesh.triangles = indices;
        mesh.bounds = bounds; // Center vertices alone would have zero-size cards and incorrect culling.
        resources.Add(mesh);
        var chunk = new GameObject(mesh.name);
        chunk.transform.SetParent(root.transform, false);
        chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = chunk.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
      }
    }
  }
}
