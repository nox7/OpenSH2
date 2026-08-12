using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Makes troops of the lord within an arbitrary proximity of the flag super aggressive
  /// </summary>
  internal class SuperAggressiveTroopsAction : Action
  {
    public FlagColor ZoneFlagColor { get; set; }
    public int ZoneFlagNumber { get; set; }
    public S2MLords Lord { get; set; }
  }
}
