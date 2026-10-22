using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Code.Lighting.Editor
{
  /// <summary>Render regression for custom map shaders under the PC SSAO depth/normal prepass.</summary>
  public static class AtmosphereDepthValidation
  {
    [MenuItem("OpenSH2/Lighting/Validate Atmosphere Depth")]
    public static void Run()
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode)
        throw new InvalidOperationException("Run atmosphere validation outside Play mode.");
      var pcPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
      if (GraphicsSettings.currentRenderPipeline != pcPipeline)
        throw new InvalidOperationException("Select the PC quality level to validate the SSAO depth/normal prepass.");

      var resources = new List<UnityEngine.Object>();
      AtmosphereController original = AtmosphereController.Active;
      bool restoreController = original != null && original.enabled;
      if (restoreController) original.enabled = false;
      var root = new GameObject("Atmosphere depth validation") { hideFlags = HideFlags.HideAndDontSave };
      resources.Add(root);
      RenderTexture previousTarget = RenderTexture.active;
      try
      {
        var controller = root.AddComponent<AtmosphereController>();
        var settings = controller.Settings;
        settings.FogColor = Constant(Color.white);
        settings.FogDensity = 0.5f;
        settings.FogStartDistance = 20f;
        settings.FogBaseHeight = 100f;
        settings.MaxFogOpacity = 1f;
        settings.MaxDistance = 100f;
        settings.SunRayIntensity = 2f;
        controller.TimeOfDay = 12f;

        var cameraObject = new GameObject("Validation camera");
        cameraObject.transform.SetParent(root.transform);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 150f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 3f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf);
        var readback = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
        resources.Add(target);
        resources.Add(readback);

        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        card.name = "Near geometry must remain outside fog";
        card.transform.SetParent(root.transform);
        card.layer = 31;
        card.transform.localScale = Vector3.one * 4f;
        Mesh mesh = UnityEngine.Object.Instantiate(card.GetComponent<MeshFilter>().sharedMesh);
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        card.GetComponent<MeshFilter>().sharedMesh = mesh;
        resources.Add(mesh);
        var mask = new Texture2D(4, 1, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
        mask.SetPixels(new[] { Color.clear, Color.green, Color.green, Color.clear });
        mask.Apply();
        resources.Add(mask);
        var terrainTexture = new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, false);
        terrainTexture.SetPixels(new[] { Color.green }, 0);
        terrainTexture.Apply();
        resources.Add(terrainTexture);

        foreach (string shaderName in new[] { "OpenSH2/Stronghold 2 Foliage", "OpenSH2/Stronghold 2 Terrain" })
        {
          Shader shader = Shader.Find(shaderName);
          if (shader == null) throw new Exception("Missing shader " + shaderName);
          var material = new Material(shader);
          resources.Add(material);
          if (material.FindPass("DepthNormals") < 0) throw new Exception(shaderName + " has no DepthNormals pass");
          if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", mask);
          if (material.HasProperty("_TerrainTextureArray")) material.SetTexture("_TerrainTextureArray", terrainTexture);
          for (int p = 0; p < material.passCount; p++) ShaderUtil.CompilePass(material, p, true);
          material.EnableKeyword("_GBUFFER_NORMALS_OCT");
          ShaderUtil.CompilePass(material, material.FindPass("DepthNormals"), true);
          material.DisableKeyword("_GBUFFER_NORMALS_OCT");
          if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader compile failed: " + shaderName);
          card.GetComponent<Renderer>().sharedMaterial = material;

          foreach (bool orthographic in new[] { true, false })
          {
            camera.orthographic = orthographic;
            Color[] baseline = Render(camera, controller, target, readback, false, false);
            foreach (var mode in new[] { (Fog: true, Rays: false), (Fog: false, Rays: true), (Fog: true, Rays: true) })
            {
              Color[] actual = Render(camera, controller, target, readback, mode.Fog, mode.Rays);
              // The opaque card is ten units away, inside the twenty-unit clear zone.
              Color delta = actual[128 * 256 + 128] - baseline[128 * 256 + 128];
              float error = Mathf.Max(Mathf.Abs(delta.r), Mathf.Abs(delta.g), Mathf.Abs(delta.b));
              if (error > 0.015f) throw new Exception($"{shaderName}: near surface washed out (error={error}, ortho={orthographic}, fog={mode.Fog}, rays={mode.Rays})");
              // Negative control: prove fog ran, rather than silently skipping the effect.
              if (mode.Fog && actual[128 * 256 + 12].r < 0.5f)
                throw new Exception("Fog background control failed; the atmosphere pass may not be running.");
              if (mode.Fog && orthographic && shaderName.Contains("Foliage") && actual[128 * 256 + 60].r < 0.5f)
                throw new Exception("Foliage alpha holes incorrectly wrote depth.");
            }
          }
          // Reproduce the original bug: removing this pass must wash out the near card.
          material.SetShaderPassEnabled("DepthNormals", false);
          Color[] missingDepth = Render(camera, controller, target, readback, true, false);
          material.SetShaderPassEnabled("DepthNormals", true);
          if (missingDepth[128 * 256 + 128].r < 0.5f)
            throw new Exception("Missing-depth negative control failed; check that PC SSAO uses DepthNormals.");
          Debug.Log("PASS atmosphere preserves nearby opaque coverage: " + shaderName);
        }
        Debug.Log("PASS atmosphere depth regression: SSAO, terrain, cutout foliage, perspective/orthographic, fog/rays/both.");
      }
      finally
      {
        RenderTexture.active = previousTarget;
        // Destroy the controller first so it restores the scene's lighting.
        UnityEngine.Object.DestroyImmediate(root);
        foreach (UnityEngine.Object resource in resources)
          if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
        if (restoreController) original.enabled = true;
      }
    }

    private static Color[] Render(Camera camera, AtmosphereController controller, RenderTexture target,
      Texture2D readback, bool fog, bool rays)
    {
      controller.Settings.FogEnabled = fog;
      controller.Settings.SunRaysEnabled = rays;
      controller.Apply();
      RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
      RenderTexture.active = target;
      readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
      readback.Apply();
      return readback.GetPixels();
    }

    private static Gradient Constant(Color color) => new Gradient
    {
      colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) }
    };
  }
}
