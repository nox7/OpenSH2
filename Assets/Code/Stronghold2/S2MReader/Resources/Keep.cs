using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public enum KeepSize { Wooden, Small, Medium, Large }

  /// <summary>A keep's model transform from its inherited Building payload.</summary>
  public sealed class Keep : BuildingPlacement
  {
    public KeepSize Size { get; internal set; }
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
  }
}
