using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public enum KeepSize { Wooden, Small, Medium, Large }

  /// <summary>A keep's model transform from its inherited Building payload.</summary>
  public sealed class Keep : BuildingPlacement
  {
    public KeepSize Size { get; internal set; }
    /// <summary>Saved world-space fire/seating centre, not the camp_fire.GR2 model pivot.</summary>
    public bool HasCampfirePosition { get; internal set; }
    public float CampfireRawX { get; internal set; }
    public float CampfireRawY { get; internal set; }
    public float CampfireRawZ { get; internal set; }
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
  }
}
