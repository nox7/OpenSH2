// Static and animated original assets share lighting, opacity, shadows and depth behavior.
Shader "OpenSH2/Original Model"
{
  Properties
  {
    [MainTexture] _BaseMap("Diffuse", 2D) = "white" {}
    [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
    _Cutoff("Alpha cutoff", Range(0,1)) = .5
    _AlphaClip("Alpha clip", Float) = 0
    _Cull("Cull", Float) = 2
    [HideInInspector] _ActorSkinning("GPU skinning", Float) = 0
    [HideInInspector] _ActorReceiveShadows("Receive shadows", Float) = 1
    [HideInInspector] _ActorClock("Animation clock", Float) = 0
    [HideInInspector] _ActorClip("Clip", Vector) = (1,2,1,.01)
    [HideInInspector] _ActorPoses("Bone poses", 2D) = "white" {}
    [HideInInspector] _LiveryMode("Livery mode", Float) = 0
    [HideInInspector] _CrestArray("Lord crests", 2DArray) = "" {}
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    Cull [_Cull]
    UsePass "OpenSH2/Instanced Actor/ForwardLit"
    UsePass "OpenSH2/Instanced Actor/ShadowCaster"
    UsePass "OpenSH2/Instanced Actor/DepthNormals"
    UsePass "OpenSH2/Instanced Actor/DepthOnly"
  }
}
