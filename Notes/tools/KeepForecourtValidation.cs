using System;
using System.Linq;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.Construction;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Simulation.Population;
using Assets.Code.Simulation.Navigation;
public static class KeepForecourtValidation
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
    static void Near(float a,float b,string message)=>Check(Math.Abs(a-b)<.0001f,message+": "+a+" != "+b);
    public static int Main()
    {try{Run();Console.WriteLine("PASS: four keep families x four rotations, saved centres, model pivots, terrain height, complete forecourt reservations and walkable ground.");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
    public static void Run()
    {
        int[] widths={4,7,8,12},depths={9,7,9,18};
        for(int size=0;size<4;size++)for(int turn=0;turn<4;turn++)
        {
            var map=new S2MFile{MapSize=96};Set(map,"HeightLayer",new HeightLayer{Width=96,Height=96,Heights=Enumerable.Repeat(123f,96*96).ToArray()});
            Set(map,"RadarEstateLayer",new EstateLayer{Width=96,Height=96,EstateIds=Enumerable.Repeat((byte)2,96*96).ToArray()});
            var keep=new Keep();Set(keep,"Id",100+size*4+turn);Set(keep,"Type","Keep"+(size+1));Set(keep,"Size",(KeepSize)size);Set(keep,"StartupEstateId",(int?)2);
            Set(keep,"RawX",48*1024f);Set(keep,"RawZ",48*1024f);Set(keep,"RawY",999f);Set(keep,"RotationDegrees",turn*90f);
            Set(keep,"FootprintWidth",turn%2==0?widths[size]:depths[size]);Set(keep,"FootprintHeight",turn%2==0?depths[size]:widths[size]);
            Set(map,"Keeps",new[]{keep});Set(map,"BuildingPlacements",new BuildingPlacement[]{keep});
            float half=new[]{2,3,4,6}[size];var delta=turn switch{0=>(half,-2f),1=>(-2f,-half),2=>(-half,2f),_=>(2f,half)};
            var camp=S2MCampfireLayout.Create(map).Single();Near(camp.Center.x,48+delta.Item1,"Native centre X");Near(camp.Center.y,48+delta.Item2,"Native centre Z");Near(camp.RawHeight,123,"Generated fire sampled keep/platform height instead of ground");
            Near(camp.ModelOrigin.x+.5f,camp.Center.x,"Unrotated model pivot X");Near(camp.ModelOrigin.y+.5f,camp.Center.y,"Unrotated model pivot Z");
            var square=S2MKeepForecourt.Square(keep);Check(square.MaxXExclusive-square.MinX==4 && square.MaxZExclusive-square.MinZ==4,"Forecourt is not 4x4");
            var placement=new S2MBuildingPlacementService(map);var nav=S2MNavigationBuilder.Build(map,new PopulationSettings());
            var probe=new BuildingPlacement();Set(probe,"Type","Probe");Set(probe,"RuntimeEstateId",(int?)2);Set(probe,"FootprintWidth",1);Set(probe,"FootprintHeight",1);
            for(int z=square.MinZ;z<square.MaxZExclusive;z++)for(int x=square.MinX;x<square.MaxXExclusive;x++)
            {
                Set(probe,"RawX",x*1024f);Set(probe,"RawZ",z*1024f);
                Check(!placement.Check(probe,0).Allowed,"Building permitted inside forecourt");Check(nav.IsWalkable(new MapPoint(x+.5f,z+.5f)),"Forecourt became a navigation obstacle");
            }
            Set(probe,"RawX",5*1024f);Set(probe,"RawZ",5*1024f);Check(placement.Check(probe,0).Allowed,"Unrelated ground was reserved");
            if(size>0)
            {var side=turn switch{0=>(48,45),1=>(45,47),2=>(47,50),_=>(50,48)};Set(probe,"RawX",side.Item1*1024f);Set(probe,"RawZ",side.Item2*1024f);Check(placement.Check(probe,0).Allowed,"Reserved the full keep width instead of its 4x4 square");}
            Set(keep,"HasCampfirePosition",true);Set(keep,"CampfireRawX",30.25f*1024);Set(keep,"CampfireRawZ",40.75f*1024);Set(keep,"CampfireRawY",77f);
            camp=S2MCampfireLayout.Create(map).Single();Near(camp.Center.x,30.25f,"Saved centre was shifted/rotated X");Near(camp.Center.y,40.75f,"Saved centre was shifted/rotated Z");Near(camp.RawHeight,77,"Saved height lost");
        }
    }
}
