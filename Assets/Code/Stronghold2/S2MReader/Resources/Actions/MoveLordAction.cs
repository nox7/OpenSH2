using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class MoveLordAction : Action
  {
    public FlagColor TargetFlagColor { get; set; }
    public int TargetFlagNumber { get; set; }
    public S2MLords Lord { get; set; }
    public bool DoesLeaveMap { get; set; }
  }
}
