using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;

namespace Assets.Code.Simulation.Labor
{
  /// <summary>Estate-scoped vacancy polling. Assignment reserves one worker and one building atomically.</summary>
  public sealed class LaborManager
  {
    public IReadOnlyList<LaborJob> Jobs { get; }
    private readonly IReadOnlyList<PeasantRecruitment> kingdoms;
    private readonly NavigationGrid navigation;
    private readonly PopulationSettings settings;
    private double untilPoll;
    public LaborManager(IReadOnlyList<LaborJob> jobs,IReadOnlyList<PeasantRecruitment> kingdoms,NavigationGrid navigation,PopulationSettings settings)
    { settings.Validate();Jobs=jobs;this.kingdoms=kingdoms;this.navigation=navigation;this.settings=settings;untilPoll=settings.JobAssignmentIntervalSeconds; }
    public void Advance(float seconds)
    {
      if(!(seconds>=0) || float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
      float remaining=seconds;
      while(remaining>0)
      {
        float step=Math.Min(remaining,(float)untilPoll);
        foreach(var kingdom in kingdoms)kingdom.Advance(step);
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
