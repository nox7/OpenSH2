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

[DefaultExecutionOrder(-1000)]
public class Main : MonoBehaviour
{
  [Min(0)]
  [Tooltip("Simulation seconds per real second. Applies live to movement, work timers and gameplay animations. Zero freezes simulation; camera/UI remain responsive.")]
  [SerializeField] private float gameSpeed = 1f;
  public float GameSpeed
  {
    get => gameSpeed;
    set { if(float.IsNaN(value) || float.IsInfinity(value) || value<0)throw new System.ArgumentOutOfRangeException(nameof(value));gameSpeed=value; }
  }

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
  [SerializeField] private S2MLiverySettings liverySettings = new();
  [Tooltip("Woodcutting action times and cart speed in map cells per game second. Cart Speed Cells Per Second can be adjusted live.")]
  [SerializeField] private Assets.Code.Simulation.Labor.WoodcuttingSettings woodcuttingSettings = new();
  public Assets.Code.Simulation.Labor.LaborManager Labor { get; private set; }
  [Tooltip("Quarry local stone capacity, timing, and block layout. Three workers are required. Reload the map to apply.")]
  [SerializeField] private Assets.Code.Simulation.Labor.QuarrySettings quarrySettings = new();
  [Tooltip("Iron mine local capacity, cycle timing and output pile position. Two workers are required. Reload the map to apply.")]
  [SerializeField] private Assets.Code.Simulation.Labor.IronMineSettings ironMineSettings = new();
  [Tooltip("Ox hauling speed, follow distance, loading interval, stone capacity and model scale. Reload the map to apply.")]
  [SerializeField] private Assets.Code.Simulation.Labor.OxHaulingSettings oxHaulingSettings = new();
  public Assets.Code.Simulation.Economy.EstateInventory Inventory => Game.GetInstance()?.Inventory;
  [Tooltip("Stockpile pallet count and resource capacities. Reload the map to apply.")]
  [SerializeField] private Assets.Code.Simulation.Economy.StorageSettings storageSettings = new();
  [Tooltip("Simulation seconds between individual stockpile deposits, shared by woodcutters and ox handlers. Applied live and affected by game speed.")]
  [SerializeField] private Assets.Code.Simulation.Economy.StockpileDeliverySettings stockpileDeliverySettings = new();
  [Tooltip("Shared stockpile item models and packing overrides. Reload the map to apply.")]
  [SerializeField] private S2MStockpileVisualSettings stockpileVisualSettings = new();
  [Tooltip("Recruitment timing, movement speed, entrance offsets, and campfire ring spacing in map cells. Applied when the map loads.")]
  [SerializeField] private Assets.Code.Simulation.Population.PopulationSettings populationSettings = new();
  [Tooltip("Horse/cart assembly offsets and saw-pit seat alignment. Reload the map to apply.")]
  [SerializeField] private HorseCartAssemblySettings horseCartSettings = new();

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
    if (GetComponent<AtmosphereController>() == null) gameObject.AddComponent<AtmosphereController>();
    if (Game.GetInstance() == null) Game.Initialize();
    RunTests();
  }

  // Update is called once per frame
  void Update()
  {
    var game = Game.GetInstance();
    if (game == null) return;
#if ENABLE_INPUT_SYSTEM
    if (UnityEngine.InputSystem.Keyboard.current?.pKey.wasPressedThisFrame == true) game.IsPaused = !game.IsPaused;
#else
    if (Input.GetKeyDown(KeyCode.P)) game.IsPaused = !game.IsPaused;
#endif
    game.Simulation.Speed = float.IsNaN(gameSpeed) || float.IsInfinity(gameSpeed) ? 1 : Mathf.Max(0,gameSpeed);
    game.Simulation.Advance(Time.unscaledDeltaTime);
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
      S2MLiveryContext.Create(mapFile, gameInstallPath, mapWorld.transform, liverySettings);
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
      foreach (var animation in mapWorld.GetComponentsInChildren<S2MBuildingAmbientAnimation>())
        animation.Clock = Game.GetInstance().Simulation;
      foreach (var fire in mapWorld.GetComponentsInChildren<S2MCampfireRenderer>())
        fire.Clock = Game.GetInstance().Simulation;
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
      populationSettings.Validate();
      horseCartSettings.Validate();
      var navigation = S2MNavigationBuilder.Build(mapFile, populationSettings, buildingData, dynamicFoliage:true);
      woodcuttingSettings.UnitScale=actorRenderSettings.ModelScale*unitModelScale/terrainSettings.HorizontalCellSize;
      var harvestResources=vegetation.GetComponent<S2MFoliageHarvestView>().Initialize(navigation,woodcuttingSettings);
      var recruitmentPlans = S2MRecruitmentPlan.Create(mapFile, navigation, populationSettings);
      var laborPlan = S2MLaborPlan.Create(mapFile,navigation,quarrySettings,oxHaulingSettings,ironMineSettings);
      var inventory=S2MStoragePlan.Create(mapFile,storageSettings,stockpileDeliverySettings);
      Game.GetInstance().Inventory=inventory;
      laborPlan.OxHauling.ConfigureStorage(inventory);
      if(stockpileVisualSettings.Enabled)
        S2MStockpileRenderer.Create(mapFile,inventory,S2MStockpileAssets.Load(gameInstallPath,settings:stockpileVisualSettings),mapWorld.transform,terrainSettings);
      foreach(var job in laborPlan.Jobs)if(job is Assets.Code.Simulation.Labor.WoodcutterJob woodcutter)
        {woodcutter.ConfigureHarvest(harvestResources,woodcuttingSettings);woodcutter.ConfigureStorage(inventory);}
      S2MActorData actorData = await new S2MActorAssetLoader().LoadAsync(mapFile, gameInstallPath,
        actorRenderSettings.Overrides, actorRenderSettings.AnimationSamplesPerSecond, placements:laborPlan.StartupActors,assembly:horseCartSettings);
      if (this == null || mapWorld == null) return;
      S2MActorRenderer.Render(actorData, mapWorld.transform, terrainSettings, actorRenderSettings, unitModelScale).Clock = Game.GetInstance().Simulation;
      var populationData = await S2MCampfirePopulationLoader.LoadRecruitsAsync(mapFile, gameInstallPath,
        recruitmentPlans, actorRenderSettings.AnimationSamplesPerSecond,jobs:laborPlan.Jobs,assembly:horseCartSettings);
      if (this == null || mapWorld == null) return;
      var populationRoot = new GameObject("Estate Population"); populationRoot.transform.SetParent(mapWorld.transform, false);
      var populationController=populationRoot.AddComponent<S2MPopulationController>();
      populationController.Initialize(mapFile, navigation, recruitmentPlans,
        populationData, terrainSettings, actorRenderSettings, populationSettings, Game.GetInstance().Simulation, unitModelScale,laborPlan.Jobs,horseCartSettings,laborPlan.Buildings);
      Labor=populationController.Labor;
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
