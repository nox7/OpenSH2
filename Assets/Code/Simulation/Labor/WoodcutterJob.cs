using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Economy;

namespace Assets.Code.Simulation.Labor
{
  public enum WoodcuttingPhase { WaitingForResource,DrivingOut,WalkingToTree,Felling,Falling,Chopping,WalkingToCart,Returning,Returned,Sawing,WaitingForStockpile,Delivering,AtStockpile,Unloading }
  public sealed class WoodcutterJob : LaborJob
  {
    public override LaborRole Role => LaborRole.Woodcutter;
    public int CartActorId { get; }
    public MapPoint CartPosition { get; private set; }
    public float CartHeading { get; private set; }
    public WoodcuttingPhase Phase { get; private set; }
    public float PhaseTime { get; private set; }
    public int Revision { get; private set; }
    public bool Loaded { get; private set; }
    public int RemainingPlanks { get; private set; }
    public bool CarryingPlanks => RemainingPlanks>0;
    public long CompletedDeliveries { get; private set; }
    private double unloadProgress;
    public int SawLogActorId => CartActorId ^ (1<<30);
    public MapPoint BuildingPosition { get; private set; }
    public MapPoint SawPosition { get; private set; }
    public float SawHeading { get; private set; }
    public float SawRawHeight { get; private set; }
    public int? StockpileId { get; private set; }
    private IReadOnlyList<WoodDeliveryPoint> stockpiles;
    public void ConfigureProduction(MapPoint sawPosition,float heading,IReadOnlyList<WoodDeliveryPoint> destinations,MapPoint? buildingPosition=null,float? sawRawHeight=null)
    { SawPosition=sawPosition;SawHeading=heading;SawRawHeight=sawRawHeight??RawHeight;BuildingPosition=buildingPosition??sawPosition;stockpiles=destinations??throw new ArgumentNullException(nameof(destinations)); }
    public bool CarryingLog { get; private set; }
    public bool WaitingForPath { get; private set; }
    public bool IsCartMoving => !WaitingForPath && (Phase==WoodcuttingPhase.DrivingOut || Phase==WoodcuttingPhase.Returning || Phase==WoodcuttingPhase.Delivering);
    public bool WorkerOnCart => Phase==WoodcuttingPhase.WaitingForResource || Phase==WoodcuttingPhase.DrivingOut || Phase==WoodcuttingPhase.Returning || Phase==WoodcuttingPhase.Returned || Phase==WoodcuttingPhase.WaitingForStockpile || Phase==WoodcuttingPhase.Delivering || Phase==WoodcuttingPhase.AtStockpile || Phase==WoodcuttingPhase.Unloading;
    public float CartAnimationTime { get; private set; }
    public float WheelDegrees { get; private set; }
    public bool HarvestEnabled => resources!=null;
    public float CartAnimationRate => settings==null?1:settings.CartSpeedCellsPerSecond/1.5f;
    public FoliageResource Target => route?.Resource;
    public WoodcuttingRoute Route => route;
    public float FellClipSeconds { get; private set; }=2;
    public float ChopClipSeconds { get; private set; }=2;
    public const int PlanksPerDelivery=16;
    public long DeliveredPlanks { get; private set; }
    private EstateInventory inventory;
    public void ConfigureStorage(EstateInventory inventory) => this.inventory=inventory??throw new ArgumentNullException(nameof(inventory));
    private FoliageResources resources;
    private WoodcuttingSettings settings;
    private WoodcuttingRoute route;
    private float retry;
    private int navigationRevision;
    private MapPoint goal;
    private bool cancelled;
    public WoodcutterJob(int buildingId,int estateId,int cartActorId,MapPoint approach,MapPoint cartPosition,float heading,float rawHeight)
      : base(buildingId,estateId,approach,cartPosition,heading,rawHeight)
    { CartActorId=cartActorId;CartPosition=cartPosition;CartHeading=heading; }
    public void ConfigureHarvest(FoliageResources resources,WoodcuttingSettings settings)
    { settings.Validate();this.resources=resources??throw new ArgumentNullException(nameof(resources));this.settings=settings; }
    public void SetClipDurations(float fell,float chop)
    {
      if(!(fell>0) || !(chop>0) || float.IsInfinity(fell) || float.IsInfinity(chop))throw new ArgumentOutOfRangeException();
      FellClipSeconds=fell;ChopClipSeconds=chop;
    }
    public override void AdvanceWork(WorkerComponent worker,float seconds)
    {
      if(resources==null || Phase==WoodcuttingPhase.Returned || Phase==WoodcuttingPhase.AtStockpile)return;
      float remaining=seconds;
      // Every movement/timer consumes exactly its portion, including terminal steps in large simulation ticks.
      while(true)
      {
        switch(Phase)
        {
          case WoodcuttingPhase.WaitingForResource:
            if(retry>0) { float wait=Math.Min(retry,remaining);retry-=wait;remaining-=wait;if(retry>0 || remaining<=0)return; }
            if(!resources.TryReserveNearest(worker.ActorId,CartPosition,WorkPosition,out route)) {retry=settings.RetrySeconds;return;}
            worker.Movement.Place(route.Start,CartHeading);CartPosition=route.Start;
            Begin(worker,WoodcuttingPhase.DrivingOut,resources.Carts,route.DriveOut,route.Parking);break;
          case WoodcuttingPhase.DrivingOut:
            if(!Travel(worker,resources.Carts,ref remaining,true))return;
            worker.Movement.Place(route.Dismount,CartHeading);
            Begin(worker,WoodcuttingPhase.WalkingToTree,resources.Walking,route.WalkOut,route.Chop);break;
          case WoodcuttingPhase.WalkingToTree:
            if(!Travel(worker,resources.Walking,ref remaining,false))return;
            worker.Movement.Place(worker.Movement.Position,Heading(worker.Movement.Position,Target.Position));
            Change(Target.State==FoliageState.LogPile?WoodcuttingPhase.Chopping:WoodcuttingPhase.Felling);break;
          case WoodcuttingPhase.Felling:
            if(!Wait(FellClipSeconds*settings.FellingCycles,ref remaining))return;
            resources.BeginFall(Target,worker.ActorId,Heading(worker.Movement.Position,Target.Position));Change(WoodcuttingPhase.Falling);break;
          case WoodcuttingPhase.Falling:
            bool done=Wait(settings.FallSeconds,ref remaining);
            resources.Fall(Target,worker.ActorId,PhaseTime/settings.FallSeconds);
            if(!done)return;resources.CreateLogPile(Target,worker.ActorId);Change(WoodcuttingPhase.Chopping);break;
          case WoodcuttingPhase.Chopping:
            if(!Wait(ChopClipSeconds*settings.ChoppingCycles,ref remaining))return;
            resources.TakeLog(Target,worker.ActorId);CarryingLog=true;
            BeginReturnWalk(worker);break;
          case WoodcuttingPhase.WalkingToCart:
            if(!Travel(worker,resources.Walking,ref remaining,false))return;
            Loaded=CarryingLog;CarryingLog=false;
            worker.Movement.Place(CartPosition,CartHeading);
            // Removing foliage may have opened a shorter return route. Never reuse a route blindly.
            resources.Carts.TryFindPath(CartPosition,route.Home,out var home);
            Begin(worker,WoodcuttingPhase.Returning,resources.Carts,home,route.Home);break;
          case WoodcuttingPhase.Returning:
            if(!Travel(worker,resources.Carts,ref remaining,true))return;
            CartPosition=WorkPosition;CartHeading=WorkHeading;worker.Movement.Place(CartPosition,CartHeading);
            if(cancelled || !Loaded || stockpiles==null) {Change(WoodcuttingPhase.Returned);return;}
            Loaded=false;worker.Movement.Place(SawPosition,SawHeading);Change(WoodcuttingPhase.Sawing);break;
          case WoodcuttingPhase.Sawing:
            if(!Wait(settings.SawingSeconds,ref remaining))return;
            Loaded=true;RemainingPlanks=PlanksPerDelivery;worker.Movement.Place(CartPosition,CartHeading);
            Change(WoodcuttingPhase.WaitingForStockpile);break;
          case WoodcuttingPhase.WaitingForStockpile:
            if(retry>0) {float step=Math.Min(retry,remaining);retry-=step;remaining-=step;if(retry>0 || remaining<=0)return;}
            if(!TryDelivery(worker)) {retry=settings.RetrySeconds;return;}
            break;
          case WoodcuttingPhase.Delivering:
            if(!Travel(worker,resources.Carts,ref remaining,true))return;
            if(inventory==null){Change(WoodcuttingPhase.AtStockpile);return;}
            Change(WoodcuttingPhase.Unloading);break;
          case WoodcuttingPhase.Unloading:
            // Each plank is its own transaction. Full stores may accept part of a cart;
            // competition cannot overfill a pallet or consume undelivered cargo.
            if(!StockpileId.HasValue || !inventory.CanDeposit(StockpileId.Value,EstateId,Goods.Wood,1))
            {StockpileId=null;Change(WoodcuttingPhase.WaitingForStockpile);break;}
            double interval=inventory.DeliverySettings.IntervalSeconds;
            if(!(interval>0) || double.IsInfinity(interval))throw new InvalidOperationException("Plank unload interval must be finite and positive.");
            float unloadStep=(float)Math.Min(remaining,Math.Max(0,interval-unloadProgress));
            remaining-=unloadStep;PhaseTime+=unloadStep;unloadProgress+=unloadStep;
            if(unloadProgress+interval*1e-6<interval)return;
            if(!inventory.TryDeposit(StockpileId.Value,EstateId,Goods.Wood,1))
            {StockpileId=null;Change(WoodcuttingPhase.WaitingForStockpile);break;}
            unloadProgress=0;RemainingPlanks--;DeliveredPlanks++;
            if(RemainingPlanks==0)
            {
              Loaded=false;CompletedDeliveries++;StockpileId=null;
              // Start at the stockpile; the resource registry keeps the original pile assigned
              // until empty, then chooses new foliage and still routes the log home for sawing.
              Change(WoodcuttingPhase.WaitingForResource);
            }
            break;
          default:return;
        }
        if(remaining<=0)return;
      }
    }
    // Destinations are map-level immutable data; paths are only computed when dispatching/retrying.
    private bool TryDelivery(WorkerComponent worker)
    {
      if(!resources.Carts.TryNearest(CartPosition,2,out var start))return false;
      IReadOnlyList<MapPoint> best=null;float bestDistance=float.PositiveInfinity;int id=0;var visited=new HashSet<(float,float)>();
      foreach(var destination in stockpiles)
      {
        if(inventory!=null && !inventory.CanDeposit(destination.BuildingId,EstateId,Goods.Wood,1))continue;
        if(MapPoint.Distance(CartPosition,destination.Position)-2>=bestDistance)continue;
        if(!resources.Carts.TryNearest(destination.Position,2,out var point)
          || !visited.Add((point.X,point.Z))
          || !resources.Carts.CanReach(start,point)
          || !resources.Carts.TryFindPath(start,point,out var path))continue;
        float distance=0;for(int i=1;i<path.Count;i++)distance+=MapPoint.Distance(path[i-1],path[i]);
        if(distance>=bestDistance)continue;bestDistance=distance;best=path;id=destination.BuildingId;
      }
      if(best==null)return false;
      CartPosition=start;worker.Movement.Place(start,CartHeading);StockpileId=id;Begin(worker,WoodcuttingPhase.Delivering,resources.Carts,best,best[best.Count-1]);return true;
    }
    private void BeginReturnWalk(WorkerComponent worker)
    {
      resources.Walking.TryFindPath(worker.Movement.Position,route.Dismount,out var walk);
      Begin(worker,WoodcuttingPhase.WalkingToCart,resources.Walking,walk,route.Dismount);
    }
    private void Begin(WorkerComponent worker,WoodcuttingPhase phase,NavigationGrid nav,IReadOnlyList<MapPoint> path,MapPoint destination)
    {
      Change(phase);goal=destination;navigationRevision=nav.Revision;
      if(path!=null && path.Count>0)worker.Movement.Follow(path);
      else { worker.Movement.Place(worker.Movement.Position,worker.Movement.Heading);WaitingForPath=true;retry=settings.RetrySeconds; }
    }
    private bool Travel(WorkerComponent worker,NavigationGrid nav,ref float remaining,bool cart)
    {
      if(WaitingForPath)
      {
        float wait=Math.Min(remaining,retry);retry-=wait;remaining-=wait;
        if(retry>0 || remaining<=0)return false;
        if(!nav.TryFindPath(worker.Movement.Position,goal,out var path)) {retry=settings.RetrySeconds;return false;}
        worker.Movement.Follow(path);WaitingForPath=false;Revision++;navigationRevision=nav.Revision;
      }
      if(navigationRevision!=nav.Revision)
      {
        navigationRevision=nav.Revision;
        if(!worker.Movement.CanContinue(nav))
        {
          if(!nav.TryFindPath(worker.Movement.Position,goal,out var path))
          {WaitingForPath=true;retry=settings.RetrySeconds;Revision++;return false;}
          worker.Movement.Follow(path);
        }
      }
      float before=remaining;double distance=worker.Movement.DistanceTravelled;
      worker.Movement.Advance(ref remaining,cart?settings.CartSpeedCellsPerSecond:worker.WalkSpeed);
      PhaseTime+=before-remaining;
      if(cart)
      {
        CartAnimationTime+=(float)(worker.Movement.DistanceTravelled-distance)/1.5f;
        CartPosition=worker.Movement.Position;CartHeading=worker.Movement.Heading;
        WheelDegrees=(float)((WheelDegrees+(worker.Movement.DistanceTravelled-distance)/(settings.WheelRadius*settings.UnitScale)*180/Math.PI)%360);
      }
      return !worker.Movement.IsFollowingPath;
    }
    private bool Wait(float duration,ref float remaining)
    { float step=Math.Min(remaining,Math.Max(0,duration-PhaseTime));PhaseTime+=step;remaining-=step;return PhaseTime>=duration; }
    private void Change(WoodcuttingPhase phase) {Phase=phase;PhaseTime=0;CartAnimationTime=0;unloadProgress=0;Revision++;WaitingForPath=false;retry=0;}
    /// <summary>Explicitly abandon this harvest and return safely, preserving any remaining ground logs.</summary>
    public void CancelHarvest(WorkerComponent worker)
    {
      if(route==null || Phase==WoodcuttingPhase.Returned || Phase==WoodcuttingPhase.AtStockpile || cancelled)return;
      cancelled=true;
      resources.ReleaseWorker(worker.ActorId);
      if(WorkerOnCart || Phase==WoodcuttingPhase.Sawing)
      {worker.Movement.Place(CartPosition,CartHeading);resources.Carts.TryFindPath(CartPosition,route.Home,out var path);Begin(worker,WoodcuttingPhase.Returning,resources.Carts,path,route.Home);}
      else BeginReturnWalk(worker);
    }
    /// <summary>Legacy integration hook; storage-backed jobs restart automatically after timed unloading.</summary>
    public bool ResumeAfterUnloading(WorkerComponent worker)
    {
      if(inventory!=null && RemainingPlanks>0)return false;
      if(worker.Job!=this || (Phase!=WoodcuttingPhase.Returned && Phase!=WoodcuttingPhase.AtStockpile && Phase!=WoodcuttingPhase.WaitingForStockpile) || cancelled)return false;
      Loaded=false;RemainingPlanks=0;CarryingLog=false;Change(WoodcuttingPhase.WaitingForResource);return true;
    }
    protected override void OnWorkerRemoved(WorkerComponent worker)
    {
      resources?.ReleaseWorker(worker.ActorId);
      route=null;Loaded=false;RemainingPlanks=0;StockpileId=null;CarryingLog=false;cancelled=false;CartPosition=WorkPosition;CartHeading=WorkHeading;
      Change(WoodcuttingPhase.WaitingForResource);
    }
    private static float Heading(MapPoint from,MapPoint to)=>(float)(Math.Atan2(to.X-from.X,to.Z-from.Z)*180/Math.PI);
  }
}
