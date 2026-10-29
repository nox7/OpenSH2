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
    public OxHaulingNetwork OxHauling { get; private set; }
    public IReadOnlyList<LaborBuilding> Buildings { get; private set; }
    public IReadOnlyList<LaborJob> Jobs { get; private set; }
    public IReadOnlyList<ActorPlacement> StartupActors { get; private set; }
    public static S2MLaborPlan Create(S2MFile map,NavigationGrid navigation,QuarrySettings quarrySettings=null,OxHaulingSettings oxSettings=null,IronMineSettings ironSettings=null)
    {
      var jobs=new List<LaborJob>();var definitions=new List<LaborBuildingDefinition>();var claimed=new HashSet<int>();
      var workforce=new List<LaborBuilding>();var known=new HashSet<int>();
      void Add(BuildingPlacement building)
      {
        if(!known.Add(building.Id))return;
        var slots=new List<LaborJob>();bool configured=LaborBuildingCatalog.TryGet(building.Type,out var definition,quarrySettings,oxSettings,ironSettings);
        if(configured)foreach(var job in definition.CreateJobs(map,building,navigation,int.MinValue+jobs.Count*64,claimed))
        {slots.Add(job);jobs.Add(job);definitions.Add(definition);}
        workforce.Add(new LaborBuilding(building.Id,S2MCampfireLayout.EstateAtBuilding(map,building),slots,configured || LaborBuildingCatalog.RequiresNoWorkers(building.Type)));
      }
      foreach(var building in map.BuildingPlacements)Add(building);
      foreach(var keep in map.Keeps)Add(keep);
      var startup=new List<ActorPlacement>();
      foreach(var actor in map.Actors)
      {
        if(claimed.Contains(actor.Id))continue;
        // Map-start worker placeholders do not satisfy a simulation vacancy.
        bool worker=false;
        for(int i=0;i<jobs.Count;i++)if(definitions[i].IsWorkerPlaceholder(actor,jobs[i])) { worker=true;break; }
        if(!worker)startup.Add(actor);
      }
      return new S2MLaborPlan { OxHauling=S2MOxHaulingPlan.Create(map,navigation,jobs),Buildings=workforce.AsReadOnly(),Jobs=jobs.AsReadOnly(),StartupActors=startup.AsReadOnly() };
    }
  }
}
