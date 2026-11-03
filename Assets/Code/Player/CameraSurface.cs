using UnityEngine;

namespace Assets.Code.Player
{
  /// <summary>Opt-in camera-ray geometry. Applied to terrain/building roots, never foliage or actors.</summary>
  [DisallowMultipleComponent]
  public sealed class CameraSurface : MonoBehaviour
  {
    [Tooltip("Only terrain participates in rotation pivots. All marked surfaces still constrain zoom/height.")]
    public bool IsTerrain;
  }
}
