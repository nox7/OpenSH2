using System;
using System.Collections.Generic;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.Construction;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading
{
    [Serializable] public sealed class KingdomStockpileSettings : EstateBuildingSettings
    {
        public float MaximumKeepDistance=40,KeepFrontGap=2;
        public KingdomStockpileSettings(){PreferredOffsetX=PreferredOffsetZ=0;}
    }
    /// <summary>General kingdom strategy's first goal. This policy does not interpret or select AIC files.</summary>
    public sealed class S2MKingdomStockpileConstruction : S2MBuildingConstructionPolicy
    {
        private readonly Dictionary<int,Keep> keeps=new();
        private readonly KingdomStockpileSettings policy;
        public S2MKingdomStockpileConstruction(S2MFile map,AiWorld world,EstateInventory inventory,string installation,NavigationGrid navigation=null,LaborManager labor=null,
            StartupBuildingSettings placementSettings=null,KingdomStockpileSettings settings=null,S2MBuildingConstructionService construction=null)
            :base(map,world,inventory,installation,navigation,labor,placementSettings,settings??new KingdomStockpileSettings(),"StockPile",construction)
        {
            policy=settings??new KingdomStockpileSettings();
            ValidateAfterNavigation=KeepCanReach;
            if(!(policy.MaximumKeepDistance>0)||float.IsInfinity(policy.MaximumKeepDistance)||!(policy.KeepFrontGap>=0)||float.IsInfinity(policy.KeepFrontGap))throw new ArgumentOutOfRangeException(nameof(settings));
            foreach(var keep in map.Keeps)
            {int estate=S2MCampfireLayout.EstateAtBuilding(map,keep);if(!keeps.TryGetValue(estate,out var previous)||keep.Id<previous.Id)keeps[estate]=keep;}
        }
        protected override bool TerritoryEligible(AiTerritory territory)=>territory.Kind==AiTerritoryKind.Kingdom && keeps.ContainsKey(territory.Id);
        protected override int RotationQuarterTurns(AiTerritory territory)=>(int)Math.Round(keeps[territory.Id].RotationDegrees/90);
        protected override bool IsSearchOriginCurrent(int estate)
        {if(!keeps.TryGetValue(estate,out var keep))return false;foreach(var current in map.Keeps)if(ReferenceEquals(keep,current))return true;return false;}
        protected override bool TrySearchOrigin(in AiEvaluation evaluation,out MapPoint origin)
        {
            var keep=keeps[evaluation.Territory.Id];float width=S2MNavigationBuilder.UnrotatedWidth(keep);
            var desired=S2MBuildingNavigation.Point(keep,(width-BuildingWidth)*.5f+policy.PreferredOffsetX,-BuildingHeight-policy.KeepFrontGap+policy.PreferredOffsetZ);
            origin=new MapPoint((int)Math.Round(desired.X),(int)Math.Round(desired.Z));return true;
        }
        public bool KeepCanReach(BuildingPlacement stockpile)
        {
            if(navigation==null)return true;
            var keep=keeps[stockpile.RuntimeEstateId.Value];
            var stair=S2MBuildingNavigation.Point(keep,S2MNavigationBuilder.UnrotatedWidth(keep)*.5f,-.35f);
            if(!navigation.TryNearest(stair,2,out var from))return false;
            foreach(var point in S2MBuildingNavigation.StockpileAccess(stockpile,navigation))if(navigation.CanReach(from,point))return true;
            return false;
        }
        protected override bool PolicyPlacementAllowed(BuildingPlacement stockpile)
        {
            var keep=keeps[stockpile.RuntimeEstateId.Value];
            var k=Assets.Code.Stronghold2.AssetLoading.S2MBuildingFootprint.FromPlacement(keep);
            var s=Assets.Code.Stronghold2.AssetLoading.S2MBuildingFootprint.FromPlacement(stockpile);
            var a=new MapPoint((k.MinX+k.MaxXExclusive)*.5f,(k.MinZ+k.MaxZExclusive)*.5f);var b=new MapPoint((s.MinX+s.MaxXExclusive)*.5f,(s.MinZ+s.MaxZExclusive)*.5f);
            return MapPoint.Distance(a,b)<=policy.MaximumKeepDistance && KeepCanReach(stockpile);
        }
    }
    /// <summary>Separate strategy from command execution. Future general kingdom goals run only after a stockpile exists.</summary>
    public sealed class GeneralKingdomPlanner : IAiPlanner
    {
        private readonly S2MKingdomStockpileConstruction stockpiles;private readonly IAiPlanner nextGoals;
        public GeneralKingdomPlanner(S2MKingdomStockpileConstruction stockpiles,IAiPlanner nextGoals=null){this.stockpiles=stockpiles;this.nextGoals=nextGoals;}
        public void Evaluate(in AiEvaluation evaluation)
        {
            if(evaluation.Territory.Kind!=AiTerritoryKind.Kingdom || !evaluation.Territory.CanPlan)return;
            if(evaluation.Territory.BuildingCount("StockPile")==0){stockpiles.Evaluate(evaluation);return;}
            nextGoals?.Evaluate(evaluation);
        }
    }
    public sealed class SettlementPlanner : IAiPlanner
    {
        private readonly IAiPlanner estates,kingdoms;
        public SettlementPlanner(IAiPlanner estates,IAiPlanner kingdoms){this.estates=estates;this.kingdoms=kingdoms;}
        public void Evaluate(in AiEvaluation evaluation)
        {if(evaluation.Territory.Kind==AiTerritoryKind.Kingdom)kingdoms.Evaluate(evaluation);else estates.Evaluate(evaluation);}
    }
}
