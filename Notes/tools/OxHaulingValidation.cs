using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;
using Assets.Code.Simulation;using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Movement;using Assets.Code.Simulation.Population;using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.MapLoading;using Assets.Code.Stronghold2.ModelRendering;using Assets.Code.Stronghold2.ModelRendering.Labor;using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;using UnityEngine;
public static class OxHaulingValidation
{
 const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/OxHaulingValidation",Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){Directory.CreateDirectory(Output);File.Delete(Output+"/failure.txt");File.Delete(Output+"/pass.txt");try{Core();Real();EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
 static QuarryProduction Filled(int id,int estate)
 {
   var production=new QuarryProduction(new QuarrySettings());var crew=new List<WorkerComponent>();
   for(int i=0;i<3;i++){var p=new MapPoint(10+i,10);var j=new QuarryJob(id,estate,i,-500,p,p,0,0,p,production);var w=new WorkerComponent(id*100+i,new MovementComponent(p,0));w.TryAssign(j,new[]{p});crew.Add(w);}
   foreach(var w in crew)w.Advance(1000,1);production.CompleteWorkStep();Check(production.StoredStone==48,"Fixture quarry not filled");return production;
 }
 static void ScarceQuarry()
 {
   var nav=new NavigationGrid(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],1);
   var producer=new QuarryProduction(new QuarrySettings());var crew=new List<WorkerComponent>();
   for(int i=0;i<3;i++){var point=new MapPoint(10+i,10);var job=new QuarryJob(100,1,i,-1000,point,point,0,0,point,producer);var w=new WorkerComponent(1000+i,new MovementComponent(point,0));w.TryAssign(job,new[]{point});crew.Add(w);}
   void Produce(float seconds){foreach(var w in crew)w.Advance(seconds,1);producer.CompleteWorkStep();}
   var other=Filled(101,1);var sharedDeposit=new StockpileDeliverySettings();var inventory=new EstateInventory(deliverySettings:sharedDeposit);Check(ReferenceEquals(sharedDeposit,inventory.DeliverySettings),"Map lost its shared live deposit settings reference");inventory.Register(150,1,StorageKind.Stockpile);
   var network=new OxHaulingNetwork(nav,new[]{new OxHaulingSource(100,1,new MapPoint(12.5f,5.5f),producer),new OxHaulingSource(101,1,new MapPoint(35.5f,5.5f),other)},
     new[]{new HaulingDestination(150,1,new MapPoint(20.5f,5.5f))});network.ConfigureStorage(inventory);
   var approach=new MapPoint(2.5f,5.5f);var ox=new OxHandlerJob(100,1,-2000,approach,new MapPoint(3.5f,5.5f),0,0,new OxHaulingSettings());ox.Configure(network);
   var handler=new WorkerComponent(1,new MovementComponent(approach,0));handler.TryAssign(ox,new[]{approach});
   for(int i=0;i<3000 && ox.Phase!=OxHaulingPhase.Loading;i++)handler.Advance(.01f,1);
   Check(ox.Phase==OxHaulingPhase.Loading && ox.Source?.BuildingId==100 && producer.HaulerId==1 && ox.CargoStone==0,"Nearest empty quarry was skipped or not reserved");
   var parked=ox.Ox.Position;Produce((float)producer.FirstBlockSeconds);handler.Advance(.5f,1);
   Check(ox.CargoStone==1 && ox.VisibleLoadBlocks==1 && producer.StoredStone==0,"First produced block not collected");
   handler.Advance(100,1);Check(ox.CargoStone==1 && ox.DeliveredStone==0 && ox.Phase==OxHaulingPhase.Loading && producer.HaulerId==1 && MapPoint.Distance(parked,ox.Ox.Position)<.001f,"Handler abandoned depleted quarry or delivered a partial load");
   Check(!producer.TryReserveHauler(2),"Second handler stole a quarry during the production wait");
   Check(network.TrySource(2,1,approach,out var alternative,out _) && alternative.BuildingId==101,"Second handler did not skip the assigned nearest quarry");other.ReleaseHauler(2);
   for(int n=2;n<=16;n++)
   {
     Produce((float)producer.CycleSeconds);handler.Advance(.5f,1);
     Check(ox.VisibleLoadBlocks==(n+1)/2,"Logical stone count does not match the eight-stage visual mapping");
     Check(ox.CargoStone==n && ox.DeliveredStone==0 && producer.HaulerId==1,"Production wait lost cargo, released the claim or departed before capacity");
   }
   handler.Advance(.01f,1);Check(ox.Phase==OxHaulingPhase.ToStockpile && ox.Source==null && producer.HaulerId==null,"Full ox did not depart and release its quarry");
   Check(network.TrySource(2,1,approach,out var released,out _) && released.BuildingId==100,"Released empty quarry could not be assigned to another handler");producer.ReleaseHauler(2);
   int changes=0;long last=0;inventory.Changed+=store=>{long amount=inventory.Amount(1,Goods.Stone);Check(amount-last==1,"Ox deposit was not a single stone transaction");last=amount;changes++;};
   for(int i=0;i<10000 && ox.Phase!=OxHaulingPhase.Unloading;i++)handler.Advance(.01f,1);
   Check(ox.Phase==OxHaulingPhase.Unloading && ox.CargoStone==16 && inventory.Amount(1,Goods.Stone)==0,"Stockpile arrival credited stone before the deposit interval");
   float wait=inventory.DeliverySettings.SecondsPerItem-ox.PhaseTime;handler.Advance(Math.Max(0,wait-.001f),1);Check(ox.DeliveredStone==0,"First stone deposited early");
   handler.Advance(.0015f,1);Check(ox.DeliveredStone==1 && ox.CargoStone==15 && changes==1,"Default 0.15-second interval did not deposit one actual stone");
   var clock=new SimulationClock{IsPaused=true};clock.Advance(10);handler.Advance(clock.DeltaTime,1);Check(ox.DeliveredStone==1,"Pause advanced stone deposits");
   inventory.DeliverySettings.SecondsPerItem=.3f;handler.Advance(.29f,1);Check(ox.DeliveredStone==1,"Live shared deposit interval was ignored");
   clock.IsPaused=false;clock.Speed=2;clock.Advance(.005f);handler.Advance(clock.DeltaTime,1);Check(ox.DeliveredStone==2 && ox.CargoStone==14,"2x game speed was not applied once to stone deposits");
   handler.Advance(.2f,1);float beforeReduction=ox.PhaseTime;sharedDeposit.SecondsPerItem=.15f;handler.Advance(.01f,1);
   Check(ox.DeliveredStone==3 && Math.Abs(ox.PhaseTime-beforeReduction-.01f)<.00001f,"Reducing the live interval created simulation time or lost a deposit");
   sharedDeposit.SecondsPerItem=.3f;handler.Advance(13*.3f,1);Check(ox.DeliveredStone==16 && inventory.Amount(1,Goods.Stone)==16 && ox.CargoStone==0 && changes==16,"Sixteen collected stone did not become exactly sixteen individual deposits");handler.Remove();
 }
 static void Core()
 {
   ScarceQuarry();
   var nav=new NavigationGrid(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],1);
   var quarry=Filled(10,1);var foreign=Filled(11,2);var inventory=new EstateInventory();inventory.Register(50,1,StorageKind.Stockpile);inventory.Register(51,2,StorageKind.Stockpile);
   var network=new OxHaulingNetwork(nav,new[]{new OxHaulingSource(10,1,new MapPoint(12.5f,5.5f),quarry),new OxHaulingSource(11,2,new MapPoint(4.5f,5.5f),foreign)},
     new[]{new HaulingDestination(50,1,new MapPoint(20.5f,5.5f)),new HaulingDestination(51,2,new MapPoint(5.5f,5.5f))});network.ConfigureStorage(inventory);
   var settings=new OxHaulingSettings();var approach=new MapPoint(2.5f,5.5f);var tether=new MapPoint(3.5f,5.5f);
   var job=new OxHandlerJob(1,1,-100,approach,tether,0,0,settings);job.Configure(network);var worker=new WorkerComponent(1,new MovementComponent(new MapPoint(.5f,5.5f),0));
   Check(!job.OxVisible,"Unstaffed tether has an ox");nav.TryFindPath(worker.Movement.Position,approach,out var recruit);Check(worker.TryAssign(job,recruit) && job.OxVisible,"Assignment did not spawn the ox");
   worker.Advance(.25f,1);Check(MapPoint.Distance(job.Ox.Position,tether)<.00001f && worker.State==WorkerState.Traveling,"Ox left before the handler arrived");
   bool loaded=false,followed=false;for(int i=0;i<30000 && job.DeliveredStone<16;i++)
   {worker.Advance(.01f,1);loaded|=job.CargoStone>0;followed|=job.OxMoving;if(job.Source!=null)Check(job.Source.EstateId==1,"Foreign quarry selected");}
   Check(loaded && followed && job.DeliveredStone==16 && inventory.Amount(1,Goods.Stone)==16 && inventory.Amount(2,Goods.Stone)==0,"Hauling did not transfer one sixteen-stone load to same-estate storage");
   Check(quarry.StoredStone==32 && foreign.StoredStone==48,"Wrong quarry debit");
   for(int i=0;i<30000 && job.Source==null;i++)worker.Advance(.01f,1);
   Check(job.Source!=null && quarry.HaulerId==1 && !quarry.TryReserveHauler(2),"Quarry claim not exclusive");worker.Remove();
   Check(!job.OxVisible && job.CargoStone==0 && quarry.HaulerId==null && MapPoint.Distance(job.Ox.Position,tether)<.00001f,"Removal retained ox, cargo, or producer claim");
   var replacement=new WorkerComponent(2,new MovementComponent(approach,0));Check(replacement.TryAssign(job,new[]{approach}) && job.OxVisible && MapPoint.Distance(job.Ox.Position,tether)<.00001f,"Replacement ox not at tether");
   var clock=new SimulationClock{IsPaused=true};clock.Advance(100);var pos=job.Ox.Position;replacement.Advance(clock.DeltaTime,1);Check(MapPoint.Distance(pos,job.Ox.Position)==0 && job.CargoStone==0,"Paused ox moved");
   // No same-estate store means no producer claim, even when a foreign store is adjacent.
   var absent=new OxHaulingNetwork(nav,network.Sources,new[]{new HaulingDestination(51,2,new MapPoint(5.5f,5.5f))});absent.ConfigureStorage(inventory);
   var waiting=new OxHandlerJob(3,1,-200,approach,tether,0,0,settings);waiting.Configure(absent);var idle=new WorkerComponent(3,new MovementComponent(approach,0));idle.TryAssign(waiting,new[]{approach});idle.Advance(100,1);
   Check(waiting.Source==null && waiting.CargoStone==0,"Handler without same-estate storage took stone");
   // A store filling during collection cannot delete cargo or credit it early.
   for(int i=0;i<30000 && job.CargoStone==0;i++)replacement.Advance(.01f,1);
   Check(job.CargoStone>0,"Replacement never resumed collection");
   inventory.TryGetBuilding(50,out var store);Check(inventory.TryDeposit(50,1,Goods.Stone,(int)store.FreeCapacity(Goods.Stone)),"Could not fill fixture stockpile");
   replacement.Advance(100,1);Check(job.CargoStone==16 && job.DeliveredStone==16 && job.Phase==OxHaulingPhase.WaitingForStockpile,"Full store lost cargo or credited undelivered stone");
   inventory.TryWithdraw(50,Goods.Stone,1);replacement.Advance(100,1);Check(job.DeliveredStone==17 && job.CargoStone==15 && store.FreeCapacity(Goods.Stone)==0,"Single free stockpile slot did not accept one stone");
   inventory.TryWithdraw(50,Goods.Stone,15);replacement.Advance(100,1);Check(job.DeliveredStone==32 && store.FreeCapacity(Goods.Stone)==0,"Hauling did not resume when storage opened");replacement.Remove();
   // A navigation revision closing a route releases its exclusive producer claim.
   inventory.TryWithdraw(50,Goods.Stone,16);
   var blocked=new OxHandlerJob(4,1,-300,approach,tether,0,0,settings);blocked.Configure(network);var walker=new WorkerComponent(4,new MovementComponent(approach,0));walker.TryAssign(blocked,new[]{approach});walker.Advance(.01f,1);
   Check(blocked.Source!=null && quarry.HaulerId==4,"Blocked-route fixture did not claim quarry");
   nav.SetObstacle(999,new MapPoint(15,15),100,true);walker.Advance(.1f,1);Check(quarry.HaulerId==null && blocked.Phase==OxHaulingPhase.WaitingForQuarry,"Blocked route retained quarry claim");nav.SetObstacle(999,new MapPoint(15,15),100,false);
   File.WriteAllText(Output+"/core-pass.txt","PASS: staffing-gated spawn; waits for handler; follows ground route; exclusive quarry claim; sixteen stone collected and delivered one-for-one; same-estate quarry/stockpile; immediate removal/replacement; pause; no-store wait; full-store cargo retention/resume; blocked-route claim release; empty nearest quarry selection; waits across sixteen production cycles; exact one-for-one resource conservation; individual 0.15s deposits; live shared interval; paused/2x deposits; single free-slot acceptance; no partial departure; claim retained until full-load departure.");
 }
 static void Real()
 {
   var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");var pop=new PopulationSettings();var buildings=new S2MBuildingAssetLoader().Load(map,Install,Converter);var nav=S2MNavigationBuilder.Build(map,pop,buildings);
   var plans=S2MRecruitmentPlan.Create(map,nav,pop);var labor=S2MLaborPlan.Create(map,nav);var inventory=S2MStoragePlan.Create(map);labor.OxHauling.ConfigureStorage(inventory);
   var jobs=labor.Jobs.OfType<OxHandlerJob>().ToArray();Check(jobs.Length==3,"Chapter 8 tether slot count wrong");
   File.WriteAllLines(Output+"/map.txt",jobs.Select(j=>$"tether={j.BuildingId} estate={j.EstateId} approach={j.Approach.X},{j.Approach.Z} ox={j.TetherPosition.X},{j.TetherPosition.Z} heading={j.TetherHeading}"));
   Check(!labor.StartupActors.Any(a=>a.Type=="Oxen"),"Legacy unstaffed ox was left visible");
   var data=new S2MActorAssetLoader().Load(map,Install,S2MCampfirePopulationLoader.Overrides(),converterPath:Converter,placements:S2MCampfirePopulationLoader.CreateRecruitPlacements(plans,labor.Jobs));
   Check(data.Issues.Count==0,"Ox native assets: "+string.Join(";",data.Issues));
   foreach(var variant in new[]{S2MOxHaulingAssets.OxWalk,S2MOxHaulingAssets.OxIdle,S2MOxHaulingAssets.OxVariant(false,8),S2MOxHaulingAssets.HandlerWalk,S2MOxHaulingAssets.HandlerIdle})Check(data.Batches.Any(b=>b.Asset.Variant==variant),"Missing ox asset "+variant);
   foreach(var batch in data.Batches.Where(b=>b.Asset.Variant?.StartsWith("working-ox-")==true))
   {Check(batch.Attachments.Any(a=>a.model.SourcePath.EndsWith("oxen_stone_prop.GR2",StringComparison.OrdinalIgnoreCase) && a.boneName=="Bip01 Pelvis"),"Ox frame not bound to pelvis");}
   var root=new GameObject("Ox hauling validation");S2MBuildingRenderer.Render(buildings,root.transform);var clock=new SimulationClock();var controller=root.AddComponent<S2MPopulationController>();controller.Initialize(map,nav,plans,data,new S2MTerrainSettings(),new S2MActorRenderSettings(),pop,clock,unitScale:1/204.8f/.01f,jobs:labor.Jobs,laborBuildings:labor.Buildings);
   var renderer=root.GetComponentInChildren<S2MActorRenderer>();Check(jobs.All(j=>!Visible(renderer,j.EquipmentBaseId)),"Unassigned ox rendered at map load");bool captured=false;bool ready=false;
   for(int tick=0;tick<16000;tick++)
   {
     clock.Advance(.1f);controller.Advance(clock.DeltaTime);
     var tethered=jobs.FirstOrDefault(j=>j.WorkerId.HasValue && !j.IsOccupied);
     if(tethered!=null && !captured){ValidateFacing(renderer,tethered,data,map);Capture(root,controller,renderer,tethered,clock,"Tethered");captured=true;}
     if(jobs.Any(j=>j.CargoStone>0) && !ready){Capture(root,controller,renderer,jobs.First(j=>j.CargoStone>0),clock,"Loaded");ready=true;}
     if(jobs.Where(j=>labor.OxHauling.Destinations.Any(d=>d.EstateId==j.EstateId)).All(j=>j.DeliveredStone>=16) && jobs.All(j=>j.IsOccupied))break;
   }
   File.WriteAllLines(Output+"/deliveries.txt",jobs.Select(j=>$"{j.BuildingId}: estate={j.EstateId} worker={j.WorkerId} phase={j.Phase} cargo={j.CargoStone} delivered={j.DeliveredStone} pos={j.Ox.Position.X},{j.Ox.Position.Z}"));
   Check(jobs.All(j=>j.IsOccupied && j.Building.MaximumWorkers==1 && j.Building.TotalWorkers==1),"Tethers did not recruit one handler each");
   foreach(var j in jobs)
   {bool hasStore=labor.OxHauling.Destinations.Any(d=>d.EstateId==j.EstateId);Check(hasStore?j.DeliveredStone>=16:j.DeliveredStone==0 && j.CargoStone==0,"Chapter 8 hauling/same-estate failure at "+j.BuildingId);}
   var victim=jobs.First(j=>j.Source!=null || j.DeliveredStone>0);int old=victim.WorkerId.Value;controller.RemoveActor(old);Check(!victim.OxVisible && !Visible(renderer,victim.EquipmentBaseId),"Handler removal left the ox or frame rendered");
   for(int tick=0;tick<10000 && !victim.WorkerId.HasValue;tick++){clock.Advance(.1f);controller.Advance(clock.DeltaTime);}
   Check(victim.WorkerId.HasValue && victim.WorkerId!=old && MapPoint.Distance(victim.Ox.Position,victim.TetherPosition)<.001f,"Automatic replacement did not spawn at tether");
   File.WriteAllText(Output+"/pass.txt","PASS: chapter 8 three one-worker tethers; all native ox/handler/rack/stone models; unstaffed ox hidden; staffed ox visible with carrying frame; same-estate quarry hauling and stockpile inventory credit; worker removal hides ox immediately and replacement respawns at tether.");UnityEngine.Object.DestroyImmediate(root);
 }
 static void ValidateFacing(S2MActorRenderer renderer,OxHandlerJob job,S2MActorData data,Assets.Code.Stronghold2.S2MReader.Resources.S2MFile map)
 {
   var actors=(IDictionary)typeof(S2MActorRenderer).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
   var eq=new OxHandlerPresentation().GetEquipment(job,0);object batch=null;int index=0;
   foreach(var piece in (IEnumerable)actors[eq.ActorId])
   {var t=piece.GetType();var b=t.GetField("Item1").GetValue(piece);if((string)b.GetType().GetField("Variant").GetValue(b)!=eq.Variant)continue;batch=b;index=(int)t.GetField("Item2").GetValue(piece);break;}
   var matrix=((Matrix4x4[])batch.GetType().GetField("Local").GetValue(batch))[index];var motion=data.Batches.First(b=>b.Asset.Variant==eq.Variant).Motion;

   var placed=map.Actors.FirstOrDefault(a=>a.Type=="Oxen" && MapPoint.Distance(new MapPoint(a.RawX/1024,a.RawZ/1024),job.TetherPosition)<.001f);
   var building=map.BuildingPlacements.First(b=>b.Id==job.BuildingId);float expected=placed?.HeadingDegrees??building.RotationDegrees;
   Check(Math.Abs(Mathf.DeltaAngle(eq.Heading,expected))<.001f,"Ox spawn heading differs from its authored placement");
   var asset=data.Batches.First(b=>b.Asset.Variant==eq.Variant).Asset;
   Check(Math.Abs(Mathf.DeltaAngle(asset.LocalEulerAngles.y,180))<.001f,"Ox lost its 180-degree model correction");
   File.WriteAllText(Output+"/facing.txt",$"PASS: authored heading={expected}; spawn heading={eq.Heading}; model correction=180 degrees.");
 }
 static bool Visible(S2MActorRenderer renderer,int id)
 {
   var actors=(IDictionary)typeof(S2MActorRenderer).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
   foreach(var piece in (IEnumerable)actors[id]){var t=piece.GetType();var batch=t.GetField("Item1").GetValue(piece);int i=(int)t.GetField("Item2").GetValue(piece);if(((bool[])batch.GetType().GetField("Visible").GetValue(batch))[i])return true;}return false;
 }
 static void Capture(GameObject root,S2MPopulationController controller,S2MActorRenderer renderer,OxHandlerJob job,SimulationClock clock,string name)
 {
   var view=new OxHandlerPresentation();for(int i=0;i<1;i++){var eq=view.GetEquipment(job,i);if(!eq.Visible)continue;renderer.SetVariant(eq.ActorId,eq.Variant,eq.AnimationTime,eq.AnimationLoops,eq.AnimationRate);}
   foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
   var camera=new GameObject().AddComponent<Camera>();camera.cullingMask=1<<30;camera.transform.position=new Vector3(job.Ox.Position.X+3,job.RawHeight/1024+3,job.Ox.Position.Z-4);camera.transform.LookAt(new Vector3(job.Ox.Position.X,job.RawHeight/1024+.5f,job.Ox.Position.Z));camera.fieldOfView=32;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
   var sun=new GameObject().AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.ambientLight=Color.gray;
   var target=new RenderTexture(1000,800,24);camera.targetTexture=target;var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
   typeof(WoodcuttingValidation).GetMethod("SubmitPreview",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{renderer,camera,clock.DeltaTime});camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=null;
   foreach(string field in new[]{"shown","shownWork","shownEquipment"})((IDictionary)typeof(S2MPopulationController).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller)).Clear();
   UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
 }
}
