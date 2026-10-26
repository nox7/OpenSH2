Shader "OpenSH2/Instanced Actor"
{
  Properties
  {
    _BaseMap("Diffuse", 2D) = "white" {}
    _BaseColor("Tint", Color) = (1,1,1,1)
    [HideInInspector] _LiveryMode("Livery mode", Float) = 0
    [HideInInspector] _CrestArray("Lord crests", 2DArray) = "" {}
    _Cutoff("Alpha cutoff", Range(0,1)) = .5
    _AlphaClip("Alpha clip", Float) = 0
    _Cull("Cull", Float) = 2
    [HideInInspector] _ActorPoses("Bone poses", 2D) = "white" {}
    [HideInInspector] _ActorClip("Clip", Vector) = (1,2,1,.01)
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    Cull [_Cull]
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    TEXTURE2D(_ActorPoses);
    TEXTURE2D_ARRAY(_CrestArray); SAMPLER(sampler_CrestArray);
    CBUFFER_START(UnityPerMaterial)
      float4 _BaseMap_ST, _BaseColor, _ActorClip;
      float _Cutoff, _AlphaClip, _LiveryMode;
    CBUFFER_END
    float _ActorClock, _ActorReceiveShadows;
    UNITY_INSTANCING_BUFFER_START(Actor)
      UNITY_DEFINE_INSTANCED_PROP(float4, _ActorPlayback)
      UNITY_DEFINE_INSTANCED_PROP(float4, _ActorLivery)
    UNITY_INSTANCING_BUFFER_END(Actor)
    struct Attributes
    {
      float4 positionOS : POSITION;
      float3 normalOS : NORMAL;
      float2 uv : TEXCOORD0;
      float4 bones : TEXCOORD1;
      float4 weights : TEXCOORD2;
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct Varyings
    {
      float4 positionCS : SV_POSITION;
      float3 positionWS : TEXCOORD0;
      half3 normalWS : TEXCOORD1;
      float2 uv : TEXCOORD2;
      UNITY_VERTEX_INPUT_INSTANCE_ID
      UNITY_VERTEX_OUTPUT_STEREO
    };
    void Skin(Attributes input, out float3 position, out float3 normal)
    {
      float4 playback = UNITY_ACCESS_INSTANCED_PROP(Actor, _ActorPlayback);
      float seconds = playback.x + _ActorClock * playback.y;
      float t = seconds / _ActorClip.z;
      t = playback.z > .5 ? frac(t) : saturate(t);
      float frame = t * (_ActorClip.y - 1);
      int first = (int)floor(frame), second = min(first + 1, (int)_ActorClip.y - 1);
      float blend = frac(frame);
      float3 p = input.positionOS.xzy / _ActorClip.w;
      float3 n = input.normalOS.xzy;
      position = 0; normal = 0;
      [unroll] for (int w = 0; w < 4; w++)
      {
        int bone = (int)input.bones[w] * 4;
        float4 r0 = lerp(LOAD_TEXTURE2D(_ActorPoses, int2(bone, first)), LOAD_TEXTURE2D(_ActorPoses, int2(bone, second)), blend);
        float4 r1 = lerp(LOAD_TEXTURE2D(_ActorPoses, int2(bone+1, first)), LOAD_TEXTURE2D(_ActorPoses, int2(bone+1, second)), blend);
        float4 r2 = lerp(LOAD_TEXTURE2D(_ActorPoses, int2(bone+2, first)), LOAD_TEXTURE2D(_ActorPoses, int2(bone+2, second)), blend);
        float4 r3 = lerp(LOAD_TEXTURE2D(_ActorPoses, int2(bone+3, first)), LOAD_TEXTURE2D(_ActorPoses, int2(bone+3, second)), blend);
        position += (p.x * r0.xyz + p.y * r1.xyz + p.z * r2.xyz + r3.xyz) * input.weights[w];
        normal += (n.x * r0.xyz + n.y * r1.xyz + n.z * r2.xyz) * input.weights[w];
      }
      position = position.xzy * _ActorClip.w;
      normal = normalize(normal.xzy);
    }
    Varyings ActorVertex(Attributes input)
    {
      Varyings o = (Varyings)0;
      UNITY_SETUP_INSTANCE_ID(input);
      UNITY_TRANSFER_INSTANCE_ID(input, o);
      UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
      float3 p, n; Skin(input, p, n);
      o.positionWS = TransformObjectToWorld(p);
      o.normalWS = TransformObjectToWorldNormal(n);
      o.positionCS = TransformWorldToHClip(o.positionWS);
      o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
      return o;
    }
    half4 Diffuse(Varyings input)
    {
      half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
      float4 livery = UNITY_ACCESS_INSTANCED_PROP(Actor, _ActorLivery);
      if (_LiveryMode > 2.5)
        c = half4(SAMPLE_TEXTURE2D_ARRAY(_CrestArray, sampler_CrestArray, input.uv, livery.w).rgb * _BaseColor.rgb, 1);
      else if (_LiveryMode > 1.5)
        c = half4(c.rgb * lerp(half3(1,1,1), livery.rgb, c.a), 1);
      else if (_LiveryMode > .5)
        c.rgb *= livery.rgb;
      if (_AlphaClip > .5) clip(c.a - _Cutoff);
      return c;
    }
    ENDHLSL
    Pass
    {
      Name "ForwardLit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex ActorVertex
      #pragma fragment Forward
      #pragma multi_compile_instancing
      #pragma instancing_options forcemaxcount:256
      #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
      #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
      half4 Forward(Varyings input) : SV_Target
      {
        UNITY_SETUP_INSTANCE_ID(input);
        half3 n = normalize(input.normalWS);
        Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
        half shadow = lerp(light.shadowAttenuation, 1.0h, GetMainLightShadowFade(input.positionWS));
        half3 illumination = SampleSH(n) + light.color * saturate(dot(n, light.direction)) * light.distanceAttenuation * lerp(1, shadow, _ActorReceiveShadows);
        // Opaque actors participate in the same depth-based atmosphere as the map.
        return half4(Diffuse(input).rgb * illumination, 1);
      }
      ENDHLSL
    }
    Pass
    {
      Name "ShadowCaster"
      Tags { "LightMode"="ShadowCaster" }
      ZWrite On ZTest LEqual ColorMask 0
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex ShadowVertex
      #pragma fragment Depth
      #pragma multi_compile_instancing
      #pragma instancing_options forcemaxcount:256
      #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
      float3 _LightDirection, _LightPosition;
      Varyings ShadowVertex(Attributes input)
      {
        Varyings o = ActorVertex(input);
        #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
          float3 direction = normalize(_LightPosition - o.positionWS);
        #else
          float3 direction = _LightDirection;
        #endif
        o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS, o.normalWS, direction));
        #if UNITY_REVERSED_Z
          o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
        #else
          o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
        #endif
        return o;
      }
      half4 Depth(Varyings input) : SV_Target { UNITY_SETUP_INSTANCE_ID(input); Diffuse(input); return 0; }
      ENDHLSL
    }
    Pass
    {
      Name "DepthNormals"
      Tags { "LightMode"="DepthNormals" }
      ZWrite On
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex ActorVertex
      #pragma fragment ActorNormals
      #pragma multi_compile_instancing
      #pragma instancing_options forcemaxcount:256
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
      void ActorNormals(Varyings input, bool frontFace : SV_IsFrontFace, out half4 normalOutput : SV_Target0
        #if defined(_WRITE_RENDERING_LAYERS)
          , out uint renderingLayers : SV_Target1
        #endif
      )
      {
        UNITY_SETUP_INSTANCE_ID(input);
        Diffuse(input);
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
      ENDHLSL
    }
    Pass
    {
      Name "DepthOnly"
      Tags { "LightMode"="DepthOnly" }
      ZWrite On ColorMask R
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex ActorVertex
      #pragma fragment Depth
      #pragma multi_compile_instancing
      #pragma instancing_options forcemaxcount:256
      half4 Depth(Varyings input) : SV_Target { Diffuse(input); return input.positionCS.z; }
      ENDHLSL
    }
  }
}

