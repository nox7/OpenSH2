// Scratch project: -batchmode -executeMethod PathOfPeaceSelectionUnityValidation.Run, without -quit.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class PathOfPeaceSelectionUnityValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/PathOfPeaceSelectionValidation",Session="PathOfPeaceSelectionValidation";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly string[] Names={"peace-sim-campaign","peace-free-build","peace-custom-maps","peace-previous-menu"};
  static readonly string[] Texts={"Help the King rebuild the county, in this gentler\nSim campaign","Build peacefully in a setting of your own\nchoosing","Load a user created sim mission","Previous menu"};
  static readonly string[] Previews={"peace_simcampaign.jpg","peace_freebuild.jpg","warpeace_custom.jpg"};
  static readonly Vector2[] Sizes={new(1024,768),new(1920,1080),new(600,900)};
  static int stage,frame,index,layoutIndex,visits;
  static double started;
  static object firstIntro;
  static RenderTexture target;
  static Texture2D previewTexture;
  static PathOfPeaceSelectionUnityValidation()
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
  static void Hover(Button button,bool entering)
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
  static void Context(VisualElement root,int index)
  {
    Check(root.Q<Label>("peace-selection-description").text==Texts[index],"Wrong peace description: "+index);
    var preview=root.Q<Image>("peace-selection-preview");
    if(index<3)Check(preview.image!=null&&preview.image.name==Previews[index]&&preview.resolvedStyle.display==DisplayStyle.Flex,"Wrong preview: "+index);
    else Check(preview.image==null&&preview.resolvedStyle.display==DisplayStyle.None,"Back retained preview.");
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
      {Submit(root.Q<Button>("play"));Submit(root.Q<Button>("peace"));frame=Time.frameCount;stage=3;}
      else if(stage==3&&Time.frameCount>frame+4)
      {
        Check(root.Q("path-of-peace-selection-screen").resolvedStyle.display==DisplayStyle.Flex,"Peace navigation failed.");
        Check(root.Q("war-peace-screen").resolvedStyle.display==DisplayStyle.None,"Path chooser remained visible.");
        Check(root.Q<Label>("peace-selection-description").text==""&&root.Q<Image>("peace-selection-preview").image==null,"Default peace context is not empty.");
        Capture("peace-default");index=0;root.Q<Button>(Names[0]).Focus();frame=Time.frameCount;stage=4;
      }
      else if(stage==4&&Time.frameCount>frame+3)
      {
        Context(root,index);Check(root.Q<Button>(Names[index]).Q(className:"highlight-art").resolvedStyle.opacity>.99,"Highlight not shown.");
        Capture("peace-"+Names[index]);
        if(index==0)previewTexture=(Texture2D)root.Q<Image>("peace-selection-preview").image;
        if(++index<Names.Length){root.Q<Button>(Names[index]).Focus();frame=Time.frameCount;return;}
        root.Q<Button>(Names[1]).Focus();Hover(root.Q<Button>(Names[0]),true);Context(root,0);Hover(root.Q<Button>(Names[0]),false);Context(root,1);
        root.Q<Button>(Names[1]).Blur();
        Check(root.Q<Label>("peace-selection-description").text==""&&root.Q<Image>("peace-selection-preview").image==null,"Leaving context did not clear both text and preview.");
        foreach(var name in Names){var button=root.Q<Button>(name);Hover(button,true);Context(root,Array.IndexOf(Names,name));Hover(button,false);}
        layoutIndex=0;stage=5;
      }
      else if(stage==5)
      {root.style.width=Sizes[layoutIndex].x;root.style.height=Sizes[layoutIndex].y;frame=Time.frameCount;stage=6;}
      else if(stage==6&&Time.frameCount>frame+4)
      {
        var bounds=root.Q("screen-frame").worldBound;var viewport=root.Q("screen-host").worldBound;
        Check(Mathf.Abs(bounds.width/bounds.height-4f/3f)<.001&&bounds.xMin>=viewport.xMin-1&&bounds.yMin>=viewport.yMin-1&&bounds.xMax<=viewport.xMax+1&&bounds.yMax<=viewport.yMax+1,"Peace frame stretched or overflowed.");
        foreach(var name in Names){var b=root.Q<Button>(name).worldBound;Check(bounds.Contains(b.min)&&bounds.Contains(b.max),"Peace button overflowed.");}
        if(++layoutIndex<Sizes.Length){stage=5;return;}
        root.style.width=1600;root.style.height=900;Submit(root.Q<Button>("peace-previous-menu"));frame=Time.frameCount;stage=7;
      }
      else if(stage==7&&Time.frameCount>frame+3)
      {
        Check(root.Q("war-peace-screen").resolvedStyle.display==DisplayStyle.Flex,"Back did not restore path chooser.");
        Check(root.Q<Label>("path-description").text=="Choose Your Path","Back retained old path help.");
        Submit(root.Q<Button>("peace"));frame=Time.frameCount;stage=8;
      }
      else if(stage==8&&Time.frameCount>frame+3)
      {
        Check(root.Q<Label>("peace-selection-description").text==""&&root.Q<Image>("peace-selection-preview").image==null,"Revisit retained peace context.");
        Check(((System.Collections.IDictionary)Field(Field(controller,"screenRouter"),"screens")).Count==3,"Peace page was not reused.");
        if(++visits<3){Submit(root.Q<Button>("peace-previous-menu"));frame=Time.frameCount;stage=7;return;}
        Check(UnityEngine.Object.FindAnyObjectByType<Main>().Inventory==null,"Peace screen started a map.");
        controller.enabled=false;frame=Time.frameCount;stage=11;
      }
      else if(stage==11&&Time.frameCount>frame+3)
      {
        Check(previewTexture==null&&UnityEngine.Object.FindAnyObjectByType<UIDocument>()==null,"Peace assets or document leaked.");
        target.Release();UnityEngine.Object.DestroyImmediate(target);
        Finish(true,"Peace navigation/back/revisits; empty default state; all three previews, exact descriptions and green highlights; hover/focus fallback; three aspect ratios; asset cleanup; no simulation startup.");
      }
    }
    catch(Exception error){Finish(false,error.ToString());}
  }
}
