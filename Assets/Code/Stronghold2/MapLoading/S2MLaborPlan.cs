using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.MapLoading.Labor;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>New-map labor starts vacant. Matching editor cart placeholders are owned by simulation.</summary>
  public sealed class S2MLaborPlan
  {
    public IReadOnlyList<LaborJob> Jobs { get; private set; }
    public IReadOnlyList<ActorPlacement> StartupActors { get; private set; }
    public static S2MLaborPlan Create(S2MFile map,NavigationGrid navigation)
    {
      var jobs=new List<LaborJob>();var definitions=new List<LaborBuildingDefinition>();var claimed=new HashSet<int>();
      foreach(var building in map.BuildingPlacements)
      {
        if(!LaborBuildingCatalog.TryGet(building.Type,out var definition))continue;
        jobs.Add(definition.Create(map,building,navigation,int.MinValue+jobs.Count,claimed));definitions.Add(definition);
      }
      var startup=new List<ActorPlacement>();
      foreach(var actor in map.Actors)
      {
        if(claimed.Contains(actor.Id))continue;
        // Map-start worker placeholders do not satisfy a simulation vacancy.
        bool worker=false;
        for(int i=0;i<jobs.Count;i++)if(definitions[i].IsWorkerPlaceholder(actor,jobs[i])) { worker=true;break; }
        if(!worker)startup.Add(actor);
      }
      return new S2MLaborPlan { Jobs=jobs.AsReadOnly(),StartupActors=startup.AsReadOnly() };
    }
  }
}
