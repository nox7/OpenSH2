// Scratch Unity project with OpenSH2 sources/plugins; no video cache or external converter.
// -batchmode -executeMethod TerrainBinkUnityValidation.Run (without -quit).
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

[InitializeOnLoad]
public static class TerrainBinkUnityValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/TerrainBinkValidation";
  const string Install="C:/Steam/steamapps/common/Stronghold 2", Session="TerrainBinkValidation";
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly int[] MaterialIndices={0,1,3};
  static GameObject water,held;
  static object[] players,heldPlayers,restarted;
  static Material[] materials;
  static string[] initial,heldHashes;
  static double started,wait;
  static double[] pausedSeconds,slowSeconds;
  static int stage,frame;
  static TerrainBinkUnityValidation()
  {
    if(!SessionState.GetBool(Session,false))return;
    started=EditorApplication.timeSinceStartup;
    EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    if(Directory.Exists("Assets/Cache/Video"))throw new Exception("Remove video cache from scratch project.");
    if(Directory.Exists("Assets/External/ffmpeg-windows"))throw new Exception("Remove external converter from scratch project.");
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    new GameObject("Camera").AddComponent<Camera>();
    SessionState.SetBool(Session,true);EditorApplication.EnterPlaymode();
  }
  static object Property(object o,string name)=>o.GetType().GetProperty(name).GetValue(o);
  static Texture2D Texture(object o)=>(Texture2D)Property(o,"Texture");
  static double Seconds(object o)=>(double)Property(o,"PresentedSeconds");
  static object[] Players(GameObject obj)
  {
    var type=typeof(S2MWaterRenderer).Assembly.GetType("Assets.Code.Stronghold2.TerrainRendering.S2MWaterVideoPlayer");
    return ((IList)type.GetField("players",Private).GetValue(obj.GetComponent(type))).Cast<object>().ToArray();
  }
  static bool Ready(object[] set)=>set.Length==3&&set.All(p=>Texture(p)!=null);
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  static void Released(object[] set)
  {
    foreach(var p in set)
    {
      var decoder=p.GetType().GetField("decoder",Private).GetValue(p);
      var worker=(System.Threading.Thread)decoder.GetType().GetField("worker",Private).GetValue(decoder);
      Check(!worker.IsAlive,"Water decoding worker survived cleanup.");
      Check(Texture(p)==null,"Water texture survived cleanup.");
    }
  }
  static string GpuHash(object player,string label)
  {
    var texture=Texture(player);
    Check(texture.width==256&&texture.height==256,"Wrong water video dimensions.");
    Check(texture.wrapMode==TextureWrapMode.Repeat&&texture.filterMode==FilterMode.Bilinear,"Water sampling changed.");
    var target=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32);
    Graphics.Blit(texture,target);
    var previous=RenderTexture.active;RenderTexture.active=target;
    var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);
    pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
    RenderTexture.active=previous;
    if(label!=null)File.WriteAllBytes(Output+"/"+label+".png",pixels.EncodeToPNG());
    using var sha=SHA256.Create();string hash=BitConverter.ToString(sha.ComputeHash(pixels.GetRawTextureData()));
    UnityEngine.Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(target);return hash;
  }
  static void Finish(bool ok,string text)
  {
    Time.timeScale=1;File.WriteAllText(Output+"/result.txt",(ok?"PASS: ":"FAIL: ")+text);
    SessionState.SetBool(Session,false);EditorApplication.Exit(ok?0:1);
  }
  static void Log(string text,string stack,LogType type)
  {
    if(text.StartsWith("Embedded Bink")||(!stack.Contains("UnityEditor.Search.")&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)))Finish(false,text+"\n"+stack);
  }
  static void Tick()
  {
    if(!EditorApplication.isPlaying)return;
    if(EditorApplication.timeSinceStartup-started>90){Finish(false,"Timeout at stage "+stage);return;}
    try
    {
      if(stage==0)
      {
        var map=S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
        water=S2MWaterRenderer.Render(map,settings:new S2MWaterSettings{GameInstallPath=Install});
        held=S2MWaterRenderer.Render(map,settings:new S2MWaterSettings{GameInstallPath=Install,RiverPlaybackSpeed=0,SeaPlaybackSpeed=0,MoatPlaybackSpeed=0});
        players=Players(water);heldPlayers=Players(held);
        materials=water.GetComponent<MeshRenderer>().sharedMaterials;
        Check(players.Length==3&&heldPlayers.Length==3,"Expected three terrain bindings.");stage=1;
      }
      else if(stage==1&&Ready(players)&&Ready(heldPlayers))
      {
        for(int i=0;i<3;i++)Check(materials[MaterialIndices[i]].GetTexture("_SurfaceTexture")==Texture(players[i]),"Material is not bound to decoded Bink texture.");
        initial=players.Select((p,i)=>GpuHash(p,"start-"+i)).ToArray();
        heldHashes=heldPlayers.Select(p=>GpuHash(p,null)).ToArray();
        Check(heldPlayers.All(p=>Seconds(p)==0),"Zero speed did not hold frame zero.");stage=2;
      }
      else if(stage==2&&players.All(p=>Seconds(p)>1))
      {
        for(int i=0;i<3;i++)Check(GpuHash(players[i],"moving-"+i)!=initial[i],"GPU water video did not animate: "+i);
        stage=3;
      }
      else if(stage==3&&players.All(p=>(int)Property(p,"CompletedLoops")>=2))
      {
        File.WriteAllText(Output+"/loops.txt",string.Join("\n",players.Select((p,i)=>$"{Texture(p).name}: loops={Property(p,"CompletedLoops")}, speed={Property(p,"PlaybackSpeed")}, presentedSeconds={Seconds(p)}")));
        for(int i=0;i<3;i++)Check(GpuHash(heldPlayers[i],null)==heldHashes[i]&&Seconds(heldPlayers[i])==0,"Zero-speed water advanced.");
        pausedSeconds=players.Select(Seconds).ToArray();Time.timeScale=0;wait=EditorApplication.timeSinceStartup;stage=4;
      }
      else if(stage==4&&EditorApplication.timeSinceStartup-wait>1)
      {
        Check(players.Select(Seconds).SequenceEqual(pausedSeconds),"Water advanced while game time was paused.");
        slowSeconds=players.Select(Seconds).ToArray();Time.timeScale=.5f;wait=EditorApplication.timeSinceStartup;stage=5;
      }
      else if(stage==5&&EditorApplication.timeSinceStartup-wait>1)
      {
        for(int i=0;i<3;i++)
        {
          double delta=Seconds(players[i])-slowSeconds[i];
          double expected=.5*(float)Property(players[i],"PlaybackSpeed");
          Check(Math.Abs(delta-expected)<.2,"Scaled playback speed changed.");
        }
        Time.timeScale=1;water.SetActive(false);held.SetActive(false);frame=Time.frameCount;stage=6;
      }
      else if(stage==6&&Time.frameCount>frame+3)
      {
        Released(players);Released(heldPlayers);
        Check(MaterialIndices.All(i=>materials[i].GetTexture("_SurfaceTexture")==Texture2D.blackTexture),"Disabled water retained disposed textures.");
        water.SetActive(true);restarted=Players(water);stage=7;
      }
      else if(stage==7&&Ready(restarted))
      {
        Check(restarted.All(p=>!players.Contains(p)),"Re-enable reused disposed decoders.");
        UnityEngine.Object.Destroy(water);UnityEngine.Object.Destroy(held);frame=Time.frameCount;stage=8;
      }
      else if(stage==8&&Time.frameCount>frame+3)
      {
        Released(restarted);
        Check(materials.All(m=>m==null),"Generated materials leaked.");
        Check(UnityEngine.Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None).Length==0,"Terrain used Unity VideoPlayer.");
        Check(!Directory.Exists("Assets/Cache/Video"),"Terrain created a conversion cache.");
        Finish(true,"Actual S2M water renderer: sea/river/moat each looped twice; bound GPU textures animated; speed/zero-speed/pause preserved; disable/re-enable/destroy released textures/workers; no converter or cache.");
      }
    }
    catch(Exception error){Finish(false,error.ToString());}
  }
}
