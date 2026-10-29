using System;using System.Collections.Generic;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading
{
  public static class S2MOxHaulingPlan
  {
    public static OxHaulingNetwork Create(S2MFile map,NavigationGrid navigation,IReadOnlyList<LaborJob> jobs)
    {
      var sources=new List<OxHaulingSource>();var destinations=new List<HaulingDestination>();
      foreach(var job in jobs)
        if(job is QuarryJob q && q.SlotIndex==0)
        {
          // Collect beside the finished bank on the front edge, using ordinary ground navigation.
          var offset=Rotate(2,-1,q.BuildingHeading);var point=new MapPoint(q.BuildingPosition.X+offset.X,q.BuildingPosition.Z+offset.Z);
          if(navigation.TryNearest(point,3,out var access))sources.Add(new OxHaulingSource(q.BuildingId,q.EstateId,access,q.Production));
        }
      foreach(var job in jobs)if(job is IronMinerJob miner && miner.SlotIndex==0)
      {
        var offset=Rotate(2,-1,miner.BuildingHeading);var point=new MapPoint(miner.BuildingPosition.X+offset.X,miner.BuildingPosition.Z+offset.Z);
        if(navigation.TryNearest(point,3,out var access))sources.Add(new OxHaulingSource(miner.BuildingId,miner.EstateId,access,miner.Production));
      }
      foreach(var building in map.BuildingPlacements)
      {
        if(building.Type!="StockPile")continue;int estate=S2MCampfireLayout.EstateAtBuilding(map,building);if(estate<0)continue;
        float width=S2MNavigationBuilder.UnrotatedWidth(building);
        float depth=Math.Abs((int)Math.Round(building.RotationDegrees/90))%2==0?building.FootprintHeight:building.FootprintWidth;
        for(float x=.5f;x<width;x++)for(float z=.5f;z<depth;z++)
        {var point=S2MNavigationBuilder.BuildingPoint(building,x,z);if(navigation.IsWalkable(point))destinations.Add(new HaulingDestination(building.Id,estate,point));}
      }
      var network=new OxHaulingNetwork(navigation,sources.AsReadOnly(),destinations.AsReadOnly());
      foreach(var job in jobs)if(job is OxHandlerJob ox)ox.Configure(network);return network;
    }
    private static MapPoint Rotate(float x,float z,float heading)
    {double angle=heading*Math.PI/180;return new MapPoint((float)(x*Math.Cos(angle)+z*Math.Sin(angle)),(float)(z*Math.Cos(angle)-x*Math.Sin(angle)));}
  }
}
