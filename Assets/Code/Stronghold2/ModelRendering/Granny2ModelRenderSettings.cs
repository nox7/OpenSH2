using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  [Serializable]
  public sealed class Granny2ModelRenderSettings
  {
    [Tooltip("Stronghold 2 landscape meshes appear to use centimeters and Z-up coordinates.")]
    [Min(0.000001f)] public float UnitScale = 0.01f;
    public bool ConvertZUpToUnityYUp = true;
    public bool DoubleSided = true;
    [Tooltip("Stronghold 2's tree shader uses an alpha-test reference of 136/255.")]
    [Range(0f, 1f)] public float AlphaCutoff = 136f / 255f;
    public Material FallbackMaterial;
  }
}
