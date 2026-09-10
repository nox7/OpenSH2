using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>Creates a Unity Terrain from the confirmed primary S2M height plane.</summary>
  public static class S2MTerrainRenderer
  {
    public static Terrain Render(
      S2MFile map,
      Transform parent = null,
      S2MTerrainSettings settings = null,
      string terrainName = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (map.HeightLayer == null)
        throw new InvalidDataException("The loaded S2M map does not contain a decodable HeightLayer.");

      return Create(map.HeightLayer, terrainName, parent, settings);
    }

    internal static Terrain Create(
      HeightLayer heightLayer,
      string terrainName,
      Transform parent = null,
      S2MTerrainSettings settings = null)
    {
      if (heightLayer == null) throw new ArgumentNullException(nameof(heightLayer));
      settings ??= new S2MTerrainSettings();
      ValidateSettings(settings);

      float[,] normalizedHeights = BuildUnityHeights(heightLayer, settings, out float minimum, out float maximum);
      int terrainResolution = normalizedHeights.GetLength(0);
      float scaledHeightRange = Math.Max((maximum - minimum) * settings.HeightUnitScale, 0.0001f);

      var terrainData = new TerrainData
      {
        heightmapResolution = terrainResolution,
        size = new Vector3(
          heightLayer.Width * settings.HorizontalCellSize,
          scaledHeightRange,
          heightLayer.Height * settings.HorizontalCellSize)
      };
      terrainData.SetHeights(0, 0, normalizedHeights);

      GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
      terrainObject.name = string.IsNullOrWhiteSpace(terrainName) ? "S2M Terrain" : $"{terrainName} Terrain";
      if (parent != null) terrainObject.transform.SetParent(parent, worldPositionStays: false);
      terrainObject.transform.localPosition = new Vector3(0f, minimum * settings.HeightUnitScale, 0f);

      Terrain terrain = terrainObject.GetComponent<Terrain>();
      if (settings.Material != null) terrain.materialTemplate = settings.Material;
      return terrain;
    }

    internal static float[,] BuildUnityHeights(
      HeightLayer heightLayer,
      S2MTerrainSettings settings,
      out float minimum,
      out float maximum)
    {
      if (heightLayer.Width <= 0 || heightLayer.Height <= 0)
        throw new InvalidDataException("HeightLayer dimensions must be positive.");
      if (heightLayer.Heights == null || heightLayer.Heights.Length != heightLayer.Width * heightLayer.Height)
        throw new InvalidDataException("HeightLayer value count does not match its dimensions.");

      minimum = float.MaxValue;
      maximum = float.MinValue;
      foreach (float value in heightLayer.Heights)
      {
        if (float.IsNaN(value) || float.IsInfinity(value))
          throw new InvalidDataException("HeightLayer contains a non-finite primary height value.");
        if (value < minimum) minimum = value;
        if (value > maximum) maximum = value;
      }

      float range = maximum - minimum;
      int unityWidth = NextPowerOfTwo(heightLayer.Width) + 1;
      int unityHeight = NextPowerOfTwo(heightLayer.Height) + 1;
      if (unityWidth != unityHeight)
        throw new InvalidDataException("Unity Terrain requires a square height grid.");

      var result = new float[unityHeight, unityWidth];
      for (int z = 0; z < unityHeight; z++)
      {
        // S2M supplies cell samples while TerrainData supplies grid vertices.
        // Duplicate the outermost source row/column instead of interpolating it.
        int sourceZ = Math.Min(z, heightLayer.Height - 1);
        if (settings.FlipZ) sourceZ = heightLayer.Height - 1 - sourceZ;

        for (int x = 0; x < unityWidth; x++)
        {
          int sourceX = Math.Min(x, heightLayer.Width - 1);
          if (settings.FlipX) sourceX = heightLayer.Width - 1 - sourceX;

          float rawHeight = heightLayer.Heights[sourceZ * heightLayer.Width + sourceX];
          result[z, x] = range > 0f ? Mathf.Clamp01((rawHeight - minimum) / range) : 0f;
        }
      }

      return result;
    }

    private static int NextPowerOfTwo(int value)
    {
      int result = 1;
      while (result < value)
      {
        if (result > 4096) throw new InvalidDataException("HeightLayer is too large for Unity TerrainData.");
        result <<= 1;
      }
      return result;
    }

    private static void ValidateSettings(S2MTerrainSettings settings)
    {
      if (settings.HorizontalCellSize <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HorizontalCellSize), "Horizontal cell size must be positive.");
      if (settings.HeightUnitScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.HeightUnitScale), "Height unit scale must be positive.");
    }
  }
}
