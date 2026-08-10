namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Convert's a castle estate into a village estate.
  /// </summary>
  internal class ConvertEstateToVillageAction : Action
  {
    public FlagColor LocationFlagColor { get; set; }
    public int LocationFlagNumber { get; set; }
  }
}
