namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Creates criminals in player's estate
  /// </summary>
  internal class CreateCriminalsAction : Action
  {
    /// <summary>
    /// % of population to convert to criminals.
    /// </summary>
    public int PercentageOfPopulation { get; set; }
  }
}
