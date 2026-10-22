Shader "OpenSH2/Atmosphere Sky"
{
  SubShader
  {
    Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
    Cull Off ZWrite Off
    Pass
    {
      HLSLPROGRAM
      #pragma vertex Vertex
      #pragma fragment Fragment
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      float3 _Zenith, _Horizon, _Sunset, _SunColor, _SunDirection;
      float _SunsetStrength, _SunSize;
      struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
      Varyings Vertex(Attributes input)
      {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS);
        output.direction = input.positionOS;
        return output;
      }
      half4 Fragment(Varyings input) : SV_Target
      {
        float3 direction = normalize(input.direction);
        float horizon = pow(1.0 - saturate(direction.y), 4.0);
        float facingSun = saturate(dot(direction, _SunDirection));
        float3 color = lerp(_Zenith, _Horizon, horizon);
        color = lerp(color, _Sunset, horizon * _SunsetStrength * (0.2 + 0.8 * pow(facingSun, 4.0)));
        float disc = smoothstep(cos(_SunSize), cos(_SunSize * 0.8), dot(direction, _SunDirection));
        float glow = pow(facingSun, 64.0) * 0.2;
        color += _SunColor * (disc * 4.0 + glow) * smoothstep(-0.03, 0.02, direction.y);
        return half4(color, 1.0);
      }
      ENDHLSL
    }
  }
}
