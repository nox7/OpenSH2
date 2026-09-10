using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>Renders water from an S2MMapLoader's already parsed map.</summary>
  public sealed class S2MWaterRendererComponent : MonoBehaviour
  {
    [SerializeField]
    private S2MMapLoader mapLoader;

    [SerializeField]
    private bool renderWhenMapLoaded = true;

    [SerializeField]
    private S2MWaterSettings settings = new();

    public GameObject RenderedWater { get; private set; }
    private S2MFile RenderedMap { get; set; }

    private void OnEnable()
    {
      if (mapLoader != null) mapLoader.MapLoaded += OnMapLoaded;
    }

    private void Start()
    {
      if (renderWhenMapLoaded && mapLoader != null && mapLoader.IsLoaded && RenderedMap != mapLoader.LoadedMap)
        RenderWater(mapLoader.LoadedMap);
    }

    private void OnDisable()
    {
      if (mapLoader != null) mapLoader.MapLoaded -= OnMapLoaded;
    }

    [ContextMenu("Render Loaded S2M Water")]
    public void RenderWater()
    {
      if (mapLoader == null)
        throw new InvalidOperationException("Assign an S2MMapLoader before rendering water.");
      if (!mapLoader.IsLoaded)
        throw new InvalidOperationException("Load the S2M map before rendering water.");

      RenderWater(mapLoader.LoadedMap);
    }

    public void RenderWater(S2MFile map)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      GameObject newWater = S2MWaterRenderer.Render(map, transform, settings);

      if (RenderedWater != null)
      {
        if (Application.isPlaying) Destroy(RenderedWater);
        else DestroyImmediate(RenderedWater);
      }

      RenderedWater = newWater;
      RenderedMap = map;
    }

    private void OnMapLoaded(S2MFile map)
    {
      if (renderWhenMapLoaded) RenderWater(map);
    }
  }
}
