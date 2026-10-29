using System;using System.Collections.Generic;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading.Labor
{
  public sealed class OxTetherBuildingDefinition : LaborBuildingDefinition
  {
    private readonly OxHaulingSettings settings;
    public OxTetherBuildingDefinition(OxHaulingSettings settings=null){this.settings=settings??new OxHaulingSettings();this.settings.Validate();}
    public override string BuildingType=>"OxTether";
    public override LaborJob Create(S2MFile map,BuildingPlacement building,NavigationGrid navigation,int equipment,ISet<int> suppressed)
    {
      var tether=S2MNavigationBuilder.BuildingPoint(building,0,-1);
      // Preserve the authored position and heading; the ox asset supplies the 180-degree forward correction.
      ActorPlacement placed=null;float best=2;
      foreach(var actor in map.Actors)
      {
        if(!actor.OnMap || actor.Type!="Oxen" || suppressed.Contains(actor.Id))continue;
        float distance=MapPoint.Distance(tether,new MapPoint(actor.RawX/1024,actor.RawZ/1024));
        if(distance<best){best=distance;placed=actor;}
      }
      if(placed!=null){suppressed.Add(placed.Id);tether=new MapPoint(placed.RawX/1024,placed.RawZ/1024);}
      var entry=S2MNavigationBuilder.BuildingPoint(building,0,-2);
      if(!navigation.TryNearest(entry,3,out var approach))throw new InvalidOperationException($"Ox tether {building.Id} has no handler approach.");
      float heading=placed?.HeadingDegrees??building.RotationDegrees;
      return new OxHandlerJob(building.Id,S2MCampfireLayout.EstateAtBuilding(map,building),equipment,approach,tether,heading,placed?.RawY??building.RawY,settings);
    }
    public override bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job)=>actor.Type=="OxHandler" && MapPoint.Distance(new MapPoint(actor.RawX/1024,actor.RawZ/1024),job.WorkPosition)<3;
  }
}
