using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>One map-world context shared by building and scenery renderers, in either load order.</summary>
  public sealed class S2MSceneryOcclusion : MonoBehaviour
  {
    public S2MBuildingOccupancy Occupancy { get; private set; }
    private S2MFile map;

    internal static S2MBuildingOccupancy GetOrCreate(Transform scope, S2MFile map)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      foreach (var context in scope.GetComponentsInParent<S2MSceneryOcclusion>(true))
        if (ReferenceEquals(context.map, map)) return context.Occupancy;
      var created = scope.gameObject.AddComponent<S2MSceneryOcclusion>();
      created.map = map;
      created.Occupancy = new S2MBuildingOccupancy(map);
      return created.Occupancy;
    }
  }
}
