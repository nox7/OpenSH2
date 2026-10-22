Shader "Hidden/OpenSH2/Atmosphere"
{
  SubShader
  {
    Tags { "RenderPipeline" = "UniversalPipeline" }
    Pass
    {
      ZWrite Off ZTest Always Cull Off
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vert
      #pragma fragment Fragment
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
      float4 _FogParams, _RayParams;
      float3 _FogColor, _RayColor, _SunDirection;
      float _ScatteringDensity;

      half4 Fragment(Varyings input) : SV_Target
      {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        float depth = SampleSceneDepth(uv);
        #if !UNITY_REVERSED_Z
          depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
        #endif
        float3 endWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
        // Reconstruct the near plane as well, supporting orthographic map cameras.
        #if UNITY_REVERSED_Z
          float nearDepth = 1.0;
        #else
          float nearDepth = UNITY_NEAR_CLIP_VALUE;
        #endif
        float3 nearWS = ComputeWorldSpacePosition(uv, nearDepth, UNITY_MATRIX_I_VP);
        float3 origin = unity_OrthoParams.w > 0.5 ? nearWS : _WorldSpaceCameraPos;
        float3 delta = endWS - origin;
        float distanceWS = min(length(delta), _RayParams.x);
        float3 direction = normalize(delta);
        float start = min(_FogParams.w, distanceWS);
        int steps = (int)_RayParams.z;
        float stepSize = (distanceWS - start) / steps;
        // Fixed screen-space jitter hides march bands without temporal shimmer.
        float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
        float opticalDepth = 0.0;
        float scattering = 0.0;
        float g = _RayParams.y;
        float cosine = dot(direction, _SunDirection);
        float phase = (1.0 - g * g) / (4.0 * PI * pow(max(0.01, 1.0 + g*g - 2.0*g*cosine), 1.5));
        bool hasRays = any(_RayColor > 0.0);
        [loop] for (int i = 0; i < steps; i++)
        {
          float3 p = origin + direction * (start + (i + jitter) * stepSize);
          float density = exp(clamp(-max(0.0, p.y - _FogParams.y) * _FogParams.z, -80.0, 0.0));
          float transmittance = exp(-opticalDepth);
          float stepOpticalDepth = density * _ScatteringDensity * stepSize;
          if (hasRays)
          {
            float shadow = 1.0;
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
              float4 shadowCoord = TransformWorldToShadowCoord(p);
              // Directional shadows do not need a perspective divide. A valid w
              // also avoids an unreachable division-by-zero warning in URP's sampler.
              shadowCoord.w = 1.0;
              shadow = MainLightRealtimeShadow(shadowCoord);
              shadow = lerp(shadow, 1.0, GetMainLightShadowFade(p));
            #endif
            // Integrate the segment's lost transmittance rather than adding
            // density indefinitely when the fog-compositing toggle is off.
            scattering += transmittance * shadow * (1.0 - exp(-stepOpticalDepth));
          }
          opticalDepth += stepOpticalDepth;
        }
        float fog = _FogParams.x > 0.0 ? min(1.0 - exp(-opticalDepth), _RayParams.w) : 0.0;
        return half4(lerp(source.rgb, _FogColor, fog) + _RayColor * scattering * phase, source.a);
      }
      ENDHLSL
    }
  }
}
