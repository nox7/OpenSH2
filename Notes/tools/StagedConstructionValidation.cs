using System;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.MapLoading;
using System.Collections.Generic;
public static class StagedConstructionValidation
{
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    sealed class Staged : IStagedAiBuilding
    {
        public int Steps;public bool Disposed,Shown,Fail;
        public bool IsPrepared=>Steps>=4;
        public void AdvancePreparation(){if(Fail)throw new Exception("controlled stage failure");Steps++;}
        public void Show(){Check(IsPrepared,"Visible before prepared");Shown=true;}
        public void Dispose()=>Disposed=true;
    }
    public static int Main()
    {
        try
        {
            using(var f=new EstateSawpitValidation.Fixture())
            {
                var view=new Staged();f.Build.PreparePresentation=_=>view;
                for(int i=0;i<200 && f.Build.PendingCount==0;i++)f.Clock.Advance(.1);
                f.Build.CommitPending(f.Clock.Now);
                Check(view.Steps==0 && f.Build.CompletedCount==0 && f.Inventory.AvailableAmount(2,Goods.Wood)==29 && f.Inventory.Amount(2,Goods.Wood)==32,"Preparation did not hold cost without spending");
                for(int i=0;i<4;i++)
                {
                    f.Build.CommitPending(f.Clock.Now);Check(view.Steps==i+1 && f.Build.CompletedCount==0 && !view.Shown,"Multiple stages or early publication");
                    Check(f.Map.BuildingPlacements.Count==2 && f.Labor.Jobs.Count==0,"Partial building affected simulation");
                }
                f.Build.CommitPending(f.Clock.Now);Check(f.Build.CompletedCount==1 && view.Shown && f.Inventory.Amount(2,Goods.Wood)==29 && f.Labor.Jobs.Count==1,"Final commit failed");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                var view=new Staged();f.Build.PreparePresentation=_=>view;
                for(int i=0;i<200 && f.Build.PendingCount==0;i++)f.Clock.Advance(.1);
                f.Build.CommitPending(f.Clock.Now);f.Build.CommitPending(f.Clock.Now);
                f.World.SetControl(2,new AiFaction("test",9),AiController.Human);f.Build.CommitPending(f.Clock.Now);
                Check(view.Disposed && !view.Shown && f.Build.CompletedCount==0 && f.Inventory.AvailableAmount(2,Goods.Wood)==32,"Authority loss did not cancel staged preparation");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                var view=new Staged{Fail=true};f.Build.PreparePresentation=_=>view;
                for(int i=0;i<200 && f.Build.PendingCount==0;i++)f.Clock.Advance(.1);
                f.Build.CommitPending(f.Clock.Now);f.Build.CommitPending(f.Clock.Now);
                Check(view.Disposed && !view.Shown && f.Inventory.AvailableAmount(2,Goods.Wood)==32 && f.Labor.Jobs.Count==0,"Stage failure leaked resources/jobs");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            {
                var view=new Staged();f.Build.PreparePresentation=_=>view;
                for(int i=0;i<200 && f.Build.PendingCount==0;i++)f.Clock.Advance(.1);
                f.Build.CommitPending(f.Clock.Now);f.Build.Dispose();Check(view.Disposed && f.Inventory.AvailableAmount(2,Goods.Wood)==32,"Disposal left reserved goods/preparation");
            }
            using(var f=new EstateSawpitValidation.Fixture())
            using(var granary=new S2MEstateGranaryConstruction(f.Map,f.World,f.Inventory,"C:/Steam/steamapps/common/Stronghold 2",f.Nav,f.Labor))
            {
                var allocated=new List<int>();
                f.Build.PreparePresentation=p=>{allocated.Add(p.Id);return new Staged();};
                granary.PreparePresentation=p=>{allocated.Add(p.Id);return new Staged();};
                f.Clock.Dispose();using var scheduler=new AiScheduler(f.World,null,planner:new EstateConstructionPlanner(granary,f.Build));
                for(int i=0;i<100 && (granary.PendingCount==0 || f.Build.PendingCount==0);i++)scheduler.Advance(.1);
                granary.CommitPending(scheduler.Now);f.Build.CommitPending(scheduler.Now);
                Check(allocated.Count==2 && allocated.Distinct().Count()==2,"Concurrent unpublished policies allocated the same building/obstacle ID");
            }
            {
                var inventory=new EstateInventory();inventory.Register(1,1,StorageKind.Stockpile);inventory.TryDeposit(1,1,Goods.Wood,32);
                Check(inventory.TryReserveEstateGoods(1,Goods.Wood,6,out var paid),"Paid reservation setup");Check(inventory.TryReserveEstateGoods(1,Goods.Wood,0,out var free),"Free reservation setup");
                paid.Commit();free.Commit();paid.Dispose();free.Dispose();
                Check(inventory.Amount(1,Goods.Wood)==26 && inventory.AvailableAmount(1,Goods.Wood)==26,"Free building reservation interfered with paid building");
            }
            Console.WriteLine("PASS: one preparation stage per advance, no partial publication, held costs, final atomic activation, capture cancellation, stage-failure rollback and disposal.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
