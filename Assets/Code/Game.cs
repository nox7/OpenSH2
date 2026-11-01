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
    public Simulation.SimulationClock Simulation { get; } = new();
    public Simulation.Economy.EstateInventory Inventory { get; internal set; }
    public Stronghold2.MapLoading.S2MAiRuntime AI { get; internal set; }
    public bool IsPaused { get => Simulation.IsPaused; set => Simulation.IsPaused = value; }

  }
}
