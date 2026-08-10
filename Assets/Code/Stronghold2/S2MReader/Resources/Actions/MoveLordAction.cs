using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class MoveLordAction : Action
  {
    public FlagColor TargetFlagColor { get; set; }
    public int TargetFlagNumber { get; set; }
    public Lord Lord { get; set; }
    public bool DoesLeaveMap { get; set; }
  }
}
