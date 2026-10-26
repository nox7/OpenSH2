using System;

namespace Assets.Code.Simulation.Population
{
  [Serializable]
  public sealed class PopulationSettings
  {
    public float SpawnIntervalSeconds = 5f;
    public float HovelSeatedSeconds = 2f;
    public float RunSpeedCellsPerSecond = 2f;
    public float WalkSpeedCellsPerSecond = .85f;
    public float FirstRingRadius = .9f;
    public float RingSpacing = .55f;
    public float SeatSpacing = .65f;
    public float KeepStairBaseOffset = .35f;
    public float HovelDoorOffset = .25f;
    public float MaximumStepHeight = .4f;
    public float JobAssignmentIntervalSeconds = 3f;

    public void Validate()
    {
      foreach (float value in new[] { SpawnIntervalSeconds, RunSpeedCellsPerSecond, WalkSpeedCellsPerSecond, FirstRingRadius, RingSpacing,
        SeatSpacing, MaximumStepHeight, JobAssignmentIntervalSeconds })
        if (!(value > 0) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(PopulationSettings), "Timing, spacing, speed and step height must be finite and positive.");
      foreach (float value in new[] { HovelSeatedSeconds, KeepStairBaseOffset, HovelDoorOffset })
        if (!(value >= 0) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(PopulationSettings));
    }
  }
}
