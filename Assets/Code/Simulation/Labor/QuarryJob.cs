using Assets.Code.Simulation.Navigation;
namespace Assets.Code.Simulation.Labor
{
  public sealed class QuarryJob : LaborJob
  {
    public override LaborRole Role=>LaborRole.Stonemason;
    public QuarryProduction Production { get; }public int SlotIndex { get; }public int EquipmentBaseId { get; }
    public MapPoint BuildingPosition { get; }public float BuildingHeading { get; }
    internal double PendingSeconds;
    public QuarryJob(int building,int estate,int slot,int equipment,MapPoint approach,MapPoint work,float heading,float height,MapPoint origin,QuarryProduction production)
      :base(building,estate,approach,work,heading,height)
    {SlotIndex=slot;EquipmentBaseId=equipment;BuildingPosition=origin;BuildingHeading=heading;Production=production;production.Add(this);}
    public override void AdvanceWork(WorkerComponent worker,float seconds){PendingSeconds+=seconds;}
    public override void CompleteWorkStep(){if(SlotIndex==0)Production.CompleteWorkStep();}
    protected override void OnWorkerRemoved(WorkerComponent worker){PendingSeconds=0;Production.Stop();}
  }
}
