using System;
namespace Assets.Code.Stronghold2.Castles
{
    public readonly struct AicBuildingSite
    {
        public readonly int TileX,TileZ,ModelX,ModelZ,RotationQuarterTurns;
        public AicBuildingSite(int x,int z,int modelX,int modelZ,int turn){TileX=x;TileZ=z;ModelX=modelX;ModelZ=modelZ;RotationQuarterTurns=turn;}
    }
    /// <summary>Resolved map-space anchor, shared by all future blueprint goals. Native entries name inclusive tile rectangles.</summary>
    public sealed class AicCastleLayout
    {
        public AicCastleBlueprint Blueprint { get; }
        public int OriginX { get; }
        public int OriginZ { get; }
        public int RotationQuarterTurns { get; }
        public AicCastleLayout(AicCastleBlueprint blueprint,int originX,int originZ,int rotationQuarterTurns)
        {Blueprint=blueprint??throw new ArgumentNullException(nameof(blueprint));OriginX=originX;OriginZ=originZ;RotationQuarterTurns=Turn(rotationQuarterTurns);}
        public static AicCastleLayout AtKeepMarker(AicCastleBlueprint blueprint,int markerX,int markerZ,int rotation)
        {
            var keep=blueprint.PrimaryKeep??throw new ArgumentException("Blueprint has no keep.",nameof(blueprint));
            var point=Rotate(keep.LocalX,keep.LocalZ,rotation);
            return new AicCastleLayout(blueprint,markerX-point.X,markerZ-point.Z,rotation);
        }
        public AicBuildingSite Project(AicBuildItem item,int width,int depth)
        {
            if(width<1 || depth<1 || item.RotationCode>3)throw new ArgumentException("Unsupported blueprint footprint or rotation.");
            int w=item.RotationCode%2==0?width:depth,d=item.RotationCode%2==0?depth:width;
            var a=Rotate(item.LocalX,item.LocalZ,RotationQuarterTurns);
            var b=Rotate(item.LocalX+w-1,item.LocalZ+d-1,RotationQuarterTurns);
            int x=OriginX+Math.Min(a.X,b.X),z=OriginZ+Math.Min(a.Z,b.Z),turn=Turn(item.RotationCode+RotationQuarterTurns);
            int modelX=x+(turn==2?width:turn==3?depth:0),modelZ=z+(turn==1?width:turn==2?depth:0);
            return new AicBuildingSite(x,z,modelX,modelZ,turn);
        }
        public AicCastleLayout WithBlueprint(AicCastleBlueprint blueprint)=>new(blueprint,OriginX,OriginZ,RotationQuarterTurns);
        private static int Turn(int turn)=>(turn%4+4)%4;
        private static (int X,int Z) Rotate(int x,int z,int turn)
        {return Turn(turn) switch{0=>(x,z),1=>(z,-x),2=>(-x,-z),_=>(-z,x)};}
    }
}
