using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  public enum KeepViewMode { Automatic, Closed, Open }

  [Serializable]
  public sealed class S2MKeepRenderSettings
  {
    public KeepViewMode ViewMode = KeepViewMode.Automatic;
    [Tooltip("Optional camera for the interior view. Uses the main camera when unset.")]
    public Camera ViewCamera;
    [Tooltip("Distance from the camera to the exterior bounds, in unscaled map units, at which interiors open.")]
    [Min(0f)] public float InteriorOpenDistance = 12f;
    [Tooltip("Extra distance before closing an open interior, preventing flicker near the threshold.")]
    [Min(0f)] public float InteriorCloseHysteresis = 2f;
  }
}
