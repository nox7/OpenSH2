namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Triggers when there is no food in the estate granary defined by the flag in the trigger
  /// </summary>
  internal class NoFoodInGranaryTrigger : Trigger
  {
    public FlagColor FlagColor { get; set; }
    public int FlagNumber { get; set; }
  }
}
