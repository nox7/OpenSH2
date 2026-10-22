using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public enum KeepSize { Wooden, Small, Medium, Large }

  /// <summary>A keep's model transform from its inherited Building payload.</summary>
  public sealed class Keep : S2Object
  {
    public KeepSize Size { get; internal set; }
    public float RawX { get; internal set; }
    public float RawY { get; internal set; }
    public float RawZ { get; internal set; }
    public float RotationDegrees { get; internal set; }
    public int FootprintWidth { get; internal set; }
    public int FootprintHeight { get; internal set; }
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
    public float MapX => RawX / 1024f;
    public float MapZ => RawZ / 1024f;
  }
}
