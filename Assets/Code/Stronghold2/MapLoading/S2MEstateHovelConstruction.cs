using System;
using Assets.Code.Stronghold2.Construction;
using System.IO;
using System.Collections.Generic;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading
{
    [Serializable] public sealed class EstateHovelSettings : EstateBuildingSettings
    {
        public int InitialWood=32;
        public EstateHovelSettings(){SearchRadius=35;PreferredOffsetX=0;PreferredOffsetZ=3;}
    }
    /// <summary>Housing ceiling comes from the header; expansion follows unfilled worker slots.</summary>
    public sealed class S2MEstateHovelConstruction : S2MEstateBuildingConstruction
    {
        public S2MEstateHovelConstruction(S2MFile map,AiWorld world,EstateInventory inventory,string installation,
            NavigationGrid navigation=null,LaborManager labor=null,StartupBuildingSettings placementSettings=null,EstateHovelSettings settings=null,S2MBuildingConstructionService construction=null)
            :base(map,world,inventory,installation,navigation,labor,placementSettings,settings??new EstateHovelSettings(),"Hovel",construction){}
        protected override int DesiredCount(AiTerritory territory)
        {
            int current=territory.BuildingCount("Hovel"),maximum=territory.MaximumHovels;
            var demand=labor?.GetLaborDemand(territory.Id)??territory.Labor;
            var supply=labor?.GetLaborSupply(territory.Id)??default;
            int available=supply.HasPopulation?supply.Available+supply.Pending:Math.Max(0,current*CampfirePopulation.PeasantsPerHovel-demand.AssignedWorkers);
            int deficit=Math.Max(0,demand.MissingLaborers+territory.PlannedLaborers-available);
            int goal=maximum==0?0:current==0?1:Math.Min(maximum,Math.Max(1,current+(deficit+7)/8));
            world.SetHovelGoal(territory.Id,goal);return goal;
        }
        public static void InitializeEstateResources(S2MFile map,AiWorld world,EstateInventory inventory,int wood=32)
        {
            if(wood<0)throw new ArgumentOutOfRangeException(nameof(wood));
            // A saved simulation already owns its resources; startup grants only belong to new maps.
            if(!string.IsNullOrEmpty(map.SourcePath) && !string.Equals(Path.GetExtension(map.SourcePath),".s2m",StringComparison.OrdinalIgnoreCase))return;
            foreach(var territory in world.Territories.Values)
                if(territory.Kind==AiTerritoryKind.Estate)inventory.TryGrantInitialWood(territory.Id,wood);
        }
    }
    public class BuildingConstructionPlanner : IAiPlanner
    {
        private readonly S2MBuildingConstructionPolicy[] policies;
        private readonly Dictionary<int,int> evaluationCursor=new();
        private int commitCursor;
        public BuildingConstructionPlanner(params S2MBuildingConstructionPolicy[] policies)=>this.policies=Array.FindAll(policies,p=>p!=null);
        public void Evaluate(in AiEvaluation evaluation)
        {
            if(policies.Length==0)return;
            evaluationCursor.TryGetValue(evaluation.Territory.Id,out int first);
            // Each territory rotates policy order, sharing the same work budget without starvation.
            for(int i=0;i<policies.Length;i++)policies[(first+i)%policies.Length].Evaluate(evaluation);
            evaluationCursor[evaluation.Territory.Id]=(first+1)%policies.Length;
        }
        public void CommitPending(double now)
        {
            // One serial construction attempt per advancing frame across all building policies.
            for(int i=0;i<policies.Length;i++)
            {
                int next=(commitCursor+i)%policies.Length;
                if(policies[next].PendingCount==0)continue;
                policies[next].CommitPending(now);commitCursor=(next+1)%policies.Length;return;
            }
        }
    }
    public sealed class EstateConstructionPlanner : BuildingConstructionPlanner
    {public EstateConstructionPlanner(params S2MBuildingConstructionPolicy[] policies):base(policies){}}

}
