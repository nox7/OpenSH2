// Scratch project with normal OpenSH2 code/plugins; -batchmode -executeMethod FrontendScreensUnityValidation.Run.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class FrontendScreensUnityValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/FrontendScreensValidation",Session="FrontendScreensValidation";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly string[] Names={"peace","war","previous-menu","tutorial"};
  static readonly string[] Texts={"The Path of Peace\n\nGentler games modes, focusing mainly on castle life.","The Path of War\n\nCombat intensive styles of play.","Previous Menu","Play the Tutorial"};
  static readonly Vector2[] Sizes={new(1024,768),new(1920,1080),new(600,900)};
  static int stage,frame,index,layoutIndex,revisits;
  static double started;
  static object firstIntro;
  static object[] titleVideos;
  static RenderTexture target;
  static FrontendScreensUnityValidation()
  {
    if(!SessionState.GetBool(Session,false))return;
    started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  public static void Run()
  {
    Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    new GameObject("Main").AddComponent<Main>();
    var camera=new GameObject("Camera").AddComponent<Camera>();camera.gameObject.AddComponent<AudioListener>();
    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
    SessionState.SetBool(Session,true);EditorApplication.EnterPlaymode();
  }
  static object Field(object o,string name)=>o.GetType().GetField(name,Private).GetValue(o);
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
  static void Finish(bool ok,string text)
  {
    File.WriteAllText(Output+"/result.txt",(ok?"PASS: ":"FAIL: ")+text);SessionState.SetBool(Session,false);EditorApplication.Exit(ok?0:1);
  }
  static void Log(string text,string stack,LogType type)
  {
    if(text.StartsWith("Embedded Bink playback failed:")||text.StartsWith("Unable to load UI artwork")||(!stack.Contains("UnityEditor.Search.")&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)))Finish(false,text+"\n"+stack);
  }
  static void Submit(Button button)
  {
    button.Focus();using var evt=NavigationSubmitEvent.GetPooled();button.SendEvent(evt);
  }
  static void Capture(string name)
  {
    var previous=RenderTexture.active;RenderTexture.active=target;
    var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
    texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;
    File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
  }
  static void ReleasedTitle()
  {
    foreach(var video in titleVideos.Where(x=>x!=null))
    {
      var decoder=Field(video,"decoder");var worker=(System.Threading.Thread)Field(decoder,"worker");
      Check(!worker.IsAlive,"Hidden title screen kept a decoding worker alive.");
    }
  }
  static void Tick()
  {
    if(!EditorApplication.isPlaying)return;
    if(EditorApplication.timeSinceStartup-started>120){Finish(false,"Timeout stage "+stage);return;}
    try
    {
      var controller=UnityEngine.Object.FindAnyObjectByType<Assets.Code.UI.Frontend.FrontendController>();
      var document=UnityEngine.Object.FindAnyObjectByType<UIDocument>();if(document==null&&stage!=11)return;
      var root=document!=null?document.rootVisualElement:null;
      if(document!=null&&target==null){target=new RenderTexture(1600,900,0,RenderTextureFormat.ARGB32);target.Create();document.panelSettings.targetTexture=target;}
      if(stage==0||stage==1)
      {
        var intro=Field(controller,"introVideo");if(intro==null||root.Q<Image>("intro-video").image==null)return;
        if(stage==1&&intro==firstIntro)return;
        if(stage==0)firstIntro=intro;
        using var evt=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(100,100)});
        root.Q("intro-screen").SendEvent(evt);stage++;
      }
      else if(stage==2&&root.Q<Image>("background-video").image!=null)
      {
        var menu=Field(controller,"mainMenu");titleVideos=new[]{Field(menu,"background"),Field(menu,"opening"),Field(menu,"idle")};
        frame=Time.frameCount;stage=12;
      }
      else if(stage==12&&Time.frameCount>frame+4&&root.Q<Image>("curtain-idle").image!=null)
      {
        var load=root.Q<Button>("load").worldBound;var exit=root.Q<Button>("exit").worldBound;
        var options=root.Q<Button>("options").worldBound;
        Check(exit.center.y-load.center.y>load.center.y-options.center.y,"Exit is missing the larger gap after Load.");
        Check(root.Q<Label>("build-version").text=="v0.1.0 OpenSH2","Build version label is incorrect.");
        Capture("title-menu");
        Check(root.Q<Button>("play").enabledSelf,"Play is disabled.");Submit(root.Q<Button>("play"));frame=Time.frameCount;stage=3;
      }
      else if(stage==3&&Time.frameCount>frame+4)
      {
        Check(root.Q("menu-screen").resolvedStyle.display==DisplayStyle.None,"Play did not hide title.");
        Check(root.Q("war-peace-screen").resolvedStyle.display==DisplayStyle.Flex,"Play did not show path selection.");
        Check(root.Q<Label>("path-description").text=="Choose Your Path","Default help text is wrong.");ReleasedTitle();
        Check(Mathf.Abs(root.Q("screen-frame").worldBound.x-200)<1,"Shared viewport is not centered like the reference.");
        Capture("war-peace-default");index=0;root.Q<Button>(Names[index]).Focus();frame=Time.frameCount;stage=4;
      }
      else if(stage==4&&Time.frameCount>frame+3)
      {
        var button=root.Q<Button>(Names[index]);
        Check(root.Q<Label>("path-description").text==Texts[index],"Focus text wrong for "+Names[index]);
        Check(button.Q(className:"highlight-art").resolvedStyle.opacity>.99,"Focused highlight missing for "+Names[index]);
        Capture("war-peace-"+Names[index]);
        index++;
        if(index<Names.Length){root.Q<Button>(Names[index]).Focus();frame=Time.frameCount;return;}
        root.Q<Button>("war").Focus();
        using(var enter=PointerEnterEvent.GetPooled()){enter.target=root.Q<Button>("peace");root.Q<Button>("peace").SendEvent(enter);}
        Check(root.Q<Label>("path-description").text==Texts[0],"Hover did not override focused help.");
        using(var leave=PointerLeaveEvent.GetPooled()){leave.target=root.Q<Button>("peace");root.Q<Button>("peace").SendEvent(leave);}
        Check(root.Q<Label>("path-description").text==Texts[1],"Leaving hover did not restore focused help.");
        root.Q<Button>("war").Blur();
        Check(root.Q<Label>("path-description").text=="Choose Your Path","Clearing focus did not restore default.");
        layoutIndex=0;stage=5;
      }
      else if(stage==5)
      {
        root.style.width=Sizes[layoutIndex].x;root.style.height=Sizes[layoutIndex].y;frame=Time.frameCount;stage=6;
      }
      else if(stage==6&&Time.frameCount>frame+4)
      {
        var viewport=root.Q("screen-host").worldBound;var bounds=root.Q("screen-frame").worldBound;
        Check(Mathf.Abs(bounds.width/bounds.height-4f/3f)<.001,"Reference frame stretched.");
        Check(bounds.xMin>=viewport.xMin-1&&bounds.yMin>=viewport.yMin-1&&bounds.xMax<=viewport.xMax+1&&bounds.yMax<=viewport.yMax+1,"Screen overflows viewport.");
        foreach(var name in Names){var b=root.Q<Button>(name).worldBound;Check(bounds.Contains(b.min)&&bounds.Contains(b.max),"Control overflows: "+name);}
        if(++layoutIndex<Sizes.Length){stage=5;return;}
        root.style.width=1600;root.style.height=900;Submit(root.Q<Button>("previous-menu"));frame=Time.frameCount;stage=7;
      }
      else if(stage==7&&Time.frameCount>frame+3)
      {
        Check(root.Q("menu-screen").resolvedStyle.display==DisplayStyle.Flex,"Back did not restore title.");
        Check(root.Q("war-peace-screen").resolvedStyle.display==DisplayStyle.None,"Back did not hide path selection.");
        if(root.Q<Image>("background-video").image==null)return;
        Submit(root.Q<Button>("play"));frame=Time.frameCount;stage=8;
      }
      else if(stage==8&&Time.frameCount>frame+3)
      {
        Check(root.Q<Label>("path-description").text=="Choose Your Path","Revisit retained stale help.");
        var router=Field(controller,"screenRouter");
        Check(((System.Collections.IDictionary)Field(router,"screens")).Count==2,"Navigation recreated cached screens.");
        if(++revisits<3){Submit(root.Q<Button>("previous-menu"));frame=Time.frameCount;stage=7;return;}
        Check(UnityEngine.Object.FindAnyObjectByType<Main>().Inventory==null&&UnityEngine.Object.FindAnyObjectByType<Main>().Labor==null,"Navigation started a test map.");
        controller.enabled=false;frame=Time.frameCount;stage=11;
      }
      else if(stage==11&&Time.frameCount>frame+3)
      {
        Check(UnityEngine.Object.FindAnyObjectByType<UIDocument>()==null,"Screen document leaked after shutdown.");
        target.Release();UnityEngine.Object.DestroyImmediate(target);
        Finish(true,"Play/back navigation, lazy screen reuse, exact help strings, pointer/focus precedence and highlight artwork, three aspect ratios, shared borders, hidden video cleanup, no simulation startup.");
      }
    }
    catch(Exception error){Finish(false,error.ToString());}
  }
}
