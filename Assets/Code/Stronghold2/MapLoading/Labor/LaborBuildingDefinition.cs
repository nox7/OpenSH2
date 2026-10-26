using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading.Labor
{
  /// <summary>Per-building map adaptation, separate from simulation mechanics and asset presentation.</summary>
  public abstract class LaborBuildingDefinition
  {
    public abstract string BuildingType { get; }
    public abstract LaborJob Create(S2MFile map,BuildingPlacement building,NavigationGrid navigation,int equipmentActorId,ISet<int> suppressed);
    public virtual bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job) => false;
  }

  public static class LaborBuildingCatalog
  {
    private static readonly Dictionary<string,LaborBuildingDefinition> definitions=new() {
      ["SawPit"]=new WoodcutterBuildingDefinition()
    };
    public static bool TryGet(string buildingType,out LaborBuildingDefinition definition) => definitions.TryGetValue(buildingType,out definition);
  }
}
