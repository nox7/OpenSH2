using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading.Labor
{
  public sealed class WoodcutterBuildingDefinition : LaborBuildingDefinition
  {
    public override string BuildingType => "SawPit";
    public override LaborJob Create(S2MFile map,BuildingPlacement building,NavigationGrid navigation,int equipmentActorId,ISet<int> suppressed)
    {
      var anchor=S2MNavigationBuilder.BuildingPoint(building,-.5f,.5f);ActorPlacement legacy=null;
      foreach(var actor in map.Actors)
        if(actor.OnMap && actor.Type=="HorseCart" && !suppressed.Contains(actor.Id)
          && MapPoint.Distance(anchor,new MapPoint(actor.RawX/1024,actor.RawZ/1024))<.25f) { legacy=actor;break; }
      if(legacy!=null)suppressed.Add(legacy.Id);
      float yaw=legacy?.HeadingDegrees ?? building.RotationDegrees,radians=yaw*(float)Math.PI/180;
      var desired=new MapPoint(anchor.X+(float)Math.Cos(radians)*.75f,anchor.Z-(float)Math.Sin(radians)*.75f);
      if(!navigation.TryNearest(desired,3,out var approach))throw new InvalidOperationException($"Saw pit {building.Id} has no walkable job approach.");
      return new WoodcutterJob(building.Id,S2MCampfireLayout.EstateAtBuilding(map,building),equipmentActorId,approach,anchor,yaw,legacy?.RawY??building.RawY);
    }
    public override bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job) => actor.Type=="WoodCutter"
      && MapPoint.Distance(new MapPoint(actor.RawX/1024,actor.RawZ/1024),job.WorkPosition)<2;
  }
}
