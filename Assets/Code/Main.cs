using System.Threading.Tasks;
using Assets.Code;
using Assets.Code.Video;
using UnityEngine;

public class Main : MonoBehaviour
{
  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
    Game.Initialize();
    RunTests();
  }

  // Update is called once per frame
  void Update()
  {
    var game = Game.GetInstance();

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
      string testFilePath = "C:\\Users\\garet\\Documents\\Stronghold 2\\Maps\\war_chapter1-inflated\\00-header.bin";
      var reader = new Assets.Code.Stronghold2.S2MReader.S2MReader(testFilePath);
      var mapFile = reader.ReadS2MFile();

      Debug.Log($"Read S2M header: author={mapFile.Author}, type={mapFile.MapType}, balanced={mapFile.Balanced}, maxPlayers={mapFile.MaxPlayers}, version={mapFile.Version}");
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
