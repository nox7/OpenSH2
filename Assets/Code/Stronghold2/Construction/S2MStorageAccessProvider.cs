using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.MapLoading;
namespace Assets.Code.Stronghold2.Construction
{
    /// <summary>Geometry index over registered map buildings; access is resolved lazily and checked against current navigation by the shared locator.</summary>
    public sealed class S2MStorageAccessProvider : IStorageAccessProvider
    {
        private readonly S2MFile map;private readonly NavigationGrid navigation;
        private IReadOnlyList<BuildingPlacement> indexed;
        private readonly Dictionary<int,IReadOnlyList<MapPoint>> points=new();
        public S2MStorageAccessProvider(S2MFile map,NavigationGrid navigation){this.map=map;this.navigation=navigation;}
        public IReadOnlyList<MapPoint> AccessPoints(int id)
        {
            if(!ReferenceEquals(indexed,map.BuildingPlacements))
            {
                points.Clear();foreach(var p in map.BuildingPlacements)
                {
                    if(p.Type=="StockPile")points[p.Id]=S2MBuildingNavigation.StockpileAccess(p,navigation,walkableOnly:false);
                    else if(p.Type=="Granary")
                    {
                        var entrance=S2MBuildingNavigation.Point(p,(float)Math.Floor(S2MNavigationBuilder.UnrotatedWidth(p)/2)+.5f,-.5f);
                        points[p.Id]=navigation.TryNearest(entrance,2,out var access)?new[]{access}:Array.Empty<MapPoint>();
                    }
                }
                indexed=map.BuildingPlacements;
            }
            return points.TryGetValue(id,out var result)?result:Array.Empty<MapPoint>();
        }
    }
}
