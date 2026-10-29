using System;using System.Collections.Generic;using Assets.Code.Simulation.Economy;using Assets.Code.Simulation.Navigation;
namespace Assets.Code.Simulation.Labor
{
  [Serializable] public sealed class IronMineSettings
  {
    public int MaximumIron=10;
    public float CycleSeconds=1577f/40f;
    public float PileOffsetX=1.75f,PileOffsetY=.125f,PileOffsetZ=.298828125f;
    public void Validate()
    {
      if(MaximumIron<1 || MaximumIron>10 || !(CycleSeconds>0) || float.IsInfinity(CycleSeconds))throw new ArgumentOutOfRangeException(nameof(IronMineSettings));
      foreach(float f in new[]{PileOffsetX,PileOffsetY,PileOffsetZ})if(float.IsNaN(f) || float.IsInfinity(f))throw new ArgumentOutOfRangeException(nameof(IronMineSettings));
    }
  }
  public sealed class IronMineProduction : IOxProduction
  {
    public const int RequiredWorkers=2;
    public const float NativeCycleSeconds=1577f/40f,OutputSeconds=1436f/40f;
    public const float ClimbDown=140f/40f,ClimbUp=574f/40f,LeverUp=688f/40f,MoveIron=827f/40f,WalkBack=1248f/40f;
    private static readonly float[] boundaries={ClimbDown,ClimbUp,LeverUp,MoveIron,828f/40f,WalkBack,OutputSeconds,NativeCycleSeconds};
    public Goods Resource=>Goods.Iron;public int StoredGoods=>StoredIron;
    public int StoredIron { get; private set; }public int MaximumIron=>Settings.MaximumIron;public bool IsFull=>StoredIron>=MaximumIron;
    public int? HaulerId { get; private set; }public int Revision { get; private set; }
    public IronMineSettings Settings { get; }public IReadOnlyList<IronMinerJob> Jobs { get; }
    private readonly List<IronMinerJob> jobs=new();private double progress;private bool running;
    public double WorkSeconds { get; private set; }
    public float NativeTime=>(float)(progress*NativeCycleSeconds/Settings.CycleSeconds);
    public float AnimationRate=>NativeCycleSeconds/Settings.CycleSeconds;
    private bool Ready {get{if(jobs.Count!=2)return false;foreach(var job in jobs)if(!job.IsOccupied)return false;return true;}}
    public bool IsWorking=>running && !IsFull && Ready;
    public event Action<IronMineProduction> Changed;
    public IronMineProduction(IronMineSettings settings)
    {settings.Validate();Settings=new IronMineSettings{MaximumIron=settings.MaximumIron,CycleSeconds=settings.CycleSeconds,PileOffsetX=settings.PileOffsetX,PileOffsetY=settings.PileOffsetY,PileOffsetZ=settings.PileOffsetZ};Jobs=jobs.AsReadOnly();}
    internal void Add(IronMinerJob job)
    {if(job.SlotIndex<0 || job.SlotIndex>=2)throw new ArgumentOutOfRangeException();foreach(var other in jobs)if(other.SlotIndex==job.SlotIndex || other.BuildingId!=job.BuildingId || other.EstateId!=job.EstateId)throw new ArgumentException("Mine crew must have two distinct slots in one building.");jobs.Add(job);}
    internal void Stop()
    {if(running || WorkSeconds>0)Revision++;running=false;progress=0;WorkSeconds=0;foreach(var job in jobs)job.PendingSeconds=0;}
    public void CompleteWorkStep()
    {
      if(!Ready){Stop();return;}
      double seconds=double.PositiveInfinity;foreach(var job in jobs)seconds=Math.Min(seconds,job.PendingSeconds);foreach(var job in jobs)job.PendingSeconds=0;
      if(IsFull || seconds<=0)return;
      if(!running){running=true;progress=0;Revision++;}
      while(seconds>0 && !IsFull)
      {
        float beat=NativeCycleSeconds;double boundary=Settings.CycleSeconds;
        foreach(float candidate in boundaries){double at=(double)candidate/NativeCycleSeconds*Settings.CycleSeconds;if(at>progress+1e-7){beat=candidate;boundary=at;break;}}
        double step=Math.Min(seconds,Math.Max(0,boundary-progress));progress+=step;WorkSeconds+=step;seconds-=step;
        if(progress+1e-7<boundary)break;progress=boundary;Revision++;
        if(beat==OutputSeconds){StoredIron++;Changed?.Invoke(this);if(IsFull){running=false;break;}}
        if(beat==NativeCycleSeconds)progress=0;
      }
    }
    public bool TryTakeGoods(int count)=>TryTakeIron(count);
    public bool TryTakeIron(int count)
    {if(count<1 || count>StoredIron)return false;StoredIron-=count;Revision++;Changed?.Invoke(this);return true;}
    public bool TryReserveHauler(int handler){if(HaulerId.HasValue && HaulerId!=handler)return false;HaulerId=handler;return true;}
    public void ReleaseHauler(int? handler){if(HaulerId==handler)HaulerId=null;}
  }
  public sealed class IronMinerJob : LaborJob
  {
    public override LaborRole Role=>LaborRole.IronMiner;
    public IronMineProduction Production { get; }public int SlotIndex { get; }public int EquipmentBaseId { get; }
    public MapPoint BuildingPosition { get; }public float BuildingHeading { get; }internal double PendingSeconds;
    public IronMinerJob(int building,int estate,int slot,int equipment,MapPoint approach,MapPoint origin,float heading,float height,IronMineProduction production)
      :base(building,estate,approach,origin,heading,height)
    {SlotIndex=slot;EquipmentBaseId=equipment;BuildingPosition=origin;BuildingHeading=heading;Production=production;production.Add(this);}
    public override void AdvanceWork(WorkerComponent worker,float seconds)=>PendingSeconds+=seconds;
    public override void CompleteWorkStep(){if(SlotIndex==0)Production.CompleteWorkStep();}
    protected override void OnWorkerRemoved(WorkerComponent worker){PendingSeconds=0;Production.Stop();}
  }
}
