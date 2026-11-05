using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Navigation;

namespace Assets.Code.Simulation.Labor
{
  public enum AppleFarmerPhase { Searching,ToTree,Planting,Picking,WaitingForGranary,Delivering,Unloading,ReturningHome,Idle }
  public sealed class AppleFarmerJob : LaborJob
  {
    public const int RequiredWorkers=1,TreesPerLoad=2,ApplesPerLoad=5;
    public override LaborRole Role=>LaborRole.AppleFarmer;
    public AppleOrchards Orchards { get; private set; }
    public override ILaborEnvironment Environment=>Orchards;
    public AppleFarmSettings Settings { get; }
    public AppleFarmerPhase Phase { get; private set; }
    public OrchardTree Target { get; private set; }
    public int PickedTrees { get; private set; }public int Cargo { get; private set; }
    public long DeliveredApples { get; private set; }public int? GranaryId { get; private set; }
    public bool Moving { get; private set; }public float PhaseTime { get; private set; }public int Revision { get; private set; }
    public float PickingSeconds { get; private set; }=4;
    public float PlantingSeconds { get; private set; }=4;
    private readonly NavigationGrid navigation;
    private EstateInventory inventory;private StorageLocator granaries;
    private NavigationGrid.PathRequest request;private AppleOrchards.NearestTreeSearch search;
    private bool routeReady,plantingTarget;private int routeRevision;private float retry,idleSeconds,depositTime,homeSearchDelay;
    public AppleFarmerJob(int building,int estate,MapPoint approach,MapPoint center,float heading,float height,NavigationGrid navigation,AppleFarmSettings settings=null)
      :base(building,estate,approach,center,heading,height)
    {settings??=new AppleFarmSettings();settings.Validate();Settings=settings.Copy();this.navigation=navigation;}
    public void ConfigureOrchards(AppleOrchards orchards)=>Orchards=orchards??throw new ArgumentNullException(nameof(orchards));
    internal override void OnBuildingRemoved()=>Orchards?.RemoveFarm(BuildingId);
    public void ConfigureStorage(EstateInventory inventory,StorageLocator granaries){this.inventory=inventory;this.granaries=granaries;}
    public void SetDurations(float picking,float planting)
    {foreach(float value in new[]{picking,planting})if(!(value>0) || float.IsInfinity(value))throw new ArgumentOutOfRangeException();PickingSeconds=picking;PlantingSeconds=planting;}
    protected override void OnWorkerArrived(WorkerComponent worker)
    {worker.Movement.Place(Approach,WorkHeading);Change(AppleFarmerPhase.Searching);}
    private void Release(int worker){Orchards?.Release(Target,worker);Target=null;}
    private void Change(AppleFarmerPhase phase)
    {request?.Dispose();request=null;search=null;Phase=phase;PhaseTime=0;depositTime=0;routeReady=false;retry=0;homeSearchDelay=0;Revision++;}
    protected override void OnWorkerRemoved(WorkerComponent worker)
    {Release(worker.ActorId);PickedTrees=0;Cargo=0;GranaryId=null;Moving=false;idleSeconds=0;Change(AppleFarmerPhase.Searching);}
    private bool TargetValid(WorkerComponent worker)=>Target!=null && Target.Active && Target.PickerId==worker.ActorId && Target.Stage==(plantingTarget?-1:4);
    public override void AdvanceWork(WorkerComponent worker,float seconds)
    {
      Moving=false;if(Orchards==null)return;float remaining=seconds;int searchBudget=Settings.TreeSearchWorkUnitsPerFrame;
      while(remaining>0)
      {
        if(retry>0){float wait=Math.Min(retry,remaining);retry-=wait;remaining-=wait;PhaseTime+=wait;if(Phase==AppleFarmerPhase.Idle)idleSeconds+=wait;if(remaining<=0)return;}
        switch(Phase)
        {
          case AppleFarmerPhase.Searching:
            // Plant this worker's own farm before joining the estate-wide harvest pool.
            foreach(var tree in Orchards.FarmTrees(BuildingId))
              if(tree.Active && tree.Stage== -1 && !tree.PickerId.HasValue && navigation.CanReach(worker.Movement.Position,tree.Approach))
              {Target=tree;plantingTarget=true;break;}
            if(Target==null)
            {
              plantingTarget=false;search??=Orchards.FindNearest(EstateId,worker.Movement.Position);
              while(!search.Complete && searchBudget-->0)search.Step();if(!search.Complete)return;
              Target=search.Result;search=null;
            }
            if(Target!=null)
            {
              if(Orchards.TryReserve(Target,worker.ActorId,EstateId,plantingTarget))
              {idleSeconds=0;Change(AppleFarmerPhase.ToTree);break;}
              // Another farmer may win a claim while this incremental search is pending.
              // Re-query the remaining trees without treating a reservation race as an empty orchard.
              Target=null;if(searchBudget>0)continue;return;
            }
            Target=null;idleSeconds+=remaining;
            if(idleSeconds>=Settings.IdleReturnDelaySeconds && MapPoint.Distance(worker.Movement.Position,WorkPosition)>.2f)
            {Change(AppleFarmerPhase.ReturningHome);break;}
            Change(AppleFarmerPhase.Idle);retry=Settings.RetrySeconds;return;
          case AppleFarmerPhase.Idle:
            Change(AppleFarmerPhase.Searching);break;
          case AppleFarmerPhase.ToTree:
            if(!TargetValid(worker)){Release(worker.ActorId);Change(AppleFarmerPhase.Searching);break;}
            if(!Travel(worker,Target.Approach,ref remaining))return;
            worker.Movement.Place(worker.Movement.Position,135);Change(plantingTarget?AppleFarmerPhase.Planting:AppleFarmerPhase.Picking);break;
          case AppleFarmerPhase.Planting:
          case AppleFarmerPhase.Picking:
            if(!TargetValid(worker)){Release(worker.ActorId);Change(AppleFarmerPhase.Searching);break;}
            float duration=(Phase==AppleFarmerPhase.Planting?PlantingSeconds:PickingSeconds)/Settings.WorkAnimationSpeed;
            float step=Math.Min(remaining,Math.Max(0,duration-PhaseTime));PhaseTime+=step;remaining-=step;
            if(PhaseTime+1e-6f<duration)return;
            if(Phase==AppleFarmerPhase.Planting)Orchards.Plant(Target,worker.ActorId);
            else if(Orchards.Harvest(Target,worker.ActorId)){PickedTrees++;if(PickedTrees==TreesPerLoad)Cargo=ApplesPerLoad;}
            Release(worker.ActorId);Change(Cargo>0?AppleFarmerPhase.WaitingForGranary:AppleFarmerPhase.Searching);break;
          case AppleFarmerPhase.WaitingForGranary:
            if(inventory==null || granaries==null){retry=Settings.RetrySeconds;return;}
            IReadOnlyList<MapPoint> route;int store;
            if(navigation.PathRequests!=null)
            {
              request??=granaries.RequestNearest(EstateId,Goods.Apples,worker.Movement.Position);
              if(request.Status==PathRequestStatus.Pending)return;
              var result=request;request=null;
              if(result.Status!=PathRequestStatus.Succeeded || result.Revision!=navigation.Revision || !inventory.CanDeposit(result.TargetId,EstateId,Goods.Apples,1)){retry=Settings.RetrySeconds;return;}
              route=result.Path;store=result.TargetId;
            }
            else if(!granaries.TryNearest(EstateId,Goods.Apples,worker.Movement.Position,out store,out route)){retry=Settings.RetrySeconds;return;}
            GranaryId=store;Change(AppleFarmerPhase.Delivering);Follow(worker,route);break;
          case AppleFarmerPhase.Delivering:
            if(!Travel(worker,default,ref remaining))return;
            Change(AppleFarmerPhase.Unloading);break;
          case AppleFarmerPhase.Unloading:
            if(Cargo==0){PickedTrees=0;GranaryId=null;idleSeconds=0;Change(AppleFarmerPhase.Searching);break;}
            if(!GranaryId.HasValue || !inventory.CanDeposit(GranaryId.Value,EstateId,Goods.Apples,1))
            {GranaryId=null;Change(AppleFarmerPhase.WaitingForGranary);break;}
            float deposit=Math.Min(remaining,Math.Max(0,Settings.GranaryDepositSecondsPerApple-depositTime));depositTime+=deposit;PhaseTime+=deposit;remaining-=deposit;
            if(depositTime+1e-6f<Settings.GranaryDepositSecondsPerApple)return;
            if(inventory.TryDeposit(GranaryId.Value,EstateId,Goods.Apples,1)){Cargo--;DeliveredApples++;Revision++;}depositTime=0;break;
          case AppleFarmerPhase.ReturningHome:
            homeSearchDelay-=remaining;
            if(homeSearchDelay<=0)
            {
              search??=Orchards.FindNearest(EstateId,worker.Movement.Position);
              while(!search.Complete && searchBudget-->0)search.Step();if(!search.Complete)return;
              var ripe=search.Result;search=null;homeSearchDelay=Settings.RetrySeconds;
              if(ripe!=null && Orchards.TryReserve(ripe,worker.ActorId,EstateId))
              {Target=ripe;plantingTarget=false;worker.Movement.Place(worker.Movement.Position,worker.Movement.Heading);Change(AppleFarmerPhase.ToTree);break;}
            }
            if(!Travel(worker,WorkPosition,ref remaining))return;
            worker.Movement.Place(worker.Movement.Position,WorkHeading);Change(AppleFarmerPhase.Searching);break;
        }
      }
    }
    private void Follow(WorkerComponent worker,IReadOnlyList<MapPoint> path)
    {worker.Movement.Follow(path);routeReady=true;routeRevision=navigation.Revision;}
    private bool Travel(WorkerComponent worker,MapPoint goal,ref float remaining)
    {
      if(routeReady && routeRevision!=navigation.Revision && worker.Movement.RouteMayHaveChanged(navigation,routeRevision))
      {worker.Movement.Place(worker.Movement.Position,worker.Movement.Heading);routeReady=false;}
      if(!routeReady)
      {
        if(Phase==AppleFarmerPhase.Delivering){GranaryId=null;Change(AppleFarmerPhase.WaitingForGranary);return false;}
        IReadOnlyList<MapPoint> path;
        if(navigation.PathRequests!=null)
        {
          request??=navigation.PathRequests.Request(worker.Movement.Position,new[]{new PathTarget(BuildingId,goal)});
          if(request.Status==PathRequestStatus.Pending)return false;
          var result=request;request=null;
          if(result.Status!=PathRequestStatus.Succeeded || result.Revision!=navigation.Revision){FailedRoute(worker);return false;}path=result.Path;
        }
        else if(!navigation.TryFindPath(worker.Movement.Position,goal,out path)){FailedRoute(worker);return false;}
        Follow(worker,path);
      }
      routeRevision=navigation.Revision;float before=remaining;worker.Movement.Advance(ref remaining,Settings.WalkSpeedCellsPerSecond);PhaseTime+=before-remaining;Moving|=before>remaining;
      return !worker.Movement.IsFollowingPath;
    }
    private void FailedRoute(WorkerComponent worker)
    {Release(worker.ActorId);worker.Movement.Place(worker.Movement.Position,worker.Movement.Heading);Change(AppleFarmerPhase.Searching);retry=Settings.RetrySeconds;}
  }
}
