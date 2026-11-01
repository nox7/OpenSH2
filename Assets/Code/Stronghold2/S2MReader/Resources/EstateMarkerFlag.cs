using Assets.Code.Stronghold2.S2MReader.Enums;
namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  public sealed class EstateMarkerFlag
  {
    public int EstateId { get; internal set; }
    public int PackedLocation { get; internal set; }
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int Z => Y;
    public EstateType Type { get; internal set; }
  }
}
