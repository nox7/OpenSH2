namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Set's the player's available troops they can produce
  /// </summary>
  internal class SetAvailableTroopTypesAction : Action
  {
    public UnitBooleanList Troops { get; set; } = new();
  }
}
