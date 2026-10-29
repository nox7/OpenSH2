using System;using System.IO;using System.Linq;using System.Collections.Generic;
using Assets.Code.Simulation;using Assets.Code.Simulation.Economy;using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Movement;using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.MapLoading;using Assets.Code.Stronghold2.ModelRendering;using UnityEditor;using UnityEngine;
public static class IncrementalDeliveryValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/StorageValidation";
  const string Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  public static void Run()=>StorageValidation.Run();
  sealed class Fixture
  {
    public readonly WoodcuttingSettings Settings=new();public readonly FoliageResources Resources;public readonly EstateInventory Inventory=new();
    public readonly WoodcutterJob Job;public readonly WorkerComponent Worker;
    public Fixture(int pallets=30,bool fast=false)
    {
      if(fast){Settings.CartSpeedCellsPerSecond=10;Settings.SawingSeconds=.1f;Settings.FallSeconds=.1f;Settings.FellingCycles=Settings.ChoppingCycles=1;}
      var nav=new NavigationGrid(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],1);
      Resources=new FoliageResources(new[]{new FoliageResource(1,new MapPoint(12,12),.3f),new FoliageResource(2,new MapPoint(17,12),.3f)},nav,Settings);
      Inventory.Register(50,1,StorageKind.Stockpile,pallets);
      var home=new MapPoint(3,4);Job=new WoodcutterJob(1,1,-1,home,home,0,0);Job.ConfigureHarvest(Resources,Settings);
      if(fast)Job.SetClipDurations(.1f,.1f);
      Job.ConfigureProduction(home,0,new[]{new WoodDeliveryPoint(50,new MapPoint(30,30))});Job.ConfigureStorage(Inventory);
      Worker=new WorkerComponent(1,new MovementComponent(home,0));Check(Worker.TryAssign(Job,new[]{home}),"Fixture staffing failed");
    }
    public void Arrive()
    {
      for(int i=0;i<30000 && Job.Phase!=WoodcuttingPhase.Unloading;i++)Worker.Advance(.01f,1);
      Check(Job.Phase==WoodcuttingPhase.Unloading && Job.DeliveredPlanks==0 && Job.RemainingPlanks==16,"Arrival credited wood prematurely");
    }
  }
  public static void Core()
  {
    var f=new Fixture();f.Arrive();var first=f.Job.Target;
    float wait=f.Inventory.DeliverySettings.SecondsPerItem-f.Job.PhaseTime;
    f.Worker.Advance(Math.Max(0,wait-.001f),1);Check(f.Inventory.Amount(1,Goods.Wood)==0,"First plank deposited early");
    f.Worker.Advance(.0015f,1);Check(f.Inventory.Amount(1,Goods.Wood)==1 && f.Job.RemainingPlanks==15 && f.Job.Loaded,"First interval did not transfer one plank");
    Check(!f.Job.ResumeAfterUnloading(f.Worker),"Legacy resume hook discarded loaded cargo");
    var clock=new SimulationClock{IsPaused=true};clock.Advance(10);f.Worker.Advance(clock.DeltaTime,1);Check(f.Job.DeliveredPlanks==1,"Pause advanced unloading");
    f.Inventory.DeliverySettings.SecondsPerItem=.3f;f.Worker.Advance(.29f,1);Check(f.Job.DeliveredPlanks==1,"Custom interval ignored");
    clock.IsPaused=false;clock.Speed=2;clock.Advance(.005f);f.Worker.Advance(clock.DeltaTime,1);Check(f.Job.DeliveredPlanks==2,"Game speed did not advance unloading timer");
    f.Worker.Advance(14*.3f,1);Check(f.Job.CompletedDeliveries==1 && f.Job.DeliveredPlanks==16 && f.Job.RemainingPlanks==0 && !f.Job.Loaded,"Cart did not empty after sixteen intervals");
    Check(f.Job.Phase!=WoodcuttingPhase.AtStockpile && f.Resources.AssignedPile(1)==first,"Automatic restart lost the assigned pile");
    for(int i=0;i<10000 && first.LogsRemaining==5;i++)f.Worker.Advance(.05f,1);
    Check(first.LogsRemaining==4 && f.Job.Target==first,"Repeat trip selected new foliage before exhausting the pile");
    Competition();
    var large=new Fixture(fast:true);var small=new Fixture(fast:true);large.Worker.Advance(1000,1);
    for(int i=0;i<2000;i++)small.Worker.Advance(.5f,1);
    Check(large.Job.DeliveredPlanks==192 && small.Job.DeliveredPlanks==192 && large.Job.CompletedDeliveries==12 && small.Job.CompletedDeliveries==12,"Large ticks lost unloading intervals or repeated work cycles");
    Check(large.Resources.All.All(r=>r.State==FoliageState.Removed) && small.Resources.All.All(r=>r.State==FoliageState.Removed),"Depleted pile did not lead to another tree");
    var dying=new Fixture();dying.Arrive();dying.Worker.Advance(.15f,1);long credited=dying.Job.DeliveredPlanks;var pile=dying.Job.Target;dying.Worker.Remove();dying.Worker.Advance(100,1);
    Check(dying.Inventory.Amount(1,Goods.Wood)==credited && pile.ReservedBy==null && dying.Job.RemainingPlanks==0,"Removal during unloading duplicated cargo or retained ownership");
    Directory.CreateDirectory(Output);File.WriteAllText(Output+"/incremental-core-pass.txt","PASS: one plank per interval; no arrival/early credit; customizable interval; pause and 2x speed; automatic same-pile return; depletion/new foliage; partial capacity and competing unloaders; removal; 12 cycles/192 planks with large and small ticks.");
  }
  static void Competition()
  {
    var first=new Fixture(1);first.Inventory.TryDeposit(50,1,Goods.Wood,20);first.Arrive();
    var home=new MapPoint(3,6);var second=new WoodcutterJob(2,1,-2,home,home,0,0);second.ConfigureHarvest(first.Resources,first.Settings);second.ConfigureProduction(home,0,new[]{new WoodDeliveryPoint(50,new MapPoint(30,30))});second.ConfigureStorage(first.Inventory);
    var worker=new WorkerComponent(2,new MovementComponent(home,0));Check(worker.TryAssign(second,new[]{home}),"Second staffing failed");
    for(int i=0;i<30000 && second.Phase!=WoodcuttingPhase.Unloading;i++)worker.Advance(.01f,1);
    Check(second.Phase==WoodcuttingPhase.Unloading,"Second arrival failed");
    for(int i=0;i<8;i++){first.Worker.Advance(.15f,1);worker.Advance(.15f,1);}
    Check(first.Inventory.Amount(1,Goods.Wood)==36 && first.Job.RemainingPlanks==8 && second.RemainingPlanks==8,"Concurrent unloading overfilled a pallet or lost partial cargo");
    first.Worker.Advance(.15f,1);worker.Advance(.15f,1);Check(first.Job.Phase==WoodcuttingPhase.WaitingForStockpile && second.Phase==WoodcuttingPhase.WaitingForStockpile,"Full storage did not retain loaded workers");
    first.Inventory.TryWithdraw(50,Goods.Wood,16);first.Worker.Advance(6.3f,1);worker.Advance(6.3f,1);
    Check(first.Job.CompletedDeliveries==1 && second.CompletedDeliveries==1 && first.Inventory.Amount(1,Goods.Wood)==36,"Partial cargo did not finish when storage room opened");
  }
  public static void Map()
  {
    Directory.CreateDirectory(Output);File.Delete(Output+"/incremental-map-pass.txt");
    var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");var settings=new WoodcuttingSettings();
    var buildings=new S2MBuildingAssetLoader().Load(map,Install,Converter);var nav=S2MNavigationBuilder.Build(map,new PopulationSettings(),buildings,dynamicFoliage:true);
    var root=new GameObject("Incremental woodcutting map");var data=new S2MVegetationAssetLoader().Load(map,Install,Converter);
    var scenery=S2MVegetationRenderer.Render(data,root.transform,treeModelScale:.5f);var resources=scenery.GetComponent<S2MFoliageHarvestView>().Initialize(nav,settings);
    var inventory=S2MStoragePlan.Create(map);var jobs=S2MLaborPlan.Create(map,nav).Jobs.OfType<WoodcutterJob>().ToArray();var workers=new List<WorkerComponent>();
    var initial=new Dictionary<int,FoliageResource>();bool partial=false,switched=false;
    var previous=new Dictionary<int,long>();int commits=0;
    inventory.Changed+=store=>{long amount=store.Amount(Goods.Wood);long old=previous.TryGetValue(store.BuildingId,out var v)?v:0;Check(amount-old==1,"A map unloading transaction transferred multiple planks");previous[store.BuildingId]=amount;commits++;};
    for(int i=0;i<jobs.Length;i++)
    {var j=jobs[i];j.ConfigureHarvest(resources,settings);j.ConfigureStorage(inventory);var w=new WorkerComponent(i+1,new MovementComponent(j.Approach,0));Check(w.TryAssign(j,new[]{j.Approach}),"Map job could not be staffed");workers.Add(w);}
    int ticks=0;
    for(;ticks<30000;ticks++)
    {
      foreach(var w in workers)
      {
        w.Advance(.25f,.85f);var job=(WoodcutterJob)w.Job;
        if(job.Target!=null)
        {
          if(!initial.TryGetValue(w.ActorId,out var resource))initial[w.ActorId]=job.Target;
          else if(job.Target!=resource){Check(resource.State==FoliageState.Removed,"Map worker abandoned a nonempty assigned pile");switched=true;}
        }
        if(job.Phase==WoodcuttingPhase.Unloading && job.RemainingPlanks>0 && job.RemainingPlanks<16)partial=true;
      }
      scenery.GetComponent<S2MFoliageHarvestView>().Synchronize();
      foreach(int estate in new[]{1,3,11})Check(inventory.Amount(estate,Goods.Wood)==jobs.Where(j=>j.EstateId==estate).Sum(j=>j.DeliveredPlanks),"Map inventory differs from delivered plank receipts");
      if(switched && jobs.Count(j=>j.CompletedDeliveries>=2)==6)break;
    }
    Check(ticks<30000 && partial && switched,"Map repeated-cycle/partial-unload coverage incomplete");
    Check(jobs.Where(j=>j.EstateId==3).All(j=>j.DeliveredPlanks==0 && j.RemainingPlanks==16 && j.Phase==WoodcuttingPhase.WaitingForStockpile),"Missing-stockpile estate lost cargo or crossed estates");
    Check(commits==jobs.Sum(j=>j.DeliveredPlanks),"Map deposits were duplicated or bundled");
    File.WriteAllText(Output+"/incremental-map-pass.txt",$"PASS: all 8 chapter-8 saw pits; six repeat workers, two same-estate missing-store waits; per-plank inventory commits={commits}; automatic assigned-pile reuse and new foliage after depletion; {ticks*.25f:F2} game seconds. Estate 1 wood={inventory.Amount(1,Goods.Wood)}, estate 11 wood={inventory.Amount(11,Goods.Wood)}.");
    UnityEngine.Object.DestroyImmediate(root);
  }
}
