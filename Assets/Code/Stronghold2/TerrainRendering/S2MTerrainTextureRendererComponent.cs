using Assets.Code.Stronghold2.AssetLoading;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>Applies loaded terrain textures to an S2MTerrainRendererComponent's mesh chunks.</summary>
  public sealed class S2MTerrainTextureRendererComponent : MonoBehaviour
  {
    [SerializeField] private S2MTerrainRendererComponent terrainRenderer;
    [SerializeField] private S2MTerrainTextureLoaderComponent textureLoader;
    [SerializeField] private bool renderWhenReady = true;
    [SerializeField] private S2MTerrainTextureRenderSettings settings = new();

    private void OnEnable()
    {
      if (terrainRenderer != null) terrainRenderer.TerrainRendered += OnTerrainRendered;
      if (textureLoader != null) textureLoader.TerrainTexturesLoaded += OnTerrainTexturesLoaded;
    }

    private void Start()
    {
      if (renderWhenReady) TryApply();
    }

    private void OnDisable()
    {
      if (terrainRenderer != null) terrainRenderer.TerrainRendered -= OnTerrainRendered;
      if (textureLoader != null) textureLoader.TerrainTexturesLoaded -= OnTerrainTexturesLoaded;
    }

    [ContextMenu("Apply Loaded S2M Terrain Textures")]
    public void Apply()
    {
      if (!TryApply())
        throw new InvalidOperationException("Load both the S2M terrain mesh and its terrain textures before applying them.");
    }

    private void OnTerrainRendered(GameObject _) { if (renderWhenReady) TryApply(); }
    private void OnTerrainTexturesLoaded(S2MTerrainTextureData _) { if (renderWhenReady) TryApply(); }

    private bool TryApply()
    {
      if (terrainRenderer?.RenderedTerrain == null || textureLoader?.LoadedTerrainTextures == null) return false;
      if (terrainRenderer.RenderedMap != textureLoader.LoadedTerrainTextures.Map) return false;
      S2MTerrainTextureRenderer.Apply(terrainRenderer.RenderedTerrain, textureLoader.LoadedTerrainTextures, settings);
      return true;
    }
  }
}
