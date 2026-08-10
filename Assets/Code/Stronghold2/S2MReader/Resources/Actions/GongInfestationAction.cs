using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Spawn X number of gong piles in estate defined by flag.
  /// </summary>
  internal class GongInfestationAction : Action
  {
    public FlagColor EstateFlagColor { get; set; }
    public int EstateFlagNumber { get; set; }
    public int NumGongPiles { get; set; }
  }
}
