using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading
{
  public static class S2MRecruitmentPlan
  {
    public static IReadOnlyList<IReadOnlyList<Peasant>> Create(S2MFile map, NavigationGrid navigation, PopulationSettings settings)
    {
      var result = new List<IReadOnlyList<Peasant>>(); int id=-1;
      foreach(var camp in S2MCampfireLayout.Create(map,settings))
      {
        var hovels=new List<BuildingPlacement>();
        foreach(var b in map.BuildingPlacements)
          if(b.Type=="Hovel" && camp.Population.EstateId>=0 && S2MCampfireLayout.EstateAtBuilding(map,b)==camp.Population.EstateId)hovels.Add(b);
        var recruits=new List<Peasant>();
        var stair=S2MNavigationBuilder.BuildingPoint(camp.Keep,S2MNavigationBuilder.UnrotatedWidth(camp.Keep)*.5f,-settings.KeepStairBaseOffset);
        if(!navigation.TryNearest(stair,2,out var keepExit))throw new InvalidOperationException($"Keep {camp.Keep.Id} has no walkable stair base.");
        // Buildings may obstruct part of a ring. Continue around/onto the next ring rather than seating inside them.
        var candidates=new CampfirePopulation(camp.Keep.Id,camp.Population.EstateId,(camp.Population.Capacity/8)+32,settings);
        foreach(var candidate in candidates.Seats)
        {
          if(recruits.Count==camp.Population.Capacity)break;
          var seat=new MapPoint(camp.Center.x+candidate.X,camp.Center.y+candidate.Z);
          if(!navigation.IsWalkable(seat) || !navigation.CanReach(keepExit,seat))continue;
          bool fromHovel=recruits.Count>=8;
          var source=fromHovel ? hovels[(recruits.Count-8)%hovels.Count] : camp.Keep;
          float width=S2MNavigationBuilder.UnrotatedWidth(source);
          var inside=fromHovel ? S2MNavigationBuilder.BuildingPoint(source,width*.5f,.6f) : keepExit;
          var door=fromHovel ? S2MNavigationBuilder.BuildingPoint(source,width*.5f,-settings.HovelDoorOffset) : keepExit;
          if(!navigation.TryNearest(door,2,out var exit))throw new InvalidOperationException($"Building {source.Id} has no walkable exit.");
          if(fromHovel && !navigation.CanReach(exit,seat))
            ResolveHovelExit(source,navigation,settings,seat,ref inside,ref exit);
          float heading=fromHovel ? (float)(Math.Atan2(exit.X-inside.X,exit.Z-inside.Z)*180/Math.PI) : source.RotationDegrees+180;
          // Seat reachability is resolved by the movement system. A blocked route waits and retries, never teleports.
          recruits.Add(new Peasant(id--,source.Id,fromHovel,inside,exit,seat,candidate.HeadingDegrees,heading,camp.Population.EstateId));
        }
        if(recruits.Count!=camp.Population.Capacity)throw new InvalidOperationException($"Keep {camp.Keep.Id} has insufficient walkable campfire seating.");
        result.Add(recruits.AsReadOnly());
      }
      return result;
    }

    private static void ResolveHovelExit(BuildingPlacement source,NavigationGrid navigation,PopulationSettings settings,
      MapPoint seat,ref MapPoint inside,ref MapPoint exit)
    {
      // No decoded door helpers exist for the six hovel appearances. Prefer the front;
      // if that opens into an enclosed pocket, use the nearest accessible side of this hovel.
      float width=S2MNavigationBuilder.UnrotatedWidth(source);
      float depth=Math.Abs((int)Math.Round(source.RotationDegrees/90))%2==0 ? source.FootprintHeight : source.FootprintWidth;
      MapPoint center=S2MNavigationBuilder.BuildingPoint(source,width*.5f,depth*.5f), preferred=exit;
      float best=float.MaxValue; MapPoint selected=exit; bool found=false;
      for(float x=.25f;x<width;x+=.25f) { Consider(x,-settings.HovelDoorOffset); Consider(x,depth+settings.HovelDoorOffset); }
      for(float z=.25f;z<depth;z+=.25f) { Consider(-settings.HovelDoorOffset,z); Consider(width+settings.HovelDoorOffset,z); }
      if(!found)return;
      exit=selected;float d=MapPoint.Distance(center,exit);
      inside=new MapPoint(exit.X+(center.X-exit.X)*Math.Min(1,.65f/d),exit.Z+(center.Z-exit.Z)*Math.Min(1,.65f/d));
      void Consider(float x,float z)
      {
        MapPoint point=S2MNavigationBuilder.BuildingPoint(source,x,z);
        if(!navigation.IsWalkable(point) || !navigation.CanReach(point,seat))return;
        float score=MapPoint.Distance(point,preferred);
        if(score<best) { best=score; selected=point; found=true; }
      }
    }
  }
}
