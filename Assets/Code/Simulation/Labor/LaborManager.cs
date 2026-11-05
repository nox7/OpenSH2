using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;

namespace Assets.Code.Simulation.Labor
{
  /// <summary>Estate-scoped vacancy polling. Assignment reserves one worker slot atomically.</summary>
  public sealed class LaborManager : IDisposable
  {
    public IReadOnlyList<LaborJob> Jobs { get; }
    public IReadOnlyList<LaborBuilding> Buildings { get; }
    private readonly Dictionary<int,LaborBuilding> buildingsById=new();
    private readonly List<LaborBuilding> registeredBuildings=new();
    private readonly List<LaborJob> registeredJobs=new();
    private readonly HashSet<ILaborEnvironment> environments=new();
    private readonly Dictionary<int,List<LaborBuilding>> byEstate=new();
    private readonly Dictionary<int,LaborDemand> demand=new();
    private readonly Dictionary<int,List<PeasantRecruitment>> populationByEstate=new();
    public event Action<int,LaborDemand> StaffingChanged;
    public LaborDemand GetLaborDemand(int estate)=>demand.TryGetValue(estate,out var value)?value:default;
    public LaborSupply GetLaborSupply(int estate)
    {
      int alive=0,available=0,pending=0,removed=0;bool has=false;
      if(!populationByEstate.TryGetValue(estate,out var groups))return default;
      foreach(var group in groups)
      {
        has=true;
        pending+=System.Math.Max(0,group.SpawnLimit-group.SpawnedCount);
        for(int i=0;i<group.SpawnedCount;i++)
        {
          var p=group.Peasants[i];if(p.State==PeasantState.Removed){removed++;continue;}
          alive++;if(p.Job==null)available++;
        }
      }
      return new LaborSupply(alive,available,pending,removed,has);
    }
    private void RefreshDemand(int estate)
    {
      int missingBuildings=0,required=0,assigned=0,arrived=0,unknown=0;
      if(byEstate.TryGetValue(estate,out var list))foreach(var b in list)
      {if(!b.StaffingConfigured){unknown++;continue;}required+=b.MaximumWorkers;assigned+=b.TotalWorkers;arrived+=b.ArrivedWorkers;if(!b.IsFullyStaffed)missingBuildings++;}
      var summary=new LaborDemand(missingBuildings,required,assigned,arrived,unknown);
      demand[estate]=summary;StaffingChanged?.Invoke(estate,summary);
    }
    private void OnStaffingChanged(LaborJob job)=>RefreshDemand(job.EstateId);
    private void Index(LaborBuilding b)
    {
      if(!byEstate.TryGetValue(b.EstateId,out var list))byEstate.Add(b.EstateId,list=new());list.Add(b);
      foreach(var job in b.Jobs){job.StaffingChanged+=OnStaffingChanged;if(job.Environment!=null)environments.Add(job.Environment);}
    }
    public bool TryGetBuilding(int id,out LaborBuilding building)=>buildingsById.TryGetValue(id,out building);
    private readonly IReadOnlyList<PeasantRecruitment> kingdoms;
    private readonly NavigationGrid navigation;
    private readonly PopulationSettings settings;
    private double untilPoll;
    private readonly Dictionary<LaborJob,(Peasant Peasant,NavigationGrid.PathRequest Request)> staffingPaths=new();
    private readonly HashSet<int> staffingClaims=new();private readonly List<LaborJob> staffingFinished=new();
    private void CancelStaffing(LaborJob job)
    {if(staffingPaths.TryGetValue(job,out var pending)){pending.Request.Dispose();staffingClaims.Remove(pending.Peasant.Id);staffingPaths.Remove(job);}}
    private void FinishStaffing()
    {
      staffingFinished.Clear();
      foreach(var pair in staffingPaths)
      {
        var job=pair.Key;var p=pair.Value.Peasant;var request=pair.Value.Request;
        bool valid=!job.WorkerId.HasValue && p.State==PeasantState.Seated && p.Job==null;
        if(valid && request.Status==PathRequestStatus.Pending)continue;
        if(valid && request.Status==PathRequestStatus.Succeeded && request.Revision==navigation.Revision)p.Worker.TryAssign(job,request.Path,navigation.Revision);
        staffingFinished.Add(job);
      }
      foreach(var job in staffingFinished)CancelStaffing(job);
    }
    public LaborManager(IReadOnlyList<LaborJob> jobs,IReadOnlyList<PeasantRecruitment> kingdoms,NavigationGrid navigation,PopulationSettings settings,IReadOnlyList<LaborBuilding> buildings=null)
    {
      settings.Validate();registeredJobs.AddRange(jobs);Jobs=registeredJobs.AsReadOnly();this.kingdoms=kingdoms;this.navigation=navigation;this.settings=settings;untilPoll=settings.JobAssignmentIntervalSeconds;
      foreach(var group in kingdoms)
      {
        if(group.Peasants.Count==0)continue;int estate=group.Peasants[0].EstateId;
        if(!populationByEstate.TryGetValue(estate,out var groups))populationByEstate.Add(estate,groups=new());groups.Add(group);
      }
      if(buildings==null)
      {
        var groups=new Dictionary<int,List<LaborJob>>();foreach(var job in jobs){if(!groups.TryGetValue(job.BuildingId,out var slots))groups.Add(job.BuildingId,slots=new List<LaborJob>());slots.Add(job);}
        var generated=new List<LaborBuilding>();foreach(var pair in groups)generated.Add(pair.Value[0].Building??new LaborBuilding(pair.Key,pair.Value[0].EstateId,pair.Value));buildings=generated.AsReadOnly();
      }
      registeredBuildings.AddRange(buildings);Buildings=registeredBuildings.AsReadOnly();foreach(var building in buildings)buildingsById.Add(building.BuildingId,building);
      foreach(var building in buildings)Index(building);foreach(int estate in byEstate.Keys)RefreshDemand(estate);
    }
    public void RegisterPassiveBuilding(int id,int estate)=>RegisterBuilding(new LaborBuilding(id,estate,Array.Empty<LaborJob>()));
    public void RegisterBuilding(LaborBuilding building)
    {
      if(building==null)throw new ArgumentNullException(nameof(building));
      if(buildingsById.ContainsKey(building.BuildingId))throw new ArgumentException("Building already registered.");
      buildingsById.Add(building.BuildingId,building);registeredBuildings.Add(building);registeredJobs.AddRange(building.Jobs);
      Index(building);RefreshDemand(building.EstateId);
    }
    /// <summary>Remove an unstaffed runtime registration, including its job slots, during construction rollback.</summary>
    public bool RemoveUnstaffedBuilding(int id)
    {
      if(!buildingsById.TryGetValue(id,out var b)||b.TotalWorkers!=0)return false;
      foreach(var job in b.Jobs){CancelStaffing(job);job.StaffingChanged-=OnStaffingChanged;registeredJobs.Remove(job);job.OnBuildingRemoved();}
      buildingsById.Remove(id);registeredBuildings.Remove(b);byEstate[b.EstateId].Remove(b);RefreshDemand(b.EstateId);return true;
    }
    public bool RemovePassiveBuilding(int id)
    {
      if(!buildingsById.TryGetValue(id,out var b)||b.MaximumWorkers!=0)return false;
      return RemoveUnstaffedBuilding(id);
    }
    public void Advance(float seconds)
    {
      if(!(seconds>=0) || float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
      if(seconds>0)navigation.PathRequests?.AdvanceFrame();
      if(seconds>0)FinishStaffing();
      float remaining=seconds;
      while(remaining>0)
      {
        float step=Math.Min(remaining,(float)untilPoll);
        foreach(var environment in environments)environment.Advance(step);
        for(int k=0;k<kingdoms.Count;k++)kingdoms[k].Advance(step);
        for(int j=0;j<Jobs.Count;j++)Jobs[j].CompleteWorkStep();
        remaining-=step;untilPoll-=step;
        if(untilPoll<=.000001) { Assign();untilPoll=settings.JobAssignmentIntervalSeconds; }
      }
    }
    public void Dispose()
    {
      foreach(var pending in staffingPaths.Values)pending.Request.Dispose();staffingPaths.Clear();staffingClaims.Clear();
      foreach(var job in Jobs)job.StaffingChanged-=OnStaffingChanged;
      StaffingChanged=null;
    }
    private void Assign()
    {
      foreach(var job in Jobs)
      {
        if(job.WorkerId.HasValue || job.EstateId<0)continue;
        if(navigation.PathRequests!=null)
        {
          if(staffingPaths.ContainsKey(job))continue;
          Peasant nearest=null;float nearestDistance=float.PositiveInfinity;
          if(populationByEstate.TryGetValue(job.EstateId,out var population))foreach(var group in population)foreach(var p in group.Peasants)
          {
            if(p.State!=PeasantState.Seated || p.Job!=null || staffingClaims.Contains(p.Id))continue;
            float d=MapPoint.Distance(p.Position,job.Approach);
            if(d<nearestDistance && navigation.CanReach(p.Position,job.Approach)){nearest=p;nearestDistance=d;}
          }
          if(nearest!=null)
          {staffingClaims.Add(nearest.Id);staffingPaths[job]=(nearest,navigation.PathRequests.Request(nearest.Position,new[]{new PathTarget(job.BuildingId,job.Approach)}));}
          continue;
        }
        Peasant selected=null;IReadOnlyList<MapPoint> selectedPath=null;float best=float.MaxValue;
        foreach(var kingdom in kingdoms)foreach(var p in kingdom.Peasants)
        {
          if(p.EstateId!=job.EstateId || p.State!=PeasantState.Seated || p.Job!=null)continue;
          float distance=MapPoint.Distance(p.Position,job.Approach);
          if(distance>=best || !navigation.TryFindPath(p.Position,job.Approach,out var path))continue;
          selected=p;selectedPath=path;best=distance;
        }
        if(selected==null)continue;
        selected.Worker.TryAssign(job,selectedPath);
      }
    }
  }
}
