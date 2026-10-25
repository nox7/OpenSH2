#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

CBUFFER_START(UnityPerMaterial)
  float4 _BaseMap_ST;
  half4 _BaseColor;
  half _Cutoff;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
UNITY_INSTANCING_BUFFER_START(ModelSway)
  UNITY_DEFINE_INSTANCED_PROP(float4, _ModelSway)
UNITY_INSTANCING_BUFFER_END(ModelSway)
float3 _LightDirection, _LightPosition;

struct Attributes
{
  float3 positionOS : POSITION;
  float3 normalOS : NORMAL;
  float2 uv : TEXCOORD0;
  half4 color : COLOR;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
  float4 positionCS : SV_POSITION;
  float3 positionWS : TEXCOORD0;
  half3 normalWS : TEXCOORD1;
  float2 uv : TEXCOORD2;
  half4 color : COLOR;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

float3 RotateSway(float3 v, float3 axis, float sine, float cosine)
{
  return v * cosine + cross(axis, v) * sine + axis * dot(axis, v) * (1.0 - cosine);
}

Varyings Vertex(Attributes input)
{
  UNITY_SETUP_INSTANCE_ID(input);
  Varyings output;
  UNITY_TRANSFER_INSTANCE_ID(input, output);
  output.positionWS = TransformObjectToWorld(input.positionOS);
  output.normalWS = TransformObjectToWorldNormal(input.normalOS);
  float4 sway = UNITY_ACCESS_INSTANCED_PROP(ModelSway, _ModelSway);
  UNITY_BRANCH if (sway.y > 0.0)
  {
    // One rigid rotation for every vertex of bark and leaves, after model scaling.
    // Unity supplies time; no per-tree CPU transforms, uploads or animation objects.
    float angle = (sin(_Time.y * sway.z * 1.570796327 + sway.x) - 1.0) * sway.y * 0.017453293;
    float sine, cosine, axisX, axisZ;
    sincos(angle, sine, cosine);
    sincos(sway.w, axisZ, axisX);
    float3 axis = TransformObjectToWorldDir(float3(axisX, 0, axisZ));
    float3 origin = TransformObjectToWorld(float3(0, 0, 0));
    output.positionWS = origin + RotateSway(output.positionWS - origin, axis, sine, cosine);
    output.normalWS = RotateSway(output.normalWS, axis, sine, cosine);
  }
  output.positionCS = TransformWorldToHClip(output.positionWS);
  output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
  output.color = input.color;
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
  output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction)));
  return output;
}

void ClipFoliage(Varyings input)
{
  #if defined(_ALPHATEST_ON)
    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * input.color.a * _BaseColor.a - _Cutoff);
  #endif
}

half4 Colour(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
{
  // SH ambient coefficients can be instanced too, not just the sway parameters.
  UNITY_SETUP_INSTANCE_ID(input);
  ClipFoliage(input);
  half3 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * input.color.rgb * _BaseColor.rgb;
  half3 normal = normalize(input.normalWS) * (frontFace ? 1.0h : -1.0h);
  Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
  half shadow = lerp(light.shadowAttenuation, 1.0h, GetMainLightShadowFade(input.positionWS));
  #if defined(_ALPHATEST_ON)
    half diffuse = saturate((dot(normal, light.direction) + 0.3h) / 1.3h);
  #else
    half diffuse = saturate(dot(normal, light.direction));
  #endif
  return half4(colour * (SampleSH(normal) + light.color * diffuse * shadow * light.distanceAttenuation), 1);
}

half4 Depth(Varyings input) : SV_Target { ClipFoliage(input); return 0; }
void Normals(Varyings input, bool frontFace : SV_IsFrontFace, out half4 normalOutput : SV_Target0
  #if defined(_WRITE_RENDERING_LAYERS)
    , out uint renderingLayers : SV_Target1
  #endif
)
{
  UNITY_SETUP_INSTANCE_ID(input);
  ClipFoliage(input);
  float3 normal = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
  #if defined(_GBUFFER_NORMALS_OCT)
    normalOutput = half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
  #else
    normalOutput = half4(normal, 0);
  #endif
  #if defined(_WRITE_RENDERING_LAYERS)
    renderingLayers = EncodeMeshRenderingLayer();
  #endif
}
