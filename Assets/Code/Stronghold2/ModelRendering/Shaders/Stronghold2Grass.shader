Shader "OpenSH2/Stronghold 2 Grass"
{
  Properties
  {
    _BaseMap("Grass Atlas", 2D) = "white" {}
    _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.35
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
    Cull Off
    ZWrite On
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    CBUFFER_START(UnityPerMaterial)
      float4 _BaseMap_ST;
      half _Cutoff;
    CBUFFER_END
    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    struct Attributes { float3 center : POSITION; float2 uv : TEXCOORD0; float2 offset : TEXCOORD1; };
    struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
    Varyings Vertex(Attributes input)
    {
      Varyings output;
      float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
      right.y = 0;
      right = normalize(right + float3(0.000001, 0, 0));
      float widthScale = length(mul((float3x3)unity_ObjectToWorld, float3(1,0,0)));
      float heightScale = length(mul((float3x3)unity_ObjectToWorld, float3(0,1,0)));
      output.positionWS = TransformObjectToWorld(input.center) + right * input.offset.x * widthScale
        + float3(0, input.offset.y * heightScale, 0);
      output.positionCS = TransformWorldToHClip(output.positionWS);
      output.uv = input.uv;
      return output;
    }
    half4 Colour(Varyings input) : SV_Target
    {
      half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
      clip(colour.a - _Cutoff);
      Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
      half shadow = lerp(light.shadowAttenuation, 1.0h, GetMainLightShadowFade(input.positionWS));
      return half4(colour.rgb * (SampleSH(float3(0,1,0)) + light.color * saturate(light.direction.y + 0.3h) * shadow), 1);
    }
    half4 Depth(Varyings input) : SV_Target
    {
      clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
      return 0;
    }
    half4 Normals(Varyings input) : SV_Target
    {
      clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
      #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormal = PackNormalOctQuadEncode(float3(0,1,0));
        return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
      #else
        return half4(0,1,0,0);
      #endif
    }
    ENDHLSL
    Pass
    {
      Name "ForwardLit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vertex
      #pragma fragment Colour
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
      #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
      ENDHLSL
    }
    Pass
    {
      Name "DepthOnly"
      Tags { "LightMode"="DepthOnly" }
      ColorMask R
      HLSLPROGRAM
      #pragma vertex Vertex
      #pragma fragment Depth
      ENDHLSL
    }
    Pass
    {
      Name "DepthNormals"
      Tags { "LightMode"="DepthNormals" }
      HLSLPROGRAM
      #pragma vertex Vertex
      #pragma fragment Normals
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      ENDHLSL
    }
  }
}
