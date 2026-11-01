using System;
using Assets.Code.Stronghold2.Construction;
using System.Collections.Generic;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading
{
    [Serializable] public sealed class EstateSawpitSettings : EstateBuildingSettings
    {
        // Zero uses the native production-profile targets; a positive value overrides wood-producing estates only.
        public int WoodEstateTargetOverride;
        public EstateSawpitSettings(){PreferredOffsetX=0;PreferredOffsetZ=0;}
    }
    /// <summary>Every estate bootstraps wood before other industries; wood profiles may expand within actual labor supply.</summary>
    public sealed class S2MEstateSawpitConstruction : S2MEstateBuildingConstruction
    {
        private readonly EstateSawpitSettings policy;
        private readonly FoliageResources foliage;
        private sealed class OriginSearch
        {
            public readonly List<BuildingPlacement> Stocks=new();
            public int Cursor,Phase;public FoliageResources.NearestTreeSearch Query;
            public FoliageResource Tree;public BuildingPlacement Stock;
            public double Distance=double.PositiveInfinity,RetryAt;
            public IReadOnlyList<BuildingPlacement> IndexedBuildings;
        }
        private readonly Dictionary<int,OriginSearch> origins=new();
        public long ResourceSearchSteps { get; private set; }
        private static bool Standing(FoliageResource tree)=>tree!=null && (tree.State==FoliageState.Available || tree.State==FoliageState.Reserved);
        public S2MEstateSawpitConstruction(S2MFile map,AiWorld world,EstateInventory inventory,string installation,
            NavigationGrid navigation=null,LaborManager labor=null,StartupBuildingSettings placementSettings=null,EstateSawpitSettings settings=null,FoliageResources foliage=null,S2MBuildingConstructionService construction=null)
            :base(map,world,inventory,installation,navigation,labor,placementSettings,settings??new EstateSawpitSettings(),"SawPit",construction)
        {
            this.foliage=foliage;policy=settings??new EstateSawpitSettings();
            BuildingCompleted+=p=>world.SetPlannedLabor(p.RuntimeEstateId.Value,"SawPit",0);
            if(policy.WoodEstateTargetOverride<0 || policy.WoodEstateTargetOverride>24)throw new ArgumentOutOfRangeException(nameof(settings));
        }
        public override void Dispose()
        {
            base.Dispose();
            foreach(var search in origins.Values)search.Query?.Dispose();origins.Clear();
            foreach(int estate in world.Territories.Keys)world.SetPlannedLabor(estate,"SawPit",0);
        }
        protected override bool IsSearchOriginCurrent(int estate)
        {
            if(foliage==null)return true;
            if(!origins.TryGetValue(estate,out var search) || !Standing(search.Tree) || search.Stock==null)return false;
            if(StocksCurrent(estate,search))return true;
            search.Query?.Dispose();origins.Remove(estate);return false;
        }
        protected override bool TrySearchOrigin(in AiEvaluation evaluation,out MapPoint origin)
        {
            origin=default;if(foliage==null)return false;int estate=evaluation.Territory.Id;
            origins.TryGetValue(estate,out var search);
            if(search!=null && search.Phase==2 && evaluation.Now<search.RetryAt)return false;
            if(search==null || !StocksCurrent(estate,search) || search.Tree!=null && !Standing(search.Tree) || search.Phase==2)
            {
                search?.Query?.Dispose();search=new OriginSearch{IndexedBuildings=map.BuildingPlacements};origins[estate]=search;
                foreach(var b in map.BuildingPlacements)if(b.Type=="StockPile" && S2MCampfireLayout.EstateAtBuilding(map,b)==estate)search.Stocks.Add(b);
                search.Stocks.Sort((a,b)=>a.Id.CompareTo(b.Id));
            }
            if(search.Tree!=null && search.Cursor>=search.Stocks.Count){origin=Origin(search.Tree);return true;}
            while(search.Cursor<search.Stocks.Count)
            {
                var stock=search.Stocks[search.Cursor];
                var f=Assets.Code.Stronghold2.AssetLoading.S2MBuildingFootprint.FromPlacement(stock);
                var center=new MapPoint((f.MinX+f.MaxXExclusive)*.5f,(f.MinZ+f.MaxZExclusive)*.5f);
                search.Query??=foliage.FindNearestTree(center,search.Phase==1);
                while(!search.Query.Complete && evaluation.Budget.TrySpend()){search.Query.Step();ResourceSearchSteps++;}
                if(!search.Query.Complete)return false;
                var tree=search.Query.Result;
                if(tree!=null && !Standing(tree)){search.Query.Dispose();search.Query=null;return false;}
                if(tree!=null)
                {
                    double dx=tree.Position.X-center.X,dz=tree.Position.Z-center.Z,d=dx*dx+dz*dz;
                    if(d<search.Distance || d==search.Distance && (search.Tree==null || tree.Id<search.Tree.Id))
                    {search.Tree=tree;search.Stock=stock;search.Distance=d;}
                }
                search.Query.Dispose();search.Query=null;search.Cursor++;
            }
            // Trees take priority; bushes are a useful fallback on maps with no standing trees.
            if(search.Tree==null)
            {
                if(search.Phase==0){search.Phase=1;search.Cursor=0;return false;}
                search.Phase=2;search.RetryAt=evaluation.Now+policy.RetrySeconds;return false;
            }
            origin=Origin(search.Tree);return true;
        }
        private bool StocksCurrent(int estate,OriginSearch search)
        {
            if(ReferenceEquals(search.IndexedBuildings,map.BuildingPlacements))return true;
            int count=0;
            foreach(var stock in map.BuildingPlacements)
            {
                if(stock.Type!="StockPile" || S2MCampfireLayout.EstateAtBuilding(map,stock)!=estate)continue;
                if(!search.Stocks.Contains(stock))return false;count++;
            }
            if(count!=search.Stocks.Count)return false;
            search.IndexedBuildings=map.BuildingPlacements;return true;
        }
        private MapPoint Origin(FoliageResource tree)=>new MapPoint((int)Math.Round(tree.Position.X-BuildingWidth*.5f)+policy.PreferredOffsetX,
            (int)Math.Round(tree.Position.Z-BuildingHeight*.5f)+policy.PreferredOffsetZ);
        protected override int DesiredCount(AiTerritory territory)
        {
            int target=territory.SawpitTarget;
            if(territory.ProducesWood && policy.WoodEstateTargetOverride>0)target=policy.WoodEstateTargetOverride;
            world.SetSawpitGoal(territory.Id,target);
            int current=territory.BuildingCount("SawPit");
            bool ready=current<target && territory.BuildingCount("Hovel")>0 && territory.BuildingCount("StockPile")>0 && labor!=null && PrepareLabor!=null && foliage!=null;
            // Housing sees one prospective worker only when this next production slot is affordable and otherwise ready.
            world.SetPlannedLabor(territory.Id,"SawPit",ready && inventory.AvailableAmount(territory.Id,Goods.Wood)>=3?1:0);
            if(!ready)return current;
            var supply=labor.GetLaborSupply(territory.Id);var demand=labor.GetLaborDemand(territory.Id);
            // Existing vacancies have first claim. Births still pending do not authorize another production slot.
            return supply.Available>demand.MissingLaborers?Math.Min(target,current+1):current;
        }
    }
}
