using System;
using UnityEngine;

namespace Assets.Code.Player
{
  /// <summary>Local top-down camera. Input and movement remain independent of simulation time.</summary>
  [RequireComponent(typeof(Camera))]
  public sealed class PlayerCameraController : MonoBehaviour
  {
    private PlayerCameraSettings settings;
    private Transform world;
    private Bounds bounds;
    private float scale, distance, orbitDistance, lowestTerrainHeight;
    private Vector3 focus;
    private float moveElapsed;
    public float CurrentMoveSpeed { get; private set; }
    private RaycastHit[] hits = new RaycastHit[32];
#if !ENABLE_INPUT_SYSTEM
    private float previousMouseX;
    private bool mousePositionReady;
#endif
    public Camera ViewCamera { get; private set; }
    public Vector3 Focus => focus;
    public float SurfaceDistance => distance;

    public void Initialize(PlayerCameraSettings settings, Transform world, Bounds bounds, Vector3 initialFocus, float scale)
    {
      settings.Validate();
      if (world == null || !(scale > 0) || float.IsInfinity(scale)) throw new ArgumentException("A scaled map world is required.");
      this.settings = settings; this.world = world; this.bounds = bounds; this.scale = scale;
      ViewCamera = GetComponent<Camera>();
      ViewCamera.orthographic = false;
      ViewCamera.nearClipPlane = .1f * scale;
      ViewCamera.farClipPlane = Mathf.Max(bounds.size.magnitude * 2, settings.MaxSurfaceDistance * scale * 4);
      distance = Mathf.Clamp(settings.InitialSurfaceDistance, settings.MinSurfaceDistance, settings.MaxSurfaceDistance) * scale;
      orbitDistance = distance;
      moveElapsed = 0; CurrentMoveSpeed = 0;
      focus = initialFocus;
      Physics.SyncTransforms();
      lowestTerrainHeight = float.PositiveInfinity;
      foreach (var surface in world.GetComponentsInChildren<CameraSurface>())
        if (surface.IsTerrain)
          foreach (var collider in surface.GetComponentsInChildren<Collider>())
            if (collider.enabled && !collider.isTrigger)
              lowestTerrainHeight = Mathf.Min(lowestTerrainHeight, collider.bounds.min.y);
      if (float.IsPositiveInfinity(lowestTerrainHeight)) lowestTerrainHeight = world.position.y;
      var origin = new Vector3(focus.x, bounds.max.y + scale, focus.z);
      if (TrySurfaceRay(new Ray(origin, Vector3.down), bounds.size.y + scale * 2, out var ground)) focus.y = ground.point.y;
      transform.rotation = Quaternion.Euler(settings.Pitch, settings.Yaw, 0);
      transform.position = focus - transform.forward * orbitDistance;
      ApplyInput(Vector2.zero, 0, 0);
    }

    private void LateUpdate()
    {
      if (settings == null || world == null || Game.GetInstance()?.State != GameState.PLAYING) return;
      Vector2 movement = Vector2.zero; float scroll = 0, rotation = 0, drag = 0;
      if (Application.isFocused)
      {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
          rotation = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
          movement = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
            (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
        }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
          scroll = mouse.scroll.ReadValue().y / 120f;
          if (mouse.middleButton.isPressed) drag = mouse.delta.ReadValue().x;
        }
#else
        movement = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
          (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        scroll = Input.mouseScrollDelta.y;
        rotation = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
        if (mousePositionReady && Input.GetMouseButton(2)) drag = Input.mousePosition.x - previousMouseX;
#endif
      }
#if !ENABLE_INPUT_SYSTEM
      previousMouseX = Input.mousePosition.x;
      mousePositionReady = Application.isFocused;
#endif
      ApplyInput(movement, scroll, Time.unscaledDeltaTime, rotation, drag);
    }

    public void ApplyInput(Vector2 viewMovement, float zoomNotches, float deltaSeconds, float rotationAxis = 0, float mouseDeltaX = 0)
    {
      settings.Validate();
      if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0 ||
          !Finite(viewMovement.x) || !Finite(viewMovement.y) || !Finite(zoomNotches) || !Finite(rotationAxis) || !Finite(mouseDeltaX)) throw new ArgumentOutOfRangeException();
      RefreshTerrainPivot();
      settings.Yaw = Mathf.Repeat(settings.Yaw + Mathf.Clamp(rotationAxis, -1, 1) * settings.KeyboardRotationSpeed * deltaSeconds
        - mouseDeltaX * settings.MouseRotationSensitivity, 360);
      ViewCamera.fieldOfView = settings.FieldOfView;
      transform.rotation = Quaternion.Euler(settings.Pitch, settings.Yaw, 0);
      var heading = Quaternion.Euler(0, settings.Yaw, 0);
      var movement = heading * new Vector3(viewMovement.x, 0, viewMovement.y);
      var previousFocus = focus;
      focus += Vector3.ClampMagnitude(movement, 1) * PanTravel(viewMovement.sqrMagnitude > 0, deltaSeconds) * scale;
      // An off-map virtual pivot may rotate in place or move back toward the map without snapping.
      focus.x = Mathf.Clamp(focus.x, Mathf.Min(bounds.min.x, previousFocus.x), Mathf.Max(bounds.max.x, previousFocus.x));
      focus.z = Mathf.Clamp(focus.z, Mathf.Min(bounds.min.z, previousFocus.z), Mathf.Max(bounds.max.z, previousFocus.z));
      // The pivot plane and camera altitude stay unchanged when panning over different surfaces.
      orbitDistance = Mathf.Max(scale, orbitDistance - zoomNotches * settings.ZoomSpeed * scale);
      transform.position = focus - transform.forward * orbitDistance;
      ConstrainSurfaceDistance();
      RefreshTerrainPivot();
    }

    private float PanTravel(bool moving, float seconds)
    {
      if (!moving) { moveElapsed = 0; CurrentMoveSpeed = 0; return 0; }
      float duration = settings.MoveSpeedInterpolationTime;
      if (duration <= 0)
      {
        CurrentMoveSpeed = settings.MaxMoveSpeed;
        return CurrentMoveSpeed * seconds;
      }
      // Integrate the linear ramp, including a frame that crosses its end, so travel
      // does not depend on frame rate. Direction changes keep a continuous ramp.
      float rampSeconds = Mathf.Min(seconds, Mathf.Max(0, duration - moveElapsed));
      float initial = Mathf.Lerp(settings.InitialMoveSpeed, settings.MaxMoveSpeed, moveElapsed / duration);
      moveElapsed = Mathf.Min(duration, moveElapsed + seconds);
      CurrentMoveSpeed = Mathf.Lerp(settings.InitialMoveSpeed, settings.MaxMoveSpeed, moveElapsed / duration);
      return (initial + CurrentMoveSpeed) * .5f * rampSeconds + settings.MaxMoveSpeed * (seconds - rampSeconds);
    }

    private void OnDisable() { moveElapsed = 0; CurrentMoveSpeed = 0; }

    private void ConstrainSurfaceDistance()
    {
      var forward = transform.forward;
      // Begin above the world's highest surface so roofs are detected even if a pan placed
      // the proposed camera inside/below a collider. Measure the hit relative to the camera.
      float back = Mathf.Max(0, (bounds.max.y + scale - transform.position.y) / -forward.y) + scale;
      var origin = transform.position - forward * back;
      float length = Mathf.Max(0, (origin.y - bounds.min.y) / -forward.y) + scale;
      if (!TrySurfaceRay(new Ray(origin, forward), length, out var hit))
      {
        orbitDistance = Mathf.Clamp(orbitDistance, settings.MinSurfaceDistance * scale, settings.MaxSurfaceDistance * scale);
        distance = orbitDistance;
        transform.position = focus - forward * orbitDistance;
        return;
      }
      float measured = hit.distance - back;
      distance = Mathf.Clamp(measured, settings.MinSurfaceDistance * scale, settings.MaxSurfaceDistance * scale);
      // Only the filtered surface ray controls height/zoom; it never becomes the orbit pivot.
      if (measured < settings.MinSurfaceDistance * scale || measured > settings.MaxSurfaceDistance * scale)
        transform.position = hit.point - forward * distance;
    }

    private void RefreshTerrainPivot()
    {
      var ray = new Ray(transform.position, transform.forward);
      float planeDistance = Mathf.Max(scale, (lowestTerrainHeight - ray.origin.y) / ray.direction.y);
      if (TrySurfaceRay(ray, planeDistance + scale, out var terrain, terrainOnly: true))
      {
        focus = terrain.point;
        orbitDistance = terrain.distance;
      }
      else
      {
        focus = ray.GetPoint(planeDistance);
        orbitDistance = planeDistance;
      }
    }

    public bool TrySurfaceRay(Ray ray, float maximumDistance, out RaycastHit nearest, bool terrainOnly = false)
    {
      int count;
      while (true)
      {
        count = Physics.RaycastNonAlloc(ray, hits, maximumDistance, ~0, QueryTriggerInteraction.Ignore);
        if (count < hits.Length) break;
        Array.Resize(ref hits, checked(hits.Length * 2));
      }
      nearest = default; float best = float.PositiveInfinity;
      for (int i = 0; i < count; i++)
      {
        var collider = hits[i].collider;
        if (!collider.transform.IsChildOf(world) || hits[i].distance >= best) continue;
        var surface = collider.GetComponentInParent<CameraSurface>();
        if (surface == null || (terrainOnly && !surface.IsTerrain)) continue;
        best = hits[i].distance; nearest = hits[i];
      }
      return best < float.PositiveInfinity;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
  }
}
