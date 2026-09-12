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

    [Tooltip("Reverse the serialized X axis when generating the terrain mesh.")]
    public bool FlipX;

    [Tooltip("Reverse the serialized row axis when generating Unity's Z axis.")]
    public bool FlipZ;

    [Tooltip("Use plane-0's four per-cell corner heights instead of collapsing the map to its primary grid.")]
    public bool UsePerCellCornerHeights = true;

    [Min(1)]
    [Tooltip("Number of terrain cells in each square render chunk.")]
    public int CellsPerChunk = 32;

    [Tooltip("Create a MeshCollider for every generated terrain chunk.")]
    public bool GenerateMeshColliders = true;

    [Tooltip("Optional runtime material override for generated terrain mesh chunks.")]
    public Material Material;
  }
}
