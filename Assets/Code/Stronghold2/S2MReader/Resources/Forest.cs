using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Placed landscape objects stored by the S2M Forest object.</summary>
  public sealed class Forest : S2Object
  {
    public IReadOnlyList<ForestInstance> Instances { get; internal set; } = Array.Empty<ForestInstance>();
    public byte[] TrailingData { get; internal set; } = Array.Empty<byte>();
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
  }

  /// <summary>A decoded 65-byte Forest placement record.</summary>
  public sealed class ForestInstance
  {
    public float RawX { get; internal set; }
    public float RawY { get; internal set; }
    public float RawZ { get; internal set; }
    public float RotationDegrees { get; internal set; }
    public float ScaleX { get; internal set; }
    public float ScaleY { get; internal set; }
    public float AppearanceScale { get; internal set; }
    public float TintRed { get; internal set; }
    public float TintGreen { get; internal set; }
    public float TintBlue { get; internal set; }
    public int RandomValue { get; internal set; }
    public ushort CellX { get; internal set; }
    public ushort CellZ { get; internal set; }
    public ushort Family { get; internal set; }
    public ushort Variant { get; internal set; }
    public byte[] UnknownTail { get; internal set; } = Array.Empty<byte>();

    public float MapX => RawX / 1024f;
    public float MapY => RawY / 1024f;
    public float MapZ => RawZ / 1024f;
    public bool IsKnownTree => Family <= 2;
  }
}
