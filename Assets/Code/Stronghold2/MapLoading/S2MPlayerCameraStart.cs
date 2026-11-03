using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.MapLoading
{
  internal static class S2MPlayerCameraStart
  {
    public static Vector3 Resolve(S2MFile map, S2MTerrainSettings settings, Transform world)
    {
      var owners = S2MLiveryOwnership.Read(map);
      float x = map.MapSize * .5f, z = x;
      foreach (var player in owners.Players.Values)
      {
        if (!player.LocalHuman) continue;
        foreach (var keep in map.Keeps)
        {
          int estate = S2MCampfireLayout.EstateAtBuilding(map, keep);
          if (owners.Estates.TryGetValue(estate, out var owner) && owner.PlayerNumber == player.Number)
            return Position(keep.MapX, keep.MapZ, map, settings, world);
        }
        foreach (var actor in map.Actors)
          if (actor.OnMap && actor.PendingPlayer == player.Number)
            return Position(actor.RawX / 1024f, actor.RawZ / 1024f, map, settings, world);
      }
      return Position(x, z, map, settings, world);
    }

    private static Vector3 Position(float x, float z, S2MFile map, S2MTerrainSettings settings, Transform world)
    {
      if (settings.FlipX) x = map.MapSize - x;
      if (settings.FlipZ) z = map.MapSize - z;
      return world.TransformPoint(new Vector3(x * settings.HorizontalCellSize, 0, z * settings.HorizontalCellSize));
    }
  }
}
