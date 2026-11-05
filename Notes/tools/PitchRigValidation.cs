using System;
using System.IO;
using System.Linq;
using System.Collections;
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
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.MapLoading.Labor;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.ModelRendering.Labor;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;

public static class PitchRigValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2",Install="C:/Steam/steamapps/common/Stronghold 2",Output=Repo+"/Temp/PitchValidation",
    Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  static readonly List<string> report=new();
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try{Core(false);Core(true);Native();report.Add("PASS: pitch rig labor, production, delivery, native assets and swamp anchors.");File.WriteAllLines(Output+"/result.txt",report);EditorApplication.Exit(0);}
    catch(Exception e){report.Add(e.ToString());File.WriteAllLines(Output+"/result.txt",report);Debug.LogException(e);EditorApplication.Exit(1);}
  }
  sealed class Access : IStorageAccessProvider
  {
    private readonly MapPoint? point;
    public Access(MapPoint? point=null){this.point=point;}
    public IReadOnlyList<MapPoint> AccessPoints(int id)=>new[]{point??new MapPoint(id==11?10:6,5)};
  }
  static void Core(bool queued)
  {
    var nav=new NavigationGrid(32,32,1,Enumerable.Repeat(true,1024).ToArray(),new float[1024],1);
    if(queued)nav.EnablePathRequests(new PathfindingSettings{WorkUnitsPerFrame=8,MillisecondsPerFrame=100});
    var point=new MapPoint(5.5f,5.5f);
    PitchWorkerJob Make(bool valid=true)=>new(1,1,-500,point,point,point,point,0,0,valid,nav,new PitchRigSettings());
    var job=Make();var inv=new EstateInventory();inv.Register(22,2,StorageKind.Stockpile);var locator=new StockpileLocator(inv,nav,new Access());job.ConfigureStorage(inv,locator);
    var worker=new WorkerComponent(100,new MovementComponent(point,0));Check(worker.TryAssign(job,new[]{point}),"Staffing failed");
    var building=new LaborBuilding(1,1,new[]{job});Check(building.MaximumWorkers==1 && building.IsCrewReady,"Rig requires one worker");
    worker.Advance(6,1);Check(job.Cargo==0 && job.Phase==PitchWorkerPhase.Pumping,"Pitch produced before five loops");worker.Advance(1,1);
    Check(job.Cargo==2 && job.DeliveredPitch==0,"Native batch is not exactly two");
    for(int i=0;i<500;i++){nav.PathRequests?.AdvanceFrame();worker.Advance(.1f,1);}
    Check(job.Cargo==2 && inv.Amount(2,Goods.Pitch)==0,"Delivered to another estate or lost cargo with no store");
    inv.Register(11,1,StorageKind.Stockpile);var seen=new HashSet<PitchWorkerPhase>();int commits=0;long last=0;
    for(int i=0;i<3000 && job.DeliveredPitch<4;i++)
    {
      nav.PathRequests?.AdvanceFrame();worker.Advance(.05f,1);seen.Add(job.Phase);
      long amount=inv.Amount(1,Goods.Pitch);if(amount!=last){Check(amount-last==1,"Deposit was not incremental");commits++;last=amount;}
    }
    Check(job.DeliveredPitch==4 && commits==4 && inv.Amount(1,Goods.Pitch)==4 && inv.Amount(2,Goods.Pitch)==0,"Repeat cycle did not conserve pitch");
    Check(seen.Contains(PitchWorkerPhase.Returning),"Worker never returned to rig");
    worker.Remove();Check(job.Cargo==0 && !job.WorkerId.HasValue && !job.IsOccupied,"Worker removal retained cargo/staff");
    var invalid=Make(false);var other=new WorkerComponent(101,new MovementComponent(point,0));other.TryAssign(invalid,new[]{point});other.Advance(100,1);Check(invalid.Cargo==0,"Non-swamp rig extracted pitch");
    var paused=Make();var clock=new SimulationClock();var w=new WorkerComponent(102,new MovementComponent(point,0));w.TryAssign(paused,new[]{point});
    clock.IsPaused=true;clock.Advance(100);w.Advance(clock.DeltaTime,1);Check(paused.PhaseTime==0,"Pause advanced extraction");
    clock.IsPaused=false;clock.Speed=2;clock.Advance(3.5f);w.Advance(clock.DeltaTime,1);Check(paused.Cargo==2,"2x simulation clock did not accelerate extraction");
    var limited=new EstateInventory(new StorageSettings{StockpilePallets=1,PalletCapacities=new[]{new PalletCapacityOverride{Goods=Goods.Pitch,Capacity=2}}});
    limited.Register(11,1,StorageKind.Stockpile);limited.TryDeposit(11,1,Goods.Pitch,2);
    var blocked=Make();blocked.ConfigureStorage(limited,new StockpileLocator(limited,nav,new Access(point)));
    var blockedWorker=new WorkerComponent(103,new MovementComponent(point,0));blockedWorker.TryAssign(blocked,new[]{point});
    for(int i=0;i<500;i++){nav.PathRequests?.AdvanceFrame();blockedWorker.Advance(.05f,1);}
    Check(blocked.Cargo==2 && blocked.DeliveredPitch==0,"Full stockpile consumed cargo or continued extraction");
    limited.TryWithdraw(11,Goods.Pitch,2);
    for(int i=0;i<1000 && blocked.Phase!=PitchWorkerPhase.Unloading;i++){nav.PathRequests?.AdvanceFrame();blockedWorker.Advance(.01f,1);}
    Check(blocked.Phase==PitchWorkerPhase.Unloading && blocked.Cargo==2,"Did not resume after capacity freed");
    blockedWorker.Advance(.149f-blocked.PhaseTime,1);Check(blocked.DeliveredPitch==0,"Deposit committed before shared 0.15s interval");
    blockedWorker.Advance(.002f,1);Check(blocked.DeliveredPitch==1 && blocked.Cargo==1,"First timed deposit did not consume one pitch");
    blockedWorker.Advance(.15f,1);Check(blocked.DeliveredPitch==2 && blocked.Cargo==0 && limited.Amount(1,Goods.Pitch)==2,"Second timed deposit did not conserve cargo");blockedWorker.Remove();
    report.Add($"Core queued={queued}: five loops/two pitch, one worker, no foreign-estate delivery, late stockpile discovery, four one-item deposits, return/repeat, removal, swamp restriction, pause/2x.");
  }
  static void Native()
  {
    S2MFile selected=null;BuildingPlacement rig=null;int count=0;
    Debug.unityLogger.logEnabled=false;
    for(int chapter=1;chapter<=12;chapter++)
    {
      var map=S2MFileLoader.Load(Path.Combine(Install,"maps",$"war_chapter{chapter}.s2m"));
      foreach(var b in map.BuildingPlacements.Where(b=>b.Type=="PitchRig"))
      {
        count++;Check(S2MPitchRigLayout.HasPitch(map,b),"Authored rig lacks swamp");
        float height=S2MPitchRigLayout.RawHeight(map,b);Check(height>=b.RawY,"Rig lowered beneath terrain");
        report.Add($"Chapter {chapter}: rig {b.Id}, terrain={b.RawY}, surface anchor={height}, rotation={b.RotationDegrees}.");
        if(selected==null){selected=map;rig=b;}
      }
    }
    Debug.unityLogger.logEnabled=true;Check(count>0,"No native pitch rigs");
    var nav=S2MNavigationBuilder.Build(selected,new PopulationSettings());var job=(PitchWorkerJob)new PitchRigBuildingDefinition().Create(selected,rig,nav,-500,new HashSet<int>());
    var labor=S2MLaborPlan.Create(selected,nav);Check(labor.Jobs.OfType<PitchWorkerJob>().All(j=>j.Building.MaximumWorkers==1 && j.ExtractionSiteValid),"Native labor registration failed");
    var map8=S2MFileLoader.Load(Path.Combine(Install,"maps","war_chapter8.s2m"));var nav8=S2MNavigationBuilder.Build(map8,new PopulationSettings());
    var rigs8=S2MLaborPlan.Create(map8,nav8).Jobs.OfType<PitchWorkerJob>().ToArray();Check(rigs8.Length==3 && rigs8.All(j=>j.Building.MaximumWorkers==1 && j.ExtractionSiteValid && nav8.IsWalkable(j.Approach)),"Chapter 8 rig staffing/approaches failed");
    var placements=S2MPitchRigAssets.Overrides().Select(o=>{var p=new ActorPlacement();Set(p,"Id",o.Variant==S2MPitchRigAssets.Scoop?-500:100);Set(p,"Type",o.ObjectType);Set(p,"OnMap",true);return p;}).ToArray();
    var data=new S2MActorAssetLoader().Load(selected,Install,S2MPitchRigAssets.Overrides(),converterPath:Converter,cacheDirectory:Output+"/Actors",placements:placements);
    Check(data.Issues.Count==0,"Pitch assets: "+string.Join(";",data.Issues));var view=new PitchWorkerPresentation();view.ConfigureJob(job,data);
    Check(Math.Abs(job.PumpClipSeconds-4f/3)<.001,"Unexpected native pump duration");
    foreach(var b in data.Batches)report.Add($"{b.Asset.Variant}: {b.Asset.AnimationPath}, seconds={b.Motion.Duration}, attachments={string.Join(",",b.Attachments.Select(a=>Path.GetFileName(a.model.SourcePath)))}");
    Check(data.Batches.Any(b=>b.Asset.Variant==S2MPitchRigAssets.Pump && b.Attachments.Any(a=>a.model.SourcePath.Contains("scoop"))),"Worker scoop attachment missing");
    Check(data.Batches.Any(b=>b.Asset.Variant==S2MPitchRigAssets.Carry && b.Attachments.Any(a=>a.model.SourcePath.Contains("pitch_pot_full"))),"Full pitch pot attachment missing");
    // Render only the rig from this real map, retaining its height and terrain planes.
    Set(selected,"BuildingPlacements",new[]{rig});Set(selected,"Keeps",Array.Empty<Keep>());
    var buildings=new S2MBuildingAssetLoader().Load(selected,Install,Converter,Output+"/Models");
    var root=new GameObject("Pitch rig validation");S2MBuildingRenderer.Render(buildings,root.transform);
    var actors=S2MActorRenderer.Render(data,root.transform,new S2MTerrainSettings(),new S2MActorRenderSettings(),1/204.8f/.01f);
    var worker=new WorkerComponent(100,new MovementComponent(job.Approach,0));worker.TryAssign(job,new[]{job.Approach});
    for(int i=0;i<1000 && job.Phase!=PitchWorkerPhase.Pumping;i++)worker.Advance(.01f,1);
    Check(job.Phase==PitchWorkerPhase.Pumping,"Native worker could not enter platform");worker.Advance(.5f,1);
    var position=view.Position(worker);actors.SetVariant(100,view.Variant(worker),view.AnimationTime(worker,1));actors.SetTransform(100,new Vector3(position.X,view.BuildingRawHeight(worker)/1024,position.Z),Quaternion.Euler(0,view.Heading(worker),0),Vector3.one/204.8f/.01f);
    var equipment=view.GetEquipment(job,0);actors.SetVariant(-500,equipment.Variant,equipment.AnimationTime);actors.SetTransform(-500,new Vector3(job.BuildingPosition.X,job.RigRawHeight/1024,job.BuildingPosition.Z),Quaternion.Euler(0,job.BuildingHeading,0),Vector3.one/204.8f/.01f);
    Capture(root,actors,job);UnityEngine.Object.DestroyImmediate(root);
    report.Add($"Verified {count} campaign rigs, native shore approach, full-pot/scoop attachments and platform render.");
  }
  static void Capture(GameObject root,S2MActorRenderer actors,PitchWorkerJob job)
  {
    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
    var camera=new GameObject().AddComponent<Camera>();camera.cullingMask=1<<30;camera.transform.position=new Vector3(job.BuildingPosition.X+4,job.RigRawHeight/1024+3.5f,job.BuildingPosition.Z-4);camera.transform.LookAt(new Vector3(job.BuildingPosition.X+1,job.RigRawHeight/1024+.6f,job.BuildingPosition.Z+1));camera.fieldOfView=38;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
    var sun=new GameObject().AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.ambientLight=Color.gray;
    var target=new RenderTexture(1000,800,24);camera.targetTexture=target;var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
    Submit(actors,camera);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Output+"/Pumping.png",image.EncodeToPNG());RenderTexture.active=null;
    UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
  }
  static void Submit(S2MActorRenderer renderer,Camera camera)
  {
    foreach(var draw in (IEnumerable)typeof(S2MActorRenderer).GetField("draws",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer))
    {
      object Get(string name)=>draw.GetType().GetField(name).GetValue(draw);
      var local=(Matrix4x4[])Get("Local");var visible=(bool[])Get("Visible");var playback=(Vector4[])Get("Playback");var livery=(Vector4[])Get("Livery");
      var matrices=new List<Matrix4x4>();var times=new List<Vector4>();var colors=new List<Vector4>();
      for(int i=0;i<local.Length;i++)if(visible[i]){matrices.Add(renderer.transform.localToWorldMatrix*local[i]);times.Add(playback[i]);colors.Add(livery[i]);}
      if(matrices.Count==0)continue;var properties=new MaterialPropertyBlock();properties.SetVectorArray("_ActorPlayback",times);properties.SetVectorArray("_ActorLivery",colors);properties.SetFloat("_ActorClock",0);
      var meshes=(Mesh[])Get("Meshes");var materials=(Material[][])Get("Materials");
      for(int m=0;m<meshes.Length;m++)for(int s=0;s<meshes[m].subMeshCount;s++)Graphics.DrawMeshInstanced(meshes[m],s,materials[m][s],matrices,properties,UnityEngine.Rendering.ShadowCastingMode.On,true,30,camera,UnityEngine.Rendering.LightProbeUsage.Off);
    }
  }
}

