using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  public enum LaborRole { Woodcutter,Stonemason,OxHandler,IronMiner,PitchWorker }

  /// <summary>Shared employment contract. Building subclasses own arrival and future work mechanics.</summary>
  public abstract class LaborJob
  {
    public event System.Action<LaborJob> StaffingChanged;
    public LaborBuilding Building { get; internal set; }
    public virtual void CompleteWorkStep() { }
    public abstract LaborRole Role { get; }
    public int BuildingId { get; }
    public int EstateId { get; }
    public MapPoint Approach { get; }
    public MapPoint WorkPosition { get; }
    public float WorkHeading { get; }
    public float RawHeight { get; }
    public int? WorkerId { get; private set; }
    public bool IsOccupied { get; private set; }
    public virtual float TravelSpeedMultiplier => 1f;

    protected LaborJob(int buildingId,int estateId,MapPoint approach,MapPoint workPosition,float heading,float rawHeight)
    { BuildingId=buildingId;EstateId=estateId;Approach=approach;WorkPosition=workPosition;WorkHeading=heading;RawHeight=rawHeight; }
    internal bool TryReserve(int actorId)
    { if(WorkerId.HasValue)return false;WorkerId=actorId;StaffingChanged?.Invoke(this);return true; }
    internal void Arrive(WorkerComponent worker)
    {
      OnWorkerArrived(worker);
      IsOccupied=true;
      StaffingChanged?.Invoke(this);
    }
    protected virtual void OnWorkerArrived(WorkerComponent worker)
      => worker.Movement.Place(WorkPosition,WorkHeading);
    /// <summary>Override in a building job to implement its work cycle. Travel and assignment stay shared.</summary>
    public virtual void AdvanceWork(WorkerComponent worker,float seconds) { }
    internal void RemoveWorker(WorkerComponent worker)
    {
      if(WorkerId!=worker.ActorId)return;
      WorkerId=null;IsOccupied=false;OnWorkerRemoved(worker);
      StaffingChanged?.Invoke(this);
    }
    protected virtual void OnWorkerRemoved(WorkerComponent worker) { }
  }
}
