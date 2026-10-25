using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Building lifetime, independent of interior/exterior view changes.</summary>
  [ExecuteAlways, DisallowMultipleComponent]
  public sealed class S2MBuildingPresence : MonoBehaviour
  {
    private S2MBuildingOccupancy occupancy;
    private BuildingPlacement placement;
    public int BuildingId => placement?.Id ?? 0;

    public void Initialize(S2MBuildingOccupancy occupancy, BuildingPlacement placement)
    {
      Release();
      this.occupancy = occupancy;
      this.placement = placement;
      if (isActiveAndEnabled) OnEnable();
    }

    private void OnEnable()
    {
      if (occupancy != null && placement != null) occupancy.Track(placement, this);
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
    private void Release()
    {
      if (occupancy != null && placement != null) occupancy.Untrack(placement.Id, this);
    }
  }
}
