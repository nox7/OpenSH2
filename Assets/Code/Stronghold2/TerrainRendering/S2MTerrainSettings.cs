using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  [Serializable]
  public class S2MTerrainSettings
  {
    [Min(0.0001f)]
    public float HorizontalCellSize = 1f;

    [Tooltip("Unity vertical units per raw S2M height unit. 1/256 is a useful initial scale because many observed values are multiples of 256.")]
    [Min(0.000001f)]
    public float HeightUnitScale = 1f / 256f;

    [Tooltip("Reverse the serialized X axis when copying it into Unity TerrainData.")]
    public bool FlipX;

    [Tooltip("Reverse the serialized row axis when copying it into Unity's Z axis.")]
    public bool FlipZ;

    [Tooltip("Optional runtime material override for the generated Terrain.")]
    public Material Material;
  }
}
