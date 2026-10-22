using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public sealed class Bridges : S2Object
  {
    public IReadOnlyList<BridgeSpan> Spans { get; internal set; } = Array.Empty<BridgeSpan>();
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
  }

  /// <summary>A 26-byte Bridges record. Coordinates identify the inclusive longitudinal cells.</summary>
  public sealed class BridgeSpan
  {
    // Observed state 1 has a completed deck; state 0 does not. Other states
    // remain unsupported rather than being guessed from Landscape feature IDs.
    public byte State { get; internal set; }
    public byte Axis { get; internal set; }
    public int StartX { get; internal set; }
    public int StartZ { get; internal set; }
    public int EndX { get; internal set; }
    public int EndZ { get; internal set; }
    public int Unknown1 { get; internal set; }
    public int Unknown2 { get; internal set; }

    public bool AlongZ => Axis == 1;
    public int Length => AlongZ ? EndZ - StartZ + 1 : EndX - StartX + 1;

    public bool CanRender(int width, int height)
    {
      if (State != 1 || Axis > 1 || Unknown1 != 0 || Unknown2 != 0
        || StartX < 0 || StartZ < 0 || EndX < StartX || EndZ < StartZ
        || EndX >= width || EndZ >= height) return false;
      if (AlongZ ? StartX != EndX : StartZ != EndZ) return false;
      if (AlongZ ? StartX >= width - 1 : StartZ >= height - 1) return false;
      return Length >= 4 && Length % 2 == 0;
    }
  }
}
