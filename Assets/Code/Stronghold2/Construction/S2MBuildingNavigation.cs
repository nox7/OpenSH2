using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.Construction
{
    /// <summary>Logical ground occupancy, with a one-tile stockpile aisle; no model-size/cart clearance.</summary>
    public static class S2MBuildingNavigation
    {
        public static IDisposable Register(NavigationGrid nav,BuildingPlacement p)
        {
            var ids=new List<long>();
            try
            {
                if(p.Type=="StockPile")
                {
                    int turn=((int)Math.Round(p.RotationDegrees/90)%4+4)%4;
                    float w=turn%2==0?p.FootprintWidth:p.FootprintHeight,d=turn%2==0?p.FootprintHeight:p.FootprintWidth,aisle=(float)Math.Floor(w/2);
                    Add(0,0,aisle,d,2);Add(aisle+1,0,w,d,3);
                }
                else{var f=S2MBuildingFootprint.FromPlacement(p);long id=(2L<<32)+(uint)p.Id;nav.SetRectangularObstacle(id,f.MinX,f.MinZ,f.MaxXExclusive,f.MaxZExclusive);ids.Add(id);}
                return new Lease(nav,ids);
            }
            catch{foreach(long id in ids)nav.SetObstacle(id,default,0,false);throw;}
            void Add(float x0,float z0,float x1,float z1,long group)
            {
                if(x1<=x0 || z1<=z0)return;
                var a=Point(p,x0,z0);var b=Point(p,x1,z1);long id=(group<<32)+(uint)p.Id;
                nav.SetRectangularObstacle(id,Math.Min(a.X,b.X),Math.Min(a.Z,b.Z),Math.Max(a.X,b.X),Math.Max(a.Z,b.Z));ids.Add(id);
            }
        }
        public static MapPoint Point(BuildingPlacement p,float x,float z)
        {double a=p.RotationDegrees*Math.PI/180;return new MapPoint(p.MapX+(float)(x*Math.Cos(a)+z*Math.Sin(a)),p.MapZ+(float)(z*Math.Cos(a)-x*Math.Sin(a)));}
        public static IReadOnlyList<MapPoint> StockpileAccess(BuildingPlacement p,NavigationGrid nav,bool walkableOnly=true)
        {
            int turn=((int)Math.Round(p.RotationDegrees/90)%4+4)%4;
            float w=turn%2==0?p.FootprintWidth:p.FootprintHeight,d=turn%2==0?p.FootprintHeight:p.FootprintWidth;
            var result=new List<MapPoint>();for(float z=.5f;z<d;z++)
            {var point=Point(p,(float)Math.Floor(w/2)+.5f,z);if(!walkableOnly || nav.IsWalkable(point))result.Add(point);}
            return result.AsReadOnly();
        }
        private sealed class Lease : IDisposable
        {private NavigationGrid nav;private readonly List<long> ids;public Lease(NavigationGrid nav,List<long> ids){this.nav=nav;this.ids=ids;}public void Dispose(){if(nav==null)return;foreach(long id in ids)nav.SetObstacle(id,default,0,false);nav=null;}}
    }
}
