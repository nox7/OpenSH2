using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class AITroopsRetreatAction : Action
  {
    public FlagColor RetreatPointFlagColor { get; set; }
    public int RetreatPointFlagNumber { get; set; }
    public Lord Lord { get; set; }
    public bool WillLeaveMap { get; set; }
  }
}
