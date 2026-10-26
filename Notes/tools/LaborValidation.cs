// Run in the isolated Unity project: -executeMethod LaborValidation.Run -quit
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Assets.Code.Simulation;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Simulation.Movement;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;

public static class LaborValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install="C:/Steam/steamapps/common/Stronghold 2";
  const string Output=Repo+"/Temp/LaborValidation";
  const string Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  public static void Run()
  {
    try
    {
      Directory.CreateDirectory(Output);File.Delete(Output+"/failure.txt");File.Delete(Output+"/pass.txt");TestLabor();TestWorkerComponents();TestPhysicalWalls();Debug.unityLogger.logEnabled=false;
      var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      var settings=new PopulationSettings();var buildings=new S2MBuildingAssetLoader().Load(map,Install,Converter,Output+"/Models");
      var nav=S2MNavigationBuilder.Build(map,settings,buildings);var plans=S2MRecruitmentPlan.Create(map,nav,settings);
      var labor=S2MLaborPlan.Create(map,nav);Debug.unityLogger.logEnabled=true;
      var woodJobs=labor.Jobs.OfType<WoodcutterJob>().ToArray();
      Check(map.WalkingLayer!=null && map.WalkingLayer.Width==256,"Walking grid missing");
      Check(map.WalkingLayer.GetClass(42,31)==0 && map.WalkingLayer.GetClass(42,28)==1,"Stockpile walking/blocked classes changed");
      Check(nav.IsWalkable(new MapPoint(42.5f,31.5f)) && !nav.IsWalkable(new MapPoint(42.5f,28.5f)),"Stockpile lanes/goods ignored");
      Check(nav.CanTravel(new MapPoint(38.5f,31.5f),new MapPoint(44.5f,31.5f)),"Cannot cross saved stockpile passage");
      var routeIssues=new List<string>();foreach(var p in plans.SelectMany(p=>p))
        if(!nav.CanReach(p.Exit,p.Seat))routeIssues.Add($"Recruit {p.Id}");
      File.WriteAllLines(Output+"/recruit-routes.txt",routeIssues.Prepend($"{200-routeIssues.Count}/200 reachable"));
      // Lowered rendered portcullises may isolate some hovels. They must wait rather than cross walls.
      Check(labor.Jobs.Count==8 && labor.Jobs.All(j=>!j.WorkerId.HasValue && !j.IsOccupied),"Jobs not vacant at start");
      Check(map.Actors.Count-labor.StartupActors.Count==8,"Wrong editor worker/cart suppression");
      Check(labor.StartupActors.Count(a=>a.Type=="HorseCart")==3,"Unrelated carts were removed");
      var data=new S2MActorAssetLoader().Load(map,Install,S2MCampfirePopulationLoader.Overrides(),converterPath:Converter,cacheDirectory:Output+"/Cache",
        placements:S2MCampfirePopulationLoader.CreateRecruitPlacements(plans,labor.Jobs));
      Check(data.Issues.Count==0,"Labor assets: "+string.Join(";",data.Issues));
      var horse=data.Batches.First(b=>b.Asset.ModelPath.EndsWith("/cart_horse.GR2",StringComparison.OrdinalIgnoreCase));
      var rider=data.Batches.First(b=>b.Asset.Variant==S2MWoodcutterAssets.Riding);
      Check(Math.Abs(horse.Asset.LocalOffset.z-2.5f)<.001 && Math.Abs(rider.Asset.LocalOffset.z+.7f)<.001,"Horse-only alignment moved the rider");
      var custom=S2MWoodcutterAssets.Overrides(new HorseCartAssemblySettings{HorseForwardOffset=3}).ToArray();
      Check(custom.First(a=>a.ObjectType=="JobWoodcutterCart").LocalOffset.z==3 && custom.First(a=>a.ObjectType=="JobWoodcutterRide").LocalOffset.z==-.7f,"Custom horse offset moved rider");
      var wheels=data.Batches.First(b=>b.Asset.ModelPath.EndsWith("cart_horse_wheels.GR2",StringComparison.OrdinalIgnoreCase));
      Vector3 axle=Quaternion.Euler(wheels.Asset.LocalEulerAngles)*Vector3.right;
      Check(Math.Abs(axle.y)<.001,"Cart wheels still horizontal");
      var wheelBounds=Granny2ModelRenderer.GetLocalBounds(wheels.Model,.01f);
      Check(wheels.Asset.LocalOffset.y-wheelBounds.extents.y>=0 && wheels.Asset.LocalOffset.y-wheelBounds.extents.y<.05f,"Wheel ground clearance");
      var cartBody=data.Batches.First(b=>b.Asset.ModelPath.EndsWith("cart_horse_cart.GR2",StringComparison.OrdinalIgnoreCase));
      Check(cartBody.Asset.UseBindPose && wheels.Asset.UseBindPose,"Rigid cart parts still use authoring animation transforms");
      Check(cartBody.Motion.Matrices[5]==1 && cartBody.Motion.Matrices[10]==1 && cartBody.Motion.Matrices[14]==0,"Cart body bind transform was not restored");
      var deck=Quaternion.Euler(cartBody.Asset.LocalEulerAngles)*new Vector3(0,1.15f,0)+cartBody.Asset.LocalOffset;
      Check(Math.Abs(deck.y-1.15f)<.001,"Cart body rotation moved deck off its pivot");
      var clock=new SimulationClock();var root=new GameObject("Labor validation");var controller=root.AddComponent<S2MPopulationController>();
      controller.Initialize(map,nav,plans,data,new S2MTerrainSettings(),new S2MActorRenderSettings(),settings,clock,jobs:labor.Jobs);
      var renderer=root.GetComponentInChildren<S2MActorRenderer>();
      Check(Visible(renderer,woodJobs.Select(j=>j.CartActorId))==0,"Vacant carts visible at start");
      controller.Advance(6);Check(labor.Jobs.All(j=>!j.IsOccupied),"Workers occupied distant jobs immediately");
      var assigned=controller.Kingdoms.SelectMany(k=>k.Peasants).Where(p=>p.Job!=null).ToArray();
      Check(assigned.All(p=>p.State==PeasantState.WalkingToJob),"Assignment does not transform/enroute");
      int births=controller.Kingdoms.Sum(k=>k.SpawnedCount);controller.Advance(0);Check(births==controller.Kingdoms.Sum(k=>k.SpawnedCount),"Zero-time update advanced population");
      controller.Advance(700);
      File.WriteAllLines(Output+"/jobs.txt",labor.Jobs.Select(j=>$"Building={j.BuildingId} estate={j.EstateId} worker={j.WorkerId} occupied={j.IsOccupied}"));
      var reachableJobs=labor.Jobs.Where(j=>plans.SelectMany(p=>p).Any(p=>p.EstateId==j.EstateId && nav.CanReach(p.Seat,j.Approach))).ToArray();
      Check(reachableJobs.All(j=>j.IsOccupied),"A reachable saw pit was never staffed");
      Check(labor.Jobs.Except(reachableJobs).All(j=>!j.WorkerId.HasValue && !j.IsOccupied),"Assigned a worker across a closed gate or wall");
      var workers=controller.Kingdoms.SelectMany(k=>k.Peasants).Where(p=>p.Job!=null).ToArray();
      Check(workers.Length==reachableJobs.Length && workers.Select(p=>p.Id).Distinct().Count()==workers.Length,"Double assignment");
      Check(workers.All(p=>p.State==PeasantState.Working && p.EstateId==p.Job.EstateId),"Cross-estate labor or incomplete boarding");
      ValidateTreasuryRoutes(map,buildings,nav,workers);
      Check(Visible(renderer,woodJobs.Select(j=>j.CartActorId))==reachableJobs.Length*3,"Cart assembly parts not visible together");
      // Render one staffed cart with its worker to inspect axes and assembly alignment.
      foreach(var p in controller.Kingdoms.SelectMany(k=>k.Peasants))renderer.HideActor(p.Id);
      foreach(var j in woodJobs)renderer.HideActor(j.CartActorId);
      var worker=workers[0];renderer.SetVariant(worker.Id,S2MWoodcutterAssets.Riding);renderer.SetTransform(worker.Id,Vector3.zero,Quaternion.identity,Vector3.one);
      var cartJob=(WoodcutterJob)worker.Job;
      renderer.SetVariant(cartJob.CartActorId,S2MWoodcutterAssets.Cart);renderer.SetTransform(cartJob.CartActorId,Vector3.zero,Quaternion.identity,Vector3.one);
      var camera=new GameObject("Cart camera").AddComponent<Camera>();camera.transform.position=new Vector3(6,4,-7);camera.transform.LookAt(new Vector3(0,1,0));
      camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.18f,.2f);
      var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.ambientLight=Color.gray;
      typeof(S2MActorRenderer).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(renderer,null);
      var target=new RenderTexture(1000,800,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
      var image=new Texture2D(1000,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Output+"/cart.png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
      UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
      File.WriteAllText(Output+"/pass.txt",$"PASS: generic job/worker/movement components; walk/run speeds and instant arrival; horse-only offset with stationary rider; treasury walls and stockpile passage; {200-routeIssues.Count}/200 recruit routes; {reachableJobs.Length}/8 saw pits staffed; cart bind poses; scheduling and pause.");
    }
    catch(Exception e){Debug.unityLogger.logEnabled=true;File.WriteAllText(Output+"/failure.txt",e.ToString());throw;}
  }
  static int Visible(S2MActorRenderer renderer,IEnumerable<int> ids)
  {
    var actors=(System.Collections.IDictionary)typeof(S2MActorRenderer).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);int count=0;
    foreach(int id in ids)foreach(var piece in (System.Collections.IEnumerable)actors[id])
    {var type=piece.GetType();var batch=type.GetField("Item1").GetValue(piece);int index=(int)type.GetField("Item2").GetValue(piece);if(((bool[])batch.GetType().GetField("Visible").GetValue(batch))[index])count++;}return count;
  }
  static void Set(object target,string property,object value)=>target.GetType().GetProperty(property).SetValue(target,value);
  static void TestPhysicalWalls()
  {
    const int size=16;var map=new S2MFile{MapSize=size};
    Set(map,"HeightLayer",new HeightLayer{Width=size,Height=size,Heights=new float[size*size]});
    var walking=new WalkingLayer();Set(walking,"Width",size);Set(walking,"Height",size);
    var layers=new[]{new ushort[size*size],new ushort[size*size],new ushort[size*size]};layers[0][9*size+6]=4;
    Set(walking,"Layers",layers);Set(map,"WalkingLayer",walking);
    var wall=new WallLayer();Set(wall,"Width",size);Set(wall,"Height",size);var cells=new WallCell[size*size];
    cells[9*size+6]=new WallCell(2048,2048,2048,2048,1,0,0,0,0,0,0,1);Set(wall,"Cells",cells);Set(map,"WallLayer",wall);
    var building=new BuildingPlacement();Set(building,"Id",1);Set(building,"Type","Treasury");Set(building,"RawX",4096f);Set(building,"RawZ",4096f);
    Set(building,"FootprintWidth",4);Set(building,"FootprintHeight",4);Set(map,"BuildingPlacements",new[]{building});
    var vertices=new List<float>();var indices=new List<int>();
    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
      int first=vertices.Count/8;foreach(var p in new[]{a,b,c,d})vertices.AddRange(new[]{p.x*204.8f,p.z*204.8f,p.y*204.8f,0f,0f,1f,0f,0f});
      indices.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
    }
    Quad(new Vector3(0,0,0),new Vector3(0,0,4),new Vector3(0,2,4),new Vector3(0,2,0));
    Quad(new Vector3(4,0,0),new Vector3(4,2,0),new Vector3(4,2,4),new Vector3(4,0,4));
    Quad(new Vector3(0,0,4),new Vector3(4,0,4),new Vector3(4,2,4),new Vector3(0,2,4));
    Quad(new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,2,0),new Vector3(0,2,0));
    Quad(new Vector3(3,0,0),new Vector3(4,0,0),new Vector3(4,2,0),new Vector3(3,2,0));
    Quad(new Vector3(0,2,0),new Vector3(4,2,0),new Vector3(4,2,4),new Vector3(0,2,4));
    var mesh=new Granny2MeshData();Set(mesh,"VertexData",vertices.ToArray());Set(mesh,"Indices",indices.ToArray());
    var model=new Granny2ModelData();Set(model,"Meshes",new[]{mesh});
    var batch=new S2MPlacedBuildingBatch();Set(batch,"Placements",new[]{building});Set(batch,"Closed",model);Set(batch,"Collision",model);
    var data=new S2MBuildingData();Set(data,"Map",map);Set(data,"Keeps",new S2MKeepData());Set(data,"Batches",new[]{batch});
    var nav=S2MNavigationBuilder.Build(map,new PopulationSettings(),data);
    Check(nav.IsWalkable(new MapPoint(6,6)),"Roof blocked usable treasury interior");
    Check(nav.CanTravel(new MapPoint(6,3),new MapPoint(6,6)),"Treasury doorway blocked");
    Check(!nav.CanTravel(new MapPoint(6,6),new MapPoint(6,8.75f)),"Route crossed treasury rear wall");
    Check(!nav.IsWalkable(new MapPoint(6.5f,9.5f)),"Saved floor class reopened a map wall");
    Check(nav.TryFindPath(new MapPoint(6,6),new MapPoint(6,10.5f),out var path) && path.Any(p=>p.Z<4),"Treasury route failed to return through doorway");
    foreach(var p in path)Check(!(p.X>4.2f && p.X<7.8f && p.Z>7.8f && p.Z<8.2f),"Smoothed route crossed rear wall");
  }
  static void ValidateTreasuryRoutes(S2MFile map,S2MBuildingData buildings,NavigationGrid nav,Peasant[] workers)
  {
    var roots=new List<GameObject>();
    foreach(var batch in buildings.Batches)
    {
      var placements=batch.Placements.Where(p=>p.Type=="Treasury").ToArray();if(placements.Length==0)continue;
      var instances=placements.Select(p=>new Granny2ModelInstance("Treasury",new Vector3(p.MapX,p.RawY/1024,p.MapZ),new Vector3(0,p.RotationDegrees,0),Vector3.one)).ToArray();
      var root=Granny2ModelRenderer.RenderInstances(batch.Closed,instances,settings:new Granny2ModelRenderSettings{UnitScale=1/204.8f,UseFoliageMaterials=false});roots.Add(root);
      foreach(var filter in root.GetComponentsInChildren<MeshFilter>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
    }
    Physics.SyncTransforms();int segments=0;bool hitBackfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
    foreach(var worker in workers)
    {
      Check(nav.TryFindPath(worker.Seat,worker.Job.Approach,out var path),"Worker job route lost");
      for(int i=1;i<path.Count;i++)
      {
        var a=path[i-1];var b=path[i];
        Vector3 from=new(a.X,S2MNavigationBuilder.SampleHeight(map,a.X,a.Z)/1024+.6f,a.Z),to=new(b.X,S2MNavigationBuilder.SampleHeight(map,b.X,b.Z)/1024+.6f,b.Z);
        Check(!Physics.Linecast(from,to,out var hit),"Worker route crosses actual treasury collider: "+(hit.collider!=null ? hit.collider.name : ""));segments++;
      }
    }
    File.WriteAllText(Output+"/treasury-routes.txt",$"PASS: {segments} route segments checked against chapter 8 treasury MeshColliders.");
    foreach(var root in roots)UnityEngine.Object.DestroyImmediate(root);
    Physics.queriesHitBackfaces=hitBackfaces;
  }
  static void TestLabor()
  {
    var walk=Enumerable.Repeat(true,100).ToArray();walk[99]=false;var nav=new NavigationGrid(10,10,1,walk,new float[100],.4f);
    var settings=new PopulationSettings{SpawnIntervalSeconds=.25f,JobAssignmentIntervalSeconds=2,RunSpeedCellsPerSecond=2,WalkSpeedCellsPerSecond=1};
    var p=new Peasant(-1,1,false,new MapPoint(.5f,.5f),new MapPoint(.5f,.5f),new MapPoint(.5f,.5f),0,0,1);
    var k=new PeasantRecruitment(new[]{p},nav,settings,1);k.Advance(1);
    var wrong=new WoodcutterJob(2,2,-100,new MapPoint(2.5f,.5f),new MapPoint(2.5f,.5f),0,0);
    var unreachable=new WoodcutterJob(3,1,-101,new MapPoint(9.5f,9.5f),new MapPoint(9.5f,9.5f),0,0);
    var job=new WoodcutterJob(4,1,-102,new MapPoint(2.5f,.5f),new MapPoint(2.5f,.5f),0,0);
    var other=new WoodcutterJob(5,1,-103,new MapPoint(3.5f,.5f),new MapPoint(3.5f,.5f),0,0);
    var labor=new LaborManager(new[]{wrong,unreachable,job,other},new[]{k},nav,settings);
    labor.Advance(0);labor.Advance(1.99f);Check(p.Job==null,"Custom job interval ignored");labor.Advance(.02f);
    Check(p.Job==job && !wrong.WorkerId.HasValue && !unreachable.WorkerId.HasValue && !other.WorkerId.HasValue,"Invalid/duplicate assignment");
    var position=p.Position;labor.Advance(0);Check(MapPoint.Distance(position,p.Position)==0,"Paused job moved");
    labor.Advance(4);Check(job.IsOccupied && p.State==PeasantState.Working,"Job travel/instant arrival failed");
  }
  sealed class WorkCycleFixture : LaborJob
  {
    public override LaborRole Role=>LaborRole.Woodcutter;
    public bool Arrived;
    public float Worked;
    public WorkCycleFixture():base(10,1,new MapPoint(2.5f,.5f),new MapPoint(4.5f,.5f),90,0) { }
    protected override void OnWorkerArrived(WorkerComponent worker) { base.OnWorkerArrived(worker);Arrived=true; }
    public override void AdvanceWork(WorkerComponent worker,float seconds) { Worked+=seconds; }
  }
  static void TestWorkerComponents()
  {
    var movement=new MovementComponent(new MapPoint(.5f,.5f),0);var worker=new WorkerComponent(12,movement);var job=new WorkCycleFixture();
    Check(worker.TryAssign(job,new[]{movement.Position,job.Approach}),"Generic job assignment failed");
    worker.Advance(1,1);Check(worker.State==WorkerState.Traveling && !job.IsOccupied && Math.Abs(movement.Position.X-1.5f)<.001,"Worker travel speed ignored");
    worker.Advance(1,1);Check(worker.State==WorkerState.Working && job.Arrived && job.IsOccupied && MapPoint.Distance(movement.Position,job.WorkPosition)<.001 && worker.StateTime==0,"Arrival is not instantaneous on the terminal movement step");
    worker.Advance(.5f,1);Check(Math.Abs(job.Worked-.5f)<.001,"Subclass work cycle did not receive remaining time");
    var settings=new PopulationSettings();
    var walking=new MovementComponent(new MapPoint(.5f,.5f),0);var running=new MovementComponent(walking.Position,0);
    var route=new[]{walking.Position,new MapPoint(8.5f,.5f)};walking.Follow(route);running.Follow(route);float walkTime=1,runTime=1;
    walking.Advance(ref walkTime,settings.WalkSpeedCellsPerSecond);running.Advance(ref runTime,settings.RunSpeedCellsPerSecond);
    Check(Math.Abs(walking.Position.X-.5f-settings.WalkSpeedCellsPerSecond)<.001 && running.Position.X>walking.Position.X,"Walk/run controls are not independent");
    var second=new WorkerComponent(13,new MovementComponent(new MapPoint(.5f,.5f),0));
    Check(!second.TryAssign(job,new[]{second.Movement.Position,job.Approach}),"Occupied job accepted another worker");
  }
}
