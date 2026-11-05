using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>Adapts saved buildings and estate cells to simulation populations.</summary>
  public sealed class S2MCampfireLayout
  {
    public Keep Keep { get; private set; }
    public Vector2 Center { get; private set; }
    public Vector2 ModelOrigin { get; private set; }
    public float RawHeight { get; private set; }
    public CampfirePopulation Population { get; private set; }

    public static IReadOnlyList<S2MCampfireLayout> Create(S2MFile map, PopulationSettings settings = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      var hovels = new Dictionary<int, int>();
      foreach (var building in map.BuildingPlacements)
      {
        if (building.Type != "Hovel") continue;
        int estate = EstateAtBuilding(map, building);
        if (estate >= 0) hovels[estate] = hovels.TryGetValue(estate, out int count) ? count + 1 : 1;
      }
      var result = new List<S2MCampfireLayout>();
      foreach (var keep in map.Keeps)
      {
        int estate = EstateAtBuilding(map, keep);
        var point=S2MKeepForecourt.Center(keep);var center=new Vector2(point.X,point.Z);
        result.Add(new S2MCampfireLayout { Keep = keep,
          // The saved Keep vector is the centre. The unrotated camp_fire.GR2 pivot is half a cell behind it on both axes.
          ModelOrigin = center-new Vector2(.5f,.5f),Center = center,
          RawHeight = keep.HasCampfirePosition ? keep.CampfireRawY : S2MNavigationBuilder.SampleHeight(map,point.X,point.Z),
          Population = new CampfirePopulation(keep.Id, estate, estate >= 0 && hovels.TryGetValue(estate, out int n) ? n : 0, settings) });
      }
      return result;
    }

    public static int EstateAtBuilding(S2MFile map, BuildingPlacement building)
    {
      if(building.RuntimeEstateId.HasValue)return building.RuntimeEstateId.Value;
      if(building.StartupEstateId.HasValue)return building.StartupEstateId.Value;
      // Model origins can sit exactly on an estate boundary. Sample inside the reserved footprint.
      int turns = ((int)Math.Round(building.RotationDegrees / 90) % 4 + 4) % 4;
      float x = building.MapX + (turns == 2 || turns == 3 ? -.5f : .5f) * building.FootprintWidth;
      float z = building.MapZ + (turns == 1 || turns == 2 ? -.5f : .5f) * building.FootprintHeight;
      var layer = map.RadarEstateLayer;
      int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
      if (layer?.EstateIds == null || ix < 0 || iz < 0 || ix >= layer.Width || iz >= layer.Height) return -1;
      int id = layer.EstateIds[iz * layer.Width + ix];
      return id > 0 && id <= 31 ? id : -1;
    }

    public static float GroundHeight(S2MFile map, float x, float z)
    {
      var h = map.HeightLayer;
      if (h?.Heights == null) return 0;
      int ix = Mathf.Clamp(Mathf.FloorToInt(x), 0, h.Width - 1), iz = Mathf.Clamp(Mathf.FloorToInt(z), 0, h.Height - 1);
      return h.Heights[iz * h.Width + ix];
    }
  }
}
