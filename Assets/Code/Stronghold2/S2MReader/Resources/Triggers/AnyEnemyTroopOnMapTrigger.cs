namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  internal class AnyEnemyTroopOnMapTrigger : Trigger
  {
    /// <summary>
    /// If this is true, then this trigger represents a check for "Enemy troops are on the map".
    /// If this is false, then this trigger is a condition for "no enemy troops left on map"
    /// </summary>
    public bool AreEnemyTroopsOnMapFlag { get; set; }
  }
}
