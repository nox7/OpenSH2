using Assets.Code.Stronghold2.AssetLoading;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  public sealed class Granny2ModelRendererComponent : MonoBehaviour
  {
    [SerializeField] private Granny2ModelLoaderComponent modelLoader;
    [SerializeField] private bool renderWhenLoaded = true;
    [SerializeField] private Granny2ModelRenderSettings settings = new();

    public GameObject RenderedModel { get; private set; }
    private Granny2ModelData RenderedData { get; set; }

    private void OnEnable()
    {
      if (modelLoader != null) modelLoader.ModelLoaded += OnModelLoaded;
    }

    private void Start()
    {
      if (renderWhenLoaded && modelLoader != null && modelLoader.IsLoaded && RenderedData != modelLoader.LoadedModel)
        Render(modelLoader.LoadedModel);
    }

    private void OnDisable()
    {
      if (modelLoader != null) modelLoader.ModelLoaded -= OnModelLoaded;
    }

    [ContextMenu("Render Loaded GR2 Model")]
    public void Render()
    {
      if (modelLoader == null || !modelLoader.IsLoaded)
        throw new InvalidOperationException("Assign a loader and load its GR2 model before rendering.");
      Render(modelLoader.LoadedModel);
    }

    public void Render(Granny2ModelData model)
    {
      GameObject newModel = Granny2ModelRenderer.Render(model, transform, settings);
      if (RenderedModel != null)
      {
        if (Application.isPlaying) Destroy(RenderedModel);
        else DestroyImmediate(RenderedModel);
      }
      RenderedModel = newModel;
      RenderedData = model;
    }

    private void OnModelLoaded(Granny2ModelData model)
    {
      if (renderWhenLoaded) Render(model);
    }
  }
}
