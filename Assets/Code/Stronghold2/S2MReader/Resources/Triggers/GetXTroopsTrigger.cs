namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Used for archers in the peace SIM mission 4, but it's assumed this is a generic trigger for getting X troops.
  /// </summary>
  internal class GetXTroopsTrigger : Trigger
  {
    public int NumberOfTroops { get; set; }
  }
}
