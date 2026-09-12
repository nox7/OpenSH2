using System.Threading.Tasks;
using Assets.Code;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.TerrainRendering;
using Assets.Code.Video;
using UnityEngine;

public class Main : MonoBehaviour
{
  [SerializeField] private S2MVegetationRenderSettings vegetationRenderSettings = new();

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
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

  private void RunTests()
  {
    try
    {
      string testFilePath = "C:\\Users\\garet\\Documents\\Stronghold 2\\Maps\\war_chapter8.s2m";
      string gameInstallPath = "C:\\Steam\\steamapps\\common\\Stronghold 2";
      var mapFile = S2MFileLoader.Load(testFilePath);
      S2MTerrainTextureData terrainTextures = new S2MTerrainTextureAssetLoader().Load(mapFile, gameInstallPath);
      GameObject terrain = S2MTerrainRenderer.Render(
        mapFile,
        transform,
        new()
        {
          UsePerCellCornerHeights = true,
        }
        );
      S2MTerrainTextureRenderer.Apply(terrain, terrainTextures, new(){
        AdjacentMaterialBlendWidth = 0.5f
        });

      GameObject water = S2MWaterRenderer.Render(mapFile, transform);
      S2MVegetationData vegetationData = new S2MVegetationAssetLoader().Load(
        mapFile,
        gameInstallPath);
      GameObject vegetation = S2MVegetationRenderer.Render(
        vegetationData,
        transform,
        vegetationRenderSettings);

      Debug.Log($"Read S2M header: author={mapFile.Author}, type={mapFile.MapType}, balanced={mapFile.Balanced}, maxPlayers={mapFile.MaxPlayers}, version={mapFile.Version}; decoded {mapFile.Forest?.Instances.Count ?? 0} Forest records, rendered {vegetationData.SupportedInstanceCount} trees ({vegetationData.UnsupportedInstanceCount} unsupported records).");
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
