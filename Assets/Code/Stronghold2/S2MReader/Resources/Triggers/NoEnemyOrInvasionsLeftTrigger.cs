namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  internal class NoEnemyOrInvasionsLeftTrigger : Trigger
  {
    /// <summary>
    /// If this is true, then this trigger represents a check for "No enemies or invasions left".
    /// If this is false, then this trigger is a condition for "any enemy or invasions left"
    /// </summary>
    public bool AreEnemiesOnMapCheck { get; set; }
  }
}
