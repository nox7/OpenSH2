using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Loads map-referenced vegetation assets; it does not create Unity render objects.</summary>
  public sealed class S2MVegetationLoaderComponent : MonoBehaviour
  {
    [SerializeField] private S2MMapLoader mapLoader;
    [SerializeField] private string gameInstallPath = "C:/Steam/steamapps/common/Stronghold 2";
    [SerializeField] private string converterExecutablePath;
    [SerializeField] private string cacheDirectory;
    [SerializeField] private bool useLowDetailMeshes;
    [SerializeField] private bool loadWhenMapLoaded = true;

    private readonly S2MVegetationAssetLoader loader = new();
    public S2MVegetationData LoadedVegetation { get; private set; }
    public bool IsLoaded => LoadedVegetation != null;
    public event Action<S2MVegetationData> VegetationLoaded;

    private void OnEnable()
    {
      if (mapLoader != null) mapLoader.MapLoaded += OnMapLoaded;
    }

    private void Start()
    {
      if (loadWhenMapLoaded && mapLoader != null && mapLoader.IsLoaded) Load(mapLoader.LoadedMap);
    }

    private void OnDisable()
    {
      if (mapLoader != null) mapLoader.MapLoaded -= OnMapLoaded;
    }

    [ContextMenu("Load S2M Vegetation Assets")]
    public void Load()
    {
      if (mapLoader == null || !mapLoader.IsLoaded)
        throw new InvalidOperationException("Assign an S2M map loader and load the map first.");
      Load(mapLoader.LoadedMap);
    }

    public void Load(S2MFile map)
    {
      LoadedVegetation = loader.Load(
        map,
        gameInstallPath,
        string.IsNullOrWhiteSpace(converterExecutablePath) ? null : converterExecutablePath,
        string.IsNullOrWhiteSpace(cacheDirectory) ? null : cacheDirectory,
        useLowDetailMeshes);
      VegetationLoaded?.Invoke(LoadedVegetation);
    }

    private void OnMapLoaded(S2MFile map)
    {
      if (loadWhenMapLoaded) Load(map);
    }
  }
}
