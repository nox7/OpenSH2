// Run in the isolated Unity project: -executeMethod RecruitmentValidation.Run -quit
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Assets.Code.Simulation;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;
using UnityEditor;

public static class RecruitmentValidation
{
  const string Repo="C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install="C:/Steam/steamapps/common/Stronghold 2";
  const string Output=Repo+"/Temp/RecruitmentValidation";
  const string Converter=Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
  static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
  public static void Run()
  {
    try
    {
      Directory.CreateDirectory(Output); TestNavigation(); TestRecruitment();
      Debug.unityLogger.logEnabled=false;
      var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      Debug.unityLogger.logEnabled=true;
      var settings=new PopulationSettings();
      var buildings=new S2MBuildingAssetLoader().Load(map,Install,Converter,Output+"/Models");
      var nav=S2MNavigationBuilder.Build(map,settings,buildings);
      var plans=S2MRecruitmentPlan.Create(map,nav,settings);
      Check(plans.Select(p=>p.Count).SequenceEqual(new[]{8,48,16,88,40}),"Estate capacities changed");
      Check(plans.All(p=>p.Take(8).All(a=>!a.FromHovel) && p.Skip(8).All(a=>a.FromHovel)),"Spawn source ordering");
      var issues=new List<string>();
      foreach(var plan in plans)foreach(var p in plan)
      {
        Check(nav.IsWalkable(p.Seat) && nav.IsWalkable(p.Exit),"Seat or exit is obstructed");
        if(!nav.TryFindPath(p.Exit,p.Seat,out var path))issues.Add($"NO ROUTE id={p.Id} building={p.SourceBuildingId} start={p.Exit.X},{p.Exit.Z} goal={p.Seat.X},{p.Seat.Z}");
        else for(int i=1;i<path.Count;i++)Check(nav.CanTravel(path[i-1],path[i]),"Unsafe smoothed route");
      }
      File.WriteAllLines(Output+"/routes.txt",issues.Prepend($"{200-issues.Count}/200 reachable"));
      Check(issues.Count==0,"Real map recruitment routes are unreachable; see routes.txt");
      var data=new S2MActorAssetLoader().Load(map,Install,S2MCampfirePopulationLoader.Overrides(),converterPath:Converter,
        cacheDirectory:Output+"/Cache",placements:S2MCampfirePopulationLoader.CreateRecruitPlacements(plans));
      Check(data.Issues.Count==0 && data.Batches.Count==7,"Recruitment clips: "+string.Join(";",data.Issues));
      var stand=data.Batches.First(b=>b.Asset.AnimationPath==S2MCampfirePopulationLoader.StandClip).Motion;
      File.WriteAllLines(Output+"/clips.txt",data.Batches.Select(b=>b.Asset.AnimationPath+" duration="+b.Motion.Duration));
      var clock=new SimulationClock(); var root=new GameObject("Recruitment test");
      var controller=root.AddComponent<S2MPopulationController>();
      controller.Initialize(map,nav,plans,data,new S2MTerrainSettings(),new S2MActorRenderSettings(),settings,clock);
      var renderer=root.GetComponentInChildren<S2MActorRenderer>(); Check(Visible(renderer)==0,"Preallocated peasants visible at load");
      controller.Advance(4.99f);Check(controller.Kingdoms.All(k=>k.SpawnedCount==0),"Early spawn");
      controller.Advance(.02f);Check(controller.Kingdoms.All(k=>k.SpawnedCount==1),"First timed spawn");
      Check(Visible(renderer)==5,"Clip variants rendered simultaneously");
      controller.Advance(35);Check(controller.Kingdoms.All(k=>k.SpawnedCount==8),"First eight timing");
      controller.Advance(5);Check(controller.Kingdoms.Skip(1).All(k=>k.Peasants[8].State==PeasantState.HovelSeated),"Ninth peasant did not begin seated in hovel");
      var ninth=controller.Kingdoms[1].Peasants[8];
      controller.Advance(settings.HovelSeatedSeconds);Check(ninth.State==PeasantState.StandingUp,"Missing stand-up stage");
      clock.IsPaused=true;clock.Advance(60);controller.Advance(clock.DeltaTime);
      Check(ninth.State==PeasantState.StandingUp && ninth.StateTime<.02,"Pause advanced recruitment");
      clock.IsPaused=false;controller.Advance(stand.Duration+.1f);
      Check(ninth.State==PeasantState.LeavingHovel || ninth.State==PeasantState.Running || ninth.State==PeasantState.WaitingForPath,"Stand-up never completed");
      controller.Advance(600);Check(controller.Kingdoms.All(k=>k.SpawnedCount==k.Peasants.Count),"Recruitment never filled capacity");
      File.WriteAllLines(Output+"/states.txt",controller.Kingdoms.SelectMany(k=>k.Peasants).GroupBy(p=>p.State).Select(g=>g.Key+"="+g.Count()));
      Check(Visible(renderer)==200,"Lost or duplicate visual variants");
      var keepData=new S2MKeepAssetLoader().Load(map,Install,Converter,Output+"/Models");
      var keeps=S2MKeepRenderer.Render(keepData,settings:new S2MBuildingRenderSettings{ViewMode=BuildingViewMode.Closed});
      var camera=new GameObject("Recruitment camera").AddComponent<Camera>();
      camera.transform.position=new Vector3(66,7,178);camera.transform.LookAt(new Vector3(60.5f,0,187.5f));
      camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.18f,.2f);
      var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-25,0);
      RenderSettings.ambientLight=Color.gray;
      var tick=typeof(S2MActorRenderer).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
      tick.Invoke(renderer,null);var target=new RenderTexture(900,700,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
      var image=new Texture2D(900,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,700),0,0);image.Apply();File.WriteAllBytes(Output+"/compact-rings.png",image.EncodeToPNG());
      RenderTexture.active=null;camera.targetTexture=null;
      UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(keeps);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
      File.WriteAllText(Output+"/pass.txt","PASS: A* detours/corners/cliffs/unreachable paths; timed keep/hovel recruitment, seating/stand-up/run variants, pause, capacity and visibility. Real map route failures="+issues.Count);
    }
    catch(Exception e){Debug.unityLogger.logEnabled=true;File.WriteAllText(Output+"/failure.txt",e.ToString());throw;}
  }
  static int Visible(S2MActorRenderer renderer)
  {
    var draws=(System.Collections.IEnumerable)typeof(S2MActorRenderer).GetField("draws",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
    int count=0;foreach(var draw in draws)count+=((bool[])draw.GetType().GetField("Visible").GetValue(draw)).Count(v=>v);return count;
  }
  static void TestNavigation()
  {
    var walk=Enumerable.Repeat(true,100).ToArray();var height=new float[100];for(int z=0;z<8;z++)walk[z*10+4]=false;
    var grid=new NavigationGrid(10,10,1,walk,height,.4f);
    Check(grid.TryFindPath(new MapPoint(1.5f,1.5f),new MapPoint(8.5f,1.5f),out var path),"Detour missing");
    Check(path.Any(p=>p.Z>=8),"Route crossed barrier");
    for(int i=1;i<path.Count;i++)Check(grid.CanTravel(path[i-1],path[i]),"Corner cut");
    var corners=new NavigationGrid(2,2,1,new[]{true,false,false,true},new float[4],.4f);
    Check(!corners.TryFindPath(new MapPoint(.5f,.5f),new MapPoint(1.5f,1.5f),out _),"Diagonal corner cutting");
    var cliff=new NavigationGrid(2,1,1,new[]{true,true},new[]{0f,1f},.4f);
    Check(!cliff.TryFindPath(new MapPoint(.5f,.5f),new MapPoint(1.5f,.5f),out _),"Cliff climbed");
  }
  static void TestRecruitment()
  {
    var grid=new NavigationGrid(20,20,1,Enumerable.Repeat(true,400).ToArray(),new float[400],.4f);
    var settings=new PopulationSettings{SpawnIntervalSeconds=5,HovelSeatedSeconds=2};
    IReadOnlyList<Peasant> Plan()=>Enumerable.Range(0,10).Select(i=>new Peasant(-i-1,1,i>=8,new MapPoint(1.5f,1.5f),new MapPoint(2.5f,1.5f),new MapPoint(8.5f+i*.2f,8.5f),0,0)).ToArray();
    var large=new PeasantRecruitment(Plan(),grid,settings,1);var small=new PeasantRecruitment(Plan(),grid,settings,1);
    large.Advance(51);for(int i=0;i<510;i++)small.Advance(.1f);
    Check(large.SpawnedCount==10 && small.SpawnedCount==10,"Frame-dependent recruitment count");
    for(int i=0;i<10;i++)Check(large.Peasants[i].State==small.Peasants[i].State && MapPoint.Distance(large.Peasants[i].Position,small.Peasants[i].Position)<.002,"Frame-dependent movement");
    var compact=new CampfirePopulation(1,1,2,settings);Check(compact.Seats.Count(p=>p.Ring==0)==8,"Initial compact ring capacity");
    Check(Math.Abs(MapPoint.Distance(new MapPoint(0,0),new MapPoint(compact.Seats[8].X,compact.Seats[8].Z))-1.45f)<.001,"Ring spacing");
  }
}
