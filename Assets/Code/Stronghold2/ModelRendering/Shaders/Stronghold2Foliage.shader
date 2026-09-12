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
      Name "ForwardUnlit"
      Tags { "LightMode" = "UniversalForward" }
      Cull Off
      ZWrite On
      Blend One Zero

      HLSLPROGRAM
      #pragma vertex Vertex
      #pragma fragment Fragment

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
        float2 uv : TEXCOORD0;
        half4 color : COLOR;
      };

      struct Varyings
      {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        half4 color : COLOR;
      };

      Varyings Vertex(Attributes input)
      {
        Varyings output;
        output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        output.color = input.color;
        return output;
      }

      half4 Fragment(Varyings input) : SV_Target
      {
        half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color * _BaseColor;
        clip(colour.a - _Cutoff);
        return half4(colour.rgb, 1.0h);
      }
      ENDHLSL
    }
  }
}
