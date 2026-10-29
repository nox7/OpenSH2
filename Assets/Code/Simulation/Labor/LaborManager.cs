using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;

namespace Assets.Code.Simulation.Labor
{
  /// <summary>Estate-scoped vacancy polling. Assignment reserves one worker slot atomically.</summary>
  public sealed class LaborManager
  {
    public IReadOnlyList<LaborJob> Jobs { get; }
    public IReadOnlyList<LaborBuilding> Buildings { get; }
    private readonly Dictionary<int,LaborBuilding> buildingsById=new();
    public bool TryGetBuilding(int id,out LaborBuilding building)=>buildingsById.TryGetValue(id,out building);
    private readonly IReadOnlyList<PeasantRecruitment> kingdoms;
    private readonly NavigationGrid navigation;
    private readonly PopulationSettings settings;
    private double untilPoll;
    public LaborManager(IReadOnlyList<LaborJob> jobs,IReadOnlyList<PeasantRecruitment> kingdoms,NavigationGrid navigation,PopulationSettings settings,IReadOnlyList<LaborBuilding> buildings=null)
    {
      settings.Validate();Jobs=jobs;this.kingdoms=kingdoms;this.navigation=navigation;this.settings=settings;untilPoll=settings.JobAssignmentIntervalSeconds;
      if(buildings==null)
      {
        var groups=new Dictionary<int,List<LaborJob>>();foreach(var job in jobs){if(!groups.TryGetValue(job.BuildingId,out var slots))groups.Add(job.BuildingId,slots=new List<LaborJob>());slots.Add(job);}
        var generated=new List<LaborBuilding>();foreach(var pair in groups)generated.Add(pair.Value[0].Building??new LaborBuilding(pair.Key,pair.Value[0].EstateId,pair.Value));buildings=generated.AsReadOnly();
      }
      Buildings=buildings;foreach(var building in buildings)buildingsById.Add(building.BuildingId,building);
    }
    public void Advance(float seconds)
    {
      if(!(seconds>=0) || float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
      float remaining=seconds;
      while(remaining>0)
      {
        float step=Math.Min(remaining,(float)untilPoll);
        for(int k=0;k<kingdoms.Count;k++)kingdoms[k].Advance(step);
        for(int j=0;j<Jobs.Count;j++)Jobs[j].CompleteWorkStep();
        remaining-=step;untilPoll-=step;
        if(untilPoll<=.000001) { Assign();untilPoll=settings.JobAssignmentIntervalSeconds; }
      }
    }
    private void Assign()
    {
      foreach(var job in Jobs)
      {
        if(job.WorkerId.HasValue || job.EstateId<0)continue;
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
