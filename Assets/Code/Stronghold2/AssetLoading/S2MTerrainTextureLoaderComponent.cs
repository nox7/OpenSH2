using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Loads map-referenced terrain textures without creating terrain render objects.</summary>
  public sealed class S2MTerrainTextureLoaderComponent : MonoBehaviour
  {
    [SerializeField] private S2MMapLoader mapLoader;
    [SerializeField] private string gameInstallPath = "C:/Steam/steamapps/common/Stronghold 2";
    [SerializeField] private bool loadWhenMapLoaded = true;

    private readonly S2MTerrainTextureAssetLoader loader = new();
    public S2MTerrainTextureData LoadedTerrainTextures { get; private set; }
    public bool IsLoaded => LoadedTerrainTextures != null;
    public event Action<S2MTerrainTextureData> TerrainTexturesLoaded;

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

    [ContextMenu("Load S2M Terrain Textures")]
    public void Load()
    {
      if (mapLoader == null || !mapLoader.IsLoaded)
        throw new InvalidOperationException("Assign an S2M map loader and load the map first.");
      Load(mapLoader.LoadedMap);
    }

    public void Load(S2MFile map)
    {
      LoadedTerrainTextures = loader.Load(map, gameInstallPath);
      TerrainTexturesLoaded?.Invoke(LoadedTerrainTextures);
    }

    private void OnMapLoaded(S2MFile map)
    {
      if (loadWhenMapLoaded) Load(map);
    }
  }
}
