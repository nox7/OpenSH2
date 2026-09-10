using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>Renders terrain from an S2MMapLoader's already parsed map.</summary>
  public sealed class S2MTerrainRendererComponent : MonoBehaviour
  {
    [SerializeField]
    private S2MMapLoader mapLoader;

    [Tooltip("Render whenever the referenced map loader finishes loading a map.")]
    [SerializeField]
    private bool renderWhenMapLoaded = true;

    [SerializeField]
    private S2MTerrainSettings settings = new();

    public Terrain RenderedTerrain { get; private set; }
    private S2MFile RenderedMap { get; set; }

    private void OnEnable()
    {
      if (mapLoader != null) mapLoader.MapLoaded += OnMapLoaded;
    }

    private void Start()
    {
      if (renderWhenMapLoaded && mapLoader != null && mapLoader.IsLoaded && RenderedMap != mapLoader.LoadedMap)
        RenderTerrain(mapLoader.LoadedMap);
    }

    private void OnDisable()
    {
      if (mapLoader != null) mapLoader.MapLoaded -= OnMapLoaded;
    }

    [ContextMenu("Render Loaded S2M Terrain")]
    public void RenderTerrain()
    {
      if (mapLoader == null)
        throw new InvalidOperationException("Assign an S2MMapLoader before rendering terrain.");
      if (!mapLoader.IsLoaded)
        throw new InvalidOperationException("Load the S2M map before rendering terrain.");

      RenderTerrain(mapLoader.LoadedMap);
    }

    public void RenderTerrain(S2MFile map)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      Terrain newTerrain = S2MTerrainRenderer.Render(map, transform, settings);

      if (RenderedTerrain != null)
      {
        if (Application.isPlaying) Destroy(RenderedTerrain.gameObject);
        else DestroyImmediate(RenderedTerrain.gameObject);
      }

      RenderedTerrain = newTerrain;
      RenderedMap = map;
    }

    private void OnMapLoaded(S2MFile map)
    {
      if (renderWhenMapLoaded) RenderTerrain(map);
    }
  }
}
