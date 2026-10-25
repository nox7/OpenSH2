// Run in the isolated Unity validation project with OpenSH2's Code and PC URP settings.
using System;
using System.IO;
using System.Linq;
using Assets.Code.Lighting;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.ModelRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class NaturalStoneMaterialValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Install = "C:/Steam/steamapps/common/Stronghold 2";
  static string Output => Repo + "/Temp/NaturalStoneProbe";
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      string[] paths = "ABCDEF".SelectMany(c => new[] {1,2}.Select(i => $"meshes/landscape/rock_natural_stone_{c}{i}.gr2")).ToArray();
      var models = Granny2AssetConverter.LoadOrConvertMany(Install,paths,
        Repo+"/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe",Output+"/Cache");
      Shader shader=Shader.Find("OpenSH2/Stronghold 2 Natural Stone"); Check(shader!=null,"Missing shader");
      // Reproduce a serialized Unity object wrapper with no native object.
      // A normal C# null misses the regression caused by null coalescing.
      var missingShader=(Shader)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Shader));
      Check(!ReferenceEquals(missingShader,null) && missingShader==null,"Missing-reference fixture is not Unity-null");
      var recovered=Granny2ModelRenderer.Render(models[paths[0]].Model,
        settings:new Granny2ModelRenderSettings {UseFoliageMaterials=false,OpaqueShader=missingShader});
      Check(recovered.GetComponentsInChildren<MeshRenderer>().SelectMany(m=>m.sharedMaterials)
        .All(m=>m.shader!=null && m.shader.name=="Universal Render Pipeline/Lit"),"Missing override blocked opaque shader fallback");
      UnityEngine.Object.DestroyImmediate(recovered);
      foreach(var pair in models)
      {
        Check(pair.Value.Error==null,"Conversion: "+pair.Key);
        Check(pair.Value.Model.Meshes.SelectMany(m=>m.Materials).All(m=>Path.GetFileNameWithoutExtension(m.TexturePath)=="raw_stone"),"Wrong source texture");
      }
      var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
      GraphicsSettings.defaultRenderPipeline=pipeline; QualitySettings.renderPipeline=pipeline;
      var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;
      light.transform.rotation=Quaternion.Euler(45,-35,0);light.intensity=1;light.color=Color.white;
      var camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;
      camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.12f);
      camera.nearClipPlane=.01f;camera.farClipPlane=200;
      camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
      var model=models[paths[0]].Model;
      var before=Granny2ModelRenderer.Render(model,settings:new Granny2ModelRenderSettings {UseFoliageMaterials=false});
      var after=Granny2ModelRenderer.Render(model,settings:new Granny2ModelRenderSettings {UseFoliageMaterials=false,OpaqueShader=shader});
      Bounds bounds=Granny2ModelRenderer.GetLocalBounds(model,.01f);
      camera.transform.position=bounds.center+new Vector3(-1,1.3f,-1.5f)*bounds.size.magnitude;
      camera.transform.LookAt(bounds.center);camera.orthographicSize=bounds.size.magnitude*.55f;
      SetAmbient(new Color(.35f,.35f,.35f));
      before.SetActive(true);after.SetActive(false);float oldBrightness=Capture(camera,"neutral-before.png");
      before.SetActive(false);after.SetActive(true);float newBrightness=Capture(camera,"neutral-after.png");
      Check(newBrightness>oldBrightness*1.05f,"Legacy diffuse response was not brighter");
      foreach(var m in after.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials))
        m.SetFloat("_TextureBrightness",2f);
      float lightenedBrightness=Capture(camera,"texture-brightness-2.png");
      Check(lightenedBrightness>newBrightness*1.15f,"Texture brightness did not lighten the stone");
      foreach(var m in after.GetComponentsInChildren<MeshRenderer>().SelectMany(m=>m.sharedMaterials))
      {
        Check(m.GetTexture("_BaseMap").name=="raw_stone","Wrong resolved texture");
        Check(m.shader==shader && !ShaderUtil.ShaderHasError(m.shader),"Stone shader error");
        Check(m.FindPass("ShadowCaster")>=0 && m.FindPass("DepthOnly")>=0 && m.FindPass("DepthNormals")>=0,"Missing geometry pass");
      }
      var atmosphere=new GameObject("Atmosphere").AddComponent<AtmosphereController>();
      atmosphere.TimeOfDay=15.7f; SetAmbient(RenderSettings.ambientSkyColor.linear);
      before.SetActive(true);after.SetActive(false);Capture(camera,"afternoon-before.png");
      before.SetActive(false);after.SetActive(true);Capture(camera,"afternoon-after.png");
      UnityEngine.Object.DestroyImmediate(atmosphere.gameObject);
      light.intensity=0;SetAmbient(Color.black);float night=Capture(camera,"no-light.png");
      Check(night<.001f,"Natural stone glows without lighting");
      Check(!ShaderUtil.ShaderHasError(shader),"Shader variant error after renders");
      File.WriteAllText(Output+"/validation.txt",$"PASS: twelve raw_stone references; correct DDS; opaque zero-alpha texture; diffuse brightness {oldBrightness:F4} -> {newBrightness:F4}; texture multiplier 2 -> {lightenedBrightness:F4}; night {night:F4}; shader and shadow/depth passes; neutral/afternoon before-after captures.\n");
      UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);
      Debug.Log("NATURAL_STONE_MATERIAL_VALIDATION_PASS");
    }
    catch(Exception e) {File.WriteAllText(Output+"/validation.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static void SetAmbient(Color colour)
  {
    RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=colour;
    var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(colour);RenderSettings.ambientProbe=probe;
  }
  static float Capture(Camera camera,string name)
  {
    var rt=new RenderTexture(800,700,24);
    for(int i=0;i<2;i++)RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
    RenderTexture.active=rt;var im=new Texture2D(800,700,TextureFormat.RGB24,false);
    im.ReadPixels(new Rect(0,0,800,700),0,0);im.Apply();File.WriteAllBytes(Output+"/"+name,im.EncodeToPNG());
    Color background=im.GetPixel(0,0);double sum=0;int count=0;
    foreach(Color c in im.GetPixels())if(Mathf.Abs(c.r-background.r)+Mathf.Abs(c.g-background.g)+Mathf.Abs(c.b-background.b)>.025f)
    {sum+=c.grayscale;count++;}
    RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(im);UnityEngine.Object.DestroyImmediate(rt);
    Check(count>1000,"Stone did not render (texture alpha is not opacity)");return(float)(sum/count);
  }
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
