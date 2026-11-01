// Scratch project: -batchmode -executeMethod CampaignSelectionUnityValidation.Run, without -quit.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class CampaignSelectionUnityValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/CampaignSelectionValidation",Session="CampaignSelectionValidation";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly string[] Titles={"The Search for the King","First Command","Edwin's Rescue","The Bull in the Borderlands","Return to the Monastery","Olaf Grimtooth, Your Time Has Come","Defense of the Abbey","Hawks Nest","Lord Barclay's Castle","Siege of the Abbey","Treachery","To Capture a King"};
  static readonly Vector2[] Sizes={new(1024,768),new(1920,1080),new(600,900)};
  static int stage,frame,index,layoutIndex,visits;
  static double started;
  static object firstIntro;
  static RenderTexture target;
  static Texture2D previewTexture;
  static CampaignSelectionUnityValidation()
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
  static void Tick()
  {
    if(!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup-started>120){Finish(false,"Timeout stage "+stage);return;}
    try
    {
      var controller=UnityEngine.Object.FindAnyObjectByType<Assets.Code.UI.Frontend.FrontendController>();
      var document=UnityEngine.Object.FindAnyObjectByType<UIDocument>();if(document==null&&stage!=11)return;
      var root=document!=null?document.rootVisualElement:null;
      if(document!=null&&target==null){target=new RenderTexture(1600,900,0,RenderTextureFormat.ARGB32);target.Create();document.panelSettings.targetTexture=target;}
      if(stage==0||stage==1)
      {
        var intro=Field(controller,"introVideo");if(intro==null||root.Q<Image>("intro-video").image==null)return;
        if(stage==1&&intro==firstIntro)return;if(stage==0)firstIntro=intro;
        using var evt=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(100,100)});root.Q("intro-screen").SendEvent(evt);stage++;
      }
      else if(stage==2&&root.Q<Image>("background-video").image!=null)
      {ProfileChecks();Submit(root.Q<Button>("play"));Submit(root.Q<Button>("war"));Submit(root.Q<Button>("war-campaign"));frame=Time.frameCount;stage=3;}
      else if(stage==3&&Time.frameCount>frame+4)
      {
        Check(root.Q("campaign-selection-screen").resolvedStyle.display==DisplayStyle.Flex,"Campaign route");
        Check(!root.Q<Button>("campaign-continue").enabledSelf,"Unimplemented mission launch should be unavailable");
        var texture=root.Q<Button>("campaign-chapter-1").Q(className:"normal-art").resolvedStyle.backgroundImage.texture;
        Check(texture!=null&&texture.width==251&&texture.height==44,"BMP artwork not loaded");
        previewTexture=texture;
        index=1;Submit(root.Q<Button>("campaign-chapter-1"));frame=Time.frameCount;stage=4;
      }
      else if(stage==4&&Time.frameCount>frame+3)
      {
        Check(root.Q("campaign-background").resolvedStyle.backgroundImage.texture.name=="campaign_chapter"+index+".jpg","Wrong chapter map");
        Check(root.Q<Label>("campaign-title").text==Titles[index-1],"Wrong chapter title");
        var button=root.Q<Button>("campaign-chapter-"+index);
        Check(button.ClassListContains("selected")&&button.Q(className:"highlight-art").resolvedStyle.opacity>.99,"Selection/highlight missing");
        string faction=index<=6?"red":index<=9?"good":"bad";
        Check(button.Q(className:"chapter-symbol").resolvedStyle.backgroundImage.texture.name=="symbol_"+faction+"_complete.tga","Wrong faction/completed symbol");
        Hover(root.Q("campaign-continue-hover"),true);
        Check(root.Q<Label>("campaign-title").text=="Play Mission "+index,"Next hover mission number");
        Hover(root.Q("campaign-continue-hover"),false);
        Check(root.Q<Label>("campaign-title").text==Titles[index-1],"Next hover did not restore chapter title");
        Capture("chapter-"+index);
        if(++index<=12){Submit(root.Q<Button>("campaign-chapter-"+index));frame=Time.frameCount;return;}
        var profile=(Assets.Code.UserData.UserProfileStore)Field(Page(controller),"profile");
        profile.Profile.completedWarChapters.Clear();profile.Profile.completedWarChapters.Add(1);profile.Profile.lastAttemptedWarChapter=2;
        Assets.Code.UserData.CampaignProgress.CompleteAllForLocalTesting=false;
        Submit(root.Q<Button>("campaign-back"));Submit(root.Q<Button>("war-campaign"));frame=Time.frameCount;stage=5;
      }
      else if(stage==5&&Time.frameCount>frame+3)
      {
        for(int chapter=1;chapter<=12;chapter++)
          Check(root.Q<Button>("campaign-chapter-"+chapter).resolvedStyle.display==(chapter<=2?DisplayStyle.Flex:DisplayStyle.None),"Locked chapter visible");
        Check(root.Q<Button>("campaign-chapter-1").Q(className:"chapter-symbol").resolvedStyle.backgroundImage.texture.name=="symbol_red_complete.tga","Completed icon");
        Check(root.Q<Button>("campaign-chapter-2").Q(className:"chapter-symbol").resolvedStyle.backgroundImage.texture.name=="symbol_red_current.tga","Current icon");
        Submit(root.Q<Button>("campaign-chapter-2"));
        Check(((Assets.Code.UserData.UserProfileStore)Field(Page(controller),"profile")).Profile.lastAttemptedWarChapter==2,"Browsing modified attempted chapter");

        frame=Time.frameCount;stage=12;
      }
      else if(stage==12&&Time.frameCount>frame+3)
      {
        Capture("partial-progress");
        var profile=(Assets.Code.UserData.UserProfileStore)Field(Page(controller),"profile");
        profile.Profile.lastAttemptedWarChapter=8;
        Submit(root.Q<Button>("campaign-back"));Submit(root.Q<Button>("war-campaign"));frame=Time.frameCount;stage=13;
      }
      else if(stage==13&&Time.frameCount>frame+3)
      {
        Check(root.Q<Button>("campaign-chapter-8").Q(className:"chapter-symbol").resolvedStyle.backgroundImage.texture.name=="symbol_good_current.tga","Good current icon");
        var profile=(Assets.Code.UserData.UserProfileStore)Field(Page(controller),"profile");
        profile.Profile.lastAttemptedWarChapter=11;
        Submit(root.Q<Button>("campaign-back"));Submit(root.Q<Button>("war-campaign"));frame=Time.frameCount;stage=14;
      }
      else if(stage==14&&Time.frameCount>frame+3)
      {
        Check(root.Q<Button>("campaign-chapter-11").Q(className:"chapter-symbol").resolvedStyle.backgroundImage.texture.name=="symbol_bad_current.tga","Bad current icon");
        Assets.Code.UserData.CampaignProgress.CompleteAllForLocalTesting=true;
        Submit(root.Q<Button>("campaign-back"));Submit(root.Q<Button>("war-campaign"));
        Submit(root.Q<Button>("campaign-chapter-2"));
        layoutIndex=0;stage=6;
      }
      else if(stage==6)
      {root.style.width=Sizes[layoutIndex].x;root.style.height=Sizes[layoutIndex].y;frame=Time.frameCount;stage=7;}
      else if(stage==7&&Time.frameCount>frame+4)
      {
        var bounds=root.Q("screen-frame").worldBound;var viewport=root.Q("screen-host").worldBound;
        Check(Mathf.Abs(bounds.width/bounds.height-4f/3f)<.001&&bounds.xMin>=viewport.xMin-1&&bounds.yMin>=viewport.yMin-1&&bounds.xMax<=viewport.xMax+1&&bounds.yMax<=viewport.yMax+1,"Campaign viewport fitting");
        foreach(var name in new[]{"campaign-chapter-1","campaign-chapter-2","campaign-back","campaign-continue"}){var b=root.Q<Button>(name).worldBound;Check(bounds.Contains(b.min)&&bounds.Contains(b.max),"Control overflow");}
        if(++layoutIndex<Sizes.Length){stage=6;return;}
        Submit(root.Q<Button>("campaign-back"));frame=Time.frameCount;stage=8;
      }
      else if(stage==8&&Time.frameCount>frame+3)
      {
        Check(root.Q("path-of-war-selection-screen").resolvedStyle.display==DisplayStyle.Flex,"Back route");
        Submit(root.Q<Button>("war-campaign"));frame=Time.frameCount;stage=9;
      }
      else if(stage==9&&Time.frameCount>frame+3)
      {
        Check(root.Q<Label>("campaign-title").text==Titles[1],"Revisit lost selection");
        Check(((System.Collections.IDictionary)Field(Field(controller,"screenRouter"),"screens")).Count==4,"Page not reused");
        Check(UnityEngine.Object.FindAnyObjectByType<Main>().Inventory==null,"Campaign browsing started simulation");
        controller.enabled=false;frame=Time.frameCount;stage=11;
      }
      else if(stage==11&&Time.frameCount>frame+3)
      {
        Check(previewTexture==null&&UnityEngine.Object.FindAnyObjectByType<UIDocument>()==null,"Campaign assets leaked");
        target.Release();UnityEngine.Object.DestroyImmediate(target);
        Finish(true,"All 12 chapter maps/titles/faction icons; BMP/highlight loading; route/back/reuse; partial-progress hiding/current icons; profile persistence and backup recovery; testing override isolation; three viewport shapes; cleanup; no simulation.");
      }
    }
    catch(Exception error){Finish(false,error.ToString());}
  }
}
