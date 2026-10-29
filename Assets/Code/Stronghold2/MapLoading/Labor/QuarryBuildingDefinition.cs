using System;using System.Collections.Generic;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading.Labor
{
  public sealed class QuarryBuildingDefinition : LaborBuildingDefinition
  {
    private readonly QuarrySettings settings;
    public QuarryBuildingDefinition(QuarrySettings settings=null){this.settings=settings??new QuarrySettings();this.settings.Validate();}
    public override string BuildingType=>"StoneQuarry";
    public override LaborJob Create(S2MFile map,BuildingPlacement building,NavigationGrid nav,int equipment,ISet<int> suppressed)
      =>Make(map,building,nav,equipment)[0];
    public override IEnumerable<LaborJob> CreateJobs(S2MFile map,BuildingPlacement building,NavigationGrid nav,int equipment,ISet<int> suppressed)
      =>Make(map,building,nav,equipment);
    private List<LaborJob> Make(S2MFile map,BuildingPlacement building,NavigationGrid nav,int equipment)
    {
      var state=new QuarryProduction(settings);var jobs=new List<LaborJob>();int estate=S2MCampfireLayout.EstateAtBuilding(map,building);
      for(int i=0;i<QuarryProduction.RequiredWorkers;i++)
      {
        var entry=S2MNavigationBuilder.BuildingPoint(building,i,-1);
        var work=new MapPoint(building.MapX,building.MapZ);
        if(!nav.TryNearest(entry,3,out var approach))throw new InvalidOperationException($"Quarry {building.Id} has no ground approach for mason {i}.");
        jobs.Add(new QuarryJob(building.Id,estate,i,equipment,approach,work,building.RotationDegrees,building.RawY,new MapPoint(building.MapX,building.MapZ),state));
      }
      return jobs;
    }
    public override bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job)=>actor.Type=="StoneMason" &&
      MapPoint.Distance(new MapPoint(actor.RawX/1024,actor.RawZ/1024),job.WorkPosition)<3;
  }
}
