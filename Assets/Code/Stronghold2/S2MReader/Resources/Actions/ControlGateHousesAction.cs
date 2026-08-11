using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Unsure as what this does.
  /// TODO Investigate
  /// </summary>
  internal class ControlGateHousesAction : Action
  {
    public S2MLords Lord { get; set; }
    /// <summary>
    /// False = Closed
    /// True = Open
    /// </summary>
    public bool GatehouseOpen { get; set; }
    public FlagColor GateHouseLocationFlagColor { get; set; }
    public int GateHouseLocationFlagNumber { get; set; }
  }
}
