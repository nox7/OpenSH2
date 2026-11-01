using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Stronghold2.Construction;
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
    public abstract class S2MBuildingConstructionPolicy : IAiPlanner, IDisposable
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
        protected readonly NavigationGrid navigation;
        protected readonly LaborManager labor;
        private readonly StartupBuildingSettings placementSettings;
        private readonly EstateBuildingSettings settings;
        private readonly S2MBuildingDefinition definition;
        private readonly S2MBuildingDescriptor descriptor;
        private readonly S2MBuildingConstructionService construction;
        private readonly bool ownsConstruction;
        private readonly string buildingType;
        public event Action<BuildingPlacement> BuildingCompleted;
        protected virtual int DesiredCount(AiTerritory territory)=>1;
        protected virtual bool TerritoryEligible(AiTerritory territory)=>territory.Kind==AiTerritoryKind.Estate && markers.ContainsKey(territory.Id);
        protected int BuildingWidth=>descriptor.Width;
        protected int BuildingHeight=>descriptor.Height;
        protected virtual bool TrySearchOrigin(in AiEvaluation evaluation,out MapPoint origin)
        {
            var marker=markers[evaluation.Territory.Id];
            origin=new MapPoint(marker.X+settings.PreferredOffsetX,marker.Z+settings.PreferredOffsetZ);return true;
        }
        protected virtual bool IsSearchOriginCurrent(int estate)=>true;
        protected virtual int RotationQuarterTurns(AiTerritory territory)=>0;
        protected virtual bool PolicyPlacementAllowed(BuildingPlacement placement)=>true;
        private bool AdditionalRules(BuildingPlacement placement)=>PolicyPlacementAllowed(placement) && (AdditionalPlacementRule?.Invoke(placement)??true);
        private readonly List<(int X,int Z)> candidates;
        private readonly Dictionary<int,EstateMarkerFlag> markers;
        private readonly Dictionary<int,Search> searches=new();
        private readonly Queue<int> pending=new();
        private readonly HashSet<int> queued=new();
        private bool disposed;
        private S2MBuildingConstructionService.Operation preparation;
        private Search preparingSearch;
        public int CompletedCount { get; private set; }
        public long CandidateTests { get; private set; }
        public int PendingCount=>pending.Count+(preparation!=null?1:0);
        public event Action<int,Exception> ConstructionFailed;
        public Func<BuildingPlacement,IPreparedAiLabor> PrepareLabor { get; set; }
        public Func<BuildingPlacement,IPreparedAiBuilding> PrepareBindings { get; set; }
        public Func<BuildingPlacement,bool> AdditionalPlacementRule { get; set; }
        public Func<BuildingPlacement,bool> ValidateAfterNavigation { get; set; }
        public Func<BuildingPlacement,IPreparedAiBuilding> PreparePresentation { get; set; }

        protected S2MBuildingConstructionPolicy(S2MFile map,AiWorld world,EstateInventory inventory,string installation,
            NavigationGrid navigation=null,LaborManager labor=null,StartupBuildingSettings placementSettings=null,EstateBuildingSettings settings=null,string buildingType="Granary",S2MBuildingConstructionService construction=null)
        {
            this.map=map??throw new ArgumentNullException(nameof(map));this.world=world??throw new ArgumentNullException(nameof(world));
            this.inventory=inventory??throw new ArgumentNullException(nameof(inventory));this.navigation=navigation;this.labor=labor;
            this.placementSettings=placementSettings??new StartupBuildingSettings();this.placementSettings.Validate();
            this.settings=settings??new EstateBuildingSettings();this.settings.Validate();
            this.buildingType=buildingType;definition=S2MBuildingDefinition.Load(installation,buildingType);descriptor=definition.Descriptor;
            ownsConstruction=construction==null;this.construction=construction??new S2MBuildingConstructionService(map,world,inventory,navigation,labor,this.placementSettings);
            this.construction.RegisterDefinition(definition);
            candidates=S2MStartupBuildings.Candidates(this.settings.SearchRadius,false);
            markers=map.EstateMarkers.Where(m=>m.Type==EstateType.Estate).ToDictionary(m=>m.EstateId);

        }
        private bool Eligible(int estate,out AiTerritory territory)
        {
            territory=null;
            return !disposed && settings.Enabled && world.TryGet(estate,out territory)
                && territory.CanPlan && TerritoryEligible(territory) && territory.ConstructionPolicy==AiConstructionPolicy.Allowed
                && territory.BuildingCount(buildingType)<DesiredCount(territory);
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
            var p=definition.Propose(estate,0,0,RotationQuarterTurns(t));
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
                if(pending.Count==0)return;int estate=pending.Dequeue();queued.Remove(estate);
                if(!searches.TryGetValue(estate,out var search) || search.Ready==null)return;
                var proposal=search.Ready;search.Ready=null;
                bool Intent()
                {
                    foreach(var existing in map.BuildingPlacements)
                        if(existing.Type==buildingType && S2MCampfireLayout.EstateAtBuilding(map,existing)==estate)world.UpsertBuilding(new AiBuilding(existing.Id,estate,buildingType));
                    return Eligible(estate,out var territory) && Nullable.Equals(territory.Owner,search.Owner) && IsSearchOriginCurrent(estate);
                }
                if(!construction.TrySubmit(new BuildingConstructionRequest{Definition=definition,Placement=proposal,ValidateIntent=Intent,
                    TroopExclusionRadius=settings.TroopExclusionRadius,AdditionalPlacementRule=AdditionalRules,
                    PreparePresentation=PreparePresentation,PrepareLabor=PrepareLabor,PrepareBindings=PrepareBindings,ValidateAfterNavigation=ValidateAfterNavigation},out preparation))
                {search.Cursor=0;search.HasOrigin=false;search.RetryAt=now+settings.RetrySeconds;return;}
                preparingSearch=search;search.Preparing=true;
            }
            preparation.Advance();
            if(preparation.Status==BuildingConstructionStatus.Preparing)return;
            var finished=preparation;preparation=null;preparingSearch.Preparing=false;
            if(finished.Status==BuildingConstructionStatus.Completed)
            {CompletedCount++;searches.Remove(finished.Building.RuntimeEstateId.Value);BuildingCompleted?.Invoke(finished.Building);}
            else
            {
                if(finished.Error==null || preparingSearch.Cursor>=candidates.Count){preparingSearch.Cursor=0;preparingSearch.HasOrigin=false;}preparingSearch.RetryAt=now+settings.RetrySeconds;
                if(finished.Error!=null)ConstructionFailed?.Invoke(finished.Building.RuntimeEstateId.Value,finished.Error);
            }
            preparingSearch=null;
        }
        private bool CanPlace(BuildingPlacement p,out float height)
        {var result=construction.Placement.Check(p,settings.TroopExclusionRadius,AdditionalRules);height=result.RawHeight;return result.Allowed;}
        public void InvalidatePlacement()=>construction.Placement.Invalidate();
        public virtual void Dispose()
        {if(disposed)return;disposed=true;preparation?.Cancel();preparation=null;pending.Clear();queued.Clear();searches.Clear();if(ownsConstruction)construction.Dispose();}
    }
    /// <summary>Compatibility specialization for estate goals; generic execution also serves kingdoms and future player commands.</summary>
    public abstract class S2MEstateBuildingConstruction : S2MBuildingConstructionPolicy
    {
        protected S2MEstateBuildingConstruction(S2MFile map,AiWorld world,EstateInventory inventory,string installation,NavigationGrid navigation=null,LaborManager labor=null,
            StartupBuildingSettings placementSettings=null,EstateBuildingSettings settings=null,string buildingType="Granary",S2MBuildingConstructionService construction=null)
            :base(map,world,inventory,installation,navigation,labor,placementSettings,settings,buildingType,construction){}
    }
    public sealed class S2MEstateGranaryConstruction : S2MEstateBuildingConstruction
    {
        public S2MEstateGranaryConstruction(S2MFile map,AiWorld world,EstateInventory inventory,string installation,
            NavigationGrid navigation=null,LaborManager labor=null,StartupBuildingSettings placementSettings=null,EstateGranarySettings settings=null,S2MBuildingConstructionService construction=null)
            :base(map,world,inventory,installation,navigation,labor,placementSettings,settings??new EstateGranarySettings(),construction:construction){}
    }

}
