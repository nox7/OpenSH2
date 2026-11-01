// Run in a scratch Unity project WITHOUT Assets/Cache or the converter executable.
// -batchmode -executeMethod FrontendAudioUnityValidation.Run (without -quit).
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

[InitializeOnLoad]
public static class FrontendAudioUnityValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/FrontendAudioValidation";
  const string Session="FrontendAudioValidation";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static int stage,frame;
  static double started;
  static object logo,movie,early,menu;
  static object[] menuPlayback;
  static MonoBehaviour controller;
  static RenderTexture target;
  static float logoOutput,movieOutput;
  static bool natural;
  static readonly float[] Samples=new float[2048];
  static FrontendAudioUnityValidation()
  {
    if(!SessionState.GetBool(Session,false))return;
    started=EditorApplication.timeSinceStartup;
    natural=SessionState.GetBool("FrontendAudioNatural",false);
    EditorApplication.update+=Tick;
    Application.logMessageReceived+=Log;
  }
  public static void Run()
  {
    SessionState.SetBool("FrontendAudioNatural",false);
    StartRun();
  }
  public static void RunNaturalIntro()
  {
    SessionState.SetBool("FrontendAudioNatural",true);
    StartRun();
  }
  static void StartRun()
  {
    Directory.CreateDirectory(Output);
    if(Directory.Exists("Assets/Cache/Video"))throw new Exception("Start with no video cache.");
    if(File.Exists("Assets/External/ffmpeg-windows/bin/ffmpeg.exe"))throw new Exception("Remove converter from scratch project.");
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    new GameObject("Main").AddComponent<Main>();
    var camera=new GameObject("Camera").AddComponent<Camera>();
    camera.gameObject.AddComponent<AudioListener>();
    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
    SessionState.SetBool(Session,true);
    EditorApplication.EnterPlaymode();
  }
  static object Field(object o,string name)=>o.GetType().GetField(name,Private).GetValue(o);
  static object Property(object o,string name)=>o.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  static void Finish(bool ok,string message)
  {
    File.WriteAllText(Output+"/result.txt",(ok?"PASS: ":"FAIL: ")+message);
    SessionState.SetBool(Session,false);EditorApplication.Exit(ok?0:1);
  }
  static void Log(string message,string stack,LogType type)
  {
    if(message.StartsWith("Embedded Bink playback failed:")||(!stack.Contains("UnityEditor.Search.")&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)))Finish(false,message+"\n"+stack);
  }
  static void Skip(VisualElement intro)
  {
    using var evt=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(100,100)});
    intro.SendEvent(evt);
  }
  static void Released(object surface)
  {
    var videoWorker=(System.Threading.Thread)Field(Field(surface,"decoder"),"worker");
    Check(!videoWorker.IsAlive,"Video worker leaked.");
    var audio=Field(surface,"audio");
    if(audio==null)return;
    var audioWorker=(System.Threading.Thread)Field(Field(audio,"decoder"),"worker");
    Check(!audioWorker.IsAlive,"Audio worker leaked.");
    var source=(AudioSource)Field(audio,"source");
    Check(source==null||(!source.isPlaying&&source.clip==null),"Skipped audio continued playing.");
  }
  static void Probe(object surface,ref float output)
  {
    var audio=Field(surface,"audio");
    var decoder=Field(audio,"decoder");
    Check((bool)Property(decoder,"HasAudio"),"Intro audio was not detected.");
    var source=(AudioSource)Field(audio,"source");
    if(source!=null&&source.isPlaying)
    {
      source.GetOutputData(Samples,0);
      output=Math.Max(output,Samples.Max(x=>Math.Abs(x)));
    }
    double playback=(double)Property(audio,"PlaybackSeconds");
    double video=(double)Field(surface,"lastSeconds");
    if(playback>1)Check(video<=playback+.05&&video>playback-.3,"Video drifted from audio DSP clock.");
  }
  static void Tick()
  {
    if(!EditorApplication.isPlaying)return;
    if(EditorApplication.timeSinceStartup-started>(natural?240:120)){Finish(false,"Timeout stage "+stage);return;}
    try
    {
      var document=UnityEngine.Object.FindAnyObjectByType<UIDocument>();
      var root=document!=null?document.rootVisualElement:null;
      controller=UnityEngine.Object.FindAnyObjectByType<Assets.Code.UI.Frontend.FrontendController>();
      var active=controller!=null?Field(controller,"introVideo"):null;
      if(document!=null&&target==null)
      {
        target=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32);target.Create();document.panelSettings.targetTexture=target;
      }
      if(stage==0&&active!=null&&root.Q<Image>("intro-video").image!=null)
      {
        logo=active;Time.timeScale=0;stage=1;
      }
      else if(stage==1)
      {
        if(active==logo){Probe(logo,ref logoOutput);return;}
        Check(logoOutput>1e-4,"Logo had no PCM at Unity AudioSource output.");
        Released(logo);
        if(active==null||root.Q<Image>("intro-video").image==null)return;
        movie=active;stage=2;
        Debug.Log("AUDIO VALIDATION: logo completed naturally with audio; intro is playing.");
      }
      else if(stage==2)
      {
        if(natural&&active!=movie)
        {
          Released(movie);
          var pcm=Field(Field(movie,"audio"),"decoder");
          long decoded=(long)Property(pcm,"DecodedFrames"),delivered=(long)Property(pcm,"DeliveredFrames");
          Check((bool)Property(pcm,"Ended")&&decoded>48000*120&&delivered==decoded,"Natural completion dropped audio samples.");
          Check((double)Field(movie,"lastSeconds")>124,"Intro ended before final video frames.");
          Check(!Directory.Exists("Assets/Cache/Video"),"Natural startup created a video cache.");
          File.WriteAllText(Output+"/natural.txt",$"Full intro: decoded/delivered PCM frames={decoded}/{delivered}; underrun frames={Property(pcm,"UnderrunFrames")}; AudioSource peak={movieOutput}.");
          Time.timeScale=1;
          Finish(true,"Full 124.96-second intro completed naturally with synchronized video/audio, every decoded PCM frame delivered, and no cache.");
          return;
        }
        Probe(movie,ref movieOutput);
        if(natural)return;
        double playback=(double)Property(Field(movie,"audio"),"PlaybackSeconds");
        if(playback<3||movieOutput<1e-4)return;
        Skip(root.Q("intro-screen"));frame=Time.frameCount;stage=3;
      }
      else if(stage==3&&Time.frameCount>frame+3)
      {
        Released(movie);Time.timeScale=1;
        Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0,"Intro audio source survived skip.");
        Check(UnityEngine.Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Length==0,"Unity VideoPlayer was used.");
        menu=Field(controller,"mainMenu");stage=4;
      }
      else if(stage==4&&root.Q<Image>("curtain-idle").image!=null)
      {
        var background=Field(menu,"background");var idle=Field(menu,"idle");
        if((int)Property(background,"CompletedLoops")<2||(int)Property(idle,"CompletedLoops")<2)return;
        Check(Field(menu,"opening")==null,"Curtain handoff failed.");
        var curtain=(Texture2D)root.Q<Image>("curtain-idle").image;
        Check(curtain.width==381&&curtain.GetPixels32().Any(p=>p.a==0)&&curtain.GetPixels32().Any(p=>p.a>0&&p.a<255),"Curtain alpha or width lost.");
        menuPlayback=new[]{Field(menu,"background"),Field(menu,"idle")};
        controller.enabled=false;frame=Time.frameCount;stage=5;
      }
      else if(stage==5&&Time.frameCount>frame+3)
      {
        Released(menuPlayback[0]);Released(menuPlayback[1]);
        UnityEngine.Object.Destroy(controller);
        // Replay the frontend to exercise skipping before video/audio preparation has completed.
        var gameType=typeof(Main).Assembly.GetType("Assets.Code.Game");
        var game=gameType.GetMethod("GetInstance").Invoke(null,null);
        var host=new GameObject("Early skip frontend");
        controller=host.AddComponent<Assets.Code.UI.Frontend.FrontendController>();
        controller.GetType().GetMethod("Begin",Private).Invoke(controller,new[]{game});
        stage=6;
      }
      else if(stage==6&&active!=null)
      {
        early=active;Skip(root.Q("intro-screen"));stage=7;
      }
      else if(stage==7&&active!=null&&active!=early)
      {
        Released(early);
        movie=active;controller.enabled=false;frame=Time.frameCount;stage=8;
      }
      else if(stage==8&&Time.frameCount>frame+3)
      {
        Released(movie);
        Check(!Directory.Exists("Assets/Cache/Video"),"Startup created a video cache.");
        var gameType=typeof(Main).Assembly.GetType("Assets.Code.Game");
        var game=gameType.GetMethod("GetInstance").Invoke(null,null);
        Check(gameType.GetField("cacheManager")==null,"Obsolete video cache manager remains.");
        Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0,"Audio sources leaked.");
        Check(UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Length==0,"Documents leaked.");
        File.WriteAllText(Output+"/audio.txt",$"Unity AudioSource output peaks: logo={logoOutput}; intro={movieOutput}. DSP/video sync checked during both intros.");
        target.Release();UnityEngine.Object.DestroyImmediate(target);
        Finish(true,"Empty-cache startup; natural logo audio completion; DSP-synchronized intro audio and click-to-skip; early skip/disable cleanup; two menu loops and curtain alpha; no converter or Unity VideoPlayer.");
      }
    }
    catch(Exception e){Finish(false,e.ToString());}
  }
}
