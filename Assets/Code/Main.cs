using System.Threading.Tasks;
using Assets.Code;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.TerrainRendering;
using Assets.Code.Video;
using Assets.Code.Lighting;
using UnityEngine;
using UnityEngine.Serialization;

public class Main : MonoBehaviour
{
  [Min(0.0001f)]
  [Tooltip("Uniform scale for all map-rendered objects: terrain, water, and vegetation. Keep all map renderers parented to this root so their coordinates remain aligned.")]
  [SerializeField] private float gameScale = 1f;

  [Min(0.0001f)]
  [Tooltip("Uniform multiplier for loaded tree models, in addition to Game Scale and vegetation asset scaling. Changes tree size without moving trees. Applied when the map loads.")]
  [SerializeField] private float treeModelScale = 0.5f;

  [Min(0.0001f)]
  [Tooltip("Size multiplier for shrub vegetation. Forest objects, including gorse and lavender, use Tree Model Scale. Applied when the map loads.")]
  [SerializeField] private float vegetationModelScale = 1f;

  [Min(0.0001f)]
  [Tooltip("Size multiplier for decorative rock models. Keeps their map positions fixed. Applied when the map loads.")]
  [SerializeField] private float rockModelScale = 1f;

  [Min(0.0001f)]
  [Tooltip("Size multiplier for natural stone deposits, separate from decorative rocks. Applied when the map loads.")]
  [SerializeField] private float naturalStoneModelScale = 1f;

  [Min(0f)]
  [Tooltip("Lightens natural stone's texture before lighting. 1 = original texture; 2 = twice the texture brightness. Applied when the map loads.")]
  [SerializeField] private float naturalStoneTextureBrightness = 2f;

  [Min(0.0001f)]
  [Tooltip("Size multiplier for camera-facing grass cards. Keeps their ground anchors fixed. Applied when the map loads.")]
  [SerializeField] private float grassModelScale = 1f;

  [Min(0.0001f)]
  [Tooltip("Size multiplier for apple trees, hops and wheat. Apple trees also retain Tree Model Scale. Keeps plant positions fixed. Applied when the map loads.")]
  [SerializeField] private float cropModelScale = 1f;

  [Min(0.0001f)]
  [Tooltip("Size multiplier for placed units, NPCs and animals, including their equipment and vehicle parts. Keeps map placement anchors fixed. Applied when the map loads.")]
  [SerializeField] private float unitModelScale = 1f;

  [SerializeField] private S2MVegetationRenderSettings vegetationRenderSettings = new();
  [FormerlySerializedAs("keepRenderSettings")]
  [SerializeField] private S2MBuildingRenderSettings buildingRenderSettings = new();
  [SerializeField] private S2MWallRenderSettings wallRenderSettings = new();
  [SerializeField] private S2MActorRenderSettings actorRenderSettings = new();

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
    if (GetComponent<AtmosphereController>() == null) gameObject.AddComponent<AtmosphereController>();
    // Game.Initialize();
    RunTests();
  }

  // Update is called once per frame
  void Update()
  {
    var game = Game.GetInstance();
    return;

    if (game.State == GameState.NONE)
    {
      // Immediately swap to loading assets
      game.State = GameState.LOADING_ASSETS;
    }
    else if (game.State == GameState.LOADING_ASSETS)
    {
      if (game.cacheManager.assetLoadingTask == null)
      {
        Debug.Log("Loading assets");
        game.cacheManager.assetLoadingTask = Task.Run(() =>
        {
          foreach (var status in game.cacheManager.ConvertAndCacheVideoAssets())
          {
            if (!status.IsFinished)
            {
              Debug.Log($"Started conversion of asset {status.AssetPath}.");
            }
            else
            {
              Debug.Log($"Finished conversion of asset {status.AssetPath}.");
            }
          }
        });
      }
      else if (game.cacheManager.assetLoadingTask.IsCompleted)
      {
        game.State = GameState.INTRO_VIDEOS;
      }
    }
    else if (game.State == GameState.INTRO_VIDEOS)
    {
      if (Input.GetMouseButtonDown(0))
      {
        game.videoManager.SkipCurrentIntroVideo();
      }

      if (game.videoManager.introVideosTask == null)
      {
        Debug.Log("Playing intro videos");
        game.videoManager.introVideosTask = PlayIntroVideosAsync(game);
      }
      else if (game.videoManager.introVideosTask.IsCompleted)
      {
        game.State = GameState.MAIN_MENU;
      }
    }
    else if (game.State == GameState.MAIN_MENU)
    {
      if (!game.mainMenuUI.IsShown)
      {
        game.mainMenuUI.Show();
      }

      game.mainMenuUI.UpdateLayout();
    }
  }

  private async void RunTests()
  {
    try
    {
      var loadTimer = System.Diagnostics.Stopwatch.StartNew();
      double previousStage = 0d;
      var loadStages = new System.Collections.Generic.List<string>();
      void RecordStage(string name)
      {
        double elapsed = loadTimer.Elapsed.TotalSeconds;
        loadStages.Add($"{name} {elapsed - previousStage:F3}s");
        previousStage = elapsed;
      }
      string testFilePath = "C:\\Users\\garet\\Documents\\Stronghold 2\\Maps\\war_chapter8.s2m";
      string gameInstallPath = "C:\\Steam\\steamapps\\common\\Stronghold 2";
      var mapFile = S2MFileLoader.Load(testFilePath);
      RecordStage("map decoding");
      if (gameScale <= 0f) throw new System.InvalidOperationException("Game scale must be positive.");
      if (!(treeModelScale > 0f) || float.IsInfinity(treeModelScale))
        throw new System.InvalidOperationException("Tree model scale must be finite and positive.");
      var sceneryScales = new S2MSceneryModelScales
      {
        Vegetation = vegetationModelScale, Rocks = rockModelScale,
        NaturalStone = naturalStoneModelScale, Grass = grassModelScale, Crops = cropModelScale
      };
      sceneryScales.Validate();
      S2MBuildingData buildingData = new S2MBuildingAssetLoader().Load(mapFile, gameInstallPath, animationSettings: buildingRenderSettings.AmbientAnimations);
      RecordStage("building assets");
      S2MTerrainTextureData terrainTextures = new S2MTerrainTextureAssetLoader().Load(
        mapFile, gameInstallPath, buildingData.PrimaryHeightCells, buildingData.SurfaceOverrides);
      RecordStage("terrain textures");
      var mapWorld = new GameObject("S2M Map World");
      mapWorld.transform.SetParent(transform, worldPositionStays: false);
      mapWorld.transform.localScale = Vector3.one * gameScale;
      var terrainSettings = new S2MTerrainSettings
      {
        UsePerCellCornerHeights = true,
        PrimaryHeightCells = buildingData.PrimaryHeightCells,
        TerrainCutoutCells = buildingData.TerrainCutoutCells,
        SurfaceOverrides = buildingData.SurfaceOverrides
      };
      GameObject terrain = S2MTerrainRenderer.Render(
        mapFile,
        mapWorld.transform,
        terrainSettings
        );
      RecordStage("terrain geometry");
      S2MTerrainTextureRenderer.Apply(terrain, terrainTextures, new(){
        AdjacentMaterialBlendWidth = 0.5f
        });
      RecordStage("terrain materials");
      S2MBuildingRenderer.Render(buildingData, mapWorld.transform, terrainSettings, buildingRenderSettings);
      RecordStage("building geometry/materials/colliders");
      S2MWallRenderer.Render(mapFile, gameInstallPath, mapWorld.transform, terrainSettings, wallRenderSettings);
      RecordStage("wall geometry/materials/colliders");
      Debug.Log($"Rendered {buildingData.BuildingCount} supported buildings and completed bridges.");
      var reportedBuildingIssues = new System.Collections.Generic.HashSet<string>();
      foreach (S2MBuildingAssetIssue issue in buildingData.Issues)
      {
        string message = $"{issue.ObjectType}: {issue.Reason}";
        if (reportedBuildingIssues.Add(message)) Debug.LogWarning(message);
      }

      GameObject water = S2MWaterRenderer.Render(mapFile, mapWorld.transform, new()
      {
        GameInstallPath = gameInstallPath
      });
      RecordStage("water and diagnostics");
      S2MVegetationData vegetationData = new S2MVegetationAssetLoader().Load(
        mapFile,
        gameInstallPath);
      RecordStage("vegetation assets");
      GameObject vegetation = S2MVegetationRenderer.Render(
        vegetationData,
        mapWorld.transform,
        vegetationRenderSettings,
        treeModelScale: treeModelScale,
        modelScales: sceneryScales,
        naturalStoneTextureBrightness: naturalStoneTextureBrightness);
      RecordStage("vegetation rendering");
      S2MActorData actorData = await new S2MActorAssetLoader().LoadAsync(mapFile, gameInstallPath,
        actorRenderSettings.Overrides, actorRenderSettings.AnimationSamplesPerSecond);
      if (this == null || mapWorld == null) return;
      S2MActorRenderer.Render(actorData, mapWorld.transform, terrainSettings, actorRenderSettings, unitModelScale);
      RecordStage("placed actors and animations");
      Debug.Log($"Rendered {actorData.PlacedCount}/{mapFile.Actors.Count} placed actors ({actorData.InactiveCount} inactive).");
      foreach (string issue in actorData.Issues) Debug.LogWarning("Actor: " + issue);
      Debug.Log($"Map loaded in {loadTimer.Elapsed.TotalSeconds:F3}s: {string.Join(", ", loadStages)}.");

      Debug.Log($"Read S2M header: author={mapFile.Author}, type={mapFile.MapType}, balanced={mapFile.Balanced}, maxPlayers={mapFile.MaxPlayers}, version={mapFile.Version}; rendered {vegetationData.SupportedInstanceCount} scenery placements including {vegetationData.Grass.Count} grass cards ({vegetationData.InactiveInstanceCount} inactive, {vegetationData.UnsupportedInstanceCount} unsupported records).");
      foreach (string issue in vegetationData.Issues) Debug.LogWarning(issue);
    }
    catch (System.Exception exception)
    {
      Debug.LogException(exception);
    }
  }

  private async Task PlayIntroVideosAsync(Game game)
  {
    await game.videoManager.PlayFullscreenBinkVideo(VideoFilePaths.FireflyLogo, fadeOutMs: 300);
    await game.videoManager.PlayFullscreenBinkVideo(VideoFilePaths.Intro, fadeOutMs: 300);
  }
}
