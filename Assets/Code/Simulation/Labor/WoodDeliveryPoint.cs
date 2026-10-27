using Assets.Code.Simulation.Navigation;
namespace Assets.Code.Simulation.Labor
{
  /// <summary>A stockpile approach authorized for this job's estate.</summary>
  public readonly struct WoodDeliveryPoint
  {
    public int BuildingId { get; }
    public MapPoint Position { get; }
    public WoodDeliveryPoint(int buildingId,MapPoint position) {BuildingId=buildingId;Position=position;}
  }
}
