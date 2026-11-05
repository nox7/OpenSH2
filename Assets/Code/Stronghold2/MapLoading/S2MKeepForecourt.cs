using System;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading
{
    public readonly struct KeepReservedArea
    {
        public readonly int MinX,MinZ,MaxXExclusive,MaxZExclusive;
        public KeepReservedArea(float x0,float z0,float x1,float z1)
        {MinX=(int)Math.Floor(Math.Min(x0,x1));MinZ=(int)Math.Floor(Math.Min(z0,z1));MaxXExclusive=(int)Math.Ceiling(Math.Max(x0,x1));MaxZExclusive=(int)Math.Ceiling(Math.Max(z0,z1));}
    }
    /// <summary>Native keep entrance/placement geometry, independent of mesh bounds. Reserved ground remains walkable.</summary>
    public static class S2MKeepForecourt
    {
        public static bool IsKeep(BuildingPlacement building)=>building is Keep || building.Type=="Keep" || building.Type=="Keep1" || building.Type=="Keep2" || building.Type=="Keep3" || building.Type=="Keep4";
        public static int UnrotatedWidth(BuildingPlacement keep)=>Turn(keep)%2==0?keep.FootprintWidth:keep.FootprintHeight;
        public static MapPoint CalculatedCenter(BuildingPlacement keep)=>Point(keep,UnrotatedWidth(keep)/2,-2);
        public static MapPoint Center(Keep keep)=>keep.HasCampfirePosition?new MapPoint(keep.CampfireRawX/1024f,keep.CampfireRawZ/1024f):CalculatedCenter(keep);
        public static KeepReservedArea Square(BuildingPlacement keep)
        {
            int width=UnrotatedWidth(keep);
            // Native 4x4 extra placement rectangles. The large keep's rectangle is one cell right of its fire centre.
            int x=width==12?5:Math.Max(0,(width-4)/2);
            return Area(keep,x,-4,x+4,0);
        }
        public static KeepReservedArea? EntranceStep(BuildingPlacement keep)=>UnrotatedWidth(keep)==7?Area(keep,0,-1,1,0):(KeepReservedArea?)null;
        private static KeepReservedArea Area(BuildingPlacement keep,float x0,float z0,float x1,float z1)
        {var a=Point(keep,x0,z0);var b=Point(keep,x1,z1);return new KeepReservedArea(a.X,a.Z,b.X,b.Z);}
        private static int Turn(BuildingPlacement keep)=>((int)Math.Round(keep.RotationDegrees/90)%4+4)%4;
        private static MapPoint Point(BuildingPlacement keep,float x,float z)
        {
            var p=Turn(keep) switch{0=>(x,z),1=>(z,-x),2=>(-x,-z),_=>(-z,x)};
            return new MapPoint(keep.MapX+p.Item1,keep.MapZ+p.Item2);
        }
    }
}
