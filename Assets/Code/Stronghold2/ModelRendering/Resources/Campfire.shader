Shader "OpenSH2/Campfire"
{
  Properties { _MainTex ("Original fire texture", 2D) = "white" {} _FireTime ("Simulation time", Float) = 0 }
  SubShader
  {
    Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
    Pass
    {
      Blend SrcAlpha One
      ZWrite Off
      Cull Off
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
        float _FireTime;
      CBUFFER_END
      struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
      struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
      Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
      half4 frag(Varyings i) : SV_Target
      {
        float2 uv=i.uv;
        half3 fire=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(uv.x,uv.y-_FireTime*.8)).rgb;
        half mask=saturate(1-abs(uv.x*2-1)) * saturate((1-uv.y)*2);
        half pulse=.85+.15*sin(_FireTime*17+uv.y*8);
        return half4(fire*1.6, mask*pulse*max(fire.r,max(fire.g,fire.b)));
      }
      ENDHLSL
    }
  }
}
