using System;
using UnityEngine;

namespace Assets.Code.Player
{
  /// <summary>Owns the map's camera and restores the pre-game scene camera on teardown.</summary>
  public sealed class LocalPlayerSession : IDisposable
  {
    private readonly Camera previous;
    private readonly bool previousEnabled;
    private readonly AudioListener previousListener;
    private readonly bool previousListenerEnabled;
    public Camera Camera { get; }
    public PlayerCameraController Controls { get; }

    public LocalPlayerSession(Transform mapWorld, Bounds bounds, Vector3 focus, float scale, PlayerCameraSettings settings)
    {
      settings.Validate();
      previous = UnityEngine.Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
      if (previous != null)
      {
        previousEnabled = previous.enabled;
        previousListener = previous.GetComponent<AudioListener>();
        if (previousListener != null) { previousListenerEnabled = previousListener.enabled; previousListener.enabled = false; }
      }
      var root = new GameObject("Local Player Camera");
      Camera = root.AddComponent<Camera>();
      if (previous != null) Camera.CopyFrom(previous);
      Camera.targetTexture = null;
      Camera.rect = new Rect(0, 0, 1, 1);
      root.tag = "MainCamera";
      root.AddComponent<AudioListener>();
      Controls = root.AddComponent<PlayerCameraController>();
      try { Controls.Initialize(settings, mapWorld, bounds, focus, scale); }
      catch { Dispose(); throw; }
      if (previous != null) previous.enabled = false;
    }

    public void Dispose()
    {
      if (Camera != null) { Camera.gameObject.SetActive(false); UnityEngine.Object.Destroy(Camera.gameObject); }
      if (previous != null) previous.enabled = previousEnabled;
      if (previousListener != null) previousListener.enabled = previousListenerEnabled;
    }
  }
}
