using Assets.Code.Stronghold2.AssetLoading;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  public sealed class S2MVegetationRendererComponent : MonoBehaviour
  {
    [SerializeField] private S2MVegetationLoaderComponent vegetationLoader;
    [SerializeField] private bool renderWhenLoaded = true;
    [SerializeField] private S2MVegetationRenderSettings settings = new();

    public GameObject RenderedVegetation { get; private set; }
    private S2MVegetationData renderedData;

    private void OnEnable()
    {
      if (vegetationLoader != null) vegetationLoader.VegetationLoaded += OnVegetationLoaded;
    }

    private void Start()
    {
      if (renderWhenLoaded && vegetationLoader != null && vegetationLoader.IsLoaded
        && renderedData != vegetationLoader.LoadedVegetation)
        Render(vegetationLoader.LoadedVegetation);
    }

    private void OnDisable()
    {
      if (vegetationLoader != null) vegetationLoader.VegetationLoaded -= OnVegetationLoaded;
    }

    [ContextMenu("Render Loaded S2M Vegetation")]
    public void Render()
    {
      if (vegetationLoader == null || !vegetationLoader.IsLoaded)
        throw new InvalidOperationException("Assign a vegetation loader and load its map assets first.");
      Render(vegetationLoader.LoadedVegetation);
    }

    public void Render(S2MVegetationData vegetation)
    {
      GameObject replacement = S2MVegetationRenderer.Render(vegetation, transform, settings);
      if (RenderedVegetation != null)
      {
        if (Application.isPlaying) Destroy(RenderedVegetation);
        else DestroyImmediate(RenderedVegetation);
      }
      RenderedVegetation = replacement;
      renderedData = vegetation;
    }

    private void OnVegetationLoaded(S2MVegetationData vegetation)
    {
      if (renderWhenLoaded) Render(vegetation);
    }
  }
}
