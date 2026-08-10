using Assets.Code.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// Player reaches the specific number of population.
  /// </summary>
  internal class PopulationReachedTrigger : Trigger
  {
    public int Population { get; set; }
  }
}
