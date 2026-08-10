using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Spawns a ship, then moves it along the destination flags with a given (optional) month delay.
  /// </summary>
  internal class MoveShipAction : Action
  {
    public FlagColor SpawnFlagColor { get; set; }
    public int SpawnFlagNumber { get; set; }
    public int SpawnDelayToNextDestination { get; set; }
    public FlagColor Destination1FlagColor { get; set; }
    public int Destination1FlagNumber { get; set; }
    public int Destination1DelayToNextDestination { get; set; }
    public FlagColor Destination2FlagColor { get; set; }
    public int Destination2FlagNumber { get; set; }
    public int Destination2DelayToNextDestination { get; set; }
    public FlagColor Destination3FlagColor { get; set; }
    public int Destination3FlagNumber { get; set; }
    public int Destination3DelayToNextDestination { get; set; }
    public FlagColor Destination4FlagColor { get; set; }
    public int Destination4FlagNumber { get; set; }
    /// <summary>
    /// 0 = Viking ship
    /// 1 = Trade ship
    /// </summary>
    public int ShipType { get; set; }
    public bool DoesLeaveMap { get; set; }
  }
}
