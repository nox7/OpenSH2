// Scratch project: -batchmode -executeMethod PlayerCameraUnityValidation.Run, without -quit.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class PlayerCameraUnityValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/PlayerCameraValidation",Session="PlayerCameraValidation";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly string[] Titles={"The Search for the King","First Command","Edwin's Rescue","The Bull in the Borderlands","Return to the Monastery","Olaf Grimtooth, Your Time Has Come","Defense of the Abbey","Hawks Nest","Lord Barclay's Castle","Siege of the Abbey","Treachery","To Capture a King"};
  static readonly Vector2[] Sizes={new(1024,768),new(1920,1080),new(600,900)};
  static int stage,frame,index,layoutIndex,visits;
  static double started;
  static object firstIntro;
  static RenderTexture target;
  static Texture2D previewTexture;
  static PlayerCameraUnityValidation()
  {
    if(!SessionState.GetBool(Session,false))return;
    started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  public static void Run()
  {
    Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    new GameObject("Main").AddComponent<Main>();var camera=new GameObject("Camera").AddComponent<Camera>();
    camera.gameObject.AddComponent<AudioListener>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
    SessionState.SetBool(Session,true);EditorApplication.EnterPlaymode();
  }
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
  static object Field(object o,string name)=>o.GetType().GetField(name,Private).GetValue(o);
  static void Finish(bool ok,string text)
  {File.WriteAllText(Output+"/result.txt",(ok?"PASS: ":"FAIL: ")+text);SessionState.SetBool(Session,false);EditorApplication.Exit(ok?0:1);}
  static void Log(string text,string stack,LogType type)
  {
    if(text.StartsWith("Embedded Bink playback failed:")||text.StartsWith("Unable to load UI artwork")||(!stack.Contains("UnityEditor.Search.")&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)))Finish(false,text+"\n"+stack);
  }
  static void Submit(Button button){button.Focus();using var evt=NavigationSubmitEvent.GetPooled();button.SendEvent(evt);}
  static void Hover(VisualElement button,bool entering)
  {
    if(entering){using var evt=PointerEnterEvent.GetPooled();evt.target=button;button.SendEvent(evt);}
    else{using var evt=PointerLeaveEvent.GetPooled();evt.target=button;button.SendEvent(evt);}
  }
  static void Capture(string name)
  {
    var previous=RenderTexture.active;RenderTexture.active=target;
    var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
    RenderTexture.active=previous;File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
  }
  static object Page(object controller)
  {
    var screens=(System.Collections.IDictionary)Field(Field(controller,"screenRouter"),"screens");
    foreach(var page in screens.Values)if(page.GetType().Name=="CampaignSelectionScreen")return page;
    throw new Exception("Missing campaign page");
  }
  static void ProfileChecks()
  {
    var path=Output+"/profile-"+Guid.NewGuid().ToString("N")+".json";
    var store=new Assets.Code.UserData.UserProfileStore(path);
    Assets.Code.UserData.CampaignProgress.CompleteAllForLocalTesting=false;
    var progress=new Assets.Code.UserData.CampaignProgress(store);
    Check(progress.IsVisible(1)&&!progress.IsVisible(2),"Fresh profile visibility");
    store.Profile.playerName="Test Lord";store.Profile.completedConquestTrials.Add("trial-1");
    store.RecordWarChapterAttempt(2);store.CompleteWarChapter(1);
    store.RecordWarChapterAttempt(2);
    store=new Assets.Code.UserData.UserProfileStore(path);
    Check(store.Profile.playerName=="Test Lord"&&store.Profile.completedConquestTrials.Contains("trial-1")&&store.Profile.completedWarChapters.Contains(1)&&store.Profile.lastAttemptedWarChapter==2,"Profile round trip");
    Check(new Assets.Code.UserData.CampaignProgress(store).IsVisible(2),"Current chapter hidden");
    File.WriteAllText(path,"invalid json");
    Check(new Assets.Code.UserData.UserProfileStore(path).Profile.completedWarChapters.Contains(1),"Backup recovery");
    Assets.Code.UserData.CampaignProgress.CompleteAllForLocalTesting=true;
    Check(!store.Profile.completedWarChapters.Contains(12)&&new Assets.Code.UserData.CampaignProgress(store).IsCompleted(12),"Testing override contaminated data");
  }
  static void CameraChecks()
  {
    var world=new GameObject("Camera physics fixture");
    var ground=new GameObject("Terrain fixture");ground.transform.SetParent(world.transform);
    var terrainMesh=new Mesh{vertices=new[]{new Vector3(-100,0,-100),new Vector3(-100,0,100),new Vector3(100,0,100),new Vector3(100,0,-100)},triangles=new[]{0,1,2,0,2,3}};
    ground.AddComponent<MeshCollider>().sharedMesh=terrainMesh;ground.AddComponent<Assets.Code.Player.CameraSurface>().IsTerrain=true;
    var building=GameObject.CreatePrimitive(PrimitiveType.Cube);building.transform.SetParent(world.transform);building.transform.position=new Vector3(20,4,20);building.transform.localScale=new Vector3(6,8,6);building.AddComponent<Assets.Code.Player.CameraSurface>();
    for(int i=0;i<70;i++){var foliage=GameObject.CreatePrimitive(PrimitiveType.Cube);foliage.name="Excluded foliage or unit";foliage.transform.SetParent(world.transform);foliage.transform.position=new Vector3(0,2+i,0);}
    var settings=new Assets.Code.Player.PlayerCameraSettings{InitialSurfaceDistance=40,MinSurfaceDistance=10,MaxSurfaceDistance=80,InitialMoveSpeed=30,MaxMoveSpeed=30};
    var session=new Assets.Code.Player.LocalPlayerSession(world.transform,new Bounds(Vector3.zero,new Vector3(200,150,200)),Vector3.zero,1,settings);
    var c=session.Controls;
    Check(c.TrySurfaceRay(new Ray(new Vector3(0,100,0),Vector3.down),150,out var hit)&&hit.collider.gameObject==ground,"Foliage/units affected surface ray or saturated buffer");
    Check(c.TrySurfaceRay(new Ray(new Vector3(20,100,20),Vector3.down),150,out hit)&&hit.collider.gameObject==building,"Building roof not detected");
    Check(c.TrySurfaceRay(new Ray(new Vector3(20,100,20),Vector3.down),150,out hit,terrainOnly:true)&&hit.collider.gameObject==ground,"Terrain pivot query counted a building");
    var start=c.Focus;c.ApplyInput(Vector2.up,0,.5f);Check(Vector3.Distance(c.Focus-start,new Vector3(0,0,15))<.01,"Forward movement with pitch");
    settings.Yaw=90;start=c.Focus;c.ApplyInput(Vector2.up,0,.5f);Check(Vector3.Distance(c.Focus-start,new Vector3(15,0,0))<.01,"Forward did not follow camera yaw");
    start=c.Focus;c.ApplyInput(Vector2.right,0,.5f);Check(Vector3.Distance(c.Focus-start,new Vector3(0,0,-15))<.01,"Right did not follow camera yaw");
    start=c.Focus;c.ApplyInput(Vector2.one,0,.5f);Check(Mathf.Abs(Vector3.Distance(start,c.Focus)-15)<.01,"Diagonal movement is faster");
    c.ApplyInput(Vector2.zero,1000,0);Check(Mathf.Abs(c.SurfaceDistance-10)<.01,"Minimum zoom clamp");
    c.ApplyInput(Vector2.zero,-1000,0);Check(Mathf.Abs(c.SurfaceDistance-80)<.01,"Maximum zoom clamp");
    settings.Pitch=35;settings.FieldOfView=40;c.ApplyInput(Vector2.zero,0,0);Check(Mathf.Abs(c.transform.eulerAngles.x-35)<.01&&Mathf.Abs(c.ViewCamera.fieldOfView-40)<.01,"Live angle/FOV adjustment");
    c.ApplyInput(new Vector2(100,100),0,100);Check(Mathf.Abs(c.Focus.x)<=100.01&&Mathf.Abs(c.Focus.z)<=100.01,"Map bounds overflow");
    settings.Yaw=0;settings.Pitch=50;c.Initialize(settings,world.transform,new Bounds(Vector3.zero,new Vector3(200,150,200)),Vector3.zero,1);
    float altitude=c.transform.position.y;
    c.ApplyInput(Vector2.zero,0,.5f,1);Check(Mathf.Abs(Mathf.DeltaAngle(c.transform.eulerAngles.y,45))<.01,"E rotation speed");
    c.ApplyInput(Vector2.zero,0,.5f,-1);Check(Mathf.Abs(Mathf.DeltaAngle(c.transform.eulerAngles.y,0))<.01,"Q rotation direction");
    c.ApplyInput(Vector2.zero,0,0,0,100);Check(Mathf.Abs(Mathf.DeltaAngle(c.transform.eulerAngles.y,-20))<.01,"Middle drag rotation sensitivity");
    c.ApplyInput(Vector2.zero,0,0,0,-100);Check(Mathf.Abs(c.transform.position.y-altitude)<.01&&Mathf.Abs(c.transform.eulerAngles.x-50)<.01,"Yaw rotation changed pitch/altitude");
    var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.transform.SetParent(world.transform);roof.transform.position=new Vector3(0,4,30);roof.transform.localScale=new Vector3(12,8,30);roof.AddComponent<Assets.Code.Player.CameraSurface>();Physics.SyncTransforms();
    c.ApplyInput(Vector2.up,0,1);Check(Mathf.Abs(c.transform.position.y-altitude)<.01,"Panning onto a roof changed altitude within zoom limits");
    Check(c.SurfaceDistance>10&&c.SurfaceDistance<40,"Roof did not change measured ray distance");
    var terrainPivot=c.Focus;Check(Mathf.Abs(terrainPivot.y)<.01,"Roof became rotation pivot");
    var beforeOrbit=c.transform.position;var expectedOrbit=terrainPivot+Quaternion.AngleAxis(45,Vector3.up)*(beforeOrbit-terrainPivot);
    c.ApplyInput(Vector2.zero,0,.5f,1);Check(Vector3.Distance(c.transform.position,expectedOrbit)<.01&&Vector3.Distance(c.Focus,terrainPivot)<.01,"Q/E orbit used roof instead of terrain");
    c.ApplyInput(Vector2.zero,0,.5f,-1);Check(Vector3.Distance(c.transform.position,beforeOrbit)<.01,"Terrain orbit round trip");
    expectedOrbit=terrainPivot+Quaternion.AngleAxis(-20,Vector3.up)*(beforeOrbit-terrainPivot);
    c.ApplyInput(Vector2.zero,0,0,0,100);Check(Vector3.Distance(c.transform.position,expectedOrbit)<.01,"Middle drag used roof pivot");
    c.ApplyInput(Vector2.zero,0,0,0,-100);
    settings.MinSurfaceDistance=35;c.ApplyInput(Vector2.zero,0,0);Check(c.transform.position.y>altitude&&Mathf.Abs(c.SurfaceDistance-35)<.01,"Roof minimum did not raise camera");
    settings.MinSurfaceDistance=10;settings.MaxSurfaceDistance=25;c.ApplyInput(Vector2.zero,0,0);Check(Mathf.Abs(c.SurfaceDistance-25)<.01,"Maximum limit adjustment");
    altitude=c.transform.position.y;settings.MaxSurfaceDistance=80;c.ApplyInput(Vector2.down,0,1);Check(Mathf.Abs(c.transform.position.y-altitude)<.01,"Panning off a roof changed altitude within zoom limits");
    UnityEngine.Object.DestroyImmediate(roof);Physics.SyncTransforms();
    Check(new Assets.Code.Player.PlayerCameraSettings().ZoomSpeed==40,"Default wheel increment is not five times larger");
    settings.InitialSurfaceDistance=65;c.Initialize(settings,world.transform,new Bounds(Vector3.zero,new Vector3(200,150,200)),Vector3.zero,1);c.ApplyInput(Vector2.zero,1,0);Check(Mathf.Abs(c.SurfaceDistance-25)<.01,"Wheel notch increment not applied");
    c.transform.SetPositionAndRotation(new Vector3(130,40,130),Quaternion.Euler(50,0,0));
    c.ApplyInput(Vector2.zero,0,0);var edgePivot=c.Focus;Check(Mathf.Abs(edgePivot.y)<.01&&edgePivot.x>100&&edgePivot.z>100,"Off-map pivot did not use lowest terrain plane");
    var edgeCamera=c.transform.position;expectedOrbit=edgePivot+Quaternion.AngleAxis(90,Vector3.up)*(edgeCamera-edgePivot);
    c.ApplyInput(Vector2.zero,0,1,1);Check(Vector3.Distance(c.Focus,edgePivot)<.01&&Vector3.Distance(c.transform.position,expectedOrbit)<.01,"Off-map virtual orbit snapped to map bounds");
    settings.Yaw=0;
    settings.InitialSurfaceDistance=40;
    settings.InitialMoveSpeed=10;settings.MaxMoveSpeed=30;settings.MoveSpeedInterpolationTime=2;
    c.Initialize(settings,world.transform,new Bounds(Vector3.zero,new Vector3(200,150,200)),Vector3.zero,1);
    start=c.transform.position;c.ApplyInput(Vector2.up,0,.5f);Check(Mathf.Abs(Vector3.Distance(start,c.transform.position)-6.25f)<.01&&Mathf.Abs(c.CurrentMoveSpeed-15)<.01,"Initial linear pan ramp");
    c.ApplyInput(Vector2.right,0,.5f);Check(Mathf.Abs(c.CurrentMoveSpeed-20)<.01,"Direction change reset the pan ramp");
    start=c.transform.position;c.ApplyInput(Vector2.up,0,1.5f);Check(Mathf.Abs(Vector3.Distance(start,c.transform.position)-40)<.01&&Mathf.Abs(c.CurrentMoveSpeed-30)<.01,"Ramp crossing and maximum speed");
    c.ApplyInput(Vector2.zero,0,.1f);Check(c.CurrentMoveSpeed==0,"Idle pan speed");
    start=c.transform.position;c.ApplyInput(Vector2.up,0,.5f);Check(Mathf.Abs(Vector3.Distance(start,c.transform.position)-6.25f)<.01,"Movement release did not reset acceleration");
    c.Initialize(settings,world.transform,new Bounds(Vector3.zero,new Vector3(200,150,200)),Vector3.zero,1);
    start=c.transform.position;for(int step=0;step<20;step++)c.ApplyInput(Vector2.up,0,.1f);float smallFrames=Vector3.Distance(start,c.transform.position);
    c.Initialize(settings,world.transform,new Bounds(Vector3.zero,new Vector3(200,150,200)),Vector3.zero,1);start=c.transform.position;c.ApplyInput(Vector2.up,0,2);
    Check(Mathf.Abs(Vector3.Distance(start,c.transform.position)-smallFrames)<.01&&Mathf.Abs(smallFrames-40)<.01,"Pan ramp depends on frame rate");
    settings.MoveSpeedInterpolationTime=0;c.ApplyInput(Vector2.zero,0,0);start=c.transform.position;c.ApplyInput(Vector2.up,0,.1f);Check(Mathf.Abs(Vector3.Distance(start,c.transform.position)-3)<.01,"Zero interpolation time");
    session.Dispose();world.transform.localScale=Vector3.one*2;Physics.SyncTransforms();
    settings.Yaw=0;settings.Pitch=50;
    session=new Assets.Code.Player.LocalPlayerSession(world.transform,new Bounds(Vector3.zero,new Vector3(400,300,400)),Vector3.zero,2,settings);
    Check(Mathf.Abs(session.Controls.SurfaceDistance-80)<.01,"Camera initial distance ignored map scale");session.Controls.ApplyInput(Vector2.zero,-1000,0);Check(Mathf.Abs(session.Controls.SurfaceDistance-160)<.01,"Zoom limit ignored map scale");
    session.Dispose();UnityEngine.Object.DestroyImmediate(world);UnityEngine.Object.DestroyImmediate(terrainMesh);
  }

  static System.Func<int,System.Threading.Tasks.Task> launch;
  static Main main;
  static void Tick()
  {
    if(!EditorApplication.isPlaying)return;
    if(EditorApplication.timeSinceStartup-started>600){Finish(false,"Timeout stage "+stage);return;}
    try
    {
      var controller=UnityEngine.Object.FindAnyObjectByType<Assets.Code.UI.Frontend.FrontendController>();
      var document=UnityEngine.Object.FindAnyObjectByType<UIDocument>();
      var root=document!=null?document.rootVisualElement:null;
      main=UnityEngine.Object.FindAnyObjectByType<Main>();
      if(stage==0||stage==1)
      {
        if(controller==null||root==null)return;
        var intro=Field(controller,"introVideo");if(intro==null||root.Q<Image>("intro-video").image==null)return;
        if(stage==1&&intro==firstIntro)return;if(stage==0)firstIntro=intro;
        using var evt=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(100,100)});root.Q("intro-screen").SendEvent(evt);stage++;
      }
      else if(stage==2&&root!=null&&root.Q<Image>("background-video").image!=null)
      {
        CameraChecks();
        for(int chapter=1;chapter<=12;chapter++)
        {
          var path=Assets.Code.Stronghold2.MapLoading.WarCampaignMaps.GetPath(chapter);
          Check(File.Exists(path)&&Path.GetFileName(path)=="war_chapter"+chapter+".s2m"&&Path.GetDirectoryName(path).Replace('\\','/').EndsWith("Stronghold 2/maps"),"Chapter map correlation");
        }
        bool rejected=false;try{Assets.Code.Stronghold2.MapLoading.WarCampaignMaps.GetPath(13);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Invalid chapter accepted");
        Submit(root.Q<Button>("play"));Submit(root.Q<Button>("war"));Submit(root.Q<Button>("war-campaign"));
        var page=Page(controller);page.GetType().GetField("profile",Private).SetValue(page,new Assets.Code.UserData.UserProfileStore(Output+"/launch-profile.json"));
        launch=(System.Func<int,System.Threading.Tasks.Task>)Field(controller,"loadCampaign");
        controller.GetType().GetField("loadCampaign",Private).SetValue(controller,new System.Func<int,System.Threading.Tasks.Task>(chapter=>System.Threading.Tasks.Task.FromException(new IOException("Validation missing map"))));
        Submit(root.Q<Button>("campaign-chapter-3"));Submit(root.Q<Button>("campaign-continue"));frame=Time.frameCount;stage=3;
      }
      else if(stage==3&&Time.frameCount>frame+6)
      {
        Check(root.Q("campaign-selection-screen").resolvedStyle.display==DisplayStyle.Flex&&root.Q<Label>("campaign-title").text.Contains("Validation missing map"),"Failed launch did not return to campaign with an error");
        Check(main.Inventory==null&&!main.IsLoadingMap,"Failed launch started simulation");
        controller.GetType().GetField("loadCampaign",Private).SetValue(controller,launch);
        Submit(root.Q<Button>("campaign-chapter-1"));Submit(root.Q<Button>("campaign-continue"));
        Check(root.Q("loading-screen").style.display==DisplayStyle.Flex,"Loading screen not shown before decoding");
        Check(main.Inventory==null,"Map began before loading overlay");
        stage=4;
      }
      else if(stage==4)
      {
        if(main.LoadedMapPath==null)
        {
          if(controller!=null&&!(bool)Field(controller,"mapLoading"))throw new Exception(root.Q<Label>("campaign-title").text);
          return;
        }
        Check(main.LoadedMapPath==Path.GetFullPath(Assets.Code.Stronghold2.MapLoading.WarCampaignMaps.GetPath(1)),"Wrong map loaded");
        Check(!main.IsLoadingMap&&main.Inventory!=null&&main.Labor!=null&&main.AI!=null,"World not fully initialized");
        var world=GameObject.Find("S2M Map World");Check(world!=null&&world.transform.childCount>0,"Rendered world missing");
        Check(new Assets.Code.UserData.UserProfileStore(Output+"/launch-profile.json").Profile.lastAttemptedWarChapter==1,"Attempt was not persisted");
        Check(main.PlayerCamera!=null&&main.PlayerControls!=null&&Camera.main==main.PlayerCamera,"Local camera was not initialized/tagged");
        var buildingSettings=(Assets.Code.Stronghold2.ModelRendering.S2MBuildingRenderSettings)Field(main,"buildingRenderSettings");
        Check(buildingSettings.ViewCamera==main.PlayerCamera,"Building ViewCamera is not the player camera");
        UnityEngine.Object.FindAnyObjectByType<Assets.Code.Lighting.AtmosphereController>().TimeOfDay=12;
        target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();main.PlayerCamera.targetTexture=target;
        frame=Time.frameCount;stage=5;
      }
      else if(stage==5&&Time.frameCount>frame+8)
      {
        Check(document==null&&!controller.enabled,"Frontend remained active after map loading");
        var game=typeof(Main).Assembly.GetType("Assets.Code.Game").GetMethod("GetInstance").Invoke(null,null);
        Check(game.GetType().GetProperty("State").GetValue(game).ToString()=="PLAYING","Simulation not playing");
        var clock=game.GetType().GetProperty("Simulation").GetValue(game);
        Check((double)clock.GetType().GetProperty("ElapsedSeconds").GetValue(clock)>0,"Simulation did not advance");
        Capture("game-camera");
        game.GetType().GetProperty("IsPaused").SetValue(game,true);
        var before=main.PlayerControls.Focus;main.PlayerControls.ApplyInput(Vector2.up,0,.1f);
        Check(Vector3.Distance(before,main.PlayerControls.Focus)>0,"Loaded player controls did not pan");
        main.PlayerCamera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.Destroy(main.gameObject);frame=Time.frameCount;stage=6;
      }
      else if(stage==6&&Time.frameCount>frame+3)
      {
        Check(GameObject.Find("S2M Map World")==null&&GameObject.Find("Local Player Camera")==null,"Map/camera leaked after shutdown");
        Check(Camera.main==null||Camera.main.name!="Local Player Camera","Player camera remained active after teardown");
        Finish(true,"Terrain-only roof orbits for Q/E and middle drag; lowest-terrain-plane off-map orbit; linear pan acceleration, reset, direction changes, frame independence and zero-duration ramp; reversed mouse drag; stable altitude over roofs within limits; limit-triggered height correction; 40-unit wheel steps; Q/E yaw; middle drag sensitivity; camera-local flat WASD; diagonal normalization; terrain/building-only rays with 70 excluded colliders; min/max zoom; scale; live angle/FOV; bounds; camera assignment/cleanup; real map screenshot; all twelve installed chapter paths; invalid chapter rejection; UI failure/retry; real chapter 1 launch via Play Mission; loading overlay; rendered world/inventory/labor/AI; persisted attempt; frontend disposal; simulation advance; shutdown cleanup.");
      }
    }
    catch(Exception error){Finish(false,error.ToString());}
  }
}
