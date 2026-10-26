using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  public sealed class WoodcutterJob : LaborJob
  {
    public override LaborRole Role => LaborRole.Woodcutter;
    public int CartActorId { get; }
    public MapPoint CartPosition => WorkPosition;
    public float CartHeading => WorkHeading;
    public WoodcutterJob(int buildingId,int estateId,int cartActorId,MapPoint approach,MapPoint cartPosition,float heading,float rawHeight)
      : base(buildingId,estateId,approach,cartPosition,heading,rawHeight) { CartActorId=cartActorId; }
    // Arrival uses the shared instantaneous placement. Future felling/hauling cycles belong here.
  }
}
