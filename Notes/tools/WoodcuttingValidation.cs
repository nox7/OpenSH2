// Copy to Assets/Editor in the isolated validation project with current Assets/Code.
// Unity -batchmode -executeMethod WoodcuttingValidation.Run -quit
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Code.Simulation;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Movement;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.ModelRendering.Labor;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;

public static class WoodcuttingValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install="C:/Steam/steamapps/common/Stronghold 2";
  const string Output=Repo+"/Temp/WoodcuttingValidation";
  const string Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      File.Delete(Output+"/pass.txt");File.Delete(Output+"/failure.txt");
      Production();Synthetic();File.WriteAllText(Output+"/stage.txt","Synthetic simulation passed");
      Debug.unityLogger.logEnabled=false;ShaderUtil.allowAsyncCompilation=false;
      QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names,"PC"),true);
      var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      var population=new PopulationSettings();var settings=new WoodcuttingSettings();
      var buildings=new S2MBuildingAssetLoader().Load(map,Install,Converter);
      var nav=S2MNavigationBuilder.Build(map,population,buildings,dynamicFoliage:true);
      var vegetation=new S2MVegetationAssetLoader().Load(map,Install,Converter);
      var root=new GameObject("Woodcutting validation map");
      var buildingView=S2MBuildingRenderer.Render(buildings,root.transform);
      foreach(var r in buildingView.GetComponentsInChildren<MeshRenderer>())
      {bool pit=false;for(var t=r.transform;t!=null;t=t.parent)if(t.name.StartsWith("SawPit ("))pit=true;if(!pit)r.enabled=false;}
      var scenery=S2MVegetationRenderer.Render(vegetation,root.transform,treeModelScale:.5f);
      var view=scenery.GetComponent<S2MFoliageHarvestView>();var resources=view.Initialize(nav,settings);
      Check(resources.All.Any(r=>r.Bush) && resources.All.Any(r=>!r.Bush),"Trees or bushes missing from harvest registry");
      var plans=S2MRecruitmentPlan.Create(map,nav,population);var labor=S2MLaborPlan.Create(map,nav);
      File.WriteAllLines(Output+"/ownership.txt",S2MLiveryOwnership.Read(map).Estates.Values.Select(e=>$"estate {e.Number} lord={e.Lord} player={e.PlayerNumber}"));
      var jobs=labor.Jobs.OfType<WoodcutterJob>().ToArray();foreach(var job in jobs)job.ConfigureHarvest(resources,settings);
      File.WriteAllLines(Output+"/delivery-paths.txt",jobs.Select(j=> {
        var points=(IReadOnlyList<WoodDeliveryPoint>)typeof(WoodcutterJob).GetField("stockpiles",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(j);
        bool snapped=resources.Carts.TryNearest(j.CartPosition,2,out var start);
        return $"job {j.BuildingId} estate={j.EstateId} points={points.Count} start={start.X},{start.Z} snapped={snapped} reachable="+string.Join(";",points.Select(d=> {bool near=resources.Carts.TryNearest(d.Position,2,out var q);return $"{d.BuildingId}@{d.Position.X},{d.Position.Z}:near={near},reach={near && resources.Carts.CanReach(start,q)},snap={q.X},{q.Z}";}));}));
      var data=new S2MActorAssetLoader().Load(map,Install,S2MCampfirePopulationLoader.Overrides(),converterPath:Converter,
        placements:S2MCampfirePopulationLoader.CreateRecruitPlacements(plans,labor.Jobs));
      var saw=data.Batches.First(b=>b.Asset.Variant==S2MWoodcutterAssets.Sawing);
      var logBounds=Granny2ModelRenderer.GetLocalBounds(data.Batches.First(b=>b.Asset.Variant==S2MWoodcutterAssets.SawLog).Model,1/204.8f);
      var site=jobs[0];var building=map.BuildingPlacements.First(b=>b.Id==site.BuildingId);
      var anchor=Quaternion.Inverse(Quaternion.Euler(0,site.SawHeading,0))*new Vector3(site.SawPosition.X-building.MapX,0,site.SawPosition.Z-building.MapZ);
      int pelvis=saw.Motion.BoneNames.FindIndex(n=>n=="Bip01 Pelvis");Check(pelvis>=0,"Upper saw clip has no pelvis");
      for(int frame=0;frame<saw.Motion.Frames;frame++)
      {
        int off=(frame*saw.Motion.Bones+saw.Motion.BoneNames.Count+pelvis)*16;var a=saw.Motion.Matrices;
        var hip=anchor+Quaternion.Euler(saw.Asset.LocalEulerAngles)*new Vector3(a[off+12],a[off+14],a[off+13])*.01f+saw.Asset.LocalOffset;
        Check(hip.x>=logBounds.min.x && hip.x<=logBounds.max.x && hip.z>=logBounds.min.z && hip.z<=logBounds.max.z,"Sawing pelvis is over empty pit instead of the log");
      }
      Check(data.Issues.Count==0,"Harvest asset issues: "+string.Join(";",data.Issues));
      Check(data.Batches.Any(b=>b.Asset.ModelPath.EndsWith("cart_horse_wood_1.GR2",StringComparison.OrdinalIgnoreCase))
        && data.Batches.Any(b=>b.Asset.ModelPath.EndsWith("cart_horse_planks.GR2",StringComparison.OrdinalIgnoreCase)),"Log or plank cart missing");
      foreach(string clip in new[]{"woodcutter_fell.GR2","woodcutter_chop.GR2","woodcutter_saw_top.GR2","cart_horse_walk.GR2"})
        Check(data.Batches.Any(b=>b.Asset.AnimationPath.EndsWith(clip,StringComparison.OrdinalIgnoreCase)),"Missing original clip "+clip);
      Check(data.Batches.Where(b=>b.Asset.Variant==S2MWoodcutterAssets.Felling || b.Asset.Variant==S2MWoodcutterAssets.Chopping)
        .All(b=>b.Attachments.Any(a=>a.model.SourcePath.EndsWith("woodcutter_axe.GR2",StringComparison.OrdinalIgnoreCase))),"Native axe attachment omitted");
      Check(data.Batches.Where(b=>b.Asset.Variant==S2MWoodcutterAssets.Sawing).All(b=>b.Attachments.Any(a=>a.model.SourcePath.EndsWith("woodcutter_saw.GR2",StringComparison.OrdinalIgnoreCase))),"Saw action is missing the native saw attachment");
      File.WriteAllLines(Output+"/models.txt",data.Batches.Where(b=>b.Asset.ModelPath.IndexOf("cart_horse",StringComparison.OrdinalIgnoreCase)>=0)
        .Select(b=>$"{b.Asset.Variant}: {b.Asset.ModelPath}; {Granny2ModelRenderer.GetLocalBounds(b.Model,.01f)} offset={b.Asset.LocalOffset}"));
      var clock=new SimulationClock();var populationRoot=new GameObject("Population");populationRoot.transform.SetParent(root.transform,false);
      var controller=populationRoot.AddComponent<S2MPopulationController>();
      controller.Initialize(map,nav,plans,data,new S2MTerrainSettings(),new S2MActorRenderSettings(),population,clock,jobs:labor.Jobs);
      var actors=populationRoot.GetComponentInChildren<S2MActorRenderer>();
      Debug.unityLogger.logEnabled=true;File.WriteAllText(Output+"/stage.txt","Real-map assets and controller ready");
      foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
      var camera=new GameObject("Harvest camera").AddComponent<Camera>();camera.cullingMask=1<<30;
      camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.18f,.2f);
      var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-25,0);
      RenderSettings.sun=sun;RenderSettings.ambientLight=Color.gray;
      var target=new RenderTexture(1000,800,24);var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
      void Capture(WoodcutterJob job,string name)
      {
        var p=controller.Kingdoms.SelectMany(k=>k.Peasants).First(a=>a.Job==job);
        foreach(var other in controller.Kingdoms.SelectMany(k=>k.Peasants))if(other.Id!=p.Id)actors.HideActor(other.Id);
        foreach(var other in jobs)if(other!=job){actors.HideActor(other.CartActorId);actors.HideActor(other.SawLogActorId);}
        // Each preview gets its own camera so the editor's same-frame DrawMesh queue cannot accumulate motion trails.
        UnityEngine.Object.DestroyImmediate(camera.gameObject);camera=new GameObject("Harvest camera").AddComponent<Camera>();
        camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.18f,.2f);
        var presentation=new WoodcutterPresentation(new HorseCartAssemblySettings());
        float rate=presentation.AnimationRate(p.Worker,population.WalkSpeedCellsPerSecond);
        actors.SetVariant(p.Id,presentation.Variant(p.Worker),presentation.AnimationTime(p.Worker,rate),presentation.AnimationLoops(p.Worker),rate);
        var equipment=presentation.GetEquipment(job,0);
        actors.SetVariant(equipment.ActorId,equipment.Variant,equipment.AnimationTime,true,equipment.AnimationRate);
        var center=new Vector3(p.Position.X,S2MNavigationBuilder.SampleHeight(map,p.Position.X,p.Position.Z)/1024,p.Position.Z);
        var away=job.Target==null?new Vector3(0,0,-1):new Vector3(p.Position.X-job.Target.Position.X,0,p.Position.Z-job.Target.Position.Z).normalized;
        camera.transform.position=center+away*10+Vector3.Cross(Vector3.up,away)*4+Vector3.up*5;camera.transform.LookAt(center+Vector3.up*1.5f);
        SubmitPreview(actors,camera,clock.DeltaTime);
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
        foreach(string field in new[]{"shown","shownWork","shownEquipment"})
          ((IDictionary)typeof(S2MPopulationController).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller)).Clear();
      }
      var captured=new HashSet<WoodcuttingPhase>();var visited=new HashSet<WoodcuttingPhase>();
      WoodcutterJob sample=null;int initial=resources.All.Count;
      for(int step=0;step<7200;step++)
      {
        clock.Advance(.25f);controller.Advance(clock.DeltaTime);
        foreach(var job in jobs)visited.Add(job.Phase);
        var claims=jobs.Where(j=>j.Target!=null && j.Target.State!=FoliageState.Removed).Select(j=>j.Target.Id).ToArray();
        Check(claims.Length==claims.Distinct().Count(),"Two woodcutters claimed the same foliage");
        sample??=jobs.FirstOrDefault(j=>j.Phase==WoodcuttingPhase.DrivingOut);
        if(sample!=null && new[]{WoodcuttingPhase.DrivingOut,WoodcuttingPhase.Felling,WoodcuttingPhase.Falling,WoodcuttingPhase.Chopping,WoodcuttingPhase.Returning,WoodcuttingPhase.Sawing,WoodcuttingPhase.Delivering,WoodcuttingPhase.WaitingForStockpile}.Contains(sample.Phase)
          && (sample.Phase!=WoodcuttingPhase.Falling || sample.Target.FallProgress>.3f) && captured.Add(sample.Phase))
        {
          Capture(sample,sample.Phase.ToString());
          if(sample.Phase==WoodcuttingPhase.Falling)
          {
            var entries=(IDictionary)typeof(S2MFoliageHarvestView).GetField("entries",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            var entry=entries[sample.Target.Id];var transform=(Transform)entry.GetType().GetField("Transform").GetValue(entry);
            Check(Quaternion.Angle(transform.localRotation,(Quaternion)entry.GetType().GetField("Rotation").GetValue(entry))>0,"Tree did not fall");
            var block=new MaterialPropertyBlock();foreach(var renderer in transform.GetComponentsInChildren<MeshRenderer>())
            {renderer.GetPropertyBlock(block);Check(block.GetVector("_ModelSway").y==0,"Harvested tree still sways");}
            var phase=sample.Phase;float time=sample.PhaseTime;clock.IsPaused=true;clock.Advance(2);controller.Advance(clock.DeltaTime);
            Check(sample.Phase==phase && sample.PhaseTime==time,"Paused harvest advanced");clock.IsPaused=false;
          }
        }
        if(step%400==0)File.WriteAllLines(Output+"/stage.txt",jobs.Select(j=>$"{j.BuildingId}: {j.Phase}; occupied={j.IsOccupied}; target={j.Target?.Id}; waiting={j.WaitingForPath}"));
        if(jobs.All(j=>j.Phase==WoodcuttingPhase.AtStockpile || j.Phase==WoodcuttingPhase.WaitingForStockpile || !j.WorkerId.HasValue) && jobs.Any(j=>j.CarryingPlanks))break;
      }
      var staffed=jobs.Where(j=>j.WorkerId.HasValue).ToArray();
      File.WriteAllLines(Output+"/jobs.txt",jobs.Select(j=>$"{j.BuildingId}: {j.Phase}, resource={j.Target?.Id}, loaded={j.Loaded}, pos={j.CartPosition.X},{j.CartPosition.Z}"));
      Check(staffed.Length>0 && staffed.All(j=>(j.Phase==WoodcuttingPhase.AtStockpile || j.Phase==WoodcuttingPhase.WaitingForStockpile) && j.Loaded && j.CarryingPlanks),"Some staffed saw pits did not return loaded");
      Check(captured.Contains(WoodcuttingPhase.Sawing) && captured.Contains(WoodcuttingPhase.Delivering),"Missing complete visual state coverage: "+string.Join(",",captured));
      int removed=resources.All.Count(r=>r.State==FoliageState.LogPile || r.State==FoliageState.Removed);
      Check(removed==staffed.Length,"Harvested foliage more than once or began extra cycles");
      Check(staffed.All(j=>j.Target.State==FoliageState.LogPile && j.Target.LogsRemaining==5 && j.Target.ReservedBy==j.WorkerId),"Returning worker lost its pile assignment or took multiple logs");
      Check(staffed.Count(j=>j.Phase==WoodcuttingPhase.AtStockpile)==6,"Expected six same-estate stockpile deliveries on chapter 8");
      foreach(var job in staffed)
      {
        var points=(IReadOnlyList<WoodDeliveryPoint>)typeof(WoodcutterJob).GetField("stockpiles",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(job);
        Check(points.All(d=>S2MCampfireLayout.EstateAtBuilding(map,map.BuildingPlacements.First(b=>b.Id==d.BuildingId))==job.EstateId),"Cross-estate stockpile authorized");
        if(job.Phase==WoodcuttingPhase.AtStockpile)Check(job.StockpileId.HasValue,"Missing destination identity");
        else
        {
          resources.Carts.TryNearest(job.CartPosition,2,out var start);
          Check(!points.Any(d=>resources.Carts.TryNearest(d.Position,2,out var end) && resources.Carts.CanReach(start,end)),"Worker waited despite a reachable stockpile");
        }
      }
      Check(vegetation.Batches.SelectMany(b=>b.Instances).Count(s=>!s.Enabled)==removed,"Consumed foliage remains in the runtime map");
      clock.Advance(100);controller.Advance(clock.DeltaTime);Check(resources.All.Count(r=>r.State==FoliageState.LogPile)==removed,"Trip continued before unloading");
      var entriesById=(IDictionary)typeof(S2MFoliageHarvestView).GetField("entries",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
      var sampleWorker=controller.Kingdoms.SelectMany(k=>k.Peasants).First(p=>p.Job==sample).Worker;
      int pileId=sample.Target.Id;
      for(int logs=4;logs>=0;logs--)
      {
        Check(sample.ResumeAfterUnloading(sampleWorker),"Cannot resume after external unloading");
        for(int tick=0;tick<4000 && sample.Phase!=WoodcuttingPhase.AtStockpile && sample.Phase!=WoodcuttingPhase.WaitingForStockpile;tick++){clock.Advance(.5f);controller.Advance(clock.DeltaTime);}
        Check((sample.Phase==WoodcuttingPhase.AtStockpile || sample.Phase==WoodcuttingPhase.WaitingForStockpile) && sample.Target.Id==pileId && sample.Target.LogsRemaining==logs,"Worker changed pile or skipped log counts");
        var entry=entriesById[pileId];var logObjects=(GameObject[])entry.GetType().GetField("Logs").GetValue(entry);
        if(logs>0)for(int i=0;i<6;i++)Check(logObjects[i].activeSelf==(i<logs),"Logs were not removed top to bottom");
      }
      Check(sample.Target.State==FoliageState.Removed && sample.Target.ReservedBy==null,"Empty pile remained assigned");
      var abandoned=staffed.First(j=>j!=sample);var abandonedPile=abandoned.Target;int count=abandonedPile.LogsRemaining;
      int actor=abandoned.WorkerId.Value;Check(controller.RemoveActor(actor),"Worker removal entry point failed");
      Check(abandonedPile.State==FoliageState.LogPile && abandonedPile.LogsRemaining==count && abandonedPile.ReservedBy==null,"Worker removal destroyed or retained its pile claim");
      UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);
      UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
      File.WriteAllText(Output+"/pass.txt",$"PASS: {staffed.Length}/{jobs.Length} Chapter 8 saw pits processed planks after 18 seconds of sawing with six deliveries and two waits for missing same-estate stockpiles; six original log meshes; same-pile repeat trips and top-to-bottom depletion; death/removal releases intact piles; exclusive claims, navigation, pause and cleanup. {removed}/{initial} foliage felled.");
      EditorApplication.Exit(0);
    }
    catch(Exception e){Debug.unityLogger.logEnabled=true;File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static NavigationGrid Flat(int side=64,bool wall=false)
  {
    int width=side*2;var open=Enumerable.Repeat(true,width*width).ToArray();
    if(wall)for(int z=0;z<width;z++)open[z*width+62]=open[z*width+63]=false;
    return new NavigationGrid(width,width,2,open,new float[open.Length],1);
  }
  static void SubmitPreview(S2MActorRenderer renderer,Camera camera,float frameClock)
  {
    var draws=(IEnumerable)typeof(S2MActorRenderer).GetField("draws",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
    foreach(var draw in draws)
    {
      object Get(string name)=>draw.GetType().GetField(name).GetValue(draw);
      var local=(Matrix4x4[])Get("Local");var visible=(bool[])Get("Visible");var playback=(Vector4[])Get("Playback");var livery=(Vector4[])Get("Livery");
      var matrices=new List<Matrix4x4>();var times=new List<Vector4>();var colors=new List<Vector4>();
      for(int i=0;i<local.Length;i++)if(visible[i]){matrices.Add(renderer.transform.localToWorldMatrix*local[i]);times.Add(playback[i]);colors.Add(livery[i]);}
      if(matrices.Count==0)continue;
      var properties=new MaterialPropertyBlock();properties.SetVectorArray("_ActorPlayback",times);properties.SetVectorArray("_ActorLivery",colors);
      properties.SetFloat("_ActorClock",frameClock);properties.SetFloat("_ActorReceiveShadows",1);
      var meshes=(Mesh[])Get("Meshes");var materials=(Material[][])Get("Materials");
      for(int m=0;m<meshes.Length;m++)for(int s=0;s<meshes[m].subMeshCount;s++)
        Graphics.DrawMeshInstanced(meshes[m],s,materials[m][s],matrices,properties,UnityEngine.Rendering.ShadowCastingMode.On,true,30,camera,UnityEngine.Rendering.LightProbeUsage.Off);
    }
  }
  static WorkerComponent Staff(WoodcutterJob job,int id)
  {
    var worker=new WorkerComponent(id,new MovementComponent(job.Approach,0));
    Check(worker.TryAssign(job,new[]{job.Approach,job.Approach}),"Could not staff fixture");worker.Advance(.001f,1);return worker;
  }
  static void Production()
  {
    var clock=new SimulationClock();clock.Speed=2;clock.Advance(9);Check(clock.DeltaTime==18 && clock.ElapsedSeconds==18,"Clock speed is not applied once");
    clock.IsPaused=true;clock.Advance(10);Check(clock.DeltaTime==0 && clock.ElapsedSeconds==18,"Pause ignored speed");
    clock.IsPaused=false;clock.Speed=0;clock.Advance(10);Check(clock.DeltaTime==0,"Zero speed advanced");
    var settings=new WoodcuttingSettings();var tree=new FoliageResource(1,new MapPoint(15,15),.3f);
    var resources=new FoliageResources(new[]{tree},Flat(),settings);
    var job=new WoodcutterJob(1,1,-1,new MapPoint(3,10),new MapPoint(3,10),0,0);job.ConfigureHarvest(resources,settings);
    job.ConfigureProduction(new MapPoint(4,11),90,new[]{new WoodDeliveryPoint(123,new MapPoint(30,30)),new WoodDeliveryPoint(124,new MapPoint(5,10))});
    Check(ReferenceEquals(resources.Carts,resources.Walking),"Carts do not share ordinary ground navigation");
    var worker=Staff(job,42);
    for(int i=0;i<20000 && job.Phase!=WoodcuttingPhase.Sawing;i++)worker.Advance(.05f,1);
    Check(job.Phase==WoodcuttingPhase.Sawing && !job.Loaded && !job.WorkerOnCart,"Log did not enter sawing");
    float left=18-job.PhaseTime;worker.Advance(left-.01f,1);Check(job.Phase==WoodcuttingPhase.Sawing,"Sawing completed before 18 seconds");
    worker.Advance(.02f,1);Check(job.Phase==WoodcuttingPhase.Delivering && job.CarryingPlanks && job.WorkerOnCart,"Sawing did not mount plank cart at 18 seconds");
    double distanceBefore=worker.Movement.DistanceTravelled;float poseBefore=job.CartAnimationTime;
    settings.CartSpeedCellsPerSecond=.375f;worker.Advance(.5f,1);
    Check(Math.Abs(worker.Movement.DistanceTravelled-distanceBefore-.1875)<.001,"Live cart speed did not control travel distance");
    Check(Math.Abs(job.CartAnimationTime-poseBefore-.125)<.001,"Cart animation phase is not driven by traveled distance");
    var presentation=new WoodcutterPresentation(new HorseCartAssemblySettings());
    Check(presentation.GetEquipment(job,0).AnimationRate==.25f && presentation.AnimationRate(worker,1)==.25f,"Horse and rider playback ignored live cart speed");
    settings.CartSpeedCellsPerSecond=.75f;
    worker.Advance(1000,1);Check(job.Phase==WoodcuttingPhase.AtStockpile && job.StockpileId==124 && job.Target.ReservedBy==42,"Delivery lost ownership or failed");
    var missing=new WoodcutterJob(2,1,-2,new MapPoint(3,10),new MapPoint(3,10),0,0);missing.ConfigureHarvest(resources,settings);
    missing.ConfigureProduction(new MapPoint(4,11),90,Array.Empty<WoodDeliveryPoint>());worker.Remove();var replacement=Staff(missing,43);
    replacement.Advance(1000,1);Check(missing.Phase==WoodcuttingPhase.WaitingForStockpile && missing.CarryingPlanks,"Missing stockpile lost cargo or deadlocked sawing");
  }
  static void Synthetic()
  {
    var settings=new WoodcuttingSettings();var nav=Flat();
    var a=new FoliageResource(1,new MapPoint(10,10),.3f);var b=new FoliageResource(2,new MapPoint(15,10),.15f,true);
    var resources=new FoliageResources(new[]{a,b},nav,settings);
    Check(resources.TryReserveNearest(10,new MapPoint(3,10),out var first) && first.Resource==a,"Not the closest foliage");
    Check(resources.TryReserveNearest(11,new MapPoint(3,10),out var second) && second.Resource==b,"Duplicate reservation or bush omitted");
    Check(!resources.TryReserveNearest(12,new MapPoint(3,10),out _),"Reserved foliage assigned twice");
    Check(!resources.Release(a,11) && resources.Release(a,10) && resources.Release(b,11),"Reservation ownership/release");
    var job=new WoodcutterJob(1,1,-1,new MapPoint(3,10),new MapPoint(3,10),0,0);job.ConfigureHarvest(resources,settings);
    var worker=Staff(job,20);worker.Advance(1000,1);
    Check(job.Phase==WoodcuttingPhase.Returned && job.Loaded && a.State==FoliageState.LogPile && a.LogsRemaining==5 && a.ReservedBy==20 && b.State==FoliageState.Available,"Large tick lost work phases or harvested an extra resource");
    for(int left=4;left>=0;left--)
    {Check(job.ResumeAfterUnloading(worker),"Resume hook failed");worker.Advance(1000,1);Check(job.Target==a && a.LogsRemaining==left,"Pile ownership did not survive a trip");}
    Check(a.State==FoliageState.Removed && a.ReservedBy==null,"Exhausted pile did not disappear");
    var cancelJob=new WoodcutterJob(2,1,-2,new MapPoint(3,12),new MapPoint(3,12),0,0);cancelJob.ConfigureHarvest(resources,settings);
    var cancel=Staff(cancelJob,30);Check(b.ReservedBy==30,"Cancellation fixture never claimed bush");cancelJob.CancelHarvest(cancel);cancel.Advance(100,1);
    Check(b.State==FoliageState.Available && b.ReservedBy==null && cancelJob.Phase==WoodcuttingPhase.Returned && !cancelJob.Loaded,"Cancellation leaked claim or cargo");
    var bushJob=new WoodcutterJob(3,1,-3,new MapPoint(3,10),new MapPoint(3,10),0,0);bushJob.ConfigureHarvest(resources,settings);
    var bushWorker=Staff(bushJob,31);var stopped=bushWorker.Movement.Position;
    resources.Carts.SetObstacle(999,stopped,1,true);bushWorker.Advance(1,1);
    Check(bushJob.WaitingForPath && MapPoint.Distance(stopped,bushWorker.Movement.Position)==0,"Blocked cart continued through an obstacle");
    resources.Carts.SetObstacle(999,stopped,0,false);bushWorker.Advance(1000,1);
    Check(bushJob.Phase==WoodcuttingPhase.Returned && bushJob.Loaded && b.State==FoliageState.Removed,"Bush harvesting or dynamic path retry failed");
    var deathTree=new FoliageResource(99,new MapPoint(10,10),.3f);
    var deathResources=new FoliageResources(new[]{deathTree},Flat(),settings);
    var deathJob=new WoodcutterJob(9,1,-9,new MapPoint(3,10),new MapPoint(3,10),0,0);deathJob.ConfigureHarvest(deathResources,settings);
    var dying=Staff(deathJob,99);
    for(int i=0;i<2000 && deathJob.Phase!=WoodcuttingPhase.Falling;i++)dying.Advance(.05f,1);
    Check(deathJob.Phase==WoodcuttingPhase.Falling,"Death fixture did not reach falling");dying.Remove();
    Check(dying.State==WorkerState.Removed && deathTree.State==FoliageState.LogPile && deathTree.LogsRemaining==6 && deathTree.ReservedBy==null,"Death during a fall lost the pile");
    var replacementJob=new WoodcutterJob(10,1,-10,new MapPoint(3,10),new MapPoint(3,10),0,0);replacementJob.ConfigureHarvest(deathResources,settings);
    var replacement=Staff(replacementJob,100);replacement.Advance(1000,1);
    Check(replacementJob.Target==deathTree && deathTree.LogsRemaining==5 && deathTree.ReservedBy==100,"Unclaimed pile was not reusable");
    nav=Flat(wall:true);a=new FoliageResource(1,new MapPoint(32.5f,10),.3f);b=new FoliageResource(2,new MapPoint(22,10),.3f);
    resources=new FoliageResources(new[]{a,b},nav,settings);
    Check(resources.TryReserveNearest(1,new MapPoint(29,10),out var reachable) && reachable.Resource==b,"Unreachable closer resource prevented a reachable target");
    nav=Flat();var point=new MapPoint(8.25f,8.25f);nav.SetObstacle(1,point,.4f,true);nav.SetObstacle(2,point,.4f,true);
    nav.SetObstacle(1,point,0,false);Check(!nav.IsWalkable(point),"Removing one overlapping obstacle opened another");
    nav.SetObstacle(2,point,0,false);Check(nav.IsWalkable(point),"Removal did not restore traversable ground");
    nav=Flat(wall:true);point=new MapPoint(31.25f,10.25f);nav.SetObstacle(1,point,.4f,true);nav.SetObstacle(1,point,0,false);
    Check(!nav.IsWalkable(point),"Removing foliage reopened a hard wall");
    nav=new NavigationGrid(8,1,1,Enumerable.Repeat(true,8).ToArray(),new float[8],1);point=new MapPoint(3.5f,.5f);
    nav.SetObstacle(1,point,.1f,true);nav.SetObstacle(2,point,.1f,true);
    Check(!nav.CanReach(new MapPoint(.5f,.5f),new MapPoint(7.5f,.5f)),"Obstacle did not split connectivity");
    nav.SetObstacle(1,point,0,false);Check(!nav.CanReach(new MapPoint(.5f,.5f),new MapPoint(7.5f,.5f)),"Connectivity ignored overlapping blockers");
    nav.SetObstacle(2,point,0,false);Check(nav.CanReach(new MapPoint(.5f,.5f),new MapPoint(7.5f,.5f)),"Incremental region merge failed");
  }
}
