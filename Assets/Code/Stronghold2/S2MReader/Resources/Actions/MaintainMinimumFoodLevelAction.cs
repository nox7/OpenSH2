
namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Causes the player's granary to never go below this food minimum
  /// </summary>
  internal class MaintainMinimumFoodLevelAction : Action
  {
    public int NumFoodUnits { get; set; }
  }
}
