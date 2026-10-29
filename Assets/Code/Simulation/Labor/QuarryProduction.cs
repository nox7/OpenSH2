using System;using System.Collections.Generic;
namespace Assets.Code.Simulation.Labor
{
  [Serializable] public sealed class QuarryStoneLayout
  {
    // A center-out 3x3 bank beside the chiseler, wholly on the native front platform.
    public int GridColumns=3,GridRows=3;
    public float BlockSpacing=.22f,LayerHeight=.14f;
    public float OffsetX=1.18f,OffsetY=.076f,OffsetZ=-.292f;
    public QuarryStoneLayout Copy()=>new QuarryStoneLayout{GridColumns=GridColumns,GridRows=GridRows,BlockSpacing=BlockSpacing,LayerHeight=LayerHeight,OffsetX=OffsetX,OffsetY=OffsetY,OffsetZ=OffsetZ};
    // Square spiral, clipped to the configured odd-sized rectangle. No per-frame allocation.
    public void CellAt(int index,out int x,out int z)
    {
      int target=index%(GridColumns*GridRows),found=0,cx=0,cz=0,dx=1,dz=0,length=1;
      x=0;z=0;if(target==0)return;
      for(;;length++)for(int turn=0;turn<2;turn++)
      {
        for(int step=0;step<length;step++)
        {
          cx+=dx;cz+=dz;
          if(Math.Abs(cx)<=GridColumns/2 && Math.Abs(cz)<=GridRows/2 && ++found==target){x=cx;z=cz;return;}
        }
        int previous=dx;dx=-dz;dz=previous;
      }
    }
    public void Validate()
    {
      if(GridColumns<1 || GridRows<1 || GridColumns%2==0 || GridRows%2==0)throw new ArgumentOutOfRangeException(nameof(QuarryStoneLayout));
      foreach(float f in new[]{OffsetX,OffsetY,OffsetZ})if(float.IsNaN(f) || float.IsInfinity(f))throw new ArgumentOutOfRangeException(nameof(QuarryStoneLayout));
      foreach(float f in new[]{BlockSpacing,LayerHeight})if(!(f>0) || float.IsInfinity(f))throw new ArgumentOutOfRangeException(nameof(QuarryStoneLayout));
    }
  }
  [Serializable] public sealed class QuarrySettings
  {
    public int MaximumStone=48;
    // Transport and chiseling overlap; the bench keeps its own rock after handoff.
    public float CycleDurationSeconds=373f/40f;
    // Reforged: first chiseling beat 227, first output beat 600, repeat every 373 ticks at 40 Hz.
    public float InitialLoadingSeconds=227f/40f;
    // Native poses have no event tracks. These estimated change points are adjustable.
    public float[] RockStageFractions={.2f,.4f,.6f,.8f};
    public QuarryStoneLayout StoneLayout=new();
    public void Validate()
    {
      if(MaximumStone<1 || MaximumStone>48)throw new ArgumentOutOfRangeException(nameof(QuarrySettings));
      StoneLayout??=new QuarryStoneLayout();StoneLayout.Validate();
      if(!(CycleDurationSeconds>0) || float.IsInfinity(CycleDurationSeconds))throw new ArgumentOutOfRangeException(nameof(CycleDurationSeconds));
      if(!(InitialLoadingSeconds>0 && InitialLoadingSeconds<CycleDurationSeconds))throw new ArgumentOutOfRangeException(nameof(InitialLoadingSeconds));
      if(RockStageFractions==null || RockStageFractions.Length!=4)throw new ArgumentException("Quarry requires four rock stage transitions.");
      float previous=0;foreach(float fraction in RockStageFractions)
      {if(!(fraction>previous && fraction<1))throw new ArgumentOutOfRangeException(nameof(RockStageFractions));previous=fraction;}
    }
  }
  /// <summary>One shared production clock and finite local buffer for a quarry, not one clock per mason.</summary>
  public sealed class QuarryProduction : IOxProduction
  {
    public Assets.Code.Simulation.Economy.Goods Resource=>Assets.Code.Simulation.Economy.Goods.Stone;
    public int StoredGoods=>StoredStone;
    public bool TryTakeGoods(int count)=>TryTakeStone(count);
    public const int RequiredWorkers=3;
    public const float NativeCycleSeconds=9.333333f,NativeRockSeconds=6.333333f;
    // Keep crane, lift/hook workers and carried rock together at the loading beat.
    // The remaining return motion fills the rest of the cycle without a pause.
    public float TransportAnimationRate=>CycleElapsedSeconds<BenchArrivalSeconds?RockAnimationRate:
      (NativeCycleSeconds-NativeRockSeconds)/(Settings.CycleDurationSeconds-Settings.InitialLoadingSeconds);
    public float HammerAnimationRate=>NativeCycleSeconds/Settings.CycleDurationSeconds;
    public double CycleSeconds=>Settings.CycleDurationSeconds;
    public double BenchArrivalSeconds=>Settings.InitialLoadingSeconds;
    public bool IsChiseling=>IsWorking && WorkSeconds>=BenchArrivalSeconds;
    public float RockAnimationRate=>NativeRockSeconds/Settings.InitialLoadingSeconds;
    public float RockAnimationTime=>(float)CycleElapsedSeconds*RockAnimationRate;
    public double FirstBlockSeconds=>BenchArrivalSeconds+CycleSeconds;
    public double CycleElapsedSeconds=>WorkSeconds%CycleSeconds;
    public bool WorkingRockVisible=>IsWorking && CycleElapsedSeconds<BenchArrivalSeconds;
    public int BenchRockStage
    {
      get
      {
        if(!IsWorking || WorkSeconds<BenchArrivalSeconds)return -1;
        double fraction=HammerPhase/CycleSeconds;int stage=0;
        while(stage<Settings.RockStageFractions.Length && fraction>=Settings.RockStageFractions[stage])stage++;
        return stage;
      }
    }
    public QuarrySettings Settings { get; }public int StoredStone { get; private set; }
    public int MaximumStone=>Settings.MaximumStone;public bool IsFull=>StoredStone>=MaximumStone;
    public int Revision { get; private set; }public double WorkSeconds { get; private set; }
    private bool running;private double nextOutputSeconds;
    private readonly List<QuarryJob> jobs=new();
    public IReadOnlyList<QuarryJob> Jobs { get; }
    public event Action<QuarryProduction> Changed;
    public QuarryProduction(QuarrySettings settings)
    {
      settings.Validate();Settings=new QuarrySettings{MaximumStone=settings.MaximumStone,CycleDurationSeconds=settings.CycleDurationSeconds,
        InitialLoadingSeconds=settings.InitialLoadingSeconds,RockStageFractions=(float[])settings.RockStageFractions.Clone(),StoneLayout=settings.StoneLayout.Copy()};
      Jobs=jobs.AsReadOnly();
    }
    internal void Add(QuarryJob job)
    {
      if(job.SlotIndex<0 || job.SlotIndex>=RequiredWorkers)throw new ArgumentOutOfRangeException(nameof(job.SlotIndex));
      foreach(var existing in jobs)
        if(existing.SlotIndex==job.SlotIndex || existing.BuildingId!=job.BuildingId || existing.EstateId!=job.EstateId)
          throw new ArgumentException("Quarry crew slots must be unique and belong to one building.");
      jobs.Add(job);
    }
    public bool IsWorking=>running && !IsFull && Ready;
    private bool Ready {get{if(jobs.Count!=RequiredWorkers)return false;foreach(var j in jobs)if(!j.IsOccupied)return false;return true;}}
    public float AnimationTime=>CycleElapsedSeconds<BenchArrivalSeconds?(float)CycleElapsedSeconds*RockAnimationRate:
      NativeRockSeconds+(float)(CycleElapsedSeconds-BenchArrivalSeconds)*TransportAnimationRate;
    private double HammerPhase=>WorkSeconds<BenchArrivalSeconds?0:((WorkSeconds-BenchArrivalSeconds)%CycleSeconds+CycleSeconds)%CycleSeconds;
    public float HammerAnimationTime=>(float)HammerPhase*HammerAnimationRate;
    internal void Stop()
    {
      if(running || WorkSeconds>0)Revision++;
      running=false;WorkSeconds=0;foreach(var job in jobs)job.PendingSeconds=0;
    }
    public void CompleteWorkStep()
    {
      if(!Ready){Stop();return;}
      double seconds=double.PositiveInfinity;foreach(var job in jobs)seconds=Math.Min(seconds,job.PendingSeconds);
      foreach(var job in jobs)job.PendingSeconds=0;
      if(IsFull || seconds<=0)return;
      if(!running){running=true;WorkSeconds=0;nextOutputSeconds=FirstBlockSeconds;Revision++;}
      // Split at initial loading, transport resets and output commits, even for long steps.
      while(seconds>0 && !IsFull)
      {
        double nextTransport=(Math.Floor((WorkSeconds+1e-7)/CycleSeconds)+1)*CycleSeconds;
        double boundary=Math.Min(nextTransport,nextOutputSeconds);
        double nextHandoff=Math.Floor((WorkSeconds+1e-7)/CycleSeconds)*CycleSeconds+BenchArrivalSeconds;
        if(nextHandoff<=WorkSeconds+1e-7)nextHandoff+=CycleSeconds;
        boundary=Math.Min(boundary,nextHandoff);
        double step=Math.Min(seconds,boundary-WorkSeconds);
        WorkSeconds+=step;seconds-=step;
        if(WorkSeconds+1e-7<boundary)break;
        WorkSeconds=boundary;Revision++;
        if(WorkSeconds+1e-7>=nextOutputSeconds)
        {
          StoredStone++;nextOutputSeconds+=CycleSeconds;Changed?.Invoke(this);
          if(IsFull)running=false;
        }
      }
    }
    public int? HaulerId { get; private set; }
    public bool TryReserveHauler(int handler)
    {if(HaulerId.HasValue && HaulerId!=handler)return false;HaulerId=handler;return true;}
    public void ReleaseHauler(int? handler){if(HaulerId==handler)HaulerId=null;}
    public bool TryTakeStone(int count)
    {
      if(count<=0 || count>StoredStone)return false;StoredStone-=count;Revision++;Changed?.Invoke(this);return true;
    }
  }
}
