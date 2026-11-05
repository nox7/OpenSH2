using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading
{
  public static class S2MAppleOrchardPlan
  {
    private static readonly (int X,int Z)[] offsets={(-2,-1),(0,-1),(2,-1),(2,1),(2,3),(0,3),(-2,3),(-2,1)};
    public static AppleOrchards Create(S2MFile map,NavigationGrid nav,IReadOnlyList<LaborJob> jobs,AppleFarmSettings settings=null)
    {
      var result=new AppleOrchards(nav,settings);var byReference=new Dictionary<uint,List<PlantActor>>();
      foreach(var plant in map.PlantActors)if(plant.Kind==SceneryKind.AppleTree)
      {if(!byReference.TryGetValue(plant.FarmReference,out var list))byReference.Add(plant.FarmReference,list=new());list.Add(plant);}
      var farmers=new Dictionary<int,AppleFarmerJob>();foreach(var job in jobs)if(job is AppleFarmerJob farmer){farmers.Add(job.BuildingId,farmer);farmer.ConfigureOrchards(result);}
      int generated=-1500000000;
      foreach(var building in map.BuildingPlacements)
      {
        if(!farmers.TryGetValue(building.Id,out var farmer))continue;
        if(building.SavedBuildingReference!=0 && byReference.TryGetValue(building.SavedBuildingReference,out var plants))
          foreach(var p in plants)Add(p.Id,p.RawX/1024,p.RawZ/1024,p.RawY,p.Stage,p.StageTimerTicks,p.RotationDegrees);
        else foreach(var offset in offsets)Add(generated++,building.MapX+offset.X,building.MapZ+offset.Z,S2MCampfireLayout.GroundHeight(map,building.MapX+offset.X,building.MapZ+offset.Z),-1,0,0);
        void Add(int id,float x,float z,float height,int stage,int timer,float heading)
        {
          var position=new MapPoint(x+.5f,z+.5f);
          // Keep blocked trees in the world, but do not invent a route through their obstacles.
          var approach=nav.TryNearest(position,2,out var access)?access:position;
          result.Add(new OrchardTree(id,building.Id,farmer.EstateId,position,new MapPoint(x+1,z),approach,height,stage,timer,heading));
        }
      }
      return result;
    }
  }
}
