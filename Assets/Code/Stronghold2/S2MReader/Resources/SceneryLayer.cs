using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public enum SceneryKind { Forest, Shrubs, Rocks, NaturalStone, Grass, AppleTree, Hops, Wheat }

  /// <summary>Packed landscape placements; original bytes are retained for undecoded fields.</summary>
  public sealed class SceneryLayer : S2Object
  {
    public SceneryKind Kind { get; internal set; }
    public IReadOnlyList<SceneryInstance> Instances { get; internal set; } = Array.Empty<SceneryInstance>();
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
  }

  public sealed class SceneryInstance
  {
    public SceneryKind Kind { get; internal set; }
    public float RawX { get; internal set; }
    public float RawY { get; internal set; }
    public float RawZ { get; internal set; }
    public float RotationDegrees { get; internal set; }
    public float ScaleX { get; internal set; } = 1f;
    public float ScaleY { get; internal set; } = 1f;
    public float AppearanceScale { get; internal set; } = 1f;
    public ushort Family { get; internal set; }
    public ushort Variant { get; internal set; }
    public bool Enabled { get; internal set; } = true;
    public bool SampleTerrainHeight { get; internal set; }
    public byte[] RawRecord { get; internal set; } = Array.Empty<byte>();
    public float MapX => RawX / 1024f;
    public float MapZ => RawZ / 1024f;
  }
}
