// Copy to an isolated Unity project's Assets/Editor with OpenSH2's Code.
// Unity -batchmode -executeMethod TreeSwayValidation.Run -quit
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class TreeSwayValidation
{
  const string Repo = "C:/Users/garet/Documents/GitHub/OpenSH2";
  const string Output = Repo + "/Temp/TreeAnimationProbe";
  public static void LightingOnly()
  {
    try
    {
      var material = new Material(Shader.Find("OpenSH2/Stronghold 2 Foliage")) { enableInstancing = true };
      material.SetColor("_BaseColor", new Color(.5f, .5f, .5f));
      var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
      var mesh = UnityEngine.Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
      UnityEngine.Object.DestroyImmediate(cube);
      mesh.vertices = mesh.vertices.Select(v => new Vector3(v.x, (v.y + .5f) * 10f, v.z)).ToArray();
      mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
      mesh.RecalculateBounds();
      var matrices = new[] { Matrix4x4.Translate(new Vector3(-2,0,0)), Matrix4x4.Translate(new Vector3(2,0,0)) };
      var properties = new MaterialPropertyBlock();
      properties.SetVectorArray("_ModelSway", new[] {Vector4.zero, Vector4.zero});
      properties.CopySHCoefficientArraysFrom(new UnityEngine.Rendering.SphericalHarmonicsL2[2]);
      foreach (bool alpha in new[] {false, true})
      {
        if (alpha) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
        var dark = Capture(mesh, material, matrices, properties, "ForwardLit", 0, 0);
        var lit = Capture(mesh, material, matrices, properties, "ForwardLit", 0, 1);
        foreach(int x in new[] {183,330})
          Check(lit[256*512+x].r > dark[256*512+x].r + 70, "Forward+ sunlight missing for " + (alpha ? "leaves" : "bark") + ": dark=" + dark[256*512+x] + ", lit=" + lit[256*512+x]);
      }
      var probes = new UnityEngine.Rendering.SphericalHarmonicsL2[2];
      for (int channel = 0; channel < 3; channel++) probes[1][channel,0] = 4f;
      properties.CopySHCoefficientArraysFrom(probes);
      var ambient = Capture(mesh, material, matrices, properties, "ForwardLit", 0, 0);
      Check(ambient[256*512+330].r > ambient[256*512+183].r + 60, "Instances lost their individual ambient-light probes");
      Check(!ShaderUtil.ShaderHasError(material.shader), "Lighting shader compilation error");
      UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(material);
      File.WriteAllText(Output + "/lighting-validation.txt", "PASS: instanced bark and leaves respond to Forward+ sunlight when the legacy per-object light flag is zero; individual ambient probes reach each fragment.\n");
      Debug.Log("FOLIAGE_LIGHTING_VALIDATION_PASS");
    }
    catch(Exception e) { File.WriteAllText(Output + "/lighting-validation.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
  }
  public static void Run()
  {
    Directory.CreateDirectory(Output);
    LightingOnly();
    try
    {
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      var data = new S2MVegetationAssetLoader().Load(map, "C:/Steam/steamapps/common/Stronghold 2",
        Repo + "/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe", Repo + "/Temp/FoliageProbe/Cache");
      var settings = new S2MVegetationRenderSettings();
      var root = S2MVegetationRenderer.Render(data, settings: settings);
      var block = new MaterialPropertyBlock();
      int animated = 0;
      var phases = new System.Collections.Generic.HashSet<float>();
      var directions = new System.Collections.Generic.HashSet<float>();
      foreach (Transform batch in root.transform)
      {
        bool sways = batch.name.Contains("/tree_") || batch.name.Contains("/forest_tree_")
          || batch.name.Contains("/winter_tree_") || batch.name.Contains("/marsh_tree_") || batch.name.Contains("/shrub_");
        if (batch.name == "Grass Billboards") continue;
        foreach (Transform placement in batch)
        {
          Vector4 first = default;
          var renderers = placement.GetComponentsInChildren<MeshRenderer>();
          for (int i = 0; i < renderers.Length; i++)
          {
            var renderer = renderers[i]; renderer.GetPropertyBlock(block);
            var sway = block.GetVector("_ModelSway");
            Check((sway.y > 0) == sways, "Wrong sway category: " + batch.name);
            if (!sways) continue;
            if (i == 0) { first = sway; animated++; phases.Add(sway.x); directions.Add(sway.w); }
            Check(sway == first, "Bark and leaves have different motion");
            Check(renderer.sharedMaterials.All(m => m.enableInstancing && m.shader.name == "OpenSH2/Stronghold 2 Foliage"), "Non-instanced sway material");
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Check(renderer.localBounds.size.x > mesh.bounds.size.x && renderer.localBounds.size.y > mesh.bounds.size.y, "Sway culling envelope missing");
          }
        }
      }
      Check(animated == 1061 && phases.Count > 900 && directions.Count > 900, "Placement coverage/variation");
      Check(root.GetComponentsInChildren<MonoBehaviour>().All(c =>
        c.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null
        && c.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null), "Per-tree update component added");
      UnityEngine.Object.DestroyImmediate(root);
      settings.TreeSway.Enabled = false;
      root = S2MVegetationRenderer.Render(data, settings: settings);
      foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
      {
        renderer.GetPropertyBlock(block); Check(block.GetVector("_ModelSway") == Vector4.zero, "Disabled sway is active");
      }
      UnityEngine.Object.DestroyImmediate(root);

      // Exercise actual GPU geometry, including an instanced draw, at exact cycle times.
      Shader shader = Shader.Find("OpenSH2/Stronghold 2 Foliage");
      var material = new Material(shader) { enableInstancing = true };
      var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
      var meshCopy = UnityEngine.Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
      UnityEngine.Object.DestroyImmediate(cube);
      meshCopy.vertices = meshCopy.vertices.Select(v => new Vector3(v.x, (v.y + .5f) * 10f, v.z)).ToArray();
      meshCopy.RecalculateBounds();
      var matrices = new[] { Matrix4x4.Translate(new Vector3(-2, 0, 0)), Matrix4x4.Translate(new Vector3(2, 0, 0)) };
      var properties = new MaterialPropertyBlock();
      properties.SetVectorArray("_ModelSway", new[] { new Vector4(0, 2, 1, 0), new Vector4(2, 2, 1, 2) });
      int differences = 0;
      foreach (string pass in new[] { "DepthOnly", "DepthNormals", "ForwardLit", "ShadowCaster" })
      {
        var a = Capture(meshCopy, material, matrices, properties, pass, 0);
        var b = Capture(meshCopy, material, matrices, properties, pass, 1);
        var c = Capture(meshCopy, material, matrices, properties, pass, 4);
        int delta = a.Zip(b, (x, y) => x.Equals(y) ? 0 : 1).Sum();
        int cycle = a.Zip(c, (x, y) => x.Equals(y) ? 0 : 1).Sum();
        if (pass == "DepthOnly" || pass == "DepthNormals") Check(delta > 50 && cycle < 5, pass + " did not sway/repeat: " + delta + "/" + cycle);
        differences += delta;
      }
      Check(!ShaderUtil.ShaderHasError(shader), "Shader compilation error");
      UnityEngine.Object.DestroyImmediate(meshCopy); UnityEngine.Object.DestroyImmediate(material);
      File.WriteAllText(Output + "/sway-validation.txt", $"PASS: {animated} animated tree/bush placements; {phases.Count} phases, {directions.Count} directions; shared bark/leaf motion, instancing, expanded culling, disabled mode, no per-tree Update; GPU geometry changes and four-second periodicity in depth/normal passes. Pixel changes: {differences}.\n");
      Debug.Log("TREE_SWAY_VALIDATION_PASS");
    }
    catch (Exception e) { File.WriteAllText(Output + "/sway-validation.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
  }

  static Color32[] Capture(Mesh mesh, Material material, Matrix4x4[] matrices, MaterialPropertyBlock properties, string pass, float time, float sunIntensity = -1)
  {
    var rt = new RenderTexture(512, 512, 24);
    rt.Create();
    var commands = new CommandBuffer();
    commands.SetRenderTarget(rt);
    commands.ClearRenderTarget(true, true, Color.white);
    var view = Matrix4x4.TRS(new Vector3(0, 5, -25), Quaternion.identity, Vector3.one).inverse;
    view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * view;
    commands.SetViewProjectionMatrices(view, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-7, 7, -7, 7, .1f, 100), true));
    // The render-texture projection flips winding relative to this manual view.
    commands.SetInvertCulling(true);
    commands.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
    if (sunIntensity >= 0)
    {
      commands.EnableShaderKeyword("_CLUSTER_LIGHT_LOOP");
      commands.DisableShaderKeyword("_MAIN_LIGHT_SHADOWS");
      commands.DisableShaderKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
      commands.SetGlobalVector("_MainLightPosition", new Vector4(0,0,-1,0));
      commands.SetGlobalVector("_MainLightColor", new Vector4(sunIntensity,sunIntensity,sunIntensity,1));
      commands.SetGlobalVector("unity_LightData", Vector4.zero);
    }
    commands.DrawMeshInstanced(mesh, 0, material, material.FindPass(pass), matrices, matrices.Length, properties);
    commands.SetInvertCulling(false);
    Graphics.ExecuteCommandBuffer(commands); commands.Dispose();
    RenderTexture.active = rt;
    var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
    texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply();
    if (pass == "DepthNormals") File.WriteAllBytes(Output + "/sway-time-" + time + ".png", texture.EncodeToPNG());
    if (sunIntensity >= 0) File.WriteAllBytes(Output + "/lighting-sun-" + sunIntensity + ".png", texture.EncodeToPNG());
    Color32[] pixels = texture.GetPixels32();
    RenderTexture.active = null;
    UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
    return pixels;
  }
  static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
