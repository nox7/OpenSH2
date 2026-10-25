using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Saved plant state, independent of the farm building's terrain surface.</summary>
  public sealed class PlantActor
  {
    public int Id { get; internal set; }
    public SceneryKind Kind { get; internal set; }
    public float RawX { get; internal set; }
    public float RawY { get; internal set; }
    public float RawZ { get; internal set; }
    public float RotationDegrees { get; internal set; }
    public int Stage { get; internal set; }
    public float[] WheatOffsets { get; internal set; } = Array.Empty<float>();
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
  }
}
