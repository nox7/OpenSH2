// Shared depth/shadow geometry for map terrain and alpha-cutout vegetation.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(OPENSH2_NORMALS_PASS)
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#endif
#if defined(OPENSH2_ALPHA_CLIP)
CBUFFER_START(UnityPerMaterial)
  float4 _BaseMap_ST;
  half4 _BaseColor;
  half _Cutoff;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
#else
CBUFFER_START(UnityPerMaterial)
  float _TextureScale;
  float _CliffWallVerticalTextureScale;
  float _CliffWallHorizontalTextureScale;
  float _AdjacentMaterialBlendWidth;
  float _ApplySerializedTextureRotation;
CBUFFER_END
#endif
float3 _LightDirection, _LightPosition;
struct Attributes
{
  float3 positionOS : POSITION;
  float3 normalOS : NORMAL;
  float2 uv : TEXCOORD0;
  half4 color : COLOR;
};
struct Varyings
{
  float4 positionCS : SV_POSITION;
  float2 uv : TEXCOORD0;
  half alpha : TEXCOORD1;
  #if defined(OPENSH2_NORMALS_PASS)
    float3 normalWS : TEXCOORD2;
  #endif
};
Varyings GeometryVertex(Attributes input)
{
  Varyings output;
  float3 positionWS = TransformObjectToWorld(input.positionOS);
  #if defined(OPENSH2_SHADOW_PASS)
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
      float3 lightDirection = normalize(_LightPosition - positionWS);
    #else
      float3 lightDirection = _LightDirection;
    #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection)));
  #else
    output.positionCS = TransformWorldToHClip(positionWS);
  #endif
  #if defined(OPENSH2_ALPHA_CLIP)
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
  #else
    output.uv = input.uv;
  #endif
  output.alpha = input.color.a;
  #if defined(OPENSH2_NORMALS_PASS)
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
  #endif
  return output;
}
void ClipGeometry(Varyings input)
{
  #if defined(OPENSH2_ALPHA_CLIP)
    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a * input.alpha - _Cutoff);
  #endif
}
half4 GeometryFragment(Varyings input) : SV_Target
{
  ClipGeometry(input);
  return 0;
}

#if defined(OPENSH2_NORMALS_PASS)
void GeometryNormalsFragment(Varyings input, bool frontFace : SV_IsFrontFace,
  out half4 normalOutput : SV_Target0
  #if defined(_WRITE_RENDERING_LAYERS)
    , out uint renderingLayers : SV_Target1
  #endif
)
{
  ClipGeometry(input);
  float3 normalWS = normalize(input.normalWS);
  #if defined(OPENSH2_ALPHA_CLIP)
    normalWS *= frontFace ? 1.0 : -1.0;
  #endif
  #if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormal = PackNormalOctQuadEncode(normalWS);
    normalOutput = half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0.0);
  #else
    normalOutput = half4(normalWS, 0.0);
  #endif
  #if defined(_WRITE_RENDERING_LAYERS)
    renderingLayers = EncodeMeshRenderingLayer();
  #endif
}
#endif
