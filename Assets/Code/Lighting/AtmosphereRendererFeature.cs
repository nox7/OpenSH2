using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Assets.Code.Lighting
{
  public sealed class AtmosphereRendererFeature : ScriptableRendererFeature
  {
    [SerializeField] private Shader atmosphereShader;
    private Material material;
    private AtmospherePass pass;

    public override void Create()
    {
      CoreUtils.Destroy(material);
      material = atmosphereShader != null ? CoreUtils.CreateEngineMaterial(atmosphereShader) : null;
      pass = new AtmospherePass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
      var controller = AtmosphereController.Active;
      var camera = renderingData.cameraData;
      if (material == null || controller == null || controller.Settings == null) return;
      if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
      if (camera.renderType == CameraRenderType.Overlay) return;
      if (camera.isSceneViewCamera && !controller.Settings.ShowInSceneView) return;
      if (!controller.Settings.FogEnabled && !controller.Settings.SunRaysEnabled) return;
      renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

    private sealed class AtmospherePass : ScriptableRenderPass
    {
      private readonly Material material;
      private sealed class PassData { public TextureHandle Source; public Material Material; }

      public AtmospherePass(Material material)
      {
        this.material = material;
        // Opaque geometry and sky are fogged before transparent water and UI.
        renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        requiresIntermediateTexture = true;
        ConfigureInput(ScriptableRenderPassInput.Depth);
      }

      public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
      {
        var controller = AtmosphereController.Active;
        var resources = frameData.Get<UniversalResourceData>();
        if (controller == null || resources.isActiveTargetBackBuffer) return;
        AtmosphereSettings s = controller.Settings;
        material.SetColor("_FogColor", controller.CurrentFogColor);
        material.SetColor("_RayColor", controller.CurrentSunColor * (s.SunRaysEnabled ? s.SunRayIntensity : 0f));
        material.SetVector("_SunDirection", controller.Sun != null ? -controller.Sun.transform.forward : Vector3.up);
        material.SetVector("_FogParams", new Vector4(s.FogEnabled ? Mathf.Max(0f, s.FogDensity) : 0f,
          s.FogBaseHeight, Mathf.Max(0.001f, s.FogHeightFalloff), Mathf.Max(0f, s.FogStartDistance)));
        // Scattering can remain enabled independently of fog extinction.
        material.SetVector("_RayParams", new Vector4(Mathf.Max(1f, s.MaxDistance), Mathf.Clamp(s.Anisotropy, 0f, 0.95f),
          Mathf.Clamp(s.RaySteps, 8, 96), Mathf.Clamp01(s.MaxFogOpacity)));
        material.SetFloat("_ScatteringDensity", Mathf.Max(0f, s.FogDensity));
        TextureHandle source = resources.activeColorTexture;
        var desc = graph.GetTextureDesc(source);
        desc.name = "OpenSH2 Atmosphere";
        desc.clearBuffer = false;
        TextureHandle destination = graph.CreateTexture(desc);
        using (var builder = graph.AddRasterRenderPass<PassData>("OpenSH2 Fog and Sun Rays", out var data))
        {
          data.Source = source;
          data.Material = material;
          builder.UseTexture(source);
          builder.UseTexture(resources.cameraDepthTexture);
          if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture);
          builder.UseAllGlobalTextures(true);
          builder.SetRenderAttachment(destination, 0);
          builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
            Blitter.BlitTexture(context.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, 0));
        }
        resources.cameraColor = destination;
      }
    }
  }
}
