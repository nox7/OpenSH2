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
    [Tooltip("Use the tree material rules and foliage shader. Disable for opaque building meshes.")]
    public bool UseFoliageMaterials = true;
    // Selected by the vegetation renderer from its TreeSway settings.
    [HideInInspector] public bool UseModelSway;
    [Tooltip("Optional opaque surface shader, for original-game material responses such as natural stone.")]
    public Shader OpaqueShader;
    [Tooltip("Diffuse texture brightness for shaders supporting _TextureBrightness. Values above 1 lighten the texture before lighting.")]
    [Min(0f)] public float TextureBrightness = 1f;
    [Tooltip("Stronghold 2's tree shader uses an alpha-test reference of 136/255.")]
    [Range(0f, 1f)] public float AlphaCutoff = 136f / 255f;
    public Material FallbackMaterial;
    [NonSerialized] public S2MLiveryContext Livery;
    [NonSerialized] public int TeamGroup = -1;
  }
}
