Shader "OpenSH2/Stronghold 2 Natural Stone"
{
  Properties
  {
    [MainTexture] _BaseMap("Diffuse Map", 2D) = "white" {}
    [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
    _TextureBrightness("Texture Brightness", Float) = 1
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
    CBUFFER_START(UnityPerMaterial)
      float4 _BaseMap_ST;
      half4 _BaseColor;
      float _TextureBrightness;
    CBUFFER_END
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    float3 _LightDirection;
    float3 _LightPosition;
    struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
    struct Varyings
    {
      float4 positionCS : SV_POSITION;
      float3 positionWS : TEXCOORD0;
      float3 normalWS : TEXCOORD1;
      float2 uv : TEXCOORD2;
    };
    Varyings Vertex(Attributes input)
    {
      Varyings output;
      output.positionWS = TransformObjectToWorld(input.positionOS);
      output.normalWS = TransformObjectToWorldNormal(input.normalOS);
      output.positionCS = TransformWorldToHClip(output.positionWS);
      output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
      return output;
    }
    Varyings ShadowVertex(Attributes input)
    {
      Varyings output = Vertex(input);
      #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 direction = normalize(_LightPosition - output.positionWS);
      #else
        float3 direction = _LightDirection;
      #endif
      output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction));
      #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
      #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
      #endif
      return output;
    }
    half4 Colour(Varyings input) : SV_Target
    {
      half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb * _TextureBrightness;
      half3 normal = normalize(input.normalWS);
      half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
      Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
      half shadow = lerp(light.shadowAttenuation, 1.0h, GetMainLightShadowFade(input.positionWS));
      // Landscape/Default.fx uses PhongShader's unit defaults: diffuse plus
      // 0.6 * rim^2, and ambient cube * 0.7. It is not a metallic/PBR BRDF.
      // Use the current scene's SH ambient in place of the original ambient cube,
      // retaining time-of-day colour, sun direction and real-time shadows.
      half rim = 1.0h - saturate(dot(normal, view));
      half diffuse = saturate(dot(normal, light.direction)) + 0.6h * rim * rim;
      half3 illumination = max(SampleSH(normal), 0) * 0.7h
        + light.color * diffuse * shadow * light.distanceAttenuation;
      // raw_stone.dds has alpha zero everywhere. In the original this is a
      // zero-gloss mask; the surface remains fully opaque, with no specular term.
      return half4(albedo * illumination, 1);
    }
    half4 Depth(Varyings input) : SV_Target { return 0; }
    half4 Normals(Varyings input) : SV_Target
    {
      float3 normal = normalize(input.normalWS);
      #if defined(_GBUFFER_NORMALS_OCT)
        return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
      #else
        return half4(normal, 0);
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
      Name "ShadowCaster"
      Tags { "LightMode"="ShadowCaster" }
      ZWrite On ZTest LEqual ColorMask 0
      HLSLPROGRAM
      #pragma vertex ShadowVertex
      #pragma fragment Depth
      #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
      ENDHLSL
    }
    Pass
    {
      Name "DepthOnly"
      Tags { "LightMode"="DepthOnly" }
      ZWrite On ColorMask R
      HLSLPROGRAM
      #pragma vertex Vertex
      #pragma fragment Depth
      ENDHLSL
    }
    Pass
    {
      Name "DepthNormals"
      Tags { "LightMode"="DepthNormals" }
      ZWrite On
      HLSLPROGRAM
      #pragma vertex Vertex
      #pragma fragment Normals
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      ENDHLSL
    }
  }
}
