using System;using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Movement;using Assets.Code.Simulation.Economy;
namespace Assets.Code.Simulation.Labor
{
  public enum OxHaulingPhase { MeetingOx,WaitingForQuarry,ToQuarry,Loading,WaitingForStockpile,ToStockpile,Unloading }
  public sealed class OxHandlerJob : LaborJob
  {
    public override LaborRole Role=>LaborRole.OxHandler;
    public OxHaulingSettings Settings { get; }public OxHaulingNetwork Network { get; private set; }
    public int EquipmentBaseId { get; }public MovementComponent Ox { get; }
    public MapPoint TetherPosition { get; }public float TetherHeading { get; }
    public bool OxVisible=>WorkerId.HasValue;public bool OxMoving { get; private set; }
    public bool HandlerMoving { get; private set; }public float OxAnimationTime { get; private set; }
    public float PhaseTime { get; private set; }public int Revision { get; private set; }
    public int CargoCount { get; private set; }public Goods CargoGoods { get; private set; }=Goods.Stone;
    public int CargoStone=>CargoGoods==Goods.Stone?CargoCount:0;
    public int CargoIron=>CargoGoods==Goods.Iron?CargoCount:0;
    public long DeliveredStone { get; private set; }public long DeliveredIron { get; private set; }
    public int Capacity=>CargoGoods==Goods.Iron?Settings.CapacityIron:Settings.CapacityStone;
    // Native cargo art has eight variants; logical goods stay one-for-one with the quarry and store.
    public int VisibleLoadBlocks=>Math.Min(8,CargoGoods==Goods.Iron?CargoCount:(CargoCount+1)/2);
    public OxHaulingPhase Phase { get; private set; }public OxHaulingSource Source { get; private set; }
    public int? StockpileId { get; private set; }
    private float retry,actionTime;private int navRevision;private bool routeReady;
    private MapPoint goal;private double handlerStart,oxStart;private float joinDistance;
    public OxHandlerJob(int building,int estate,int equipment,MapPoint approach,MapPoint tether,float heading,float height,OxHaulingSettings settings)
      :base(building,estate,approach,approach,heading,height)
    {settings.Validate();Settings=settings;EquipmentBaseId=equipment;TetherPosition=tether;TetherHeading=heading;Ox=new MovementComponent(tether,heading);}
    public void Configure(OxHaulingNetwork network)=>Network=network??throw new ArgumentNullException(nameof(network));
    protected override void OnWorkerArrived(WorkerComponent worker){Change(OxHaulingPhase.MeetingOx);}
    private void Change(OxHaulingPhase phase){Phase=phase;PhaseTime=0;actionTime=0;retry=0;Revision++;}
    private void ReleaseSource(){Source?.Production.ReleaseHauler(WorkerId);Source=null;}
    protected override void OnWorkerRemoved(WorkerComponent worker)
    {
      Source?.Production.ReleaseHauler(worker.ActorId);Source=null;CargoCount=0;CargoGoods=Goods.Stone;StockpileId=null;routeReady=false;
      Ox.Place(TetherPosition,TetherHeading);OxMoving=false;HandlerMoving=false;OxAnimationTime=0;Change(OxHaulingPhase.MeetingOx);
    }
    public override void AdvanceWork(WorkerComponent worker,float seconds)
    {
      OxMoving=false;HandlerMoving=false;if(Network==null)return;float remaining=seconds;
      while(remaining>0)
      {
        if(retry>0){float wait=Math.Min(retry,remaining);retry-=wait;remaining-=wait;PhaseTime+=wait;if(retry>0 || remaining<=0)return;}
        switch(Phase)
        {
          case OxHaulingPhase.MeetingOx:
            // Until the handler physically reaches the ox, it stays at its post.
            if(MapPoint.Distance(worker.Movement.Position,Ox.Position)>Settings.HandlerPickupRadius)
            {
              if(!routeReady)
              {
                if(!Network.Navigation.TryNearest(Ox.Position,2,out var meet) || !Network.Navigation.TryFindPath(worker.Movement.Position,meet,out var meetPath))
                {retry=Settings.RetrySeconds;return;}
                worker.Movement.Follow(meetPath);routeReady=true;
              }
              float before=remaining;worker.Movement.Advance(ref remaining,Settings.HandlerSpeedCellsPerSecond);PhaseTime+=before-remaining;HandlerMoving=before>remaining;
              if(MapPoint.Distance(worker.Movement.Position,Ox.Position)>Settings.HandlerPickupRadius)return;
            }
            routeReady=false;Change(OxHaulingPhase.WaitingForQuarry);break;
          case OxHaulingPhase.WaitingForQuarry:
            if(!Network.TrySource(worker.ActorId,EstateId,worker.Movement.Position,out var source,out var sourcePath,CargoCount>0?CargoGoods:(Goods?)null)){retry=Settings.RetrySeconds;return;}
            Source=source;CargoGoods=source.Resource;if(!BeginTravel(worker,sourcePath,OxHaulingPhase.ToQuarry)){ReleaseSource();retry=Settings.RetrySeconds;return;}break;
          case OxHaulingPhase.ToQuarry:
          case OxHaulingPhase.ToStockpile:
            if(!Travel(worker,ref remaining))return;
            Change(Phase==OxHaulingPhase.ToQuarry?OxHaulingPhase.Loading:OxHaulingPhase.Unloading);break;
          case OxHaulingPhase.Loading:
            if(Source==null || Source.Production.HaulerId!=worker.ActorId){ReleaseSource();Change(OxHaulingPhase.WaitingForQuarry);break;}
            if(CargoCount>=Capacity)
            {Change(OxHaulingPhase.WaitingForStockpile);break;}
            // An empty output buffer is temporary: keep this quarry reserved and
            // wait here for production instead of dispatching a partial load.
            if(Source.Production.StoredGoods==0)
            {actionTime=0;PhaseTime+=remaining;return;}
            if(!Wait(CargoGoods==Goods.Iron?Settings.LoadSecondsPerIron:Settings.LoadSecondsPerStone,ref remaining))return;
            if(Source.Production.TryTakeGoods(1)){CargoCount++;Revision++;}actionTime=0;break;
          case OxHaulingPhase.WaitingForStockpile:
            if(!Network.TryDestination(EstateId,worker.Movement.Position,out int store,out var delivery,CargoGoods)){retry=Settings.RetrySeconds;return;}
            StockpileId=store;if(!BeginTravel(worker,delivery,OxHaulingPhase.ToStockpile)){retry=Settings.RetrySeconds;return;}
            ReleaseSource();break;
          case OxHaulingPhase.Unloading:
            if(CargoCount==0){StockpileId=null;Change(OxHaulingPhase.WaitingForQuarry);break;}
            if(!StockpileId.HasValue || !Network.Inventory.CanDeposit(StockpileId.Value,EstateId,CargoGoods,1))
            {StockpileId=null;Change(OxHaulingPhase.WaitingForStockpile);break;}
            if(!Wait((float)Network.Inventory.DeliverySettings.IntervalSeconds,ref remaining))return;
            if(Network.Inventory.TryDeposit(StockpileId.Value,EstateId,CargoGoods,1))
            {CargoCount--;if(CargoGoods==Goods.Iron)DeliveredIron++;else DeliveredStone++;Revision++;}actionTime=0;break;
        }
      }
    }
    private bool BeginTravel(WorkerComponent worker,IReadOnlyList<MapPoint> path,OxHaulingPhase phase)
    {
      if(!Network.Navigation.TryNearest(Ox.Position,2,out var oxStartPoint) || !Network.Navigation.TryFindPath(oxStartPoint,worker.Movement.Position,out var join))return false;
      // Reuse the handler's route, including every corner. The model's size adds no clearance.
      var oxPath=new List<MapPoint>{Ox.Position};if(MapPoint.Distance(Ox.Position,oxStartPoint)>.001f)oxPath.Add(oxStartPoint);
      for(int i=1;i<join.Count;i++)oxPath.Add(join[i]);joinDistance=OxHaulingNetwork.Length(oxPath);
      for(int i=1;i<path.Count;i++)oxPath.Add(path[i]);
      handlerStart=worker.Movement.DistanceTravelled;oxStart=Ox.DistanceTravelled;
      worker.Movement.Follow(path);Ox.Follow(oxPath);goal=path[path.Count-1];navRevision=Network.Navigation.Revision;routeReady=true;Change(phase);return true;
    }
    private bool Travel(WorkerComponent worker,ref float remaining)
    {
      if(!routeReady || navRevision!=Network.Navigation.Revision && (!worker.Movement.CanContinue(Network.Navigation) || !Ox.CanContinue(Network.Navigation)))
      {
        if(!Network.Navigation.TryFindPath(worker.Movement.Position,goal,out var path) || !BeginTravel(worker,path,Phase))
        {
          // A closed route must not hold a quarry hostage or pin cargo to a blocked store.
          worker.Movement.Place(worker.Movement.Position,worker.Movement.Heading);Ox.Place(Ox.Position,Ox.Heading);routeReady=false;
          if(Phase==OxHaulingPhase.ToQuarry){ReleaseSource();Change(OxHaulingPhase.WaitingForQuarry);}
          else{StockpileId=null;Change(OxHaulingPhase.WaitingForStockpile);}
          retry=Settings.RetrySeconds;return false;
        }
      }
      navRevision=Network.Navigation.Revision;float before=remaining;
      worker.Movement.Advance(ref remaining,Settings.HandlerSpeedCellsPerSecond);float used=before-remaining;HandlerMoving|=used>0;PhaseTime+=used;
      double available=Math.Max(0,joinDistance+worker.Movement.DistanceTravelled-handlerStart-Settings.FollowDistance-(Ox.DistanceTravelled-oxStart));
      float oxTime=Math.Min(before,(float)available/Settings.OxSpeedCellsPerSecond),oxBefore=oxTime;double distance=Ox.DistanceTravelled;
      Ox.Advance(ref oxTime,Settings.OxSpeedCellsPerSecond);float moved=(float)(Ox.DistanceTravelled-distance);
      if(moved>0){OxMoving=true;OxAnimationTime+=moved/1.5f;}
      if(worker.Movement.IsFollowingPath)return false;
      // Handler waits for the ox to catch up at collection and delivery points.
      float extra=Math.Max(0,oxBefore-oxTime-used);remaining=Math.Max(0,remaining-extra);PhaseTime+=extra;
      if(MapPoint.Distance(worker.Movement.Position,Ox.Position)>Settings.HandlerPickupRadius)return false;
      routeReady=false;return true;
    }
    private bool Wait(float duration,ref float remaining)
    {float step=Math.Min(remaining,Math.Max(0,duration-actionTime));actionTime+=step;PhaseTime+=step;remaining-=step;return actionTime+1e-6f>=duration;}
  }
}
