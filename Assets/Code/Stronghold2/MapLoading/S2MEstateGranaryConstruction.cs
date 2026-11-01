using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.MapLoading
{
    [Serializable] public class EstateBuildingSettings
    {
        public bool Enabled=true;
        public int SearchRadius=40;
        public int PreferredOffsetX=2, PreferredOffsetZ=0;
        public float TroopExclusionRadius=7;
        public double RetrySeconds=5;
        public void Validate()
        {
            if(SearchRadius<0 || SearchRadius>128 || Math.Abs((long)PreferredOffsetX)>128 || Math.Abs((long)PreferredOffsetZ)>128
                ||float.IsNaN(TroopExclusionRadius)||float.IsInfinity(TroopExclusionRadius)||TroopExclusionRadius<0||TroopExclusionRadius>32
                ||!(RetrySeconds>0)||double.IsInfinity(RetrySeconds))throw new ArgumentOutOfRangeException(nameof(EstateBuildingSettings));
        }
    }
    [Serializable] public sealed class EstateGranarySettings : EstateBuildingSettings {}
    public interface IPreparedAiBuilding : IDisposable { void Show(); }
    public interface IStagedAiBuilding : IPreparedAiBuilding { bool IsPrepared { get; } void AdvancePreparation(); }
    public interface IPreparedAiLabor : IPreparedAiBuilding { LaborBuilding Building { get; } }

    /// <summary>Budgeted estate policy plus serial commit boundary. Assets are prepared by the host before activation.</summary>
    public abstract class S2MEstateBuildingConstruction : IAiPlanner, IDisposable
    {
        private sealed class Search
        {
            public AiFaction? Owner;
            public int Cursor;
            public double RetryAt;
            public BuildingPlacement Ready;
            public MapPoint Origin;
            public bool HasOrigin,Preparing;
        }
        protected readonly S2MFile map;
        protected readonly AiWorld world;
        protected readonly EstateInventory inventory;
        private readonly NavigationGrid navigation;
        protected readonly LaborManager labor;
        private readonly StartupBuildingSettings placementSettings;
        private readonly EstateBuildingSettings settings;
        private readonly S2MBuildingDescriptor descriptor;
        private readonly string buildingType;
        private readonly int woodCost;
        public event Action<BuildingPlacement> BuildingCompleted;
        protected virtual int DesiredCount(AiTerritory territory)=>1;
        protected int BuildingWidth=>descriptor.Width;
        protected int BuildingHeight=>descriptor.Height;
        protected virtual bool TrySearchOrigin(in AiEvaluation evaluation,out MapPoint origin)
        {
            var marker=markers[evaluation.Territory.Id];
            origin=new MapPoint(marker.X+settings.PreferredOffsetX,marker.Z+settings.PreferredOffsetZ);return true;
        }
        protected virtual bool IsSearchOriginCurrent(int estate)=>true;
        private readonly List<(int X,int Z)> candidates;
        private readonly Dictionary<int,EstateMarkerFlag> markers;
        private readonly Dictionary<int,Search> searches=new();
        private readonly Queue<int> pending=new();
        private readonly HashSet<int> queued=new();
        private readonly Dictionary<int,IPreparedAiBuilding> views=new();
        private readonly HashSet<int> ids;
        private S2MStartupBuildings.PlacementWorld placementWorld;
        private IReadOnlyList<BuildingPlacement> indexedBuildings;
        private IReadOnlyList<ActorPlacement> indexedActors;
        private bool[] troopMask;
        private bool disposed;
        private sealed class Preparation
        {
            public int Estate;public float Height;public Search Search;public BuildingPlacement Placement;
            public IPreparedAiBuilding View;public EstateInventory.GoodsReservation Cost;
        }
        private Preparation preparation;
        public int CompletedCount { get; private set; }
        public long CandidateTests { get; private set; }
        public int PendingCount=>pending.Count+(preparation!=null?1:0);
        public event Action<int,Exception> ConstructionFailed;
        public Func<BuildingPlacement,IPreparedAiLabor> PrepareLabor { get; set; }
        private readonly Dictionary<int,IPreparedAiLabor> laborViews=new();
        public Func<BuildingPlacement,bool> AdditionalPlacementRule { get; set; }
        public Func<BuildingPlacement,IPreparedAiBuilding> PreparePresentation { get; set; }

        protected S2MEstateBuildingConstruction(S2MFile map,AiWorld world,EstateInventory inventory,string installation,
            NavigationGrid navigation=null,LaborManager labor=null,StartupBuildingSettings placementSettings=null,EstateBuildingSettings settings=null,string buildingType="Granary")
        {
            this.map=map??throw new ArgumentNullException(nameof(map));this.world=world??throw new ArgumentNullException(nameof(world));
            this.inventory=inventory??throw new ArgumentNullException(nameof(inventory));this.navigation=navigation;this.labor=labor;
            this.placementSettings=placementSettings??new StartupBuildingSettings();this.placementSettings.Validate();
            this.settings=settings??new EstateBuildingSettings();this.settings.Validate();
            this.buildingType=buildingType;woodCost=buildingType=="Hovel"?6:buildingType=="SawPit"?3:0;
            if(!S2MBuildingAssetCatalog.TryGetDescriptor(new BuildingPlacement{Type=buildingType,HovelAppearance=0},null,out string descriptorPath,out string reason))throw new ArgumentException(reason);
            descriptor=S2MBuildingDescriptorReader.Read(installation,descriptorPath);
            candidates=S2MStartupBuildings.Candidates(this.settings.SearchRadius,false);
            markers=map.EstateMarkers.Where(m=>m.Type==EstateType.Estate).ToDictionary(m=>m.EstateId);
            ids=new HashSet<int>(map.S2GameObjects.Select(o=>o.Id));
            foreach(var b in map.BuildingPlacements)ids.Add(b.Id);
            foreach(var a in map.Actors)ids.Add(a.Id);
        }
        private bool Eligible(int estate,out AiTerritory territory)
        {
            territory=null;
            return !disposed && settings.Enabled && world.TryGet(estate,out territory)
                && territory.CanPlan && territory.Kind==AiTerritoryKind.Estate && territory.ConstructionPolicy==AiConstructionPolicy.Allowed
                && territory.BuildingCount(buildingType)<DesiredCount(territory) && markers.ContainsKey(estate);
        }
        public void Evaluate(in AiEvaluation evaluation)
        {
            int estate=evaluation.Territory.Id;
            if(!Eligible(estate,out var t)){searches.Remove(estate);return;}
            if((evaluation.Work&AiWork.Economy)==0 && (evaluation.Changes&(AiChange.Buildings|AiChange.Ownership|AiChange.Profile))==0)return;
            if(!searches.TryGetValue(estate,out var search) || !Nullable.Equals(search.Owner,t.Owner))
                searches[estate]=search=new Search{Owner=t.Owner};
            if(search.Ready!=null || search.Preparing || evaluation.Now<search.RetryAt)return;
            if(search.HasOrigin && !IsSearchOriginCurrent(estate)){search.HasOrigin=false;search.Cursor=0;}
            if(!search.HasOrigin)
            {
                if(!TrySearchOrigin(evaluation,out search.Origin))return;
                search.HasOrigin=true;search.Cursor=0;
            }
            RefreshPlacementWorld();
            var p=new BuildingPlacement{Type=buildingType,HovelAppearance=buildingType=="Hovel"?0:(int?)null,RuntimeEstateId=estate,FootprintWidth=descriptor.Width,FootprintHeight=descriptor.Height};
            while(search.Cursor<candidates.Count && evaluation.Budget.TrySpend())
            {
                CandidateTests++;
                var offset=candidates[search.Cursor++];
                p.RawX=(search.Origin.X+offset.X)*1024;
                p.RawZ=(search.Origin.Z+offset.Z)*1024;
                if(!CanPlace(p,out float height))continue;
                p.RawY=height;search.Ready=p;if(queued.Add(estate))pending.Enqueue(estate);return;
            }
            if(search.Cursor==candidates.Count){search.Cursor=0;search.RetryAt=evaluation.Now+settings.RetrySeconds;}
        }
        /// <summary>Call once per construction frame action. Starts, advances one inactive preparation stage, or commits a ready building.</summary>
        public void CommitPending(double now)
        {
            if(disposed)return;
            if(preparation==null)
            {
                if(pending.Count==0)return;
                int estate=pending.Dequeue();queued.Remove(estate);
                if(!searches.TryGetValue(estate,out var search) || search.Ready==null)return;
                var p=search.Ready;search.Ready=null;
                if(!Revalidate(estate,search,p,now))return;
                p.Id=world.AllocateBuildingId(ids);ids.Add(p.Id);
                if(!inventory.TryReserveEstateGoods(estate,Goods.Wood,woodCost,out var cost))
                {search.Cursor=0;search.RetryAt=now+settings.RetrySeconds;return;}
                var next=new Preparation{Estate=estate,Height=p.RawY,Search=search,Placement=p,Cost=cost};preparation=next;search.Preparing=true;
                try{next.View=PreparePresentation?.Invoke(p);}
                catch(Exception error){CancelPreparation(now,error);return;}
                // Starting a staged view and advancing it are separate frame actions.
                if(next.View is IStagedAiBuilding)return;
            }
            var active=preparation;
            if(!Revalidate(active.Estate,active.Search,active.Placement,now)){CancelPreparation(now);return;}
            if(active.Height!=active.Placement.RawY){active.Search.Cursor=0;CancelPreparation(now);return;}
            if(active.View is IStagedAiBuilding staged && !staged.IsPrepared)
            {
                try{staged.AdvancePreparation();}
                catch(Exception error){CancelPreparation(now,error);}
                return;
            }
            CompletePreparation(active,now);
        }
        private bool Revalidate(int estate,Search search,BuildingPlacement p,double now)
        {
            if(!Eligible(estate,out var territory) || !Nullable.Equals(territory.Owner,search.Owner)){searches.Remove(estate);return false;}
            if(!IsSearchOriginCurrent(estate)){search.HasOrigin=false;search.Cursor=0;return false;}
            // Reconcile an external authority before creating a duplicate building.
            foreach(var existing in map.BuildingPlacements)
                if(existing.Type==buildingType && S2MCampfireLayout.EstateAtBuilding(map,existing)==estate)world.UpsertBuilding(new AiBuilding(existing.Id,estate,buildingType));
            if(!Eligible(estate,out _)){searches.Remove(estate);return false;}
            RefreshPlacementWorld();
            if(!CanPlace(p,out float height)){search.Cursor=0;search.RetryAt=now+settings.RetrySeconds;return false;}
            p.RawY=height;return true;
        }
        private void CancelPreparation(double now,Exception error=null)
        {
            var active=preparation;if(active==null)return;preparation=null;
            active.Search.Preparing=false;active.View?.Dispose();active.Cost.Dispose();
            if(error!=null)
            {
                if(active.Search.Cursor>=candidates.Count)active.Search.Cursor=0;
                active.Search.RetryAt=now+settings.RetrySeconds;ConstructionFailed?.Invoke(active.Estate,error);
            }
        }
        private void CompletePreparation(Preparation active,double now)
        {
            int estate=active.Estate;var p=active.Placement;var search=active.Search;var view=active.View;
            IPreparedAiLabor preparedLabor=null;var before=map.BuildingPlacements;
            bool storage=false,blocker=false,staffing=false,published=false;
            try
            {
                if(buildingType=="Granary"){inventory.Register(p.Id,estate,StorageKind.Granary);storage=true;}
                if(navigation!=null)
                {
                    var f=S2MBuildingFootprint.FromPlacement(p);
                    navigation.SetRectangularObstacle(ObstacleId(p.Id),f.MinX,f.MinZ,f.MaxXExclusive,f.MaxZExclusive);blocker=true;
                }
                if(labor!=null)
                {
                    preparedLabor=PrepareLabor?.Invoke(p);
                    if(preparedLabor!=null)
                    {
                        if(preparedLabor.Building.BuildingId!=p.Id || preparedLabor.Building.EstateId!=estate)throw new InvalidOperationException("Prepared staffing belongs to another building/estate.");
                        labor.RegisterBuilding(preparedLabor.Building);
                    }
                    else if(buildingType=="SawPit")throw new InvalidOperationException("A sawpit requires a prepared woodcutter job.");
                    else labor.RegisterPassiveBuilding(p.Id,estate);
                    staffing=true;
                }
                var buildings=new List<BuildingPlacement>(before){p};map.BuildingPlacements=buildings.AsReadOnly();
                published=true;world.UpsertBuilding(new AiBuilding(p.Id,estate,p.Type));
                view?.Show();preparedLabor?.Show();
                if(view!=null)views.Add(p.Id,view);
                if(preparedLabor!=null)laborViews.Add(p.Id,preparedLabor);
                placementWorld.Reserve(p);indexedBuildings=map.BuildingPlacements;
                CompletedCount++;searches.Remove(estate);
            }
            catch(Exception error)
            {
                views.Remove(p.Id);preparedLabor?.Dispose();laborViews.Remove(p.Id);map.BuildingPlacements=before;
                if(published)world.RemoveBuilding(p.Id);
                if(staffing)labor.RemoveUnstaffedBuilding(p.Id);
                if(blocker)navigation.SetObstacle(ObstacleId(p.Id),default,0,false);
                if(storage)inventory.TryRemoveEmptyBuilding(p.Id);
                placementWorld=null;CancelPreparation(now,error);return;
            }
            preparation=null;search.Preparing=false;
            active.Cost.Commit();active.Cost.Dispose();BuildingCompleted?.Invoke(p);
        }
        private static long ObstacleId(int id)=>(2L<<32)+(uint)id;
        private void RefreshPlacementWorld()
        {
            if(placementWorld!=null && ReferenceEquals(indexedBuildings,map.BuildingPlacements) && ReferenceEquals(indexedActors,map.Actors))return;
            if(placementWorld!=null && ReferenceEquals(indexedActors,map.Actors) && indexedBuildings.Count<=map.BuildingPlacements.Count)
            {
                bool append=true;for(int i=0;i<indexedBuildings.Count;i++)if(!ReferenceEquals(indexedBuildings[i],map.BuildingPlacements[i])){append=false;break;}
                if(append)
                {
                    for(int i=indexedBuildings.Count;i<map.BuildingPlacements.Count;i++){var added=map.BuildingPlacements[i];placementWorld.Reserve(added);ids.Add(added.Id);}
                    indexedBuildings=map.BuildingPlacements;return;
                }
            }
            placementWorld=new S2MStartupBuildings.PlacementWorld(map,map.BuildingPlacements,map.Keeps,placementSettings);
            troopMask=placementWorld.CreateTroopMask(settings.TroopExclusionRadius,descriptor.Width*.5f+1.5f,descriptor.Height*.5f+1.5f);
            indexedBuildings=map.BuildingPlacements;indexedActors=map.Actors;
            foreach(var b in map.BuildingPlacements)ids.Add(b.Id);
        }
        private bool CanPlace(BuildingPlacement p,out float height)
        {
            if(!placementWorld.CanPlace(p,out height))return false;
            if(AdditionalPlacementRule!=null && !AdditionalPlacementRule(p))return false;
            if(troopMask[(int)p.MapZ*map.MapSize+(int)p.MapX])return false;
            if(navigation!=null)
            {
                var f=S2MBuildingFootprint.FromPlacement(p);
                int resolution=navigation.Resolution;
                for(int z=f.MinZ*resolution;z<f.MaxZExclusive*resolution;z++)for(int x=f.MinX*resolution;x<f.MaxXExclusive*resolution;x++)
                    if(!navigation.IsWalkable(new MapPoint((x+.5f)/resolution,(z+.5f)/resolution)))return false;
            }
            return true;
        }
        /// <summary>Call when authoritative actor positions change in-place; replacement map lists invalidate automatically.</summary>
        public void InvalidatePlacement()=>placementWorld=null;
        public virtual void Dispose()
        {
            if(disposed)return;disposed=true;CancelPreparation(0);pending.Clear();queued.Clear();searches.Clear();
            foreach(var view in views.Values)view.Dispose();views.Clear();
            foreach(var view in laborViews.Values)view.Dispose();laborViews.Clear();
        }
    }
    public sealed class S2MEstateGranaryConstruction : S2MEstateBuildingConstruction
    {
        public S2MEstateGranaryConstruction(S2MFile map,AiWorld world,EstateInventory inventory,string installation,
            NavigationGrid navigation=null,LaborManager labor=null,StartupBuildingSettings placementSettings=null,EstateGranarySettings settings=null)
            :base(map,world,inventory,installation,navigation,labor,placementSettings,settings??new EstateGranarySettings()){}
    }

}
