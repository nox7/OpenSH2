using System;using System.Linq;using UnityEngine;using UnityEditor;
using Assets.Code.Stronghold2.MapLoading;using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;using Assets.Code.Stronghold2.Construction;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Movement;using Assets.Code.Simulation.AI;using Assets.Code.Simulation.Economy;using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.ModelRendering;using Assets.Code.Stronghold2.TerrainRendering;
public static class KingdomNativeValidation
{
 public static void Run()
 {
  try
  {
   if(KingdomStockpileValidation.Main()!=0)throw new Exception("Core validation failed");
   const string install="C:/Steam/steamapps/common/Stronghold 2",converter="C:/Users/garet/Documents/GitHub/OpenSH2/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
   var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");S2MStartupBuildings.Apply(map,install);
   var data=new S2MBuildingAssetLoader().Load(map,install,converter);
   var nav=S2MNavigationBuilder.Build(map,new PopulationSettings(),data);nav.PrepareConnectivity();
   var inv=S2MStoragePlan.Create(map);using var ai=S2MAiRuntime.Create(map,inv,install);
   using var service=new S2MBuildingConstructionService(map,ai.World,inv,nav);
   using var policy=new S2MKingdomStockpileConstruction(map,ai.World,inv,install,nav,construction:service);
   var root=new GameObject("Stockpile validation");
   var settings=new S2MTerrainSettings();S2MLiveryContext.Create(map,install,root.transform);
   var view=new S2MAiBuildingPresentation(map,install,root.transform,settings,new S2MBuildingRenderSettings(),converter);
   policy.PreparePresentation=view.BeginPreparation;policy.ConstructionFailed+=(id,e)=>Debug.LogError(id+": "+e);
   ai.Scheduler.PlannerFaulted+=(id,e)=>Debug.LogError(id+": "+e);
   ai.Scheduler.SetPlanner(new SettlementPlanner(new BuildingConstructionPlanner(),new GeneralKingdomPlanner(policy)));
   var lookup=new StockpileLocator(inv,nav,new S2MStorageAccessProvider(map,nav));
   var labor=S2MLaborPlan.Create(map,nav);labor.OxHauling.ConfigureStorage(inv,lookup);
   var stockView=S2MStockpileRenderer.Create(map,inv,S2MStockpileAssets.Load(install,converter),root.transform,settings); int before=map.BuildingPlacements.Count;
   for(int i=0;i<3000;i++){ai.Scheduler.Advance(.1);policy.CommitPending(ai.Scheduler.Now);}
   var added=map.BuildingPlacements.Skip(before).ToArray();
   foreach(var t in ai.World.Territories.Values.Where(t=>t.Kind==AiTerritoryKind.Kingdom))Debug.Log("KINGDOM "+t.Id+" controller="+t.Controller+" policy="+t.ConstructionPolicy+" stocks="+t.BuildingCount("StockPile"));
   if(!added.Any(p=>p.RuntimeEstateId==3 && p.Type=="StockPile"))throw new Exception("Edwin stockpile missing");
   foreach(var p in added)
   {
    if(!policy.KeepCanReach(p))throw new Exception("Keep access blocked");
    if(!lookup.HasCapacity(p.RuntimeEstateId.Value,Goods.Stone))throw new Exception("Store not found");
   }
   var source=labor.OxHauling.Sources.First(s=>s.EstateId==3);
   if(!labor.OxHauling.TryDestination(3,source.Position,out int target,out var route))throw new Exception("Edwin quarry cannot reach stockpile");
      foreach(var job in labor.Jobs.OfType<QuarryJob>().Where(j=>j.EstateId==3))
   {
    var worker=new WorkerComponent(-10000-job.SlotIndex,new MovementComponent(job.Approach,0));
    worker.TryAssign(job,new[]{job.Approach});worker.Advance(1000,1);
   }
   ((QuarryProduction)source.Production).CompleteWorkStep();
   var handler=labor.Jobs.OfType<OxHandlerJob>().First(j=>j.EstateId==3);var haulingWorker=new WorkerComponent(-999,new MovementComponent(handler.Approach,0));
   haulingWorker.TryAssign(handler,new[]{handler.Approach});
   for(int i=0;i<60000 && handler.DeliveredStone<16;i++)haulingWorker.Advance(.05f,1);
   if(handler.DeliveredStone!=16 || inv.Amount(3,Goods.Stone)!=16)throw new Exception("Haul failed phase="+handler.Phase+" cargo="+handler.CargoStone+" delivered="+handler.DeliveredStone);
   stockView.Synchronize();if(stockView.VisiblePieces==0)throw new Exception("Runtime stockpile goods invisible");
   Debug.Log("DELIVERY PASS: Edwin collected and deposited sixteen stone through shared lookup; visible pieces="+stockView.VisiblePieces);
   Debug.Log("KINGDOM PASS: added="+added.Length+" Edwin target="+target+" route points="+route.Count+" rendered meshes="+root.GetComponentsInChildren<MeshRenderer>().Length);
   UnityEngine.Object.DestroyImmediate(root);EditorApplication.Exit(0);
  }
  catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
