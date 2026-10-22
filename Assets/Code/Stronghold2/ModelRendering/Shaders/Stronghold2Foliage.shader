Shader "OpenSH2/Stronghold 2 Foliage"
{
  Properties
  {
    _BaseMap("Diffuse Map", 2D) = "white" {}
    _BaseColor("Tint", Color) = (1, 1, 1, 1)
    _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5333333
  }

  SubShader
  {
    Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }

    Pass
    {
      Name "ForwardLit"
      Tags { "LightMode" = "UniversalForward" }
      Cull Off
      ZWrite On
      Blend One Zero

      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vertex
      #pragma fragment Fragment
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
      #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _Cutoff;
      CBUFFER_END

      TEXTURE2D(_BaseMap);
      SAMPLER(sampler_BaseMap);

      struct Attributes
      {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
        half4 color : COLOR;
      };

      struct Varyings
      {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float3 positionWS : TEXCOORD2;
        half4 color : COLOR;
      };

      Varyings Vertex(Attributes input)
      {
        Varyings output;
        output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
        output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        output.color = input.color;
        return output;
      }

      half4 Fragment(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
      {
        half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color * _BaseColor;
        clip(colour.a - _Cutoff);
        half3 normal = normalize(input.normalWS) * (frontFace ? 1.0h : -1.0h);
        Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
        half shadow = lerp(light.shadowAttenuation, 1.0h, GetMainLightShadowFade(input.positionWS));
        // A little wrap keeps thin leaf cards from becoming black at grazing angles.
        half diffuse = saturate((dot(normal, light.direction) + 0.3h) / 1.3h);
        return half4(colour.rgb * (SampleSH(normal) + light.color * diffuse * shadow), 1.0h);
      }
      ENDHLSL
    }
    Pass
    {
      Name "ShadowCaster"
      Tags { "LightMode" = "ShadowCaster" }
      ZWrite On ZTest LEqual ColorMask 0 Cull Off
      HLSLPROGRAM
      #pragma vertex GeometryVertex
      #pragma fragment GeometryFragment
      #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
      #define OPENSH2_ALPHA_CLIP
      #define OPENSH2_SHADOW_PASS
      #include "Assets/Code/Lighting/Shaders/MapGeometryPass.hlsl"
      ENDHLSL
    }
    Pass
    {
      Name "DepthNormals"
      Tags { "LightMode" = "DepthNormals" }
      ZWrite On Cull Off
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex GeometryVertex
      #pragma fragment GeometryNormalsFragment
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
      #define OPENSH2_ALPHA_CLIP
      #define OPENSH2_NORMALS_PASS
      #include "Assets/Code/Lighting/Shaders/MapGeometryPass.hlsl"
      ENDHLSL
    }
    Pass
    {
      Name "DepthOnly"
      Tags { "LightMode" = "DepthOnly" }
      ZWrite On ColorMask R Cull Off
      HLSLPROGRAM
      #pragma vertex GeometryVertex
      #pragma fragment GeometryFragment
      #define OPENSH2_ALPHA_CLIP
      #include "Assets/Code/Lighting/Shaders/MapGeometryPass.hlsl"
      ENDHLSL
    }
  }
}
