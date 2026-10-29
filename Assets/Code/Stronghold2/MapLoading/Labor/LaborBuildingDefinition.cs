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
    public virtual IEnumerable<LaborJob> CreateJobs(S2MFile map,BuildingPlacement building,NavigationGrid navigation,int equipmentActorId,ISet<int> suppressed)
    {yield return Create(map,building,navigation,equipmentActorId,suppressed);}
    public virtual bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job) => false;
  }

  public static class LaborBuildingCatalog
  {
    private static readonly Dictionary<string,LaborBuildingDefinition> definitions=new() {
      ["SawPit"]=new WoodcutterBuildingDefinition()
    };
    // Passive stores are explicitly configured as zero-worker buildings, not unknown requirements.
    public static bool RequiresNoWorkers(string buildingType)=>buildingType=="StockPile";
    public static bool TryGet(string buildingType,out LaborBuildingDefinition definition,QuarrySettings quarrySettings=null,OxHaulingSettings oxSettings=null,IronMineSettings ironSettings=null)
    {if(buildingType=="IronMine"){definition=new IronMineBuildingDefinition(ironSettings);return true;}if(buildingType=="OxTether"){definition=new OxTetherBuildingDefinition(oxSettings);return true;}if(buildingType=="StoneQuarry"){definition=new QuarryBuildingDefinition(quarrySettings);return true;}return definitions.TryGetValue(buildingType,out definition);}
  }
}
