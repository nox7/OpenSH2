using System;
using System.Linq;
using System.Collections.Generic;
using Assets.Code.Stronghold2.Castles;
using Assets.Code.Stronghold2.Construction;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Navigation;
public static class BlueprintPlacementValidation
{
    const string Install="C:/Steam/steamapps/common/Stronghold 2";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static AicCastleBlueprint Blueprint(byte kind,int x,int z,int rotation=0)
    {
        var bytes=new byte[4+8+AicCastleReader.TailByteLength];BitConverter.GetBytes(1).CopyTo(bytes,0);
        BitConverter.GetBytes((uint)(kind|x<<8|z<<16|rotation<<24)).CopyTo(bytes,4);return AicCastleReader.Read(bytes,"fixture.aic");
    }
    sealed class View : IStagedAiBuilding
    {
        public int Steps;public bool Disposed;public bool IsPrepared=>Steps>=3;
        public void AdvancePreparation()=>Steps++;public void Show(){Check(IsPrepared,"Shown early");}public void Dispose()=>Disposed=true;
    }
    public static int Main()
    {
        try
        {
            for(int own=0;own<4;own++)for(int global=0;global<4;global++)
            {
                var bp=Blueprint(20,48,40,own);var layout=AicCastleLayout.AtKeepMarker(bp,100,120,global);var site=layout.Project(bp.PrimaryKeep.Value,4,9);
                int w=own%2==0?4:9,d=own%2==0?9:4;
                // Independently rotate every occupied native cell and compare the resulting rectangle.
                var cells=new List<(int X,int Z)>();
                for(int z=40;z<40+d;z++)for(int x=48;x<48+w;x++)
                {var p=global switch{0=>(x,z),1=>(z,-x),2=>(-x,-z),_=>(-z,x)};cells.Add((layout.OriginX+p.Item1,layout.OriginZ+p.Item2));}
                Check(site.TileX==cells.Min(p=>p.X) && site.TileZ==cells.Min(p=>p.Z) && site.RotationQuarterTurns==(own+global)%4,"Rotated blueprint rectangle incorrect");
                var def=S2MBuildingDefinition.Load(Install,"Hovel");
                using var search=new BlueprintBuildingSiteStrategy(layout,20).Begin(new BuildingSiteContext(2,def,new MapPoint(0,0),0));
                Check(search.Step(out var candidate)==BuildingSiteStep.Candidate && candidate.BlueprintItemIndex==0 && candidate.BlueprintKey==bp.Key,"Blueprint site provenance missing");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            using(var build=new S2MEstateGranaryConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor))
            {
                f.Clock.Dispose();var layout=new AicCastleLayout(Blueprint(6,8,8),0,0,0);build.UseBlueprintPlacement(2,layout,6,false);
                BuildingSiteCandidate provenance=default;build.SiteCompleted+=(_,site)=>provenance=site;
                using var clock=new AiScheduler(f.World,null,planner:build);
                for(int i=0;i<200;i++){clock.Advance(.1);build.CommitPending(clock.Now);}
                var granary=f.Map.BuildingPlacements.Single(p=>p.Type=="Granary");
                Check(granary.MapX==8 && granary.MapZ==8 && provenance.BlueprintItemIndex==0,"Blueprint-only goal did not use exact site");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            using(var build=new S2MEstateGranaryConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor))
            {
                f.Clock.Dispose();var layout=new AicCastleLayout(Blueprint(6,20,20),0,0,0);build.UseBlueprintPlacement(2,layout,6,true);
                string provenance="uncommitted";build.SiteCompleted+=(_,site)=>provenance=site.BlueprintKey;
                using var clock=new AiScheduler(f.World,null,planner:build);
                for(int i=0;i<200;i++){clock.Advance(.1);build.CommitPending(clock.Now);}
                Check(build.CompletedCount==1 && provenance==null,"Occupied blueprint slot did not fall back to ordinary placement");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                var layout=new AicCastleLayout(Blueprint(2,8,8),0,0,0);f.Build.UseBlueprintPlacement(2,layout,2,false);
                var view=new View();f.Build.PreparePresentation=_=>view;
                for(int i=0;i<200 && f.Build.PendingCount==0;i++)f.Clock.Advance(.1);
                f.Build.CommitPending(f.Clock.Now);Check(f.Inventory.AvailableAmount(2,Goods.Wood)==29,"Preparation did not reserve cost");
                f.Build.UseBlueprintPlacement(2,layout,2,false);Check(!view.Disposed && f.Inventory.AvailableAmount(2,Goods.Wood)==29,"Repeated unchanged strategy cancelled valid preparation");
                f.Build.SetPlacementStrategy(2,null);
                Check(view.Disposed && f.Inventory.AvailableAmount(2,Goods.Wood)==32,"Strategy switch did not cancel/refund unpublished work");
                f.Build.PreparePresentation=null;f.Tick();Check(f.Build.CompletedCount==1 && f.Inventory.Amount(2,Goods.Wood)==29,"General strategy did not resume or charged twice");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            using(var build=new S2MEstateGranaryConstruction(f.Map,f.World,f.Inventory,Install,f.Nav,f.Labor))
            {
                f.Clock.Dispose();build.UseBlueprintPlacement(2,new AicCastleLayout(Blueprint(6,20,20),0,0,0),6,false);
                using var clock=new AiScheduler(f.World,null,planner:build);
                for(int i=0;i<200;i++){clock.Advance(.1);build.CommitPending(clock.Now);}
                Check(build.CompletedCount==0,"Strict blueprint mode silently placed elsewhere");
                build.SetPlacementStrategy(2,null);
                for(int i=0;i<200;i++){clock.Advance(.1);build.CommitPending(clock.Now);}
                Check(build.CompletedCount==1,"Strict-to-general switch stalled");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                f.Build.UseBlueprintPlacement(2,new AicCastleLayout(Blueprint(2,8,8),0,0,0),2,false);
                var view=new View();f.Build.PreparePresentation=_=>{f.Build.SetPlacementStrategy(2,null);return view;};
                for(int i=0;i<200 && f.Build.PendingCount==0;i++)f.Clock.Advance(.1);
                f.Build.CommitPending(f.Clock.Now);
                Check(view.Disposed && f.Build.CompletedCount==0 && f.Inventory.AvailableAmount(2,Goods.Wood)==32,"Reentrant preparation switch leaked state or costs");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                f.Build.UseBlueprintPlacement(2,new AicCastleLayout(Blueprint(2,8,8),0,0,0),2,false);
                f.World.Changed+=(estate,change)=>{if(estate==2 && f.World.Territories[2].BuildingCount("SawPit")>0)f.Build.SetPlacementStrategy(2,null);};
                f.Tick();Check(f.Build.CompletedCount==1 && f.Inventory.Amount(2,Goods.Wood)==29,"Switch during publication interrupted atomic cost commit");
            }
            Check(StagedConstructionValidation.Main()==0,"Staging regression");Check(SawpitSitingValidation.Main()==0,"Estate/siting regression");
            Console.WriteLine("PASS: sixteen blueprint rotations, exact sites/provenance, occupied-slot fallback, strict mode, live strategy switches, held-cost cancellation and existing construction regressions.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
