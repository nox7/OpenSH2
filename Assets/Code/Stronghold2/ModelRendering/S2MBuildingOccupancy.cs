using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Mutable map-cell occupancy, independent of meshes, shaders and world scaling.</summary>
  public sealed class S2MBuildingOccupancy
  {
    private readonly Dictionary<int, S2MBuildingFootprint> buildings = new();
    private readonly Dictionary<int, object> owners = new();
    private readonly Dictionary<(int x, int z), int> cells = new();
    public int Width { get; }
    public int Height { get; }
    public event Action<S2MBuildingFootprint> FootprintChanged;

    public S2MBuildingOccupancy(S2MFile map)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      Width = map.HeightLayer?.Width ?? map.MapSize;
      Height = map.HeightLayer?.Height ?? map.MapSize;
      foreach (BuildingPlacement building in map.BuildingPlacements)
        if (CanResolve(building)) SetBuilding(building);
    }

    public bool IsOccupied(int x, int z) => cells.ContainsKey((x, z));

    /// <summary>Adds or moves a building. Call when simulation placement/footprint changes.</summary>
    public void SetBuilding(BuildingPlacement building)
    {
      if (building == null) throw new ArgumentNullException(nameof(building));
      if (!CanResolve(building)) throw new ArgumentException("A building needs a positive, quarter-turn footprint.", nameof(building));
      S2MBuildingFootprint next = S2MBuildingFootprint.FromPlacement(building);
      bool hadPrevious = buildings.TryGetValue(building.Id, out S2MBuildingFootprint previous);
      if (hadPrevious && previous.MinX == next.MinX && previous.MinZ == next.MinZ
        && previous.MaxXExclusive == next.MaxXExclusive && previous.MaxZExclusive == next.MaxZExclusive) return;
      if (hadPrevious) Adjust(previous, -1);
      buildings[building.Id] = next;
      Adjust(next, 1);
      // Notify after both changes, so overlapping footprints never flash visible.
      if (hadPrevious) FootprintChanged?.Invoke(previous);
      FootprintChanged?.Invoke(next);
    }

    /// <summary>Releases coverage without changing the saved map or its stone resources.</summary>
    public bool RemoveBuilding(int buildingId)
    {
      if (!buildings.TryGetValue(buildingId, out S2MBuildingFootprint footprint)) return false;
      buildings.Remove(buildingId); owners.Remove(buildingId);
      Adjust(footprint, -1);
      FootprintChanged?.Invoke(footprint);
      return true;
    }

    internal static bool CanResolve(BuildingPlacement building) => building.FootprintWidth > 0 && building.FootprintHeight > 0
      && !float.IsNaN(building.RotationDegrees) && !float.IsInfinity(building.RotationDegrees)
      && Math.Abs(building.RotationDegrees / 90d - Math.Round(building.RotationDegrees / 90d)) <= 0.0001d;

    internal void Track(BuildingPlacement building, object owner)
    {
      SetBuilding(building);
      owners[building.Id] = owner;
    }

    internal void Untrack(int buildingId, object owner)
    {
      // An old renderer being replaced must not unregister its replacement.
      if (owners.TryGetValue(buildingId, out object current) && ReferenceEquals(current, owner)) RemoveBuilding(buildingId);
    }

    private void Adjust(S2MBuildingFootprint footprint, int delta)
    {
      for (int z = Math.Max(0, footprint.MinZ); z < Math.Min(Height, footprint.MaxZExclusive); z++)
        for (int x = Math.Max(0, footprint.MinX); x < Math.Min(Width, footprint.MaxXExclusive); x++)
        {
          cells.TryGetValue((x, z), out int count);
          count += delta;
          if (count == 0) cells.Remove((x, z)); else cells[(x, z)] = count;
        }
    }
  }
}
