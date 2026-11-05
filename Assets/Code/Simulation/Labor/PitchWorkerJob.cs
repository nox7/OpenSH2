using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  [Serializable] public sealed class PitchRigSettings
  {
    public float WalkSpeedCellsPerSecond=1;
    public float PumpAnimationSpeed=1;
    public float RetrySeconds=2;
    public void Validate()
    {
      foreach(float value in new[]{WalkSpeedCellsPerSecond,PumpAnimationSpeed,RetrySeconds})
        if(!(value>0) || float.IsInfinity(value))throw new ArgumentOutOfRangeException(nameof(PitchRigSettings));
    }
  }
  public enum PitchWorkerPhase { Entering,Pumping,Exiting,WaitingForStockpile,Delivering,Unloading,Returning }

  /// <summary>One worker carries exactly two pitch after five synchronized scoop loops.
  /// Storage is discovered through the estate registry; no per-rig store registration.</summary>
  public sealed class PitchWorkerJob : LaborJob
  {
    public const int RequiredWorkers=1,PitchPerBatch=2,PumpLoops=5;
    public override LaborRole Role=>LaborRole.PitchWorker;
    public PitchRigSettings Settings { get; }
    public int EquipmentId { get; }
    public MapPoint BuildingPosition { get; }
    public float BuildingHeading { get; }
    public float RigRawHeight { get; }
    public float ApproachRawHeight { get; }
    public bool ExtractionSiteValid { get; }
    public PitchWorkerPhase Phase { get; private set; }
    public float PhaseTime { get; private set; }
    public int Revision { get; private set; }
    public int Cargo { get; private set; }
    public long DeliveredPitch { get; private set; }
    public int? StockpileId { get; private set; }
    public bool Moving { get; private set; }
    public float PumpClipSeconds { get; private set; }=4f/3f;
    private readonly NavigationGrid navigation;
    private readonly MapPoint pier;
    private EstateInventory inventory;
    private StockpileLocator stores;
    private NavigationGrid.PathRequest request;
    private bool routeReady;
    private int routeRevision,entryLeg;
    private float retry,depositTime;
    public PitchWorkerJob(int building,int estate,int equipment,MapPoint approach,MapPoint pier,MapPoint work,
      MapPoint origin,float heading,float rigHeight,bool valid,NavigationGrid navigation,PitchRigSettings settings,float? approachRawHeight=null)
      :base(building,estate,approach,work,heading+225,rigHeight+382)
    {
      settings.Validate();Settings=new PitchRigSettings{WalkSpeedCellsPerSecond=settings.WalkSpeedCellsPerSecond,PumpAnimationSpeed=settings.PumpAnimationSpeed,RetrySeconds=settings.RetrySeconds};
      EquipmentId=equipment;BuildingPosition=origin;BuildingHeading=heading;RigRawHeight=rigHeight;
      ApproachRawHeight=approachRawHeight??rigHeight;
      ExtractionSiteValid=valid;this.navigation=navigation;this.pier=pier;
    }
    public void ConfigureStorage(EstateInventory inventory,StockpileLocator stores){this.inventory=inventory;this.stores=stores;}
    public void SetPumpDuration(float duration)
    {if(!(duration>0) || float.IsInfinity(duration))throw new ArgumentOutOfRangeException(nameof(duration));PumpClipSeconds=duration;}
    protected override void OnWorkerArrived(WorkerComponent worker)
    {worker.Movement.Place(Approach,BuildingHeading);Change(PitchWorkerPhase.Entering);}
    private void Change(PitchWorkerPhase phase)
    {request?.Dispose();request=null;Phase=phase;PhaseTime=0;depositTime=0;entryLeg=0;routeReady=false;retry=0;Revision++;}
    protected override void OnWorkerRemoved(WorkerComponent worker)
    {request?.Dispose();request=null;Cargo=0;StockpileId=null;Moving=false;Change(PitchWorkerPhase.Entering);}
    public override void AdvanceWork(WorkerComponent worker,float seconds)
    {
      Moving=false;if(!ExtractionSiteValid)return;
      float remaining=seconds;
      while(remaining>0)
      {
        if(retry>0){float wait=Math.Min(retry,remaining);retry-=wait;remaining-=wait;if(remaining<=0)return;}
        switch(Phase)
        {
          case PitchWorkerPhase.Entering:
          case PitchWorkerPhase.Exiting:
            // The native rig has a short scripted platform approach across non-navigable swamp.
            // Only this known pier is traversed directly; all outdoor travel uses ground navigation.
            if(!routeReady)
            {
              var end=Phase==PitchWorkerPhase.Entering?(entryLeg==0?pier:WorkPosition):(entryLeg==0?pier:Approach);
              worker.Movement.Follow(new[]{worker.Movement.Position,end});routeReady=true;
            }
            if(!Move(worker,ref remaining))return;
            routeReady=false;if(++entryLeg<2)break;
            if(Phase==PitchWorkerPhase.Entering){worker.Movement.Place(WorkPosition,WorkHeading);Change(PitchWorkerPhase.Pumping);}
            else Change(PitchWorkerPhase.WaitingForStockpile);
            break;
          case PitchWorkerPhase.Pumping:
            float duration=PumpClipSeconds*PumpLoops/Settings.PumpAnimationSpeed;
            float step=Math.Min(remaining,Math.Max(0,duration-PhaseTime));PhaseTime+=step;remaining-=step;
            if(PhaseTime+1e-6f<duration)return;
            Cargo=PitchPerBatch;Change(PitchWorkerPhase.Exiting);break;
          case PitchWorkerPhase.WaitingForStockpile:
            if(inventory==null || stores==null){retry=Settings.RetrySeconds;return;}
            IReadOnlyList<MapPoint> delivery;int store;
            if(navigation.PathRequests!=null)
            {
              request??=stores.RequestNearest(EstateId,Goods.Pitch,worker.Movement.Position);
              if(request.Status==PathRequestStatus.Pending)return;
              var result=request;request=null;
              if(result.Status!=PathRequestStatus.Succeeded || result.Revision!=navigation.Revision || !inventory.CanDeposit(result.TargetId,EstateId,Goods.Pitch,1)){retry=Settings.RetrySeconds;return;}
              delivery=result.Path;store=result.TargetId;
            }
            else if(!stores.TryNearest(EstateId,Goods.Pitch,worker.Movement.Position,out store,out delivery)){retry=Settings.RetrySeconds;return;}
            StockpileId=store;Change(PitchWorkerPhase.Delivering);Follow(worker,delivery);break;
          case PitchWorkerPhase.Delivering:
          case PitchWorkerPhase.Returning:
            if(routeReady && routeRevision!=navigation.Revision && worker.Movement.RouteMayHaveChanged(navigation,routeRevision))
            {
              worker.Movement.Place(worker.Movement.Position,worker.Movement.Heading);routeReady=false;
              if(Phase==PitchWorkerPhase.Delivering){StockpileId=null;Change(PitchWorkerPhase.WaitingForStockpile);break;}
            }
            if(!routeReady)
            {
              IReadOnlyList<MapPoint> path;
              if(navigation.PathRequests!=null)
              {
                request??=navigation.PathRequests.Request(worker.Movement.Position,new[]{new PathTarget(BuildingId,Approach)});
                if(request.Status==PathRequestStatus.Pending)return;
                var result=request;request=null;
                if(result.Status!=PathRequestStatus.Succeeded || result.Revision!=navigation.Revision){retry=Settings.RetrySeconds;return;}path=result.Path;
              }
              else if(!navigation.TryFindPath(worker.Movement.Position,Approach,out path)){retry=Settings.RetrySeconds;return;}
              Follow(worker,path);
            }
            routeRevision=navigation.Revision;
            if(!Move(worker,ref remaining))return;
            Change(Phase==PitchWorkerPhase.Delivering?PitchWorkerPhase.Unloading:PitchWorkerPhase.Entering);break;
          case PitchWorkerPhase.Unloading:
            if(Cargo==0){StockpileId=null;Change(PitchWorkerPhase.Returning);break;}
            if(!StockpileId.HasValue || !inventory.CanDeposit(StockpileId.Value,EstateId,Goods.Pitch,1))
            {StockpileId=null;Change(PitchWorkerPhase.WaitingForStockpile);break;}
            float interval=(float)inventory.DeliverySettings.IntervalSeconds;
            float deposit=Math.Min(remaining,Math.Max(0,interval-depositTime));depositTime+=deposit;PhaseTime+=deposit;remaining-=deposit;
            if(depositTime+1e-6f<interval)return;
            if(inventory.TryDeposit(StockpileId.Value,EstateId,Goods.Pitch,1)){Cargo--;DeliveredPitch++;Revision++;}
            depositTime=0;break;
        }
      }
    }
    private void Follow(WorkerComponent worker,IReadOnlyList<MapPoint> route)
    {worker.Movement.Follow(route);routeReady=true;routeRevision=navigation.Revision;}
    private bool Move(WorkerComponent worker,ref float remaining)
    {float before=remaining;worker.Movement.Advance(ref remaining,Settings.WalkSpeedCellsPerSecond);PhaseTime+=before-remaining;Moving|=before>remaining;return !worker.Movement.IsFollowingPath;}
  }
}
