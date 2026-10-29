using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;
using Assets.Code.Simulation;using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Movement;using Assets.Code.Simulation.Population;using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.MapLoading;using Assets.Code.Stronghold2.ModelRendering;using Assets.Code.Stronghold2.ModelRendering.Labor;using Assets.Code.Stronghold2.S2MReader.Resources;using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;using UnityEngine;
public static class IronMineValidation
{
 const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/IronMineValidation",Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){Directory.CreateDirectory(Output);File.Delete(Output+"/failure.txt");File.Delete(Output+"/pass.txt");try{Core();Real();EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
 sealed class Mine
 {
   public readonly IronMineProduction Production=new(new IronMineSettings());public readonly IronMinerJob[] Jobs=new IronMinerJob[2];public readonly WorkerComponent[] Workers=new WorkerComponent[2];
   public Mine(){for(int i=0;i<2;i++){var p=new MapPoint(10+i,10);Jobs[i]=new IronMinerJob(10,1,i,-500,p,new MapPoint(10,10),0,0,Production);Workers[i]=new WorkerComponent(100+i,new MovementComponent(p,0));}}
   public void Assign(int i)=>Check(Workers[i].TryAssign(Jobs[i],new[]{Jobs[i].Approach}),"Could not staff mine");
   public void Tick(float seconds){foreach(var w in Workers)w.Advance(seconds,1);Jobs[0].CompleteWorkStep();}
 }
 static void Core()
 {
   var m=new Mine();m.Assign(0);m.Tick(100);Check(m.Production.StoredIron==0 && !m.Production.IsWorking,"One miner produced iron");m.Assign(1);var building=new LaborBuilding(10,1,m.Jobs);Check(building.MaximumWorkers==2 && building.IsCrewReady,"Mine staffing count incorrect");
   var view=new IronMinerPresentation();m.Tick(.01f);Check(view.AnimationRate(m.Workers[0],1)==0 && view.AnimationRate(m.Workers[1],1)<0,"Mine did not start lever-down before climbing");
   m.Tick(3.5f);Check(view.AnimationRate(m.Workers[0],1)>0,"Miner did not begin climbing down");
   m.Tick(14.35f-(float)m.Production.WorkSeconds+.001f);Check(view.AnimationRate(m.Workers[0],1)<0,"Miner did not reverse the climb clip");
   m.Tick(20.675f-(float)m.Production.WorkSeconds+.001f);Check(view.Variant(m.Workers[0])==S2MIronMineAssets.Carry,"Miner did not carry the ore");
   m.Tick(31.2f-(float)m.Production.WorkSeconds+.001f);Check(view.Variant(m.Workers[0])==S2MIronMineAssets.Return && view.GetEquipment(m.Jobs[0],2).Visible,"Return/molten stages not synchronized");
   m.Tick(35.9f-(float)m.Production.WorkSeconds-.002f);Check(m.Production.StoredIron==0,"Iron credited before the output beat");m.Tick(.004f);Check(m.Production.StoredIron==1,"First output missing at 35.9 seconds");
   double before=m.Production.WorkSeconds;var clock=new SimulationClock{IsPaused=true};clock.Advance(50);m.Tick(clock.DeltaTime);Check(m.Production.WorkSeconds==before,"Paused production advanced");
   clock.IsPaused=false;clock.Speed=2;clock.Advance(m.Production.Settings.CycleSeconds/2);m.Tick(clock.DeltaTime);Check(m.Production.StoredIron==2,"2x clock did not produce one repeat");
   m.Tick(10000);Check(m.Production.StoredIron==10 && m.Production.IsFull && !m.Production.IsWorking,"Mine capacity did not stop at ten");
   Check(m.Production.TryTakeIron(1) && !m.Production.TryTakeIron(20),"Invalid iron withdrawal");m.Tick(35.91f);Check(m.Production.StoredIron==10,"Mine did not resume after collection");
   m.Workers[0].Remove();m.Production.TryTakeIron(1);m.Tick(100);Check(m.Production.StoredIron==9 && building.TotalWorkers==1,"Worker removal did not stop/reset mine");
   m.Workers[0]=new WorkerComponent(110,new MovementComponent(m.Jobs[0].Approach,0));m.Assign(0);m.Tick(35.91f);Check(m.Production.StoredIron==10,"Replacement failed to restore mine production");
   // Ox takes real iron units, uses a different art set, and deposits each unit independently.
   var nav=new NavigationGrid(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],1);var inventory=new EstateInventory();inventory.Register(50,1,StorageKind.Stockpile);inventory.Register(51,2,StorageKind.Stockpile);
   var network=new OxHaulingNetwork(nav,new[]{new OxHaulingSource(10,1,new MapPoint(12.5f,5.5f),m.Production)},new[]{new HaulingDestination(50,1,new MapPoint(20.5f,5.5f)),new HaulingDestination(51,2,new MapPoint(3.5f,5.5f))});network.ConfigureStorage(inventory);
   var approach=new MapPoint(2.5f,5.5f);var ox=new OxHandlerJob(20,1,-2000,approach,new MapPoint(3.5f,5.5f),0,0,new OxHaulingSettings());ox.Configure(network);var handler=new WorkerComponent(200,new MovementComponent(approach,0));handler.TryAssign(ox,new[]{approach});
   long previous=0;int commits=0;inventory.Changed+=s=>{var amount=inventory.Amount(1,Goods.Iron);Check(amount-previous==1,"Iron deposited more than one unit");previous=amount;commits++;};
   bool full=false;for(int i=0;i<20000 && ox.DeliveredIron<8;i++){handler.Advance(.01f,1);if(ox.CargoIron==8)full=true;Check(ox.CargoStone==0,"Mixed stone into iron cargo");}
   Check(full && ox.DeliveredIron==8 && ox.DeliveredStone==0 && commits==8 && inventory.Amount(1,Goods.Iron)==8 && inventory.Amount(2,Goods.Iron)==0 && m.Production.StoredIron==2,"Iron hauling did not conserve eight units in same-estate storage");handler.Remove();
   Check(m.Production.HaulerId==null,"Removing iron handler retained source claim");
   File.WriteAllText(Output+"/core-pass.txt","PASS: two fixed workers; understaffed stop; lever/climb/reverse/carry/return and molten beats; output at 35.9; pause/2x; ten-unit capacity; collection restart; worker removal/replacement; exclusive typed ox cargo; eight real iron units collected/deposited individually; same-estate storage.");
 }
 static void Real()
 {
   Debug.unityLogger.logEnabled=false;S2MFile map=null;string selected=null;
   foreach(var file in Directory.EnumerateFiles("C:/Users/garet/Documents/Stronghold 2/Maps","*.s2m").OrderBy(f=>f))
   {try{var candidate=S2MFileLoader.Load(file);if(candidate.BuildingPlacements.Any(b=>b.Type=="IronMine") && candidate.BuildingPlacements.Any(b=>b.Type=="OxTether") && candidate.BuildingPlacements.Any(b=>b.Type=="StockPile")){map=candidate;selected=file;break;}}catch{} }
   Debug.unityLogger.logEnabled=true;Check(map!=null,"No installed map with an iron mine, tether and stockpile");File.WriteAllText(Output+"/map.txt",selected);
   var pop=new PopulationSettings();var buildings=new S2MBuildingAssetLoader().Load(map,Install,Converter);var nav=S2MNavigationBuilder.Build(map,pop,buildings);var plans=S2MRecruitmentPlan.Create(map,nav,pop);var labor=S2MLaborPlan.Create(map,nav);var inventory=S2MStoragePlan.Create(map);labor.OxHauling.ConfigureStorage(inventory);
   var mines=labor.Jobs.OfType<IronMinerJob>().Where(j=>j.SlotIndex==0).ToArray();Check(mines.Length>0 && mines.All(j=>j.Building.MaximumWorkers==2),"Placed mines did not register two slots");
   var data=new S2MActorAssetLoader().Load(map,Install,S2MCampfirePopulationLoader.Overrides(),converterPath:Converter,placements:S2MCampfirePopulationLoader.CreateRecruitPlacements(plans,labor.Jobs));Check(data.Issues.Count==0,"Mine assets: "+string.Join(";",data.Issues));
   File.WriteAllLines(Output+"/assets.txt",data.Batches.Where(b=>b.Asset.Variant?.StartsWith("iron-")==true).Select(b=>$"{b.Asset.Variant}: {b.Asset.ModelPath}; {b.Asset.AnimationPath}; duration={b.Motion.Duration}; attachments={b.Attachments.Count}"));
   foreach(var name in new[]{S2MIronMineAssets.Climb,S2MIronMineAssets.Lever,S2MIronMineAssets.Carry,S2MIronMineAssets.Return,S2MIronMineAssets.Lift,S2MIronMineAssets.Rock,S2MIronMineAssets.Molten,S2MOxHaulingAssets.OxVariant(false,8,Goods.Iron)})Check(data.Batches.Any(b=>b.Asset.Variant==name),"Missing native variant "+name);
   var root=new GameObject("Iron mine validation");S2MBuildingRenderer.Render(buildings,root.transform);var clock=new SimulationClock();var controller=root.AddComponent<S2MPopulationController>();controller.Initialize(map,nav,plans,data,new S2MTerrainSettings(),new S2MActorRenderSettings(),pop,clock,unitScale:1/204.8f/.01f,jobs:labor.Jobs,laborBuildings:labor.Buildings);var renderer=root.GetComponentInChildren<S2MActorRenderer>();
   var captured=new HashSet<string>();bool delivered=false;
   for(int i=0;i<24000;i++)
   {
     clock.Advance(.1f);controller.Advance(clock.DeltaTime);
     foreach(var mine in mines.Where(j=>j.Production.IsWorking))
     {string stage=mine.Production.NativeTime<3.5f?"Lever":mine.Production.NativeTime<14.35f?"Down":mine.Production.NativeTime<20.675f?"Up":mine.Production.NativeTime<31.2f?"Carry":"Molten";if(captured.Add(stage))Capture(root,renderer,mine,clock,stage);}
     delivered=labor.Jobs.OfType<OxHandlerJob>().Any(j=>j.DeliveredIron>=8);if(delivered && captured.Count==5)break;
   }
   File.WriteAllLines(Output+"/result.txt",mines.Select(j=>$"mine={j.BuildingId} estate={j.EstateId} staffing={j.Building.ArrivedWorkers}/2 stored={j.Production.StoredIron} work={j.Production.WorkSeconds}").Concat(labor.Jobs.OfType<OxHandlerJob>().Select(j=>$"handler={j.BuildingId} estate={j.EstateId} cargo={j.CargoCount} {j.CargoGoods} deliveredIron={j.DeliveredIron} deliveredStone={j.DeliveredStone} phase={j.Phase}")));
   Check(mines.Any(j=>j.Building.IsCrewReady && j.Production.WorkSeconds>35.9),"No placed mine staffed and produced");Check(captured.Count==5,"Did not render all mine stages");Check(delivered,"No iron delivered in the installed map");
   var full=mines.First(j=>j.Building.IsCrewReady);foreach(var j in full.Production.Jobs)
     controller.Kingdoms.SelectMany(k=>k.Peasants).First(p=>p.Id==j.WorkerId).Worker.Advance(1000,1);
   full.CompleteWorkStep();clock.Advance(0);controller.Advance(0);Check(full.Production.StoredIron==10,"Native mine did not fill to its local cap");Capture(root,renderer,full,clock,"Full");
   File.WriteAllText(Output+"/pass.txt","PASS: original map iron mines staff two peasants, load all worker/tool/lift/rock/molten/output assets, play every production stage and deliver eight iron via ox to same-estate stockpile.");UnityEngine.Object.DestroyImmediate(root);
 }
 static void Capture(GameObject root,S2MActorRenderer renderer,IronMinerJob mine,SimulationClock clock,string name)
 {
   foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
   var camera=new GameObject().AddComponent<Camera>();camera.cullingMask=1<<30;camera.transform.position=new Vector3(mine.BuildingPosition.X+4,mine.RawHeight/1024+3.2f,mine.BuildingPosition.Z-4);camera.transform.LookAt(new Vector3(mine.BuildingPosition.X+1,mine.RawHeight/1024+.6f,mine.BuildingPosition.Z+1));camera.fieldOfView=38;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
   var sun=new GameObject().AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.ambientLight=Color.gray;var target=new RenderTexture(1000,800,24);camera.targetTexture=target;var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
   typeof(WoodcuttingValidation).GetMethod("SubmitPreview",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{renderer,camera,clock.DeltaTime});camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=null;
   UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
 }
}
