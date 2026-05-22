using Assets.Code.Caching;
using Assets.Code.UI;
using Assets.Code.Video;
using System;

namespace Assets.Code
{
  class Game
  {
    private static Game Instance = null;

    public static Game Initialize()
    {
      if (Instance != null)
      {
        throw new Exception("Already initialized Game instance.");
      }

      Instance = new Game();
      return Instance;
    }

    public static Game GetInstance()
    {
      return Instance!;
    }

    public GameState State { get; set; } = GameState.NONE;
    public CacheManager cacheManager;
    public VideoManager videoManager;
    public MainMenuUI mainMenuUI;

    public Game()
    {
      cacheManager = new();
      videoManager = new();
      mainMenuUI = new();
    }
  }
}
