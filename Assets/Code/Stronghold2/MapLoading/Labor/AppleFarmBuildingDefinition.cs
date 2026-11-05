using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading.Labor
{
  public sealed class AppleFarmBuildingDefinition : LaborBuildingDefinition
  {
    private readonly AppleFarmSettings settings;
    public AppleFarmBuildingDefinition(AppleFarmSettings settings=null){this.settings=settings??new AppleFarmSettings();this.settings.Validate();}
    public override string BuildingType=>"AppleFarm";
    public override LaborJob Create(S2MFile map,BuildingPlacement b,NavigationGrid nav,int equipment,ISet<int> suppressed)
    {
      if(!nav.TryNearest(S2MNavigationBuilder.BuildingPoint(b,.5f,-.5f),3,out var entry))throw new InvalidOperationException($"Apple farm {b.Id} has no entrance.");
      if(!nav.TryNearest(S2MNavigationBuilder.BuildingPoint(b,.5f,.5f),3,out var center))center=entry;
      return new AppleFarmerJob(b.Id,S2MCampfireLayout.EstateAtBuilding(map,b),entry,center,b.RotationDegrees,b.RawY,nav,settings);
    }
    public override bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job)=>actor.Type=="AppleFarmer" && MapPoint.Distance(new MapPoint(actor.RawX/1024,actor.RawZ/1024),job.WorkPosition)<5;
  }
}
