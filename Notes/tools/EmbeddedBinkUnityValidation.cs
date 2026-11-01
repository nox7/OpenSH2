// Copy into an isolated Unity project's Assets/Editor, with OpenSH2 sources, plugins and intro caches.
// Run -batchmode -executeMethod EmbeddedBinkUnityValidation.Run (do not add -quit).
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
public static class EmbeddedBinkUnityValidation
{
  const string Output = "C:/Users/garet/Documents/GitHub/OpenSH2/Temp/EmbeddedBinkValidation";
  const string Session = "EmbeddedBinkValidation";
  static int stage, frame;
  static double started;
  static RenderTexture target;
  static object menu, background, idle;
  static MonoBehaviour controller;
  static Texture2D curtain;
  static Color32[] initialRender;
  static FieldInfo menuField;
  static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
  static EmbeddedBinkUnityValidation()
  {
    if (!SessionState.GetBool(Session, false)) return;
    started = EditorApplication.timeSinceStartup;
    EditorApplication.update += Tick;
    Application.logMessageReceived += Log;
  }
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    var main = new GameObject("Main").AddComponent<Main>();
    var serialized = new SerializedObject(main);
    serialized.FindProperty("useEmbeddedMenuBink").boolValue = true;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    var camera = new GameObject("Camera").AddComponent<Camera>();
    camera.clearFlags = CameraClearFlags.SolidColor;
    camera.backgroundColor = Color.black;
    SessionState.SetBool(Session, true);
    EditorApplication.EnterPlaymode();
  }
  static object Field(object instance, string name) => instance.GetType().GetField(name, Private).GetValue(instance);
  static int Loops(object surface) => (int)surface.GetType().GetProperty("CompletedLoops").GetValue(surface);
  static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
  static void Log(string message, string stack, LogType type)
  {
    if (message.StartsWith("Embedded Bink playback failed:") || (!stack.Contains("UnityEditor.Search.") && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)))
      Finish(false, message + "\n" + stack);
  }
  static void Finish(bool success, string message)
  {
    File.WriteAllText(Output + "/result.txt", (success ? "PASS: " : "FAIL: ") + message);
    SessionState.SetBool(Session, false);
    EditorApplication.Exit(success ? 0 : 1);
  }
  static void Skip(VisualElement intro)
  {
    using var evt = PointerDownEvent.GetPooled(new Event {type=EventType.MouseDown,button=0,mousePosition=new Vector2(100,100)});
    intro.SendEvent(evt);
  }
  static Texture2D Capture(string name)
  {
    var previous = RenderTexture.active;
    RenderTexture.active = target;
    var result = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
    result.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
    result.Apply();
    RenderTexture.active = previous;
    File.WriteAllBytes(Output + "/" + name + ".png",result.EncodeToPNG());
    return result;
  }
  static void Tick()
  {
    if (!EditorApplication.isPlaying) return;
    if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout at stage " + stage); return; }
    try
    {
      var document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
      var root = document != null ? document.rootVisualElement : null;
      if (root != null && target == null)
      {
        target = new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32);
        target.Create();
        document.panelSettings.targetTexture = target;
      }
      if (stage == 0 || stage == 1)
      {
        var player = UnityEngine.Object.FindAnyObjectByType<VideoPlayer>();
        if (player == null || player.frame < 1) return;
        if (stage == 1 && !player.url.Contains("Stronghold2")) return;
        Skip(root.Q("intro-screen"));
        stage++;
      }
      else if (stage == 2 && root != null && root.Q<Image>("curtain-idle").image != null)
      {
        controller = UnityEngine.Object.FindAnyObjectByType<Assets.Code.UI.Frontend.FrontendController>();
        menuField = controller.GetType().GetField("mainMenu",Private);
        menu = menuField.GetValue(controller);
        background = Field(menu,"background");
        idle = Field(menu,"idle");
        Check(background.GetType().Name == "BinkVideoSurface" && idle.GetType().Name == "BinkVideoSurface", "Menu used cached playback.");
        Check(Field(menu,"opening") == null, "Opening did not hand off to idle.");
        Check(root.Q<Image>("curtain-opening").image == null,"Opening texture leaked.");
        curtain = root.Q<Image>("curtain-idle").image as Texture2D;
        Check(curtain != null && curtain.width == 381 && curtain.height == 768,"Original odd-width curtain was cropped.");
        var initial=Capture("menu-start");
        initialRender=initial.GetPixels32();
        UnityEngine.Object.DestroyImmediate(initial);
        Debug.Log("BINK VALIDATION: native menu and curtain handoff are displaying.");
        stage = 3;
      }
      else if (stage == 3)
      {
        foreach (var surface in new[] { background,idle })
          Check(surface.GetType().GetProperty("Error").GetValue(surface) == null,"Native playback error.");
        if (Loops(background) < 2 || Loops(idle) < 2) return;
        Check(UnityEngine.Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Length == 0,"Menu created Unity VideoPlayers.");
        Check(UnityEngine.Object.FindAnyObjectByType<Main>().Labor == null,"Test map started.");
        var alpha = curtain.GetPixels32();
        Check(alpha.Any(p=>p.a==0) && alpha.Any(p=>p.a==255) && alpha.Any(p=>p.a>0 && p.a<255),"Transparent or partial-alpha pixels were lost.");
        var rendered=Capture("menu");
        var currentRender=rendered.GetPixels32();
        int changed=0;
        for(int y=100;y<600;y++)for(int x=600;x<1050;x++)
        {
          int index=y*target.width+x;
          if(!currentRender[index].Equals(initialRender[index]))changed++;
        }
        Check(changed>500,"GPU video remained static despite frame uploads.");
        File.WriteAllText(Output+"/motion.txt",$"GPU background pixels changed: {changed}");
        UnityEngine.Object.DestroyImmediate(rendered);
        File.WriteAllText(Output + "/loops.txt",$"Background completed loops: {Loops(background)}; curtain completed loops: {Loops(idle)}");
        // Pause view ticking without disabling/disposal, then render alpha against a known color.
        menuField.SetValue(controller,null);
        root.Q("menu-column").style.display = DisplayStyle.None;
        root.Q("background-video").style.display = DisplayStyle.None;
        root.Q("menu-frame").style.backgroundColor = Color.magenta;
        frame = Time.frameCount;
        stage = 4;
      }
      else if (stage == 4 && Time.frameCount > frame + 4)
      {
        var capture = Capture("alpha");
        var pixels = curtain.GetPixels32();
        Rect bounds = root.Q<Image>("curtain-idle").worldBound;
        int transparent=0,opaque=0;
        // Samples away from texel edges avoid scaling ambiguity. Validate real GPU compositing.
        for (int y=2;y<curtain.height-2;y+=4)
          for (int x=2;x<curtain.width-2;x+=4)
          {
            byte a=pixels[y*curtain.width+x].a;
            if (a!=0 && a!=255) continue;
            bool solid=true;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
              if(pixels[(y+dy)*curtain.width+x+dx].a!=a)solid=false;
            if(!solid)continue;
            int sx=Mathf.FloorToInt(bounds.x+(x+.5f)*bounds.width/curtain.width);
            int sy=target.height-1-Mathf.FloorToInt(bounds.yMax-(y+.5f)*bounds.height/curtain.height);
            Color32 rendered=capture.GetPixel(sx,sy);
            bool magenta=rendered.r>250 && rendered.g<5 && rendered.b>250;
            if(a==0){Check(magenta,"Transparent curtain pixel obscured the background.");transparent++;}
            else {Check(!magenta,"Opaque curtain pixel disappeared.");opaque++;}
          }
        Check(transparent>100 && opaque>100,"Insufficient GPU alpha samples.");
        File.WriteAllText(Output+"/alpha.txt",$"GPU samples: {transparent} transparent, {opaque} opaque; partial alpha retained in texture.");
        UnityEngine.Object.DestroyImmediate(capture);
        menuField.SetValue(controller,menu);
        controller.enabled=false;
        frame=Time.frameCount;
        stage=5;
      }
      else if (stage == 5 && Time.frameCount > frame + 3)
      {
        foreach(var surface in new[]{background,idle})
        {
          var decoder=Field(surface,"decoder");
          var thread=(System.Threading.Thread)Field(decoder,"worker");
          Check(!thread.IsAlive,"Native decoding worker survived disposal.");
        }
        Check(curtain==null,"Curtain texture survived disposal.");
        Check(UnityEngine.Object.FindAnyObjectByType<UIDocument>()==null,"Document survived disposal.");
        target.Release(); UnityEngine.Object.DestroyImmediate(target);
        Finish(true,"Original Bink background and curtain: two displayed loops each, opening-to-idle handoff, exact 381px width, GPU transparency, worker/texture cleanup; no Unity menu VideoPlayer.");
      }
    }
    catch(Exception error) { Finish(false,error.ToString()); }
  }
}
