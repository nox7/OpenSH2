using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Reversible, cell-indexed render suppression. Source placements remain unchanged.</summary>
  [ExecuteAlways]
  internal sealed class S2MNaturalStoneVisibility : MonoBehaviour
  {
    private readonly Dictionary<(int x, int z), List<Renderer>> stones = new();
    private S2MBuildingOccupancy occupancy;

    internal void Initialize(S2MBuildingOccupancy occupancy)
    {
      this.occupancy = occupancy;
      occupancy.FootprintChanged += Refresh;
    }

    internal void Register(SceneryInstance source, GameObject model)
    {
      var key = (Mathf.FloorToInt(source.MapX), Mathf.FloorToInt(source.MapZ));
      if (!stones.TryGetValue(key, out var renderers)) stones.Add(key, renderers = new List<Renderer>());
      foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
      {
        renderers.Add(renderer);
        renderer.forceRenderingOff = occupancy.IsOccupied(key.Item1, key.Item2);
      }
    }

    private void Refresh(S2MBuildingFootprint footprint)
    {
      for (int z = Math.Max(0, footprint.MinZ); z < Math.Min(occupancy.Height, footprint.MaxZExclusive); z++)
        for (int x = Math.Max(0, footprint.MinX); x < Math.Min(occupancy.Width, footprint.MaxXExclusive); x++)
          if (stones.TryGetValue((x, z), out var renderers))
          {
            bool hidden = occupancy.IsOccupied(x, z);
            foreach (Renderer renderer in renderers) if (renderer != null) renderer.forceRenderingOff = hidden;
          }
    }

    private void OnDestroy()
    {
      if (occupancy != null) occupancy.FootprintChanged -= Refresh;
    }
  }
}
