using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Assets.Code.Simulation;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Movement;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Construction;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.ModelRendering.Labor;
using Assets.Code.Stronghold2.S2MReader.Resources;

public static class AppleFarmValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/AppleValidation",Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  static readonly List<string> report=new();
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static void Set(object target,string property,object value)=>target.GetType().GetProperty(property).SetValue(target,value);
  public static void Run()
  {
    Directory.CreateDirectory(Output);try{Core(false);Core(true);Growth();Native();report.Add("PASS: apple farmer/orchard validation.");File.WriteAllLines(Output+"/result.txt",report);EditorApplication.Exit(0);}
    catch(Exception error){report.Add(error.ToString());File.WriteAllLines(Output+"/result.txt",report);Debug.LogException(error);EditorApplication.Exit(1);}
  }
  static NavigationGrid Grid(bool queued=false)
  {var nav=new NavigationGrid(64,64,1,Enumerable.Repeat(true,4096).ToArray(),new float[4096],1);if(queued)nav.EnablePathRequests(new PathfindingSettings{WorkUnitsPerFrame=8,MillisecondsPerFrame=100});return nav;}
  sealed class Access : IStorageAccessProvider
  {public IReadOnlyList<MapPoint> AccessPoints(int id)=>new[]{id==11?new MapPoint(22.5f,10.5f):new MapPoint(8.5f,10.5f)};}
  static OrchardTree Tree(int id,int farm,int estate,float x,float z,int stage)
  {var p=new MapPoint(x,z);return new OrchardTree(id,farm,estate,p,new MapPoint(x+.5f,z-.5f),p,0,stage);}
  static AppleFarmerJob Job(AppleOrchards orchard,int farm,int id,MapPoint point,out WorkerComponent worker)
  {var job=new AppleFarmerJob(farm,1,point,point,0,0,orchard.Navigation,orchard.Settings);job.ConfigureOrchards(orchard);job.SetDurations(.2f,.2f);worker=new WorkerComponent(id,new MovementComponent(point,0));Check(worker.TryAssign(job,new[]{point}),"Staffing failed");return job;}
  static void Core(bool queued)
  {
    var nav=Grid(queued);var settings=new AppleFarmSettings{GrowthStageSeconds=1000,PlantedSeconds=1000,WalkSpeedCellsPerSecond=4,TreeSearchWorkUnitsPerFrame=2};var orchard=new AppleOrchards(nav,settings);
    orchard.Add(Tree(1,1,1,8.5f,10.5f,4));orchard.Add(Tree(2,1,1,9.5f,10.5f,4));orchard.Add(Tree(3,1,1,10.5f,10.5f,1));
    orchard.Add(Tree(4,2,1,20.5f,10.5f,4));orchard.Add(Tree(5,2,1,21.5f,10.5f,4));orchard.Add(Tree(6,2,1,19.5f,10.5f,1));
    orchard.Add(Tree(7,3,2,22.5f,10.5f,4));
    var job=Job(orchard,1,100,new MapPoint(8.5f,9.5f),out var worker);var inv=new EstateInventory();inv.Register(22,2,StorageKind.Granary);inv.Register(33,1,StorageKind.Stockpile);
    job.ConfigureStorage(inv,new StorageLocator(inv,nav,new Access(),StorageKind.Granary));
    void Tick(float dt){nav.PathRequests?.AdvanceFrame();orchard.Advance(dt);worker.Advance(dt,1);}
    for(int i=0;i<4000 && job.Phase!=AppleFarmerPhase.WaitingForGranary;i++)Tick(.05f);
    Check(job.Cargo==5 && job.PickedTrees==2,"Two trees did not yield five apples");
    for(int i=0;i<100;i++)Tick(.05f);
    Check(job.Cargo==5 && job.DeliveredApples==0 && inv.Amount(2,Goods.Apples)==0,"Cargo lost or delivered to foreign estate/stockpile");
    inv.Register(11,1,StorageKind.Granary);long last=0;int commits=0;
    for(int i=0;i<4000 && job.DeliveredApples<5;i++)
    {Tick(.025f);long amount=inv.Amount(1,Goods.Apples);if(amount!=last){Check(amount==last+1,"Food delivery was not incremental");commits++;last=amount;}}
    Check(job.DeliveredApples==5 && commits==5 && inv.Amount(1,Goods.Apples)==5,"Five real apples were not delivered");
    for(int i=0;i<200 && job.Target==null;i++)Tick(.025f);
    Check(job.Target?.FarmId==2,"Farmer returned to home farm instead of nearest tree after delivery");
    var target=job.Target;worker.Remove();Check(target.PickerId==null && target.Harvestable && job.Cargo==0,"Removal did not release pending tree");
    var job2=Job(orchard,1,101,new MapPoint(22.5f,10.5f),out var w2);var job3=Job(orchard,2,102,new MapPoint(22.5f,10.5f),out var w3);
    for(int i=0;i<100 && (job2.Target==null || job3.Target==null);i++){nav.PathRequests?.AdvanceFrame();w2.Advance(.001f,1);w3.Advance(.001f,1);}
    Check(job2.Target!=null && job3.Target!=null && job2.Target!=job3.Target && job2.Target.EstateId==1 && job3.Target.EstateId==1,"Farmers double-claimed a tree or crossed estate");w2.Remove();w3.Remove();
    report.Add($"Core queued={queued}: fixed one worker; two trees/five apples; same-estate granary registry; five one-item deposits; cross-farm nearest after delivery; exclusive claims and death release; bounded search.");
  }
  static void Growth()
  {
    var nav=Grid();var settings=new AppleFarmSettings();var orchard=new AppleOrchards(nav,settings);var tree=Tree(1,1,1,10.5f,10.5f,-1);orchard.Add(tree);
    Check(!tree.Harvestable && !orchard.TryReserve(tree,1,1),"Unplanted tree harvestable");Check(orchard.TryReserve(tree,1,1,true) && orchard.Plant(tree,1),"Planting failed");
    orchard.Advance(4.99f);Check(tree.Stage==0,"Sapling grew early");orchard.Advance(.02f);Check(tree.Stage==1,"Sapling did not become bare");
    orchard.Advance(12.5f);Check(tree.Stage==2,"Blossom stage missing");orchard.Advance(12.5f);Check(tree.Stage==3,"Leaf stage missing");orchard.Advance(12.5f);Check(tree.Stage==4,"Ripe stage missing");
    Check(orchard.TryReserve(tree,1,1) && !orchard.TryReserve(tree,2,1) && orchard.Harvest(tree,1),"Harvest reservation failed");
    orchard.Advance(.03f);Check(tree.Stage==5 && !tree.Harvestable,"Picked state not retained during rest");orchard.Advance(12.5f);Check(tree.Stage==1,"Picked tree never regrew");
    var idleOrchard=new AppleOrchards(nav,settings);var idleTree=Tree(2,2,1,20.5f,20.5f,1);idleOrchard.Add(idleTree);
    var home=new MapPoint(5.5f,5.5f);var job=Job(idleOrchard,2,100,home,out var worker);worker.Movement.Place(new MapPoint(10.5f,5.5f),0);
    for(int i=0;i<200;i++)worker.Advance(.1f,1);Check(MapPoint.Distance(worker.Movement.Position,home)<.2f && job.PickedTrees==0,"No-ripe farmer did not idle near farm centre");
    var clock=new SimulationClock{IsPaused=true};int before=idleTree.Stage;clock.Advance(100);idleOrchard.Advance(clock.DeltaTime);Check(idleTree.Stage==before,"Paused trees grew");
    clock.IsPaused=false;clock.Speed=2;clock.Advance(6.25f);idleOrchard.Advance(clock.DeltaTime);Check(idleTree.Stage==2,"2x clock not applied to growth");worker.Remove();
    // Tree clocks are shared, not multiplied by the number of jobs or staff.
    var shared=new AppleOrchards(nav);var unstaffed=Tree(3,3,1,5.5f,5.5f,0);shared.Add(unstaffed);
    var a=new AppleFarmerJob(3,1,home,home,0,0,nav);a.ConfigureOrchards(shared);var b=new AppleFarmerJob(4,1,home,home,0,0,nav);b.ConfigureOrchards(shared);
    using(var manager=new LaborManager(new LaborJob[]{a,b},Array.Empty<PeasantRecruitment>(),nav,new PopulationSettings())){manager.Advance(4);Check(unstaffed.Stage==0,"Shared orchard clock advanced twice");manager.Advance(1.01f);Check(unstaffed.Stage==1,"Unstaffed planted tree stopped growing");}
    var custom=new AppleOrchards(nav,new AppleFarmSettings{RestSeconds=2,SeasonCooldownSeconds=.1f});var customTree=Tree(4,4,1,6.5f,6.5f,4);custom.Add(customTree);custom.TryReserve(customTree,1,1);custom.Harvest(customTree,1);custom.Advance(2.2f);Check(customTree.Stage==1,"Short custom cooldown perpetually restarted tree rest");
    report.Add("Growth: planted/bare/blossom/leaf/ripe/picked-rest/regrow; planting requirement; idle at home; pause/2x; exactly one shared orchard clock, including unstaffed planted farms.");
  }
  static void Native()
  {
    Debug.unityLogger.logEnabled=false;int farms=0,trees=0;
    for(int chapter=1;chapter<=12;chapter++)
    {
      var map=S2MFileLoader.Load($"{Install}/maps/war_chapter{chapter}.s2m");var nav=S2MNavigationBuilder.Build(map,new PopulationSettings());var plan=S2MLaborPlan.Create(map,nav);
      var jobs=plan.Jobs.OfType<AppleFarmerJob>().ToArray();farms+=jobs.Length;trees+=plan.AppleOrchards.Trees.Count;
      Check(plan.AppleOrchards.Trees.Count==jobs.Length*8,"Native farm tree ownership mismatch in chapter "+chapter);
      Check(jobs.All(j=>j.Building.MaximumWorkers==1 && plan.AppleOrchards.FarmTrees(j.BuildingId).Count==8),"Native staffing mismatch");
      report.Add($"Chapter {chapter}: {jobs.Length} farms, {plan.AppleOrchards.Trees.Count} owned trees; {map.PlantActors.Count(p=>p.Kind==SceneryKind.AppleTree)-plan.AppleOrchards.Trees.Count} orphan records excluded.");
    }
    Debug.unityLogger.logEnabled=true;
    var map8=S2MFileLoader.Load(Install+"/maps/war_chapter8.s2m");var nav8=S2MNavigationBuilder.Build(map8,new PopulationSettings());var plan8=S2MLaborPlan.Create(map8,nav8);
    var job=plan8.Jobs.OfType<AppleFarmerJob>().First();var view=new AppleFarmerPresentation();
    var placements=S2MAppleFarmAssets.Overrides().Select(o=>{var p=new ActorPlacement();Set(p,"Id",100);Set(p,"Type",o.ObjectType);Set(p,"OnMap",true);return p;}).ToArray();
    var data=new S2MActorAssetLoader().Load(map8,Install,S2MAppleFarmAssets.Overrides(),converterPath:Converter,cacheDirectory:Output+"/Actors",placements:placements);
    Check(data.Issues.Count==0,"Native apple assets failed: "+string.Join(";",data.Issues));view.ConfigureJob(job,data);
    Check(data.Batches.Where(b=>b.Asset.Variant==S2MAppleFarmAssets.Pick || b.Asset.Variant==S2MAppleFarmAssets.Carry || b.Asset.Variant==S2MAppleFarmAssets.CarryIdle).All(b=>b.Attachments.Any(a=>a.model.SourcePath.Contains("goods_apple_basket"))),"Apple basket attachment missing");
    foreach(var batch in data.Batches)report.Add($"{batch.Asset.Variant}: {batch.Asset.AnimationPath}; duration={batch.Motion.Duration}; attachments={batch.Attachments.Count}");
    NativeFlow(map8,nav8,plan8,view,data);
    var vegetation=new S2MVegetationAssetLoader().Load(map8,Install,Converter,Output+"/Models");Check(vegetation.AppleTreeModels.Count==5,"Missing native tree stages");
    var settings=new AppleFarmSettings();var orchard=new AppleOrchards(nav8,settings);int id=0;
    for(int stage=-1;stage<=5;stage++)orchard.Add(Tree(++id,1,1,100+(stage+1)*3,100,stage));
    var root=new GameObject("Apple stage validation");var stageView=root.AddComponent<S2MAppleOrchardView>();stageView.Initialize(orchard,vegetation,new S2MVegetationRenderSettings(),.5f,new S2MSceneryModelScales());
    int bark=0,leaves=0;
    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))foreach(var material in renderer.sharedMaterials)
    {
      var texture=material.GetTexture("_BaseMap");if(texture==null)continue;
      if(texture.name.Equals("wood_walls",StringComparison.OrdinalIgnoreCase)){bark++;Check(!material.IsKeywordEnabled("_ALPHATEST_ON") && material.renderQueue==2000,"Apple trunk incorrectly uses opacity alpha");}
      else{leaves++;Check(material.IsKeywordEnabled("_ALPHATEST_ON"),"Apple foliage lost its alpha cutout");}
    }
    Check(bark==4 && leaves==5,"Expected four opaque trunk groups and five cutout leaf groups");
    var placementsField=typeof(S2MAppleOrchardView).GetField("placements",BindingFlags.Instance|BindingFlags.NonPublic);
    var actual=(Dictionary<int,Matrix4x4>)placementsField.GetValue(stageView);
    Check(Math.Abs(actual[1].lossyScale.x-1)<.001f,"Apple tree still inherits the half-size forest multiplier");
    var actors=S2MActorRenderer.Render(data,root.transform,null,null,1);actors.SetVariant(100,S2MAppleFarmAssets.Pick,.75f);actors.SetTransform(100,new Vector3(114.7f,0,99),Quaternion.Euler(0,135,0),Vector3.one);
    Capture(root,stageView,actors);Check(stageView.VisibleTrees==6,"Unplanted tree rendered or picked tree missing");UnityEngine.Object.DestroyImmediate(root);
    report.Add($"Native: {farms} campaign farms / {trees} correctly owned trees, worker clips and baskets, all five stage models/materials plus picked/bare reuse, instanced stage rendering.");
  }
  sealed class NativeAccess : IStorageAccessProvider
  {
    private readonly S2MStorageAccessProvider source;private readonly MapPoint fallback;
    public NativeAccess(S2MStorageAccessProvider source,MapPoint fallback){this.source=source;this.fallback=fallback;}
    public IReadOnlyList<MapPoint> AccessPoints(int id)=>id== -900?new[]{fallback}:source.AccessPoints(id);
  }
  static void NativeFlow(S2MFile map,NavigationGrid nav,S2MLaborPlan plan,AppleFarmerPresentation view,S2MActorData data)
  {
    var inv=S2MStoragePlan.Create(map);var geometry=new S2MStorageAccessProvider(map,nav);var farmers=plan.Jobs.OfType<AppleFarmerJob>().ToArray();AppleFarmerJob selected=null;
    foreach(var farmer in farmers)
      if(inv.Estate(farmer.EstateId).Buildings.Any(b=>b.Kind==StorageKind.Granary && geometry.AccessPoints(b.BuildingId).Any(p=>nav.CanReach(farmer.Approach,p)))){selected=farmer;break;}
    bool synthetic=selected==null;selected??=farmers[0];if(synthetic)inv.Register(-900,selected.EstateId,StorageKind.Granary);
    var locator=new StorageLocator(inv,nav,new NativeAccess(geometry,selected.Approach),StorageKind.Granary);
    selected.ConfigureStorage(inv,locator);view.ConfigureJob(selected,data);nav.EnablePathRequests(new PathfindingSettings{WorkUnitsPerFrame=256,MillisecondsPerFrame=100});
    var worker=new WorkerComponent(991,new MovementComponent(selected.Approach,0));Check(worker.TryAssign(selected,new[]{selected.Approach}),"Could not staff real farm");
    var phases=new HashSet<AppleFarmerPhase>();var stages=new HashSet<int>();
    for(int i=0;i<16000 && selected.DeliveredApples<10;i++)
    {
      nav.PathRequests.AdvanceFrame();plan.AppleOrchards.Advance(.1f);worker.Advance(.1f,1);phases.Add(selected.Phase);
      foreach(var tree in plan.AppleOrchards.FarmTrees(selected.BuildingId))stages.Add(tree.Stage);
    }
    report.Add($"Chapter 8 live farm={selected.BuildingId}, estate={selected.EstateId}, delivered={selected.DeliveredApples}, remaining={selected.Cargo}, phase={selected.Phase}, granary={(synthetic?"registered fixture at farm entrance":"authored same-estate granary")}; phases={string.Join(",",phases)}; stages={string.Join(",",stages)}");
    Check(selected.DeliveredApples>=10 && phases.Contains(AppleFarmerPhase.Planting) && phases.Contains(AppleFarmerPhase.Picking) && stages.Contains(4),"Real farm did not plant, ripen, harvest and deliver twice on native navigation");
    Check(plan.AppleOrchards.FarmTrees(selected.BuildingId).All(t=>t.Stage>=0),"Real farmer left unplanted trees");worker.Remove();
  }
  static void Capture(GameObject root,S2MAppleOrchardView view,S2MActorRenderer actors)
  {
    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
    var camera=new GameObject().AddComponent<Camera>();camera.cullingMask=1<<30;camera.transform.position=new Vector3(110,15,85);camera.transform.LookAt(new Vector3(110,1,100));camera.orthographic=true;camera.orthographicSize=11;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
    var sun=new GameObject().AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(50,-25,0);RenderSettings.ambientLight=Color.gray;
    var rt=new RenderTexture(1400,700,24);camera.targetTexture=rt;var image=new Texture2D(1400,700,TextureFormat.RGB24,false);view.Draw(camera);
    typeof(PitchRigValidation).GetMethod("Submit",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actors,camera});camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,700),0,0);image.Apply();File.WriteAllBytes(Output+"/Stages.png",image.EncodeToPNG());RenderTexture.active=null;
    UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
  }
}
