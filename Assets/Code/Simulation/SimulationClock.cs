using System;

namespace Assets.Code.Simulation
{
  /// <summary>Game time independent of Unity's global time scale and camera/UI updates.</summary>
  public sealed class SimulationClock
  {
    public bool IsPaused { get; set; }
    private float speed = 1;
    /// <summary>Simulation seconds per real second; zero freezes game actions.</summary>
    public float Speed
    {
      get => speed;
      set { if(float.IsNaN(value) || float.IsInfinity(value) || value<0)throw new ArgumentOutOfRangeException(nameof(value));speed=value; }
    }
    public double ElapsedSeconds { get; private set; }
    public float DeltaTime { get; private set; }

    public void Advance(float seconds)
    {
      if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
        throw new ArgumentOutOfRangeException(nameof(seconds));
      DeltaTime = IsPaused ? 0 : seconds * speed;
      ElapsedSeconds += DeltaTime;
    }
  }
}
