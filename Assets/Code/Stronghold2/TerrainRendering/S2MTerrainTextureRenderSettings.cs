using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  [Serializable]
  public sealed class S2MTerrainTextureRenderSettings
  {
    [Min(0.0001f)]
    [Tooltip("Ground-terrain texture repeats per S2M map cell. The default 0.25 produces one repeat across four cells, reducing the original per-cell checkerboard tiling.")]
    public float TextureScale = 0.25f;

    [Tooltip("Optional material override. It must support the OpenSH2 terrain texture property names.")]
    public Material Material;

    [Range(0f, 0.5f)]
    [Tooltip("Fraction of each cell used to blend toward differing adjacent materials and terrain tint colors. The original tag-27 blend payload is not decoded yet.")]
    public float AdjacentMaterialBlendWidth = 0.2f;

    [Tooltip("Apply the candidate per-cell quarter-turn values decoded from Landscape tag 30.")]
    public bool ApplySerializedTextureRotation = true;

    [Min(0.0001f)]
    [Tooltip("Vertical texture-repeat multiplier for generated cliff walls and transition wedges. This is independent of horizontal terrain-cell tiling.")]
    public float CliffWallVerticalTextureScale = 0.25f;

    [Min(0.0001f)]
    [Tooltip("Horizontal texture-repeat multiplier for generated cliff walls and transition wedges. Their U coordinate is shared across adjacent map-space wall faces.")]
    public float CliffWallHorizontalTextureScale = 0.1f;

  }
}
