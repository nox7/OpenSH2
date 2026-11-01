// Console validation against the compiled project; does not start Unity or mutate game files.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Simulation;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Enums;

public static class AiInfrastructureValidation
{
    static void Check(bool v,string why){if(!v)throw new Exception(why);}
    static readonly AiFaction Owner=new("test",1);
    sealed class Planner : IAiPlanner
    {
        public readonly Dictionary<int,int> Calls=new();
        public AiChange Seen;public bool Fail;
        public void Evaluate(in AiEvaluation e)
        {
            Calls[e.Territory.Id]=Calls.TryGetValue(e.Territory.Id,out int n)?n+1:1;Seen|=e.Changes;
            int units=0;while(e.Budget.TrySpend())units++;
            Check(units==128,"Work budget changed");Check(!e.Budget.TrySpend(),"Work budget overrun");
            if(Fail && e.Territory.Id==1)throw new InvalidOperationException("controlled planner failure");
        }
    }
    static AiTerritory T(int id,AiController control=AiController.Autonomous,AiTerritoryKind kind=AiTerritoryKind.Estate)=>new(id,kind,control,Owner);
    public static int Main()
    {
        try { Facts();Scheduling();Scale();Bootstrap();Console.WriteLine("PASS: AI facts, ownership, attacks/expiry, pause/speed, bounded/coalesced scheduling, fairness, planner isolation, 10,000 territories, map bootstrap and AIC catalog.");return 0; }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Facts()
    {
        var w=new AiWorld();w.Add(T(1,kind:AiTerritoryKind.Kingdom));w.Add(T(2));w.SetParentKingdom(2,1);
        w.UpsertBuilding(new AiBuilding(50,2,"Granary"));w.UpsertBuilding(new AiBuilding(50,2,"Granary"));
        Check(w.Territories[2].BuildingCount("Granary")==1,"Existing building duplicated");
        w.UpsertBuilding(new AiBuilding(50,1,"Hovel"));
        Check(w.Territories[2].BuildingCount("Granary")==0 && w.Territories[1].BuildingCount("Hovel")==1,"Building capture/type indexes stale");
        Check(w.RemoveBuilding(50) && !w.RemoveBuilding(50),"Destroyed building removal not idempotent");
        w.ReportAttack(2,7,2);w.ReportAttack(2,7,3);
        Check(w.Territories[2].IsUnderAttack && w.Territories[1].Threats.Count==1 && !w.Territories[1].IsUnderAttack,"Attack/aid distinction or coalescing");
        w.ClearAttack(2,7);Check(!w.Territories[2].IsUnderAttack && w.Territories[1].Threats.Count==0,"Attack clear not relayed");
        w.ReportAttack(2,7,3);w.SetControl(2,new AiFaction("test",2),AiController.Autonomous);
        Check(w.Territories[2].ParentKingdomId==null && w.Territories[1].Threats.Count==0,"Capture retained old aid relationship");
        w.Add(T(3,AiController.Human,AiTerritoryKind.Kingdom));w.ReportAttack(3,9,.1);
        using var s=new AiScheduler(w,null);s.Advance(.2);
        Check(!w.Territories[3].IsUnderAttack && w.Territories[3].Threats.Count==0,"Human territory incident never expires");
        w.Remove(2);Check(!w.TryGet(2,out _),"Territory removal failed");
    }
    static void Scheduling()
    {
        var w=new AiWorld();w.Add(T(1));w.Add(T(2,AiController.Human,AiTerritoryKind.Kingdom));
        var p=new Planner();using var s=new AiScheduler(w,null,planner:p);
        Check(s.RegisteredTerritories==1,"Human kingdom registered for autonomous work");
        for(int i=0;i<10000;i++)w.Notify(1,AiChange.Resources);
        Check(s.PendingWakeups==1,"Event storm created unbounded queue entries");
        var clock=new SimulationClock{IsPaused=true};clock.Advance(10);s.Advance(clock.DeltaTime);
        Check(s.EvaluationCount==0 && s.Now==0,"Paused AI dispatched");
        clock.IsPaused=false;clock.Speed=2;clock.Advance(.5f);s.Advance(clock.DeltaTime);
        Check(s.Now==1 && p.Calls[1]==1 && !p.Calls.ContainsKey(2),"Speed or exactly-once coalesced evaluation");
        w.SetControl(1,Owner,AiController.Human);Check(s.RegisteredTerritories==0,"AI authority survived human takeover");
        w.SetControl(1,Owner,AiController.Autonomous);Check(s.RegisteredTerritories==1,"AI authority not restored");
        p.Fail=true;w.Notify(1,AiChange.Buildings);s.Advance(1);
        Check(s.PlannerFailureCount==1,"Planner fault not isolated/reported");
        s.Dispose();long before=s.EvaluationCount;w.Notify(1,AiChange.Resources);Check(s.PendingWakeups==0 && s.EvaluationCount==before,"Disposed scheduler still subscribed");
        var w2=new AiWorld();for(int i=1;i<=100;i++)w2.Add(T(i));
        var p2=new Planner();using var s2=new AiScheduler(w2,null,new AiSettings{MaximumEvaluationsPerAdvance=8},p2);
        for(int frame=0;frame<100;frame++)
        {
            foreach(var t in w2.Territories.Values)w2.Notify(t.Id,AiChange.Threats);
            s2.Advance(.1);Check(s2.LastAdvanceEvaluations<=8,"Evaluation budget exceeded");
        }
        Check(p2.Calls.Count==100,"Urgent events starved routine territories");
        var jump=s2.EvaluationCount;s2.Advance(10000);Check(s2.EvaluationCount-jump<=8,"Large time jump caused catch-up storm");
    }
    static void Scale()
    {
        var w=new AiWorld();for(int i=1;i<=10000;i++)w.Add(T(i));
        var p=new Planner();using var s=new AiScheduler(w,null,planner:p);
        var timer=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<3000;i++){s.Advance(.02);Check(s.LastAdvanceEvaluations<=8,"Scale budget exceeded");}
        timer.Stop();Check(p.Calls.Count==10000,"Some territories never evaluated at scale");
        Console.WriteLine($"10,000 territories / 3,000 advances: {timer.ElapsedMilliseconds} ms; {s.EvaluationCount} bounded evaluations.");
    }
    static void Set(object o,string p,object v)=>o.GetType().GetProperty(p).SetValue(o,v);
    static byte[] Bytes(Action<BinaryWriter> write){using var stream=new MemoryStream();using var b=new BinaryWriter(stream);write(b);return stream.ToArray();}
    static void Bootstrap()
    {
        var map=new S2MFile{MapSize=8};var markers=new List<EstateMarkerFlag>();
        for(int id=1;id<=6;id++){var m=new EstateMarkerFlag();Set(m,"EstateId",id);Set(m,"Type",id==1||id==3||id==6?EstateType.Castle:EstateType.Estate);markers.Add(m);}
        Set(map,"EstateMarkers",markers);
        var scenario=new RawS2Object{Payload=Bytes(b=>{
            b.Write(4);b.Write(0);b.Write(3);b.Write(84);foreach(int lord in new[]{0,1,1,10,10,0,0}.Concat(Enumerable.Repeat(0,14)))b.Write(lord);
            b.Write(4);b.Write(84);for(int i=0;i<21;i++)b.Write(i==4?5:0);
            b.Write(5);b.Write(21);for(int i=0;i<21;i++)b.Write((byte)(i==4?1:0));
        })};Set(scenario,"Type","Scenario");Set(map,"MapHeaderObjects",new S2Object[]{scenario});
        var stock=new BuildingPlacement();Set(stock,"Id",100);Set(stock,"Type","StockPile");Set(stock,"StartupEstateId",(int?)2);
        Set(map,"BuildingPlacements",new[]{stock});var inventory=new EstateInventory();inventory.Register(100,2,StorageKind.Stockpile);
        using var runtime=S2MAiRuntime.Create(map,inventory,"C:/Steam/steamapps/common/Stronghold 2");
        Check(!runtime.World.Territories[1].CanPlan && runtime.World.Territories[1].Controller==AiController.Human,"Initial human kingdom policy");
        Check(runtime.World.Territories[2].CanPlan && runtime.World.Territories[2].ParentKingdomId==1,"Human-owned estate should remain autonomous");
        Check(runtime.World.Territories[3].CanPlan && runtime.World.Territories[4].ParentKingdomId==3,"AI kingdom/estate policy");
        Check(runtime.World.Territories[5].CanPlan && !runtime.World.Territories[6].CanPlan,"Neutral estate/unknown kingdom policy");
        Check(runtime.World.Territories[4].ProductionCode==5 && runtime.World.Territories[4].ConstructionPolicy==AiConstructionPolicy.UseMapBuildings,"Scenario production/construction profile decoding");
        Check(runtime.World.Territories[2].BuildingCount("StockPile")==1,"Existing/startup buildings not indexed");
        Check(runtime.Blueprints.Count>0 && runtime.Blueprints.All(b=>b.Bytes>0),"AIC catalog unavailable");
        long revision=runtime.World.Territories[2].Revision;inventory.TryDeposit(100,2,Goods.Wood,1);
        Check(runtime.World.Territories[2].Revision>revision,"Inventory not connected");
        runtime.Dispose();revision=runtime.World.Territories[2].Revision;inventory.TryDeposit(100,2,Goods.Wood,1);
        Check(runtime.World.Territories[2].Revision==revision,"Disposed inventory observer still subscribed");
    }
}
