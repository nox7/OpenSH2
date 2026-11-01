// Scratch project: -batchmode -executeMethod CampaignMapLaunchUnityValidation.Run, without -quit.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class CampaignMapLaunchUnityValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/CampaignMapLaunchValidation",Session="CampaignMapLaunchValidation";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly string[] Titles={"The Search for the King","First Command","Edwin's Rescue","The Bull in the Borderlands","Return to the Monastery","Olaf Grimtooth, Your Time Has Come","Defense of the Abbey","Hawks Nest","Lord Barclay's Castle","Siege of the Abbey","Treachery","To Capture a King"};
  static readonly Vector2[] Sizes={new(1024,768),new(1920,1080),new(600,900)};
  static int stage,frame,index,layoutIndex,visits;
  static double started;
  static object firstIntro;
  static RenderTexture target;
  static Texture2D previewTexture;
  static CampaignMapLaunchUnityValidation()
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
        frame=Time.frameCount;stage=5;
      }
      else if(stage==5&&Time.frameCount>frame+8)
      {
        Check(document==null&&!controller.enabled,"Frontend remained active after map loading");
        var game=typeof(Main).Assembly.GetType("Assets.Code.Game").GetMethod("GetInstance").Invoke(null,null);
        Check(game.GetType().GetProperty("State").GetValue(game).ToString()=="PLAYING","Simulation not playing");
        var clock=game.GetType().GetProperty("Simulation").GetValue(game);
        Check((double)clock.GetType().GetProperty("ElapsedSeconds").GetValue(clock)>0,"Simulation did not advance");
        UnityEngine.Object.Destroy(main.gameObject);frame=Time.frameCount;stage=6;
      }
      else if(stage==6&&Time.frameCount>frame+3)
      {
        Check(GameObject.Find("S2M Map World")==null,"Map world leaked after shutdown");
        Finish(true,"All twelve installed chapter paths; invalid chapter rejection; UI failure/retry; real chapter 1 launch via Play Mission; loading overlay; rendered world/inventory/labor/AI; persisted attempt; frontend disposal; simulation advance; shutdown cleanup.");
      }
    }
    catch(Exception error){Finish(false,error.ToString());}
  }
}
