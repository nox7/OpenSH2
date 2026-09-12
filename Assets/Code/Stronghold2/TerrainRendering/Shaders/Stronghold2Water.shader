Shader "OpenSH2/Stronghold 2 Water"
{
  Properties
  {
    _SurfaceTexture("Water Surface Texture", 2D) = "white" {}
    _BaseColor("Water Tint", Color) = (1, 1, 1, 1)
    _SurfaceTextureScale("Surface Texture Repeats per Cell", Float) = 0.25
    _Opacity("Opacity", Range(0, 1)) = 0.8
    _UseFlowProjectedVideoUvs("Use Flow-Projected Video UVs", Float) = 0
    _UseWaveHighlights("Use Animated Wave Highlights", Float) = 0
    _SurfaceBrightness("Surface Brightness", Float) = 1.35
    _SunGlintIntensity("Sun Glint Intensity", Float) = 1.5
    _SunGlintSharpness("Sun Glint Sharpness", Range(4, 128)) = 32
    _WaveNormalStrength("Wave Normal Strength", Float) = 8
  }

  SubShader
  {
    Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
    Pass
    {
      Name "ForwardLit"
      Tags { "LightMode" = "UniversalForward" }
      Cull Back
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha

      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vertex
      #pragma fragment Fragment

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float _SurfaceTextureScale;
        float _Opacity;
        float _UseFlowProjectedVideoUvs;
        float _UseWaveHighlights;
        float _SurfaceBrightness;
        float _SunGlintIntensity;
        float _SunGlintSharpness;
        float _WaveNormalStrength;
      CBUFFER_END

      TEXTURE2D(_SurfaceTexture); SAMPLER(sampler_SurfaceTexture);

      struct Attributes
      {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 flow : TEXCOORD1;
        float2 worldUv : TEXCOORD2;
      };

      struct Varyings
      {
        float4 positionHCS : SV_POSITION;
        float3 normalWS : TEXCOORD0;
        float2 uv : TEXCOORD1;
        float3 positionWS : TEXCOORD2;
      };

      Varyings Vertex(Attributes input)
      {
        Varyings output;
        output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
        output.uv = input.worldUv * _SurfaceTextureScale;
        if (_UseFlowProjectedVideoUvs > 0.5)
        {
          // Rotating around the shared map origin gives same-direction cells one
          // continuous animated projection instead of visible per-cell playback.
          float2 centeredUv = output.uv;
          output.uv = float2(
            input.flow.x * centeredUv.x - input.flow.y * centeredUv.y,
            input.flow.y * centeredUv.x + input.flow.x * centeredUv.y) + 0.5;
        }
        return output;
      }

      half4 Fragment(Varyings input) : SV_Target
      {
        float2 uv = frac(input.uv);
        half3 surface = SAMPLE_TEXTURE2D(_SurfaceTexture, sampler_SurfaceTexture, uv).rgb;
        half3 albedo = surface * _BaseColor.rgb;
        Light mainLight = GetMainLight();
        float surfaceHeight = dot(surface, float3(0.2126, 0.7152, 0.0722));
        float heightX = dot(SAMPLE_TEXTURE2D(_SurfaceTexture, sampler_SurfaceTexture, frac(uv + float2(0.002, 0))).rgb, float3(0.2126, 0.7152, 0.0722));
        float heightZ = dot(SAMPLE_TEXTURE2D(_SurfaceTexture, sampler_SurfaceTexture, frac(uv + float2(0, 0.002))).rgb, float3(0.2126, 0.7152, 0.0722));

        // The Bink frame provides moving light/dark wave detail. Its local luminance
        // gradient becomes a small simulated normal, producing camera-dependent sun glints.
        float2 videoGradient = (float2(heightX, heightZ) - surfaceHeight) * _WaveNormalStrength * _UseWaveHighlights;
        float3 waveNormalWS = normalize(float3(-videoGradient.x, 1.0, -videoGradient.y));
        float3 baseNormalWS = normalize(input.normalWS);
        float3 waterNormalWS = normalize(lerp(baseNormalWS, waveNormalWS, _UseWaveHighlights));
        float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
        float3 halfDirectionWS = normalize(mainLight.direction + viewDirectionWS);
        half directLight = saturate(dot(waterNormalWS, mainLight.direction));
        // Do not let a smooth, planar mesh create one large white reflection. A glint
        // exists only where the current video frame contains a local moving wave feature.
        float waveFeature = saturate((abs(heightX - surfaceHeight) + abs(heightZ - surfaceHeight)) * 10.0);
        waveFeature *= waveFeature;
        half specular = pow(saturate(dot(waterNormalWS, halfDirectionWS)), _SunGlintSharpness)
          * _SunGlintIntensity * _UseWaveHighlights * waveFeature;
        half3 diffuse = albedo * (SampleSH(waterNormalWS) + mainLight.color * directLight) * _SurfaceBrightness;
        return half4(diffuse + mainLight.color * specular, _Opacity);
      }
      ENDHLSL
    }
  }
}
