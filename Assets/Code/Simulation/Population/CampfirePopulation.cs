using System;
using System.Collections.Generic;

namespace Assets.Code.Simulation.Population
{
  /// <summary>Estate population rules. Coordinates are map cells, independent of Unity.</summary>
  public sealed class CampfirePopulation
  {
    public const int PeasantsPerHovel = 8;
    public int KeepId { get; }
    public int EstateId { get; }
    public int Capacity { get; }
    public IReadOnlyList<CampfireSeat> Seats { get; }
    public CampfirePopulation(int keepId, int estateId, int hovelCount, PopulationSettings settings = null)
    {
      if (hovelCount < 0) throw new ArgumentOutOfRangeException(nameof(hovelCount));
      settings ??= new PopulationSettings(); settings.Validate();
      KeepId = keepId; EstateId = estateId;
      Capacity = checked(8 + PeasantsPerHovel * hovelCount);
      var seats = new List<CampfireSeat>(Capacity);
      for (int ring = 0; seats.Count < Capacity; ring++)
      {
        float radius = settings.FirstRingRadius + ring * settings.RingSpacing;
        int count = Math.Max(1, (int)Math.Floor(2 * Math.PI * radius / settings.SeatSpacing));
        for (int slot = 0; slot < count && seats.Count < Capacity; slot++)
        {
          double angle = 2 * Math.PI * (slot + (ring % 2) * .5) / count;
          seats.Add(new CampfireSeat((float)Math.Sin(angle) * radius, (float)Math.Cos(angle) * radius,
            (float)(angle * 180 / Math.PI + 180), ring));
        }
      }
      Seats = seats.AsReadOnly();
    }
  }

  public readonly struct CampfireSeat
  {
    public float X { get; }
    public float Z { get; }
    public float HeadingDegrees { get; }
    public int Ring { get; }
    public CampfireSeat(float x, float z, float heading, int ring)
    { X = x; Z = z; HeadingDegrees = heading; Ring = ring; }
  }
}
