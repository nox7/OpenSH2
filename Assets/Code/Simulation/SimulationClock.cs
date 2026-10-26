using System;

namespace Assets.Code.Simulation
{
  /// <summary>Game time independent of Unity's global time scale and camera/UI updates.</summary>
  public sealed class SimulationClock
  {
    public bool IsPaused { get; set; }
    public double ElapsedSeconds { get; private set; }
    public float DeltaTime { get; private set; }

    public void Advance(float seconds)
    {
      if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
        throw new ArgumentOutOfRangeException(nameof(seconds));
      DeltaTime = IsPaused ? 0 : seconds;
      ElapsedSeconds += DeltaTime;
    }
  }
}
