using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class AITroopRetreatAction : Action
  {
    public FlagColor RetreatPointFlagColor { get; set; }
    public int RetreatPointFlagNumber { get; set; }
    public S2MLords Lord { get; set; }
    public bool WillLeaveMap { get; set; }
  }
}
