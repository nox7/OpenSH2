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
      var job=new WoodcutterJob(building.Id,S2MCampfireLayout.EstateAtBuilding(map,building),equipmentActorId,approach,anchor,yaw,legacy?.RawY??building.RawY);
      var destinations=new List<WoodDeliveryPoint>();
      foreach(var stockpile in map.BuildingPlacements)
      {
        if(stockpile.Type!="StockPile")continue;
        int estate=S2MCampfireLayout.EstateAtBuilding(map,stockpile);
        if(estate<0 || estate!=job.EstateId)continue;
        float width=S2MNavigationBuilder.UnrotatedWidth(stockpile);
        float depth=Math.Abs((int)Math.Round(stockpile.RotationDegrees/90))%2==0?stockpile.FootprintHeight:stockpile.FootprintWidth;
        // Logical cart positions can use the same stockpile aisles as other ground actors.
        for(float x=.5f;x<width;x+=1)for(float z=.5f;z<depth;z+=1)
        {
          var point=S2MNavigationBuilder.BuildingPoint(stockpile,x,z);
          if(navigation.IsWalkable(point))destinations.Add(new WoodDeliveryPoint(stockpile.Id,point));
        }
      }
      // Native work-log cutting end: approximately (115,182), center height 60 model units.
      // Buildings use 204.8 model units/cell and 5 raw height units/model unit.
      job.ConfigureProduction(S2MNavigationBuilder.BuildingPoint(building,.56f,.89f),building.RotationDegrees,destinations.AsReadOnly(),new MapPoint(building.MapX,building.MapZ),building.RawY+300);
      return job;
    }
    public override bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job) => actor.Type=="WoodCutter"
      && MapPoint.Distance(new MapPoint(actor.RawX/1024,actor.RawZ/1024),job.WorkPosition)<2;
  }
}
