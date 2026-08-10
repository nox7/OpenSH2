
namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// Limit's the player's production of weapons. How? Idk
  /// TODO Test this
  /// </summary>
  internal class LimitWeaponProductionAction : Action
  {
    public bool BowsEnabled { get; set; }
    public bool CrossbowsEnabled { get; set; }
    public bool SpearsEnabled { get; set; }
    public bool PikesEnabled { get; set; }
    public bool MacesEnabled { get; set; }
    public bool SwordsEnabled { get; set; }
  }
}
