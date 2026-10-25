using System;
using System.Threading.Tasks;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Optional map-loader integration for scenes that do not use Main's test map pipeline.</summary>
  public sealed class S2MActorRendererComponent : MonoBehaviour
  {
    [SerializeField] private S2MMapLoader mapLoader;
    [SerializeField] private string gameInstallPath = "C:/Steam/steamapps/common/Stronghold 2";
    [SerializeField] private S2MActorRenderSettings settings = new();
    [SerializeField] private S2MTerrainSettings terrainSettings = new();
    private int generation;
    public S2MActorRenderer RenderedActors { get; private set; }
    public event Action<S2MActorData> ActorsLoaded;

    private void OnEnable()
    {
      if (mapLoader == null) mapLoader = GetComponent<S2MMapLoader>();
      if (mapLoader == null) return;
      mapLoader.MapLoaded += OnMapLoaded;
      if (mapLoader.IsLoaded) OnMapLoaded(mapLoader.LoadedMap);
    }

    private void OnDisable()
    {
      generation++;
      if (mapLoader != null) mapLoader.MapLoaded -= OnMapLoaded;
      if (RenderedActors != null) RenderedActors.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (RenderedActors != null) Destroy(RenderedActors.gameObject); }

    private async void OnMapLoaded(S2MFile map)
    {
      try { await LoadAsync(map); }
      catch (Exception e) { if (this != null) Debug.LogException(e, this); }
    }

    public async Task LoadAsync(S2MFile map)
    {
      settings.Validate();
      int request = ++generation;
      // Conversion can finish and populate the cache after a map has been superseded.
      var data = await new S2MActorAssetLoader().LoadAsync(map, gameInstallPath, settings.Overrides, settings.AnimationSamplesPerSecond);
      if (this == null || !isActiveAndEnabled || request != generation) return;
      var replacement = S2MActorRenderer.Render(data, transform, terrainSettings, settings);
      if (RenderedActors != null) { RenderedActors.gameObject.SetActive(false); Destroy(RenderedActors.gameObject); }
      RenderedActors = replacement;
      foreach (string issue in data.Issues) Debug.LogWarning("Actor: " + issue, this);
      ActorsLoaded?.Invoke(data);
    }
  }
}
