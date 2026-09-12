Shader "OpenSH2/Stronghold 2 Terrain"
{
  Properties
  {
    _TextureScale("Texture Repeats per Cell", Float) = 1
    _AdjacentMaterialBlendWidth("Adjacent Material Blend Width", Range(0, 0.5)) = 0.2
    _ApplySerializedTextureRotation("Apply Serialized Texture Rotation", Float) = 1
    _TerrainTextureArray("Terrain Texture Array", 2DArray) = "white" {}
  }

  SubShader
  {
    Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

    Pass
    {
      Name "ForwardLit"
      Tags { "LightMode" = "UniversalForward" }
      Cull Back
      ZWrite On

      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex Vertex
      #pragma fragment Fragment

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      CBUFFER_START(UnityPerMaterial)
        float _TextureScale;
        float _AdjacentMaterialBlendWidth;
        float _ApplySerializedTextureRotation;
      CBUFFER_END

      TEXTURE2D_ARRAY(_TerrainTextureArray); SAMPLER(sampler_TerrainTextureArray);

      struct Attributes
      {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 uv : TEXCOORD0;
        float4 cellData : TEXCOORD1;
        float4 neighbourMaterialIds : TEXCOORD3;
        float4 neighbourTintWest : TEXCOORD4;
        float4 neighbourTintEast : TEXCOORD5;
        float4 neighbourTintSouth : TEXCOORD6;
        float4 neighbourTintNorth : TEXCOORD7;
        half4 color : COLOR;
      };

      struct Varyings
      {
        float4 positionHCS : SV_POSITION;
        float3 normalWS : TEXCOORD0;
        float2 uv : TEXCOORD1;
        nointerpolation float materialId : TEXCOORD2;
        nointerpolation float4 neighbourMaterialIds : TEXCOORD3;
        float2 cellUv : TEXCOORD4;
        nointerpolation float4 neighbourTintWest : TEXCOORD5;
        nointerpolation float4 neighbourTintEast : TEXCOORD6;
        nointerpolation float4 neighbourTintSouth : TEXCOORD7;
        nointerpolation float4 neighbourTintNorth : TEXCOORD8;
        nointerpolation float textureRotation : TEXCOORD9;
        half4 color : COLOR;
      };

      float2 RotateTextureUv(float2 uv, float quarterTurns)
      {
        float2 tileOrigin = floor(uv);
        float2 localUv = frac(uv);
        float turn = fmod(round(quarterTurns), 4.0);
        if (turn < 0.5) localUv = localUv;
        else if (turn < 1.5) localUv = float2(1.0 - localUv.y, localUv.x);
        else if (turn < 2.5) localUv = 1.0 - localUv;
        else localUv = float2(localUv.y, 1.0 - localUv.x);
        return tileOrigin + localUv;
      }

      half4 SampleMaterial(float materialId, float2 uv, float quarterTurns)
      {
        // Slices 0..23 are the GROUND1 IDs 0x08..0x1F. Slice 24 is the
        // separately serialized cliff-face selector 0x27.
        float slice = materialId >= 8.0 && materialId <= 31.0
          ? materialId - 8.0
          : (materialId == 39.0 ? 24.0 : 4.0);
        return SAMPLE_TEXTURE2D_ARRAY(_TerrainTextureArray, sampler_TerrainTextureArray, RotateTextureUv(uv, quarterTurns), slice);
      }

      half4 SampleBlendedMaterial(float materialId, float4 neighbours, float4 neighbourRotations, float textureRotation, float2 textureUv, float2 cellUv)
      {
        half4 baseColor = SampleMaterial(materialId, textureUv, textureRotation);
        float width = _AdjacentMaterialBlendWidth;
        if (width <= 0.0) return baseColor;

        float2 localCellUv = saturate(cellUv);
        float westWeight = 1.0 - smoothstep(0.0, width, localCellUv.x);
        float eastWeight = smoothstep(1.0 - width, 1.0, localCellUv.x);
        float southWeight = 1.0 - smoothstep(0.0, width, localCellUv.y);
        float northWeight = smoothstep(1.0 - width, 1.0, localCellUv.y);
        return (baseColor
          + SampleMaterial(neighbours.x, textureUv, neighbourRotations.x * 3.0 * _ApplySerializedTextureRotation) * westWeight
          + SampleMaterial(neighbours.y, textureUv, neighbourRotations.y * 3.0 * _ApplySerializedTextureRotation) * eastWeight
          + SampleMaterial(neighbours.z, textureUv, neighbourRotations.z * 3.0 * _ApplySerializedTextureRotation) * southWeight
          + SampleMaterial(neighbours.w, textureUv, neighbourRotations.w * 3.0 * _ApplySerializedTextureRotation) * northWeight)
          / (1.0 + westWeight + eastWeight + southWeight + northWeight);
      }

      half4 BlendTerrainTint(half4 baseTint, float4 west, float4 east, float4 south, float4 north, float2 cellUv)
      {
        float width = _AdjacentMaterialBlendWidth;
        if (width <= 0.0) return baseTint;

        float2 localCellUv = saturate(cellUv);
        float westWeight = 1.0 - smoothstep(0.0, width, localCellUv.x);
        float eastWeight = smoothstep(1.0 - width, 1.0, localCellUv.x);
        float southWeight = 1.0 - smoothstep(0.0, width, localCellUv.y);
        float northWeight = smoothstep(1.0 - width, 1.0, localCellUv.y);
        return (baseTint
          + (half4)west * westWeight
          + (half4)east * eastWeight
          + (half4)south * southWeight
          + (half4)north * northWeight)
          / (1.0 + westWeight + eastWeight + southWeight + northWeight);
      }

      Varyings Vertex(Attributes input)
      {
        Varyings output;
        output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.uv = input.uv.xy * _TextureScale;
        output.materialId = round(input.cellData.x * 255.0);
        output.neighbourMaterialIds = round(input.neighbourMaterialIds * 255.0);
        output.cellUv = input.uv.zw;
        output.textureRotation = round(input.cellData.z * 3.0) * _ApplySerializedTextureRotation;
        output.neighbourTintWest = input.neighbourTintWest;
        output.neighbourTintEast = input.neighbourTintEast;
        output.neighbourTintSouth = input.neighbourTintSouth;
        output.neighbourTintNorth = input.neighbourTintNorth;
        output.color = input.color;
        return output;
      }

      half4 Fragment(Varyings input) : SV_Target
      {
        half3 terrainTint = BlendTerrainTint(input.color, input.neighbourTintWest, input.neighbourTintEast, input.neighbourTintSouth, input.neighbourTintNorth, input.cellUv).rgb;
        float4 neighbourRotations = float4(input.neighbourTintWest.w, input.neighbourTintEast.w, input.neighbourTintSouth.w, input.neighbourTintNorth.w);
        half3 albedo = SampleBlendedMaterial(input.materialId, input.neighbourMaterialIds, neighbourRotations, input.textureRotation, input.uv, input.cellUv).rgb * terrainTint;
        half3 normalWS = normalize(input.normalWS);
        Light mainLight = GetMainLight();
        half directLight = saturate(dot(normalWS, mainLight.direction));
        half3 lighting = SampleSH(normalWS) + mainLight.color * directLight;
        return half4(albedo * lighting, 1.0h);
      }

      // Terrain tint and material transitions share the same adjustable edge width.
      ENDHLSL
    }
  }
}
