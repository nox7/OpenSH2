using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>
  /// Builds a chunked terrain mesh from an already loaded S2M map. Each cell owns its
  /// four vertices so HeightLayer plane-0 corner overrides are preserved.
  /// </summary>
  public static class S2MTerrainRenderer
  {
    public static GameObject Render(
      S2MFile map,
      Transform parent = null,
      S2MTerrainSettings settings = null,
      string terrainName = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (map.HeightLayer == null)
        throw new InvalidDataException("The loaded S2M map does not contain a decodable HeightLayer.");

      settings ??= new S2MTerrainSettings();
      Validate(map.HeightLayer, settings);
      byte[] surfaceMaterialIds = S2MTerrainSurfaceMaterialGrid.Create(map);

      var root = new GameObject(string.IsNullOrWhiteSpace(terrainName) ? "S2M Terrain" : terrainName);
      if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

      Material generatedMaterial = null;
      Material material = settings.Material;
      if (material == null)
      {
        material = CreateFallbackMaterial();
        generatedMaterial = material;
      }

      var generatedMeshes = new List<Mesh>();
      for (int z = 0; z < map.HeightLayer.Height; z += settings.CellsPerChunk)
      {
        int chunkHeight = Math.Min(settings.CellsPerChunk, map.HeightLayer.Height - z);
        for (int x = 0; x < map.HeightLayer.Width; x += settings.CellsPerChunk)
        {
          int chunkWidth = Math.Min(settings.CellsPerChunk, map.HeightLayer.Width - x);
          GameObject chunk = CreateChunk(map, surfaceMaterialIds, settings, material, x, z, chunkWidth, chunkHeight, generatedMeshes);
          chunk.transform.SetParent(root.transform, worldPositionStays: false);
        }
      }

      root.AddComponent<S2MGeneratedTerrainResources>().Initialize(generatedMeshes, generatedMaterial);
      return root;
    }

    private static GameObject CreateChunk(
      S2MFile map,
      byte[] surfaceMaterialIds,
      S2MTerrainSettings settings,
      Material material,
      int startX,
      int startZ,
      int chunkWidth,
      int chunkHeight,
      List<Mesh> generatedMeshes)
    {
      int cellCount = checked(chunkWidth * chunkHeight);
      var vertices = new List<Vector3>(cellCount * 4);
      var normals = new List<Vector3>(cellCount * 4);
      var textureCoordinates = new List<Vector4>(cellCount * 4);
      var cellData = new List<Vector4>(cellCount * 4);
      var neighbourMaterialIds = new List<Vector4>(cellCount * 4);
      var neighbourTintWest = new List<Vector4>(cellCount * 4);
      var neighbourTintEast = new List<Vector4>(cellCount * 4);
      var neighbourTintSouth = new List<Vector4>(cellCount * 4);
      var neighbourTintNorth = new List<Vector4>(cellCount * 4);
      var colors = new List<Color32>(cellCount * 4);
      var triangles = new List<int>(cellCount * 6);

      for (int z = startZ; z < startZ + chunkHeight; z++)
      {
        for (int x = startX; x < startX + chunkWidth; x++)
        {
          int cellIndex = z * map.HeightLayer.Width + x;
          if (surfaceMaterialIds[cellIndex] == S2MTerrainSurfaceMaterialGrid.CliffFaceMaterialId
            && TryAddSubdividedCliffCell(
              vertices, normals, textureCoordinates, cellData, neighbourMaterialIds,
              neighbourTintWest, neighbourTintEast, neighbourTintSouth, neighbourTintNorth,
              colors, map, surfaceMaterialIds, settings, cellIndex, x, z, triangles))
          {
            continue;
          }

          int vertexStart = vertices.Count;
          AddVertex(vertices, normals, textureCoordinates, cellData, neighbourMaterialIds, neighbourTintWest, neighbourTintEast, neighbourTintSouth, neighbourTintNorth, colors, map, surfaceMaterialIds, settings, cellIndex, x, z, 2);
          AddVertex(vertices, normals, textureCoordinates, cellData, neighbourMaterialIds, neighbourTintWest, neighbourTintEast, neighbourTintSouth, neighbourTintNorth, colors, map, surfaceMaterialIds, settings, cellIndex, x + 1, z, 3);
          AddVertex(vertices, normals, textureCoordinates, cellData, neighbourMaterialIds, neighbourTintWest, neighbourTintEast, neighbourTintSouth, neighbourTintNorth, colors, map, surfaceMaterialIds, settings, cellIndex, x, z + 1, 0);
          AddVertex(vertices, normals, textureCoordinates, cellData, neighbourMaterialIds, neighbourTintWest, neighbourTintEast, neighbourTintSouth, neighbourTintNorth, colors, map, surfaceMaterialIds, settings, cellIndex, x + 1, z + 1, 1);
          AddCellTriangles(triangles, vertexStart, settings.FlipX ^ settings.FlipZ);
        }
      }

      var mesh = new Mesh
      {
        name = $"S2M Terrain {startX},{startZ}",
        indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
      };
      mesh.SetVertices(vertices);
      mesh.SetNormals(normals);
      mesh.SetUVs(0, textureCoordinates);
      mesh.SetUVs(1, cellData);
      mesh.SetUVs(3, neighbourMaterialIds);
      mesh.SetUVs(4, neighbourTintWest);
      mesh.SetUVs(5, neighbourTintEast);
      mesh.SetUVs(6, neighbourTintSouth);
      mesh.SetUVs(7, neighbourTintNorth);
      mesh.SetColors(colors);
      mesh.SetTriangles(triangles, 0, calculateBounds: true);
      generatedMeshes.Add(mesh);

      var chunk = new GameObject($"Terrain Chunk {startX},{startZ}");
      chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
      chunk.AddComponent<MeshRenderer>().sharedMaterial = material;
      if (settings.GenerateMeshColliders) chunk.AddComponent<MeshCollider>().sharedMesh = mesh;
      return chunk;
    }

    private static void AddVertex(
      List<Vector3> vertices,
      List<Vector3> normals,
      List<Vector4> textureCoordinates,
      List<Vector4> cellData,
      List<Vector4> neighbourMaterialIds,
      List<Vector4> neighbourTintWest,
      List<Vector4> neighbourTintEast,
      List<Vector4> neighbourTintSouth,
      List<Vector4> neighbourTintNorth,
      List<Color32> colors,
      S2MFile map,
      byte[] surfaceMaterialIds,
      S2MTerrainSettings settings,
      int cellIndex,
      int gridX,
      int gridZ,
      int corner)
    {
      HeightLayer heightLayer = map.HeightLayer;
      float x = settings.FlipX ? heightLayer.Width - gridX : gridX;
      float z = settings.FlipZ ? heightLayer.Height - gridZ : gridZ;
      float height = GetBaseCornerHeight(heightLayer, settings, cellIndex, corner, gridX, gridZ);
      vertices.Add(new Vector3(
        x * settings.HorizontalCellSize,
        height * settings.HeightUnitScale,
        z * settings.HorizontalCellSize));
      normals.Add(GetPrimaryNormal(heightLayer, settings, gridX, gridZ));
      float localCellX = corner == 1 || corner == 3 ? 1f : 0f;
      float localCellZ = corner == 0 || corner == 1 ? 1f : 0f;
      textureCoordinates.Add(new Vector4(gridX, gridZ, localCellX, localCellZ));

      byte materialId = surfaceMaterialIds[cellIndex];
      byte featureId = map.WaterLayer?.LandscapeFeatureValues?[cellIndex] ?? 0;
      byte textureRotation = GetTerrainTextureRotation(map.Landscape?.TerrainTextureRotationValues, heightLayer.Width, heightLayer.Height, cellIndex % heightLayer.Width, cellIndex / heightLayer.Width);
      cellData.Add(new Vector4(materialId / 255f, featureId / 255f, textureRotation / 3f, 0f));
      // Neighbour IDs belong to the owning cell, not the vertex coordinate. The
      // right/top vertices are one past the last cell index at map boundaries.
      neighbourMaterialIds.Add(GetNeighbourMaterialIds(surfaceMaterialIds, heightLayer.Width, heightLayer.Height, cellIndex % heightLayer.Width, cellIndex / heightLayer.Width, materialId));
      Color32 terrainTint = map.Landscape?.TerrainTintColors?.Length > cellIndex
        ? map.Landscape.TerrainTintColors[cellIndex]
        : Color.white;
      colors.Add(terrainTint);
      AddNeighbourTerrainTints(
        map.Landscape?.TerrainTintColors,
        map.Landscape?.TerrainTextureRotationValues,
        heightLayer.Width,
        heightLayer.Height,
        cellIndex % heightLayer.Width,
        cellIndex / heightLayer.Width,
        neighbourTintWest,
        neighbourTintEast,
        neighbourTintSouth,
        neighbourTintNorth);
    }

    private static bool TryAddSubdividedCliffCell(
      List<Vector3> vertices,
      List<Vector3> normals,
      List<Vector4> textureCoordinates,
      List<Vector4> cellData,
      List<Vector4> neighbourMaterialIds,
      List<Vector4> neighbourTintWest,
      List<Vector4> neighbourTintEast,
      List<Vector4> neighbourTintSouth,
      List<Vector4> neighbourTintNorth,
      List<Color32> colors,
      S2MFile map,
      byte[] surfaceMaterialIds,
      S2MTerrainSettings settings,
      int cellIndex,
      int x,
      int z,
      List<int> triangles)
    {
      var builder = new CliffCellBuilder(
        vertices, normals, textureCoordinates, cellData, neighbourMaterialIds,
        neighbourTintWest, neighbourTintEast, neighbourTintSouth, neighbourTintNorth,
        colors, map, surfaceMaterialIds, settings, cellIndex, x, z, triangles);
      return builder.Build();
    }

    private readonly struct CliffCellBuilder
    {
      private const float SplitFraction = 0.5f;

      private readonly List<Vector3> vertices;
      private readonly List<Vector3> normals;
      private readonly List<Vector4> textureCoordinates;
      private readonly List<Vector4> cellData;
      private readonly List<Vector4> neighbourMaterialIds;
      private readonly List<Vector4> neighbourTintWest;
      private readonly List<Vector4> neighbourTintEast;
      private readonly List<Vector4> neighbourTintSouth;
      private readonly List<Vector4> neighbourTintNorth;
      private readonly List<Color32> colors;
      private readonly S2MFile map;
      private readonly byte[] surfaceMaterialIds;
      private readonly S2MTerrainSettings settings;
      private readonly int cellIndex;
      private readonly int x;
      private readonly int z;
      private readonly List<int> triangles;
      private readonly bool reverseWinding;

      public CliffCellBuilder(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> textureCoordinates,
        List<Vector4> cellData,
        List<Vector4> neighbourMaterialIds,
        List<Vector4> neighbourTintWest,
        List<Vector4> neighbourTintEast,
        List<Vector4> neighbourTintSouth,
        List<Vector4> neighbourTintNorth,
        List<Color32> colors,
        S2MFile map,
        byte[] surfaceMaterialIds,
        S2MTerrainSettings settings,
        int cellIndex,
        int x,
        int z,
        List<int> triangles)
      {
        this.vertices = vertices;
        this.normals = normals;
        this.textureCoordinates = textureCoordinates;
        this.cellData = cellData;
        this.neighbourMaterialIds = neighbourMaterialIds;
        this.neighbourTintWest = neighbourTintWest;
        this.neighbourTintEast = neighbourTintEast;
        this.neighbourTintSouth = neighbourTintSouth;
        this.neighbourTintNorth = neighbourTintNorth;
        this.colors = colors;
        this.map = map;
        this.surfaceMaterialIds = surfaceMaterialIds;
        this.settings = settings;
        this.cellIndex = cellIndex;
        this.x = x;
        this.z = z;
        this.triangles = triangles;
        reverseWinding = settings.FlipX ^ settings.FlipZ;
      }

      public bool Build()
      {
        HeightLayer heightLayer = map.HeightLayer;
        var corners = new[]
        {
          new Vector3(x, GetBaseCornerHeight(heightLayer, settings, cellIndex, 0, x, z + 1), z + 1),
          new Vector3(x + 1, GetBaseCornerHeight(heightLayer, settings, cellIndex, 1, x + 1, z + 1), z + 1),
          new Vector3(x + 1, GetBaseCornerHeight(heightLayer, settings, cellIndex, 3, x + 1, z), z),
          new Vector3(x, GetBaseCornerHeight(heightLayer, settings, cellIndex, 2, x, z), z)
        };

        float minimum = corners[0].y;
        float maximum = corners[0].y;
        for (int i = 1; i < corners.Length; i++)
        {
          minimum = Math.Min(minimum, corners[i].y);
          maximum = Math.Max(maximum, corners[i].y);
        }
        if (maximum - minimum < S2MTerrainSurfaceMaterialGrid.RockSlopeHeightDifference)
          return false;

        float middle = (minimum + maximum) * 0.5f;
        var high = new bool[corners.Length];
        int highCount = 0;
        int highIndex = -1;
        int lowIndex = -1;
        for (int i = 0; i < corners.Length; i++)
        {
          high[i] = corners[i].y >= middle;
          if (high[i])
          {
            highCount++;
            highIndex = i;
          }
          else
          {
            lowIndex = i;
          }
        }
        if (highCount == 1) return BuildHighCorner(corners, highIndex);
        if (highCount == 3) return BuildLowCorner(corners, lowIndex);
        if (highCount != 2) return false;

        for (int highA = 0; highA < corners.Length; highA++)
        {
          int highB = (highA + 1) % corners.Length;
          if (high[highA] && high[highB]) return BuildSide(corners, highA);
        }
        return false;
      }

      private bool BuildSide(Vector3[] corners, int highAIndex)
      {
        int highBIndex = (highAIndex + 1) % 4;
        int lowBIndex = (highAIndex + 2) % 4;
        int lowAIndex = (highAIndex + 3) % 4;
        Vector3 highA = corners[highAIndex];
        Vector3 highB = corners[highBIndex];
        Vector3 lowB = corners[lowBIndex];
        Vector3 lowA = corners[lowAIndex];
        Vector3 upperA = Split(highA, lowA, highA.y);
        Vector3 upperB = Split(highB, lowB, highB.y);
        Vector3 lowerA = Split(highA, lowA, lowA.y);
        Vector3 lowerB = Split(highB, lowB, lowB.y);

        AddSurfaceQuad(highA, highB, upperB, upperA);
        AddWallQuad(upperA, upperB, lowerB, lowerA);
        AddSurfaceQuad(lowerA, lowerB, lowB, lowA);
        return true;
      }

      private bool BuildHighCorner(Vector3[] corners, int highIndex)
      {
        int nextIndex = (highIndex + 1) % 4;
        int oppositeIndex = (highIndex + 2) % 4;
        int previousIndex = (highIndex + 3) % 4;
        Vector3 high = corners[highIndex];
        Vector3 next = corners[nextIndex];
        Vector3 opposite = corners[oppositeIndex];
        Vector3 previous = corners[previousIndex];
        Vector3 upperNext = Split(high, next, high.y);
        Vector3 upperPrevious = Split(high, previous, high.y);
        Vector3 lowerNext = Split(high, next, next.y);
        Vector3 lowerPrevious = Split(high, previous, previous.y);

        AddSurfaceTriangle(high, upperNext, upperPrevious);
        AddWallTriangle(upperPrevious, upperNext, lowerNext, 0f, 1f, 1f);
        AddWallTriangle(upperPrevious, lowerNext, lowerPrevious, 0f, 1f, 0f);
        AddSurfaceTriangle(lowerNext, next, opposite);
        AddSurfaceTriangle(lowerNext, opposite, previous);
        AddSurfaceTriangle(lowerNext, previous, lowerPrevious);
        return true;
      }

      private bool BuildLowCorner(Vector3[] corners, int lowIndex)
      {
        int nextIndex = (lowIndex + 1) % 4;
        int oppositeIndex = (lowIndex + 2) % 4;
        int previousIndex = (lowIndex + 3) % 4;
        Vector3 low = corners[lowIndex];
        Vector3 next = corners[nextIndex];
        Vector3 opposite = corners[oppositeIndex];
        Vector3 previous = corners[previousIndex];
        Vector3 upperNext = Split(low, next, next.y);
        Vector3 upperPrevious = Split(low, previous, previous.y);
        Vector3 lowerNext = Split(low, next, low.y);
        Vector3 lowerPrevious = Split(low, previous, low.y);

        AddSurfaceTriangle(upperNext, next, opposite);
        AddSurfaceTriangle(upperNext, opposite, previous);
        AddSurfaceTriangle(upperNext, previous, upperPrevious);
        AddWallTriangle(upperNext, upperPrevious, lowerPrevious, 0f, 1f, 1f);
        AddWallTriangle(upperNext, lowerPrevious, lowerNext, 0f, 1f, 0f);
        AddSurfaceTriangle(low, lowerNext, lowerPrevious);
        return true;
      }

      private static Vector3 Split(Vector3 high, Vector3 low, float height)
      {
        Vector3 point = Vector3.Lerp(high, low, SplitFraction);
        point.y = height;
        return point;
      }

      private void AddSurfaceQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
      {
        AddSurfaceTriangle(a, b, c);
        AddSurfaceTriangle(a, c, d);
      }

      private void AddWallQuad(Vector3 upperA, Vector3 upperB, Vector3 lowerB, Vector3 lowerA)
      {
        AddWallTriangle(upperA, upperB, lowerB, 0f, 1f, 1f);
        AddWallTriangle(upperA, lowerB, lowerA, 0f, 1f, 0f);
      }

      private void AddSurfaceTriangle(Vector3 a, Vector3 b, Vector3 c)
      {
        AddTriangle(a, b, c, S2MTerrainSurfaceMaterialGrid.RockSlopeMaterialId, false, 0f, 0f, 0f);
      }

      private void AddWallTriangle(Vector3 a, Vector3 b, Vector3 c, float uvA, float uvB, float uvC)
      {
        AddTriangle(a, b, c, S2MTerrainSurfaceMaterialGrid.CliffFaceMaterialId, true, uvA, uvB, uvC);
      }

      private void AddTriangle(
        Vector3 gridA,
        Vector3 gridB,
        Vector3 gridC,
        byte materialId,
        bool wall,
        float wallUvA,
        float wallUvB,
        float wallUvC)
      {
        Vector3 worldA = ToWorldPosition(gridA);
        Vector3 worldB = ToWorldPosition(gridB);
        Vector3 worldC = ToWorldPosition(gridC);
        Vector3 normal = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;
        if (reverseWinding) normal = -normal;

        int start = vertices.Count;
        AddGeneratedVertex(gridA, worldA, normal, materialId, wall, wallUvA);
        AddGeneratedVertex(gridB, worldB, normal, materialId, wall, wallUvB);
        AddGeneratedVertex(gridC, worldC, normal, materialId, wall, wallUvC);
        triangles.Add(start);
        triangles.Add(reverseWinding ? start + 2 : start + 1);
        triangles.Add(reverseWinding ? start + 1 : start + 2);
      }

      private Vector3 ToWorldPosition(Vector3 gridPoint)
      {
        HeightLayer heightLayer = map.HeightLayer;
        float worldX = (settings.FlipX ? heightLayer.Width - gridPoint.x : gridPoint.x) * settings.HorizontalCellSize;
        float worldZ = (settings.FlipZ ? heightLayer.Height - gridPoint.z : gridPoint.z) * settings.HorizontalCellSize;
        return new Vector3(worldX, gridPoint.y * settings.HeightUnitScale, worldZ);
      }

      private void AddGeneratedVertex(
        Vector3 gridPoint,
        Vector3 worldPoint,
        Vector3 normal,
        byte materialId,
        bool wall,
        float wallU)
      {
        vertices.Add(worldPoint);
        normals.Add(normal);
        Vector2 cellUv = wall ? new Vector2(0.5f, 0.5f) : new Vector2(gridPoint.x - x, gridPoint.z - z);
        Vector2 textureUv = wall
          ? new Vector2(wallU, worldPoint.y / settings.HorizontalCellSize)
          : new Vector2(gridPoint.x, gridPoint.z);
        textureCoordinates.Add(new Vector4(textureUv.x, textureUv.y, cellUv.x, cellUv.y));
        AddCellSurfaceData(
          cellData, neighbourMaterialIds, neighbourTintWest, neighbourTintEast,
          neighbourTintSouth, neighbourTintNorth, colors, map, surfaceMaterialIds,
          cellIndex, x, z, materialId, blendNeighbours: !wall, applyTextureRotation: !wall);
      }
    }

    private static void AddCellSurfaceData(
      List<Vector4> cellData,
      List<Vector4> neighbourMaterialIds,
      List<Vector4> neighbourTintWest,
      List<Vector4> neighbourTintEast,
      List<Vector4> neighbourTintSouth,
      List<Vector4> neighbourTintNorth,
      List<Color32> colors,
      S2MFile map,
      byte[] surfaceMaterialIds,
      int cellIndex,
      int x,
      int z,
      byte materialId,
      bool blendNeighbours,
      bool applyTextureRotation)
    {
      HeightLayer heightLayer = map.HeightLayer;
      byte featureId = map.WaterLayer?.LandscapeFeatureValues?[cellIndex] ?? 0;
      byte textureRotation = applyTextureRotation
        ? GetTerrainTextureRotation(map.Landscape?.TerrainTextureRotationValues, heightLayer.Width, heightLayer.Height, x, z)
        : (byte)0;
      cellData.Add(new Vector4(materialId / 255f, featureId / 255f, textureRotation / 3f, 0f));
      neighbourMaterialIds.Add(blendNeighbours
        ? GetNeighbourMaterialIds(surfaceMaterialIds, heightLayer.Width, heightLayer.Height, x, z, materialId)
        : new Vector4(materialId, materialId, materialId, materialId) / 255f);
      Color32 terrainTint = map.Landscape?.TerrainTintColors?.Length > cellIndex
        ? map.Landscape.TerrainTintColors[cellIndex]
        : Color.white;
      colors.Add(terrainTint);
      AddNeighbourTerrainTints(map.Landscape?.TerrainTintColors, map.Landscape?.TerrainTextureRotationValues, heightLayer.Width, heightLayer.Height, x, z, neighbourTintWest, neighbourTintEast, neighbourTintSouth, neighbourTintNorth);
    }

    private static void AddNeighbourTerrainTints(
      Color32[] tintColors,
      byte[] rotationValues,
      int width,
      int height,
      int x,
      int z,
      List<Vector4> west,
      List<Vector4> east,
      List<Vector4> south,
      List<Vector4> north)
    {
      Color32 baseTint = GetTerrainTint(tintColors, width, height, x, z, Color.white);
      west.Add(ToNormalizedColorWithRotation(GetTerrainTint(tintColors, width, height, x - 1, z, baseTint), GetTerrainTextureRotation(rotationValues, width, height, x - 1, z)));
      east.Add(ToNormalizedColorWithRotation(GetTerrainTint(tintColors, width, height, x + 1, z, baseTint), GetTerrainTextureRotation(rotationValues, width, height, x + 1, z)));
      south.Add(ToNormalizedColorWithRotation(GetTerrainTint(tintColors, width, height, x, z - 1, baseTint), GetTerrainTextureRotation(rotationValues, width, height, x, z - 1)));
      north.Add(ToNormalizedColorWithRotation(GetTerrainTint(tintColors, width, height, x, z + 1, baseTint), GetTerrainTextureRotation(rotationValues, width, height, x, z + 1)));
    }

    private static Color32 GetTerrainTint(Color32[] tintColors, int width, int height, int x, int z, Color32 fallback)
    {
      if (tintColors == null || tintColors.Length < width * height)
        return fallback;
      x = Math.Max(0, Math.Min(x, width - 1));
      z = Math.Max(0, Math.Min(z, height - 1));
      return tintColors[z * width + x];
    }

    private static Vector4 ToNormalizedColorWithRotation(Color32 color, byte rotation)
    {
      return new Vector4(color.r / 255f, color.g / 255f, color.b / 255f, rotation / 3f);
    }

    private static byte GetTerrainTextureRotation(byte[] rotationValues, int width, int height, int x, int z)
    {
      if (rotationValues == null || rotationValues.Length < width * height) return 0;
      x = Math.Max(0, Math.Min(x, width - 1));
      z = Math.Max(0, Math.Min(z, height - 1));
      return (byte)(rotationValues[z * width + x] & 3);
    }

    private static Vector4 GetNeighbourMaterialIds(byte[] materialIds, int width, int height, int x, int z, byte materialId)
    {
      x = Math.Max(0, Math.Min(x, width - 1));
      z = Math.Max(0, Math.Min(z, height - 1));
      byte west = x > 0 ? materialIds[z * width + x - 1] : materialId;
      byte east = x + 1 < width ? materialIds[z * width + x + 1] : materialId;
      byte south = z > 0 ? materialIds[(z - 1) * width + x] : materialId;
      byte north = z + 1 < height ? materialIds[(z + 1) * width + x] : materialId;
      return new Vector4(west, east, south, north) / 255f;
    }

    private static float GetBaseCornerHeight(
      HeightLayer heightLayer,
      S2MTerrainSettings settings,
      int cellIndex,
      int corner,
      int gridX,
      int gridZ)
    {
      if (settings.UsePerCellCornerHeights
        && heightLayer.CornerHeightPlanes?.Length > 0
        && heightLayer.CornerHeightPlanes[0]?.Length == heightLayer.Width * heightLayer.Height * HeightLayer.CornersPerCell)
      {
        return heightLayer.GetCornerHeight(0, cellIndex, corner);
      }

      int sourceX = Math.Min(gridX, heightLayer.Width - 1);
      int sourceZ = Math.Min(gridZ, heightLayer.Height - 1);
      return heightLayer.Heights[sourceZ * heightLayer.Width + sourceX];
    }

    private static Vector3 GetPrimaryNormal(
      HeightLayer heightLayer,
      S2MTerrainSettings settings,
      int gridX,
      int gridZ)
    {
      float left = GetPrimaryHeight(heightLayer, gridX - 1, gridZ);
      float right = GetPrimaryHeight(heightLayer, gridX + 1, gridZ);
      float down = GetPrimaryHeight(heightLayer, gridX, gridZ - 1);
      float up = GetPrimaryHeight(heightLayer, gridX, gridZ + 1);
      float horizontalDistance = 2f * settings.HorizontalCellSize;
      float slopeX = (right - left) * settings.HeightUnitScale / horizontalDistance;
      float slopeZ = (up - down) * settings.HeightUnitScale / horizontalDistance;
      var normal = new Vector3(-slopeX, 1f, -slopeZ).normalized;
      if (settings.FlipX) normal.x = -normal.x;
      if (settings.FlipZ) normal.z = -normal.z;
      return normal;
    }

    private static float GetPrimaryHeight(HeightLayer heightLayer, int gridX, int gridZ)
    {
      int x = Mathf.Clamp(gridX, 0, heightLayer.Width - 1);
      int z = Mathf.Clamp(gridZ, 0, heightLayer.Height - 1);
      return heightLayer.Heights[z * heightLayer.Width + x];
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

    private static Material CreateFallbackMaterial()
    {
      Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
      if (shader == null) throw new InvalidOperationException("No fallback terrain shader is available.");
      var material = new Material(shader) { name = "S2M Terrain Fallback" };
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.35f, 0.5f, 0.24f));
      else material.color = new Color(0.35f, 0.5f, 0.24f);
      return material;
    }

    private static void Validate(HeightLayer heightLayer, S2MTerrainSettings settings)
    {
      if (heightLayer.Width <= 0 || heightLayer.Height <= 0)
        throw new InvalidDataException("HeightLayer dimensions must be positive.");
      if (heightLayer.Heights == null || heightLayer.Heights.Length != heightLayer.Width * heightLayer.Height)
        throw new InvalidDataException("HeightLayer value count does not match its dimensions.");
      if (settings.HorizontalCellSize <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HorizontalCellSize), "Horizontal cell size must be positive.");
      if (settings.HeightUnitScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HeightUnitScale), "Height unit scale must be positive.");
      if (settings.CellsPerChunk <= 0)
        throw new ArgumentOutOfRangeException(nameof(settings.CellsPerChunk), "CellsPerChunk must be positive.");
    }
  }

  internal sealed class S2MGeneratedTerrainResources : MonoBehaviour
  {
    private IReadOnlyList<Mesh> GeneratedMeshes { get; set; }
    private Material GeneratedMaterial { get; set; }

    public void Initialize(IReadOnlyList<Mesh> meshes, Material material)
    {
      GeneratedMeshes = meshes;
      GeneratedMaterial = material;
    }

    private void OnDestroy()
    {
      if (GeneratedMeshes != null)
      {
        foreach (Mesh mesh in GeneratedMeshes)
        {
          if (mesh == null) continue;
          if (Application.isPlaying) Destroy(mesh);
          else DestroyImmediate(mesh);
        }
      }
      if (GeneratedMaterial == null) return;
      if (Application.isPlaying) Destroy(GeneratedMaterial);
      else DestroyImmediate(GeneratedMaterial);
    }
  }
}
