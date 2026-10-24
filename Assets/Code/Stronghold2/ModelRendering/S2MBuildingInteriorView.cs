using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Switches one building independently while its collision mesh stays active.</summary>
  public sealed class S2MBuildingInteriorView : MonoBehaviour
  {
    private S2MBuildingRenderSettings settings;
    private GameObject closedView;
    private GameObject openView;
    private GameObject table;
    private Bounds localBounds;
    private bool isOpen;

    public void Initialize(S2MBuildingRenderSettings settings, GameObject closedView,
      GameObject openView, GameObject table, Bounds localBounds)
    {
      this.settings = settings;
      this.closedView = closedView;
      this.openView = openView;
      this.table = table;
      this.localBounds = localBounds;
      SetOpen(false);
      UpdateView();
    }

    private void LateUpdate()
    {
      if (settings != null) UpdateView();
    }

    private void UpdateView()
    {
      float? distanceSquared = null;
      if (settings.ViewMode == BuildingViewMode.Automatic)
      {
        Camera camera = settings.ViewCamera != null ? settings.ViewCamera : Camera.main;
        if (camera != null)
        {
          // Transforming the camera into building space makes the threshold follow
          // the map's game scale, rotation and optional reflected map axes.
          Vector3 cameraPosition = transform.InverseTransformPoint(camera.transform.position);
          distanceSquared = localBounds.SqrDistance(cameraPosition);
        }
      }
      bool showInterior = ShouldShowInterior(settings, isOpen, distanceSquared);
      if (showInterior != isOpen) SetOpen(showInterior);
    }

    internal static bool ShouldShowInterior(S2MBuildingRenderSettings settings, bool currentlyOpen, float? distanceSquared)
    {
      if (settings.ViewMode == BuildingViewMode.Open) return true;
      if (settings.ViewMode == BuildingViewMode.Closed || !distanceSquared.HasValue) return false;
      float distance = System.Math.Max(0f, settings.InteriorOpenDistance)
        + (currentlyOpen ? System.Math.Max(0f, settings.InteriorCloseHysteresis) : 0f);
      return distanceSquared.Value <= distance * distance;
    }

    private void SetOpen(bool value)
    {
      isOpen = value;
      closedView.SetActive(!value);
      openView.SetActive(value);
      if (table != null) table.SetActive(value);
    }
  }
}
