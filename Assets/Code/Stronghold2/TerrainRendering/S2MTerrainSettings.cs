using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  [Serializable]
  public class S2MTerrainSettings
  {
    [Min(0.0001f)]
    public float HorizontalCellSize = 1f;

    [Tooltip("Unity vertical units per raw S2M height unit. Editor probes place the six standard terrain levels between 0 and 10240, making 1/1024 the best current calibration.")]
    [Min(0.000001f)]
    public float HeightUnitScale = 1f / 1024f;

    [Tooltip("Reverse the serialized X axis when copying it into Unity TerrainData.")]
    public bool FlipX;

    [Tooltip("Reverse the serialized row axis when copying it into Unity's Z axis.")]
    public bool FlipZ;

    [Tooltip("Optional runtime material override for the generated Terrain.")]
    public Material Material;
  }
}
