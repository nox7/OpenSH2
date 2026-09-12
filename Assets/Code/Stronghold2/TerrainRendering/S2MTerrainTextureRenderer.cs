using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>Applies already loaded terrain DDS assets to an already generated terrain mesh.</summary>
  public static class S2MTerrainTextureRenderer
  {
    private const int FirstMaterialId = 0x08;
    private const int CliffFaceMaterialId = 0x27;
    private const int MaterialCount = 25;

    public static void Apply(
      GameObject terrainRoot,
      S2MTerrainTextureData terrainTextures,
      S2MTerrainTextureRenderSettings settings = null)
    {
      if (terrainRoot == null) throw new ArgumentNullException(nameof(terrainRoot));
      if (terrainTextures == null) throw new ArgumentNullException(nameof(terrainTextures));
      settings ??= new S2MTerrainTextureRenderSettings();
      if (settings.TextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.TextureScale), "TextureScale must be positive.");
      if (settings.CliffWallVerticalTextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.CliffWallVerticalTextureScale), "CliffWallVerticalTextureScale must be positive.");
      if (settings.CliffWallHorizontalTextureScale <= 0f)
        throw new ArgumentOutOfRangeException(nameof(settings.CliffWallHorizontalTextureScale), "CliffWallHorizontalTextureScale must be positive.");
      if (settings.AdjacentMaterialBlendWidth < 0f || settings.AdjacentMaterialBlendWidth > 0.5f)
        throw new ArgumentOutOfRangeException(nameof(settings.AdjacentMaterialBlendWidth), "Adjacent material blend width must be in the range 0 through 0.5.");

      Material generatedMaterial = null;
      Material material = settings.Material;
      if (material == null)
      {
        Shader shader = Shader.Find("OpenSH2/Stronghold 2 Terrain");
        if (shader == null) throw new InvalidOperationException("The OpenSH2 terrain shader was not found.");
        material = new Material(shader) { name = "S2M Terrain Material" };
        generatedMaterial = material;
      }

      if (!material.HasProperty("_TerrainTextureArray"))
        throw new InvalidOperationException("The terrain material does not support the OpenSH2 terrain texture array.");
      if (material.HasProperty("_TextureScale")) material.SetFloat("_TextureScale", settings.TextureScale);
      if (material.HasProperty("_CliffWallVerticalTextureScale")) material.SetFloat("_CliffWallVerticalTextureScale", settings.CliffWallVerticalTextureScale);
      if (material.HasProperty("_CliffWallHorizontalTextureScale")) material.SetFloat("_CliffWallHorizontalTextureScale", settings.CliffWallHorizontalTextureScale);
      if (material.HasProperty("_AdjacentMaterialBlendWidth")) material.SetFloat("_AdjacentMaterialBlendWidth", settings.AdjacentMaterialBlendWidth);
      if (material.HasProperty("_ApplySerializedTextureRotation")) material.SetFloat("_ApplySerializedTextureRotation", settings.ApplySerializedTextureRotation ? 1f : 0f);
      Texture2DArray textureArray = CreateTextureArray(terrainTextures.Textures);
      material.SetTexture("_TerrainTextureArray", textureArray);

      foreach (MeshRenderer renderer in terrainRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        renderer.sharedMaterial = material;

      S2MGeneratedTerrainTextureResources resources = terrainRoot.GetComponent<S2MGeneratedTerrainTextureResources>();
      if (resources == null) resources = terrainRoot.AddComponent<S2MGeneratedTerrainTextureResources>();
      resources.ReplaceGeneratedResources(generatedMaterial, textureArray);
    }

    private static Texture2DArray CreateTextureArray(IReadOnlyDictionary<byte, Texture2D> textures)
    {
      int width = 1;
      int height = 1;
      foreach (Texture2D texture in textures.Values)
      {
        width = Math.Max(width, texture.width);
        height = Math.Max(height, texture.height);
      }

      var array = new Texture2DArray(width, height, MaterialCount, TextureFormat.RGBA32, mipChain: true)
      {
        name = "S2M Terrain Textures",
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Repeat
      };
      for (int slice = 0; slice < MaterialCount; slice++)
      {
        byte materialId = slice < 24 ? (byte)(FirstMaterialId + slice) : (byte)CliffFaceMaterialId;
        if (textures.TryGetValue(materialId, out Texture2D texture))
          array.SetPixels32(Resample(texture, width, height), slice);
        else
          array.SetPixels32(CreateFallbackPixels(width, height), slice);
      }
      array.Apply(updateMipmaps: true, makeNoLongerReadable: false);
      return array;
    }

    private static Color32[] Resample(Texture2D source, int width, int height)
    {
      Color32[] sourcePixels = source.GetPixels32();
      if (source.width == width && source.height == height) return sourcePixels;

      var result = new Color32[width * height];
      for (int y = 0; y < height; y++)
      {
        int sourceY = y * source.height / height;
        for (int x = 0; x < width; x++)
        {
          int sourceX = x * source.width / width;
          result[y * width + x] = sourcePixels[sourceY * source.width + sourceX];
        }
      }
      return result;
    }

    private static Color32[] CreateFallbackPixels(int width, int height)
    {
      var pixels = new Color32[width * height];
      for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
      return pixels;
    }
  }

  internal sealed class S2MGeneratedTerrainTextureResources : MonoBehaviour
  {
    private Material GeneratedMaterial { get; set; }
    private Texture2DArray GeneratedTextureArray { get; set; }

    public void ReplaceGeneratedResources(Material material, Texture2DArray textureArray)
    {
      if (GeneratedMaterial != null && GeneratedMaterial != material)
      {
        if (Application.isPlaying) Destroy(GeneratedMaterial);
        else DestroyImmediate(GeneratedMaterial);
      }
      GeneratedMaterial = material;
      if (GeneratedTextureArray != null && GeneratedTextureArray != textureArray)
      {
        if (Application.isPlaying) Destroy(GeneratedTextureArray);
        else DestroyImmediate(GeneratedTextureArray);
      }
      GeneratedTextureArray = textureArray;
    }

    private void OnDestroy()
    {
      if (GeneratedMaterial != null)
      {
        if (Application.isPlaying) Destroy(GeneratedMaterial);
        else DestroyImmediate(GeneratedMaterial);
      }
      if (GeneratedTextureArray != null)
      {
        if (Application.isPlaying) Destroy(GeneratedTextureArray);
        else DestroyImmediate(GeneratedTextureArray);
      }
    }
  }
}
