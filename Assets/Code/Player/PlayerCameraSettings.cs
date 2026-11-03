using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Code.Player
{
  [Serializable]
  public sealed class PlayerCameraSettings
  {
    [Range(15, 85), Tooltip("Fixed downward camera angle in degrees; editable live.")]
    public float Pitch = 50;
    [Tooltip("Initial/current compass heading in degrees. WASD follows this heading on the horizontal plane.")]
    public float Yaw;
    [Range(15, 75)] public float FieldOfView = 35;
    [Min(1), Tooltip("Minimum distance along the centre viewing ray above terrain/buildings, in unscaled map units.")]
    public float MinSurfaceDistance = 12;
    [Min(1), Tooltip("Maximum distance along the centre viewing ray above terrain/buildings, in unscaled map units.")]
    public float MaxSurfaceDistance = 120;
    [Min(1)] public float InitialSurfaceDistance = 65;
    [FormerlySerializedAs("MoveSpeed")]
    [Min(0), Tooltip("Pan speed when movement begins, in map units per second.")]
    public float InitialMoveSpeed = 30;
    [Min(0), Tooltip("Pan speed reached after holding movement for the interpolation time.")]
    public float MaxMoveSpeed = 60;
    [Min(0), Tooltip("Seconds of continuous movement to reach maximum pan speed. Zero uses maximum speed immediately.")]
    public float MoveSpeedInterpolationTime = 1;
    [Min(0), Tooltip("Distance change per wheel notch in map units. Wheel up zooms in.")]
    public float ZoomSpeed = 40;
    [Min(0), Tooltip("Yaw degrees per second while holding Q/E.")]
    public float KeyboardRotationSpeed = 90;
    [Min(0), Tooltip("Yaw degrees per horizontal mouse pixel while holding the middle button.")]
    public float MouseRotationSensitivity = .2f;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public void Validate()
    {
      if (!Finite(Pitch) || !Finite(Yaw) || !Finite(FieldOfView) || !Finite(MinSurfaceDistance) ||
          !Finite(MaxSurfaceDistance) || !Finite(InitialSurfaceDistance) || !Finite(InitialMoveSpeed) || !Finite(MaxMoveSpeed) || !Finite(MoveSpeedInterpolationTime) || !Finite(ZoomSpeed) || !Finite(KeyboardRotationSpeed) || !Finite(MouseRotationSensitivity))
        throw new ArgumentOutOfRangeException(nameof(PlayerCameraSettings));
      if (Pitch < 15 || Pitch > 85 || FieldOfView < 15 || FieldOfView > 75 || MinSurfaceDistance < 1 ||
          MaxSurfaceDistance < MinSurfaceDistance || InitialSurfaceDistance < 1 || InitialMoveSpeed < 0 || MaxMoveSpeed < InitialMoveSpeed || MoveSpeedInterpolationTime < 0 || ZoomSpeed < 0 || KeyboardRotationSpeed < 0 || MouseRotationSensitivity < 0)
        throw new ArgumentOutOfRangeException(nameof(PlayerCameraSettings));
    }
  }
}
