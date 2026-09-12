using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  [Serializable]
  public sealed class S2MTerrainTextureRenderSettings
  {
    [Min(0.0001f)]
    [Tooltip("Texture repeats per S2M map cell. Tune after comparing the original game's terrain tiling.")]
    public float TextureScale = 1f;

    [Tooltip("Optional material override. It must support the OpenSH2 terrain texture property names.")]
    public Material Material;

    [Range(0f, 0.5f)]
    [Tooltip("Fraction of each cell used to blend toward differing adjacent materials and terrain tint colors. The original tag-27 blend payload is not decoded yet.")]
    public float AdjacentMaterialBlendWidth = 0.2f;

    [Tooltip("Apply the candidate per-cell quarter-turn values decoded from Landscape tag 30.")]
    public bool ApplySerializedTextureRotation = true;

  }
}
