using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections;using System.Collections.Generic;
using Assets.Code.Simulation;using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Movement;using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.MapLoading;using Assets.Code.Stronghold2.ModelRendering;using Assets.Code.Stronghold2.ModelRendering.Labor;using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;using UnityEngine;
public static class QuarryValidation
{
 const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/QuarryValidation",Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){Directory.CreateDirectory(Output);File.Delete(Output+"/failure.txt");File.Delete(Output+"/pass.txt");try{Core();Real();EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
 static void Core()
 {
   var settings=new QuarrySettings();var production=new QuarryProduction(settings);var jobs=new QuarryJob[3];var workers=new WorkerComponent[3];
   for(int i=0;i<3;i++){var p=new MapPoint(1.5f+i,.5f);jobs[i]=new QuarryJob(1,1,i,-100,p,p,0,0,new MapPoint(0,0),production);workers[i]=new WorkerComponent(i+1,new MovementComponent(p,0));}
   var building=new LaborBuilding(1,1,jobs);var zero=new LaborBuilding(2,1,Array.Empty<LaborJob>());
   Check(zero.MaximumWorkers==0 && zero.IsFullyStaffed && zero.IsCrewReady,"Zero-worker staffing failed");
   Check(!new LaborBuilding(3,1,Array.Empty<LaborJob>(),false).IsFullyStaffed,"Unimplemented building claimed known zero staffing");
   Check(building.TotalWorkers==0 && building.MaximumWorkers==3,"Initial staffing counts incorrect");
   void Tick(float seconds){foreach(var worker in workers)worker.Advance(seconds,1);jobs[0].CompleteWorkStep();}
   for(int i=0;i<2;i++)Check(workers[i].TryAssign(jobs[i],new[]{jobs[i].Approach}),"Worker reservation failed");
   Tick(100);Check(production.StoredStone==0 && building.TotalWorkers==2 && !building.IsFullyStaffed,"Understaffed quarry produced");
   workers[2].Movement.Place(new MapPoint(.5f,.5f),0);Check(workers[2].TryAssign(jobs[2],new[]{new MapPoint(.5f,.5f),jobs[2].Approach}),"Third assignment failed");
   Check(building.IsFullyStaffed && !building.IsCrewReady && building.ArrivedWorkers==2,"Assigned/arrived counts confused");
   Tick(3);Check(building.IsCrewReady && production.StoredStone==0,"Travel time was counted as quarry work");
   var view=new QuarryPresentation();double cycle=production.CycleSeconds;
   Check(Math.Abs(production.BenchArrivalSeconds-5.675)<.00001 && Math.Abs(production.FirstBlockSeconds-15)<.00001 && Math.Abs(cycle-9.325)<.00001,"Reforged timing defaults changed");
   Tick((float)production.BenchArrivalSeconds-.01f);
   Check(production.BenchRockStage==-1 && production.WorkingRockVisible,"Bench appeared before incoming rock arrived");
   Check(view.AnimationRate(workers[0],1)==0 && view.AnimationTime(workers[0],0)==0 && workers.Skip(1).All(w=>view.AnimationRate(w,1)>0),"Chiseler started before the first rock was loaded");
   Check(view.GetEquipment(jobs[0],49).AnimationTime>6.31f,"Transport rock will not reach its loading endpoint before chiseling");
   Tick(.011f);Check(production.IsChiseling && view.AnimationRate(workers[0],1)>0 && production.BenchRockStage==0 && !production.WorkingRockVisible,"Incoming rock did not hand off to native raw bench pose");
   for(int stage=0;stage<5;stage++)
   {
     Check(production.BenchRockStage==stage,"Missing intermediate rock pose "+stage);
     Check(Enumerable.Range(50,5).Count(i=>view.GetEquipment(jobs[0],i).Visible)==1,"Bench showed overlapping stone stages");
     Check(workers.All(w=>view.AnimationRate(w,1)>0),"Chiseling paused transport workers");
     if(stage<4)Tick((float)(cycle*.2));
   }
   Check(production.StoredStone==0,"Stone credited before final chiseling stage");
   Tick((float)(cycle*.2));Check(production.StoredStone==1 && production.BenchRockStage==0,"Output/handoff did not coincide with the next incoming stone");
   Tick((float)cycle);Check(production.StoredStone==2,"Continuous repeat cycle timing wrong");
   // Default production now fills 3x3 layers, center first, spiraling around it.
   var layout=settings.StoneLayout;var cells=new HashSet<(int,int)>();
   for(int i=0;i<9;i++){layout.CellAt(i,out int x,out int z);Check(Math.Abs(x)<=1 && Math.Abs(z)<=1 && cells.Add((x,z)),"Center-out grid duplicates or exceeds its layer");if(i==0)Check(x==0 && z==0,"First block not centered");}
   var first=view.GetEquipment(jobs[0],0);var upper=view.GetEquipment(jobs[0],9);
   Check(MapPoint.Distance(first.Position,upper.Position)<.00001f && Math.Abs((upper.RawHeight-first.RawHeight)/1024-layout.LayerHeight)<.00001f,"New layer did not restart at the center");
   var clock=new SimulationClock{IsPaused=true};double elapsed=production.WorkSeconds;clock.Advance(9);Tick(clock.DeltaTime);Check(production.WorkSeconds==elapsed,"Pause advanced production");
   clock.IsPaused=false;clock.Speed=2;clock.Advance((float)cycle/2);Tick(clock.DeltaTime);Check(production.StoredStone==3,"Game speed not applied once");
   Tick(10000);Check(production.StoredStone==48 && production.IsFull && !production.IsWorking && !production.WorkingRockVisible,"Capacity failed to stop quarry or left a duplicate working rock");
   Check(production.TryTakeStone(2) && !production.TryTakeStone(100),"Stone withdrawal bounds failed");Tick((float)production.FirstBlockSeconds);Check(production.StoredStone==47,"Quarry failed to restart after space opened");Tick((float)cycle);Check(production.StoredStone==48,"Quarry did not refill capacity");
   workers[0].Remove();Check(building.TotalWorkers==2 && building.ArrivedWorkers==2,"Removal did not vacate slot");production.TryTakeStone(1);Tick(100);Check(production.StoredStone==47,"Missing crew did not stop work");
   workers[0]=new WorkerComponent(10,new MovementComponent(jobs[0].Approach,0));Check(workers[0].TryAssign(jobs[0],new[]{jobs[0].Approach}),"Vacancy could not be refilled");Tick((float)production.FirstBlockSeconds);Check(production.StoredStone==48 && building.IsFullyStaffed,"Replacement did not restore production");
   var custom=new QuarryProduction(new QuarrySettings{MaximumStone=1});var customJobs=new QuarryJob[QuarryProduction.RequiredWorkers];var customWorkers=new WorkerComponent[QuarryProduction.RequiredWorkers];
   for(int i=0;i<customJobs.Length;i++)
   {var point=new MapPoint(i,0);customJobs[i]=new QuarryJob(5,1,i,-500,point,point,0,0,point,custom);customWorkers[i]=new WorkerComponent(100+i,new MovementComponent(point,0));customWorkers[i].TryAssign(customJobs[i],new[]{point});}
   foreach(var w in customWorkers)w.Advance(20,1);customJobs[0].CompleteWorkStep();Check(custom.StoredStone==1 && custom.IsFull,"Configured capacity not honored with the required three workers");
   File.WriteAllText(Output+"/core-pass.txt","PASS: three independent vacancies; assigned/arrived counts; zero-worker buildings; under-staffing; Reforged 5.675s loading delay, 15s first output, 9.325s repeats; no early hammer playback; continuous overlapping transport/chiseling; native five-stage bench handoff; center-out 3x3 layers; no travel-time or triple credit; pause/2x; 48-block stop; withdrawal/restart; removal/refill.");
 }
 static void Real()
 {
   var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");var pop=new PopulationSettings();var settings=new QuarrySettings();
   var buildings=new S2MBuildingAssetLoader().Load(map,Install,Converter);var nav=S2MNavigationBuilder.Build(map,pop,buildings);
   var plans=S2MRecruitmentPlan.Create(map,nav,pop);var plan=S2MLaborPlan.Create(map,nav,settings);var quarryJobs=plan.Jobs.OfType<QuarryJob>().ToArray();
   Check(quarryJobs.Length==9 && quarryJobs.Select(j=>j.Production).Distinct().Count()==3,"Chapter 8 quarry slot count wrong");
   Check(plan.Buildings.Count>=map.BuildingPlacements.Count && plan.Buildings.Where(b=>b.Jobs.Any(j=>j is QuarryJob)).All(b=>b.MaximumWorkers==3),"Building workforce missing");
   Check(plan.Buildings.Where(b=>map.BuildingPlacements.Any(p=>p.Id==b.BuildingId && p.Type=="StockPile")).All(b=>b.MaximumWorkers==0 && b.IsFullyStaffed),"Passive stockpiles were not recognized as fully staffed at zero workers");
   var data=new S2MActorAssetLoader().Load(map,Install,S2MCampfirePopulationLoader.Overrides(),converterPath:Converter,placements:S2MCampfirePopulationLoader.CreateRecruitPlacements(plans,plan.Jobs));
   Check(data.Issues.Count==0,"Quarry assets: "+string.Join(";",data.Issues));
   foreach(string name in new[]{S2MQuarryAssets.Hammer,S2MQuarryAssets.Lift,S2MQuarryAssets.Hook,S2MQuarryAssets.Rig,S2MQuarryAssets.Rock,S2MQuarryAssets.Stone,S2MQuarryAssets.RockPose(0),S2MQuarryAssets.RockPose(1),S2MQuarryAssets.RockPose(2),S2MQuarryAssets.RockPose(3),S2MQuarryAssets.RockPose(4)})Check(data.Batches.Any(b=>b.Asset.Variant==name),"Missing native quarry variant "+name);
   File.WriteAllLines(Output+"/motions.txt",data.Batches.Where(b=>b.Asset.Variant?.StartsWith("quarry-")==true).Select(b=>$"{b.Asset.Variant}: {b.Motion.Duration}; offsets={b.Asset.LocalOffset}, yaw={b.Asset.LocalEulerAngles}; attachments={b.Attachments.Count}"));
   File.WriteAllLines(Output+"/hips.txt",data.Batches.Where(b=>new[]{S2MQuarryAssets.Hammer,S2MQuarryAssets.Lift,S2MQuarryAssets.Hook}.Contains(b.Asset.Variant)).Select(b=>{
     int bone=b.Motion.BoneNames.FindIndex(n=>n=="Bip01 Pelvis");int off=(b.Motion.BoneNames.Count+bone)*16;var a=b.Motion.Matrices;return $"{b.Asset.Variant}: hip in building cells {a[off+12]/204.8f},{a[off+14]/204.8f},{a[off+13]/204.8f}";}));
   foreach(var q in quarryJobs)Check(MapPoint.Distance(q.WorkPosition,q.BuildingPosition)<.001f,"Quarry native work pose was placed at the walking approach instead of the building origin");
   var stone=data.Batches.First(b=>b.Asset.Variant==S2MQuarryAssets.Stone);var bounds=Granny2ModelRenderer.GetLocalBounds(stone.Model,1/204.8f);
   var layoutView=new QuarryPresentation();var primary=quarryJobs.First(j=>j.SlotIndex==0);
   for(int i=0;i<48;i++)
   {
     var equipment=layoutView.GetEquipment(primary,i);
     var offset=Quaternion.Inverse(Quaternion.Euler(0,primary.BuildingHeading,0))*new Vector3(equipment.Position.X-primary.BuildingPosition.X,0,equipment.Position.Z-primary.BuildingPosition.Z);
     var min=offset+bounds.min*stone.Asset.Scale;var max=offset+bounds.max*stone.Asset.Scale;
     Check(min.x>=1.20f && max.x<=2.736f && min.z>=-.004f && max.z<=.922f,"Stored stone extends outside the platform or into the chiseling station");
     if(i<primary.Production.Settings.StoneLayout.GridColumns*primary.Production.Settings.StoneLayout.GridRows)
       Check(Math.Abs((equipment.RawHeight-primary.RawHeight)/1024+bounds.min.y*stone.Asset.Scale-.293f)<.01f,"Stored stone is not resting on the platform");
   }

   var root=new GameObject("Quarry map validation");S2MBuildingRenderer.Render(buildings,root.transform);
   var clock=new SimulationClock();var controller=root.AddComponent<S2MPopulationController>();controller.Initialize(map,nav,plans,data,new S2MTerrainSettings(),new S2MActorRenderSettings(),pop,clock,jobs:plan.Jobs,laborBuildings:plan.Buildings);
   var renderer=root.GetComponentInChildren<S2MActorRenderer>();ValidateStoneRotation(renderer,quarryJobs.First(j=>j.SlotIndex==0));var production=quarryJobs.Select(j=>j.Production).Distinct().ToArray();bool captured=false,paused=false,hammerCaptured=false,hammerEndCaptured=false;var stagesCaptured=new HashSet<int>();
   for(int tick=0;tick<10000 && !production.All(p=>p.IsFull);tick++)
   {
     clock.Advance(.25f);controller.Advance(clock.DeltaTime);
     var working=quarryJobs.FirstOrDefault(j=>j.SlotIndex==0 && j.Production.IsWorking);
     if(working!=null && working.Production.BenchRockStage>=0 && stagesCaptured.Add(working.Production.BenchRockStage))Capture(map,root,controller,renderer,working,clock,"Pose"+working.Production.BenchRockStage);
     if(working!=null && working.Production.BenchRockStage>=0 && !hammerCaptured){Capture(map,root,controller,renderer,working,clock,"ChiselingStart");hammerCaptured=true;}
     if(working!=null && working.Production.HammerAnimationTime>8.8f && !hammerEndCaptured){Capture(map,root,controller,renderer,working,clock,"ChiselingEnd");hammerEndCaptured=true;}
     if(working!=null && !captured){Capture(map,root,controller,renderer,working,clock,"Working");captured=true;}
     if(working!=null && !paused){var time=working.Production.WorkSeconds;clock.IsPaused=true;clock.Advance(20);controller.Advance(clock.DeltaTime);Check(working.Production.WorkSeconds==time,"Paused live quarry advanced");clock.IsPaused=false;paused=true;}
   }
   Check(production.All(p=>p.StoredStone==48),"Some quarries did not staff and fill");
   Check(quarryJobs.Select(j=>j.WorkerId).Distinct().Count()==9 && quarryJobs.All(j=>j.IsOccupied),"Peasants were reused or did not arrive");
   Check(plan.Buildings.Where(b=>b.Jobs.Any(j=>j is QuarryJob)).All(b=>b.TotalWorkers==3 && b.ArrivedWorkers==3 && b.IsFullyStaffed),"Live workforce counters wrong");
   var target=quarryJobs.First(j=>j.SlotIndex==0);Capture(map,root,controller,renderer,target,clock,"Full");int removed=target.WorkerId.Value;Check(controller.RemoveActor(removed),"Cannot remove mason");
   Check(target.Building.TotalWorkers==2 && target.Production.StoredStone==48,"Removal deleted local stock or retained employment");Check(Visible(renderer,Enumerable.Range(target.EquipmentBaseId,48))==48,"Removing a mason hid the building-owned output");target.Production.TryTakeStone(1);
   for(int i=0;i<2000 && target.Production.StoredStone<48;i++){clock.Advance(.25f);controller.Advance(clock.DeltaTime);}
   Check(target.WorkerId.HasValue && target.WorkerId.Value!=removed && target.Building.ArrivedWorkers==3 && target.Production.StoredStone==48,"Recruitment did not replace quarry vacancy");
   var inventory=S2MStoragePlan.Create(map);Check(inventory.Amount(target.EstateId,Assets.Code.Simulation.Economy.Goods.Stone)==0,"Quarry output was prematurely credited to stockpile");
   File.WriteAllText(Output+"/pass.txt","PASS: 3 chapter-8 quarries, 9 distinct recruited/pathfinding masons; all native work clips, tools, animated mechanism and local stone; all crews fill to 48 and stop; pause, workforce queries, removal and automatic replacement/refill; stockpile inventory untouched.");UnityEngine.Object.DestroyImmediate(root);
 }
 static void ValidateStoneRotation(S2MActorRenderer renderer,QuarryJob job)
 {
   var view=new QuarryPresentation();var eq=view.GetEquipment(job,0);
   var actors=(IDictionary)typeof(S2MActorRenderer).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
   object piece=null;foreach(var entry in (IEnumerable)actors[eq.ActorId]){piece=entry;break;}
   var type=piece.GetType();var batch=type.GetField("Item1").GetValue(piece);int index=(int)type.GetField("Item2").GetValue(piece);
   var matrices=(Matrix4x4[])batch.GetType().GetField("Local").GetValue(batch);var meshes=(Mesh[])batch.GetType().GetField("Meshes").GetValue(batch);var mesh=meshes[0];
   Vector3 position=new(eq.Position.X,eq.RawHeight/1024,eq.Position.Z);var rotation=Quaternion.Euler(0,eq.Heading,0);var scale=Vector3.one*(1/204.8f/.01f);
   renderer.SetTransform(eq.ActorId,position,rotation,scale,0);var before=matrices[index].MultiplyPoint3x4(mesh.bounds.center);
   renderer.SetTransform(eq.ActorId,position,rotation,scale,eq.MotionDegrees);var after=matrices[index].MultiplyPoint3x4(mesh.bounds.center);
   Check(Vector3.Distance(before,after)<.00001f,"Stone rotation changed its grid center");
   var points=mesh.vertices.Select(v=>matrices[index].MultiplyPoint3x4(v)).Select(v=>new Vector2(v.x,v.z)).Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToList();
   float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
   var hull=new List<Vector2>();foreach(var point in points){while(hull.Count>=2 && Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=.000001f)hull.RemoveAt(hull.Count-1);hull.Add(point);}int lower=hull.Count;
   for(int i=points.Count-2;i>=0;i--){var point=points[i];while(hull.Count>lower && Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=.000001f)hull.RemoveAt(hull.Count-1);hull.Add(point);}hull.RemoveAt(hull.Count-1);
   float longest=0;for(int i=0;i<hull.Count;i++)longest=Math.Max(longest,(hull[(i+1)%hull.Count]-hull[i]).magnitude);
   Check(longest<job.Production.Settings.StoneLayout.BlockSpacing,"Finished stone overlaps adjacent grid cells or lost its asset scale");
   int checkedEdges=0;for(int i=0;i<hull.Count;i++)
   {var edge=hull[(i+1)%hull.Count]-hull[i];if(edge.magnitude<longest*.5f)continue;checkedEdges++;Check(Math.Min(Math.Abs(edge.x),Math.Abs(edge.y))<edge.magnitude*.03f,"Finished stone edges are not parallel to the platform");}
   Check(checkedEdges>=4,"Stone orientation check did not find its four main edges");
   File.WriteAllText(Output+"/stone-rotation.txt",$"PASS: {checkedEdges} actual rendered stone edges parallel to platform; center unchanged; yaw={eq.MotionDegrees} degrees.");
 }
 static int Visible(S2MActorRenderer renderer,IEnumerable<int> ids)
 {
   var actors=(IDictionary)typeof(S2MActorRenderer).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);int count=0;
   foreach(int id in ids)foreach(var piece in (IEnumerable)actors[id])
   {var type=piece.GetType();var batch=type.GetField("Item1").GetValue(piece);int index=(int)type.GetField("Item2").GetValue(piece);if(((bool[])batch.GetType().GetField("Visible").GetValue(batch))[index])count++;}return count;
 }
 static void Capture(Assets.Code.Stronghold2.S2MReader.Resources.S2MFile map,GameObject root,S2MPopulationController controller,S2MActorRenderer renderer,QuarryJob job,SimulationClock clock,string name)
 {
   var presentation=new QuarryPresentation();var ids=job.Production.Jobs.Select(j=>j.WorkerId).ToHashSet();
   foreach(var p in controller.Kingdoms.SelectMany(k=>k.Peasants))
   {
     if(!ids.Contains(p.Id)){renderer.HideActor(p.Id);continue;}
     float rate=presentation.AnimationRate(p.Worker,.85f);renderer.SetVariant(p.Id,presentation.Variant(p.Worker),presentation.AnimationTime(p.Worker,rate),presentation.AnimationLoops(p.Worker),rate);
   }
   foreach(var other in controller.Labor.Jobs)
   {
     var view=new LaborPresentationCatalog().Get(other.Role);
     for(int i=0;i<view.EquipmentCountFor(other);i++)
     {
       var eq=view.GetEquipment(other,i);if(other.BuildingId!=job.BuildingId || !eq.Visible){renderer.HideActor(eq.ActorId);continue;}
       renderer.SetVariant(eq.ActorId,eq.Variant,eq.AnimationTime,eq.AnimationLoops,eq.AnimationRate);
     }
   }
   foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
   var camera=new GameObject().AddComponent<Camera>();camera.cullingMask=1<<30;camera.transform.position=new Vector3(job.BuildingPosition.X+5,job.RawHeight/1024+4,job.BuildingPosition.Z-6);camera.transform.LookAt(new Vector3(job.BuildingPosition.X+1.5f,job.RawHeight/1024+1,job.BuildingPosition.Z+1));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.18f,.2f);
   var sun=new GameObject().AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.ambientLight=Color.gray;
   var target=new RenderTexture(1000,800,24);camera.targetTexture=target;var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
   typeof(WoodcuttingValidation).GetMethod("SubmitPreview",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{renderer,camera,clock.DeltaTime});camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=null;
   foreach(string field in new[]{"shown","shownWork","shownEquipment"})((IDictionary)typeof(S2MPopulationController).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller)).Clear();
   UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
 }
}


