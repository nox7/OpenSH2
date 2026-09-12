using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Loads only the terrain DDS files referenced by an already parsed S2M map.</summary>
  public sealed class S2MTerrainTextureAssetLoader
  {
    private readonly Dictionary<string, Texture2D> textureCache = new(StringComparer.OrdinalIgnoreCase);

    public S2MTerrainTextureData Load(S2MFile map, string gameInstallPath)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (map.WaterLayer?.SurfaceMaterialIds == null)
        throw new InvalidDataException("The loaded map does not contain Landscape surface-material IDs.");
      if (string.IsNullOrWhiteSpace(gameInstallPath))
        throw new ArgumentException("A Stronghold 2 installation path is required.", nameof(gameInstallPath));

      var textures = new Dictionary<byte, Texture2D>();
      var unsupported = new HashSet<byte>();
      foreach (byte materialId in S2MTerrainSurfaceMaterialGrid.Create(map))
      {
        if (textures.ContainsKey(materialId) || unsupported.Contains(materialId)) continue;
        if (!S2MTerrainTextureAssetCatalog.TryGetAssetPath(materialId, out string relativePath))
        {
          unsupported.Add(materialId);
          continue;
        }

        string texturePath = ResolveFile(gameInstallPath, relativePath);
        if (!textureCache.TryGetValue(texturePath, out Texture2D texture))
        {
          // The terrain renderer packs heterogeneous DDS dimensions into one Texture2DArray.
          // Retain CPU pixels so it can resample each decoded source texture once.
          texture = DdsTextureLoader.Load(texturePath, keepReadable: true);
          textureCache.Add(texturePath, texture);
        }
        textures.Add(materialId, texture);
      }

      return new S2MTerrainTextureData
      {
        Map = map,
        Textures = textures,
        UnsupportedMaterialIds = unsupported
      };
    }

    private static string ResolveFile(string gameInstallPath, string relativePath)
    {
      string directPath = Path.Combine(gameInstallPath, relativePath);
      if (File.Exists(directPath)) return directPath;

      string directory = Path.GetDirectoryName(directPath);
      string fileName = Path.GetFileName(directPath);
      if (Directory.Exists(directory))
      {
        foreach (string candidate in Directory.EnumerateFiles(directory))
        {
          if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase)) return candidate;
        }
      }
      throw new FileNotFoundException($"Required Stronghold 2 terrain texture was not found: {relativePath}", directPath);
    }
  }
}
