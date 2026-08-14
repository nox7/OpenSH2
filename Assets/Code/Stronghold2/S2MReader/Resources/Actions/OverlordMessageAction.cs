using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  internal class OverlordMessageAction : Action
  {
    public S2MLords Lord { get; set; }
    /// <summary>
    /// Unknown if this is actually what this represents.
    /// </summary>
    public int SoundFileId { get; set; }
  }
}
