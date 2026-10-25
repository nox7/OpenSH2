Shader "OpenSH2/Stronghold 2 Foliage"
{
  Properties
  {
    _BaseMap("Diffuse Map", 2D) = "white" {}
    _BaseColor("Tint", Color) = (1,1,1,1)
    _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5333333
    [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    [HideInInspector] _ModelSway("Model Sway", Vector) = (0,0,0,0)
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
    Pass
    {
      Name "ForwardLit"
      Tags { "LightMode"="UniversalForward" }
      Cull [_Cull] ZWrite On
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vertex
      #pragma fragment Colour
      #pragma multi_compile_instancing
      #pragma multi_compile_local _ _ALPHATEST_ON
      #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
      #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
      #include "Assets/Code/Stronghold2/ModelRendering/Shaders/Stronghold2FoliagePass.hlsl"
      ENDHLSL
    }
    Pass
    {
      Name "ShadowCaster"
      Tags { "LightMode"="ShadowCaster" }
      Cull [_Cull] ZWrite On ColorMask 0 ZTest LEqual
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex ShadowVertex
      #pragma fragment Depth
      #pragma multi_compile_instancing
      #pragma multi_compile_local _ _ALPHATEST_ON
      #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
      #include "Assets/Code/Stronghold2/ModelRendering/Shaders/Stronghold2FoliagePass.hlsl"
      ENDHLSL
    }
    Pass
    {
      Name "DepthNormals"
      Tags { "LightMode"="DepthNormals" }
      Cull [_Cull] ZWrite On
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vertex
      #pragma fragment Normals
      #pragma multi_compile_instancing
      #pragma multi_compile_local _ _ALPHATEST_ON
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
      #include "Assets/Code/Stronghold2/ModelRendering/Shaders/Stronghold2FoliagePass.hlsl"
      ENDHLSL
    }
    Pass
    {
      Name "DepthOnly"
      Tags { "LightMode"="DepthOnly" }
      Cull [_Cull] ZWrite On ColorMask R
      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vertex
      #pragma fragment Depth
      #pragma multi_compile_instancing
      #pragma multi_compile_local _ _ALPHATEST_ON

      #include "Assets/Code/Stronghold2/ModelRendering/Shaders/Stronghold2FoliagePass.hlsl"
      ENDHLSL
    }
  }
}
