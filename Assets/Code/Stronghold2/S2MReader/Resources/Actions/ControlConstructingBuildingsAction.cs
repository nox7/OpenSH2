using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Makes X constructing-building sites active or inactive
  /// </summary>
  internal class ControlConstructingBuildingsAction : Action
  {
    public int NumberOfConstructingBuildings { get; set; }
    public bool IsActive { get; set; }
  }
}
