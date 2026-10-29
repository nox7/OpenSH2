using System;using System.Collections.Generic;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading.Labor
{
  public sealed class IronMineBuildingDefinition : LaborBuildingDefinition
  {
    private readonly IronMineSettings settings;
    public IronMineBuildingDefinition(IronMineSettings settings=null){this.settings=settings??new IronMineSettings();this.settings.Validate();}
    public override string BuildingType=>"IronMine";
    public override LaborJob Create(S2MFile map,BuildingPlacement building,NavigationGrid nav,int equipment,ISet<int> suppressed)=>Make(map,building,nav,equipment)[0];
    public override IEnumerable<LaborJob> CreateJobs(S2MFile map,BuildingPlacement building,NavigationGrid nav,int equipment,ISet<int> suppressed)=>Make(map,building,nav,equipment);
    private List<LaborJob> Make(S2MFile map,BuildingPlacement building,NavigationGrid nav,int equipment)
    {
      var production=new IronMineProduction(settings);var jobs=new List<LaborJob>();int estate=S2MCampfireLayout.EstateAtBuilding(map,building);
      for(int slot=0;slot<2;slot++)
      {var entry=S2MNavigationBuilder.BuildingPoint(building,slot,-1);if(!nav.TryNearest(entry,3,out var approach))throw new InvalidOperationException($"Iron mine {building.Id} has no approach for worker {slot}.");
        jobs.Add(new IronMinerJob(building.Id,estate,slot,equipment,approach,new MapPoint(building.MapX,building.MapZ),building.RotationDegrees,building.RawY,production));}
      return jobs;
    }
    public override bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job)=>actor.Type=="IronMiner" && MapPoint.Distance(new MapPoint(actor.RawX/1024,actor.RawZ/1024),job.WorkPosition)<3;
  }
}
