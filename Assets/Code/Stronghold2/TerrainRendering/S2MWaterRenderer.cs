using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>Creates a water-surface mesh from an already loaded S2M map.</summary>
  public static class S2MWaterRenderer
  {
    public static GameObject Render(
      S2MFile map,
      Transform parent = null,
      S2MWaterSettings settings = null,
      string waterName = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (map.WaterLayer == null)
        throw new InvalidDataException("The loaded S2M map does not contain a decodable Landscape water layer.");

      settings ??= new S2MWaterSettings();
      Validate(map, settings);

      WaterLayer water = map.WaterLayer;
      HeightLayer heights = map.HeightLayer;
      var vertices = new List<Vector3>();
      var textureCoordinates = new List<Vector2>();
      var flowVectors = new List<Vector2>();
      var cellData = new List<Color32>();
      var riverTriangles = new List<int>();
      var seaTriangles = new List<int>();
      var pitchSwampTriangles = new List<int>();
      var moatTriangles = new List<int>();

      for (int z = 0; z < water.Height; z++)
      {
        for (int x = 0; x < water.Width; x++)
        {
          S2MWaterType waterType = water.GetWaterType(x, z);
          if (!water.HasWaterLikeSurface(x, z)) continue;

          int cellIndex = z * water.Width + x;
          int heightPlane = waterType == S2MWaterType.Moat
            ? settings.MoatHeightPlane
            : settings.SurfaceHeightPlane;
          int vertexStart = vertices.Count;
          AddVertex(vertices, heights, settings, heightPlane, cellIndex, x, z, corner: 2);
          AddVertex(vertices, heights, settings, heightPlane, cellIndex, x + 1, z, corner: 3);
          AddVertex(vertices, heights, settings, heightPlane, cellIndex, x, z + 1, corner: 0);
          AddVertex(vertices, heights, settings, heightPlane, cellIndex, x + 1, z + 1, corner: 1);

          AddTextureCoordinates(textureCoordinates, x, z, settings.TextureScale);

          float angle = (water.GetFlowAngleDegrees(x, z) + settings.FlowAngleOffsetDegrees) * Mathf.Deg2Rad;
          var flow = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
          if (settings.FlipX) flow.x = -flow.x;
          if (settings.FlipZ) flow.y = -flow.y;
          for (int i = 0; i < 4; i++) flowVectors.Add(flow);

          byte rawWater = water.GetRawLandscapeFeatureValue(x, z);
          byte materialId = water.SurfaceMaterialIds[cellIndex];
          byte direction = water.GetFlowDirection(x, z);
          var encodedCell = new Color32(rawWater, materialId, direction, 255);
          for (int i = 0; i < 4; i++) cellData.Add(encodedCell);

          List<int> triangles = waterType switch
          {
            S2MWaterType.River => riverTriangles,
            S2MWaterType.Sea => seaTriangles,
            S2MWaterType.PitchSwamp => pitchSwampTriangles,
            S2MWaterType.Moat => moatTriangles,
            _ => throw new InvalidDataException($"Unexpected renderable landscape surface type {waterType}.")
          };
          AddCellTriangles(triangles, vertexStart, settings.FlipX ^ settings.FlipZ);
        }
      }

      var mesh = new Mesh
      {
        name = "S2M Water Mesh",
        indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
      };
      mesh.SetVertices(vertices);
      mesh.SetUVs(0, textureCoordinates);
      mesh.SetUVs(1, flowVectors);
      mesh.SetColors(cellData);
      mesh.subMeshCount = 4;
      mesh.SetTriangles(riverTriangles, 0);
      mesh.SetTriangles(seaTriangles, 1);
      mesh.SetTriangles(pitchSwampTriangles, 2);
      mesh.SetTriangles(moatTriangles, 3);
      mesh.RecalculateNormals();
      mesh.RecalculateBounds();

      var waterObject = new GameObject(string.IsNullOrWhiteSpace(waterName) ? "S2M Water" : waterName);
      if (parent != null) waterObject.transform.SetParent(parent, worldPositionStays: false);
      waterObject.AddComponent<MeshFilter>().sharedMesh = mesh;
      MeshRenderer meshRenderer = waterObject.AddComponent<MeshRenderer>();

      var generatedMaterials = new List<Material>();
      meshRenderer.sharedMaterials = new[]
      {
        GetMaterial(settings.RiverMaterial, "S2M River", new Color(0.12f, 0.42f, 0.58f), generatedMaterials),
        GetMaterial(settings.SeaMaterial, "S2M Sea", new Color(0.08f, 0.28f, 0.52f), generatedMaterials),
        GetMaterial(settings.PitchSwampMaterial, "S2M Pitch Swamp", new Color(0.18f, 0.12f, 0.05f), generatedMaterials),
        GetMaterial(settings.MoatMaterial, "S2M Moat", new Color(0.05f, 0.24f, 0.12f), generatedMaterials)
      };

      waterObject.AddComponent<S2MGeneratedWaterResources>().Initialize(mesh, generatedMaterials);
      return waterObject;
    }

    private static void AddVertex(
      List<Vector3> vertices,
      HeightLayer heights,
      S2MWaterSettings settings,
      int heightPlane,
      int cellIndex,
      int gridX,
      int gridZ,
      int corner)
    {
      float x = settings.FlipX ? heights.Width - gridX : gridX;
      float z = settings.FlipZ ? heights.Height - gridZ : gridZ;
      float y = heights.GetCornerHeight(heightPlane, cellIndex, corner)
        * settings.HeightUnitScale + settings.SurfaceOffset;
      vertices.Add(new Vector3(x * settings.HorizontalCellSize, y, z * settings.HorizontalCellSize));
    }

    private static void AddTextureCoordinates(List<Vector2> coordinates, int x, int z, float scale)
    {
      coordinates.Add(new Vector2(x, z) * scale);
      coordinates.Add(new Vector2(x + 1, z) * scale);
      coordinates.Add(new Vector2(x, z + 1) * scale);
      coordinates.Add(new Vector2(x + 1, z + 1) * scale);
    }

    private static void AddCellTriangles(List<int> triangles, int start, bool reverseWinding)
    {
      if (!reverseWinding)
      {
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
      }
      else
      {
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
      }
    }

    private static Material GetMaterial(
      Material configuredMaterial,
      string name,
      Color color,
      List<Material> generatedMaterials)
    {
      if (configuredMaterial != null) return configuredMaterial;

      Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
      if (shader == null) throw new InvalidOperationException("No fallback water shader is available.");

      var material = new Material(shader) { name = name };
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
      else material.color = color;
      generatedMaterials.Add(material);
      return material;
    }

    private static void Validate(S2MFile map, S2MWaterSettings settings)
    {
      if (settings.HorizontalCellSize <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HorizontalCellSize));
      if (settings.HeightUnitScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HeightUnitScale));
      if (settings.TextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.TextureScale));
      if (map.HeightLayer?.CornerHeightPlanes == null
        || settings.SurfaceHeightPlane < 0
        || settings.SurfaceHeightPlane >= map.HeightLayer.CornerHeightPlanes.Length)
        throw new InvalidDataException("The selected water-surface corner-height plane is unavailable.");
      if (settings.MoatHeightPlane < 0 || settings.MoatHeightPlane >= map.HeightLayer.CornerHeightPlanes.Length)
        throw new InvalidDataException("The selected moat corner-height plane is unavailable.");
      if (map.WaterLayer.Width != map.HeightLayer.Width || map.WaterLayer.Height != map.HeightLayer.Height)
        throw new InvalidDataException("Landscape and HeightLayer dimensions do not match.");
    }
  }

  internal sealed class S2MGeneratedWaterResources : MonoBehaviour
  {
    private Mesh GeneratedMesh { get; set; }
    private IReadOnlyList<Material> GeneratedMaterials { get; set; }

    public void Initialize(Mesh mesh, IReadOnlyList<Material> materials)
    {
      GeneratedMesh = mesh;
      GeneratedMaterials = materials;
    }

    private void OnDestroy()
    {
      DestroyGenerated(GeneratedMesh);
      if (GeneratedMaterials == null) return;
      foreach (Material material in GeneratedMaterials) DestroyGenerated(material);
    }

    private static void DestroyGenerated(UnityEngine.Object generatedObject)
    {
      if (generatedObject == null) return;
      if (Application.isPlaying) Destroy(generatedObject);
      else DestroyImmediate(generatedObject);
    }
  }
}
