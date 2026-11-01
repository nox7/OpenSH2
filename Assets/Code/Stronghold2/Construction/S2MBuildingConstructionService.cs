using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.Construction
{
    public enum BuildingConstructionStatus { Preparing,Completed,Cancelled,Failed }
    /// <summary>A trusted game command. Selection of player/AI/blueprint intent and authority belongs to the caller.</summary>
    public sealed class BuildingConstructionRequest
    {
        public S2MBuildingDefinition Definition;
        public BuildingPlacement Placement;
        public Func<bool> ValidateIntent;
        public Func<BuildingPlacement,bool> AdditionalPlacementRule;
        public Func<BuildingPlacement,bool> ValidateAfterNavigation;
        public float TroopExclusionRadius=7;
        public Func<BuildingPlacement,IPreparedAiBuilding> PreparePresentation;
        public Func<BuildingPlacement,IPreparedAiLabor> PrepareLabor;
        public Func<BuildingPlacement,IPreparedAiBuilding> PrepareBindings;
    }
    /// <summary>Shared staged command executor. It never chooses an AI strategy or reads AIC blueprints.</summary>
    public sealed class S2MBuildingConstructionService : IDisposable
    {
        private readonly S2MFile map;private readonly AiWorld world;private readonly EstateInventory inventory;
        private readonly NavigationGrid navigation;private readonly LaborManager labor;
        public S2MBuildingPlacementService Placement { get; }
        private readonly Dictionary<string,S2MBuildingDefinition> definitions=new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string,S2MBuildingDefinition> Definitions=>definitions;
        public void RegisterDefinition(S2MBuildingDefinition definition)=>definitions[definition.Type]=definition;
        private readonly HashSet<int> occupied;
        private readonly HashSet<Operation> active=new();private readonly List<Instance> instances=new();private bool disposed;
        public S2MBuildingConstructionService(S2MFile map,AiWorld world,EstateInventory inventory,NavigationGrid navigation=null,LaborManager labor=null,StartupBuildingSettings settings=null)
        {
            this.map=map;this.world=world;this.inventory=inventory;this.navigation=navigation;this.labor=labor;
            Placement=new S2MBuildingPlacementService(map,navigation,settings);
            occupied=new HashSet<int>(map.S2GameObjects.Select(o=>o.Id));foreach(var b in map.BuildingPlacements)occupied.Add(b.Id);foreach(var a in map.Actors)occupied.Add(a.Id);
        }
        public bool TrySubmit(BuildingConstructionRequest request,out Operation operation)
        {
            operation=null;if(disposed)throw new ObjectDisposedException(nameof(S2MBuildingConstructionService));
            if(request?.ValidateIntent==null || request.Definition==null || request.Placement==null)throw new ArgumentException("A definition, proposal and authoritative intent validator are required.");
            if(!request.ValidateIntent() || !ShapeMatches(request))return false;
            var initial=Placement.Check(request.Placement,request.TroopExclusionRadius,request.AdditionalPlacementRule);if(!initial.Allowed)return false;
            // A cursor/blueprint may reuse its proposal object; an accepted command owns a snapshot.
            var source=request;var proposed=source.Placement;
            request=new BuildingConstructionRequest{Definition=source.Definition,Placement=source.Definition.Propose(proposed.RuntimeEstateId.Value,(int)proposed.MapX,(int)proposed.MapZ,(int)Math.Round(proposed.RotationDegrees/90)),
                ValidateIntent=source.ValidateIntent,AdditionalPlacementRule=source.AdditionalPlacementRule,ValidateAfterNavigation=source.ValidateAfterNavigation,
                TroopExclusionRadius=source.TroopExclusionRadius,PreparePresentation=source.PreparePresentation,PrepareLabor=source.PrepareLabor,PrepareBindings=source.PrepareBindings};
            var check=Placement.Check(request.Placement,request.TroopExclusionRadius,request.AdditionalPlacementRule);if(!check.Allowed)return false;
            var costs=new List<EstateInventory.GoodsReservation>();
            foreach(var cost in request.Definition.Costs)
            {
                if(!inventory.TryReserveEstateGoods(request.Placement.RuntimeEstateId.Value,cost.Goods,cost.Amount,out var held))
                {foreach(var previous in costs)previous.Dispose();return false;}costs.Add(held);
            }
            request.Placement.RawY=check.RawHeight;request.Placement.Id=world.AllocateBuildingId(occupied);occupied.Add(request.Placement.Id);
            operation=new Operation(this,request,costs,check.RawHeight);active.Add(operation);return true;
        }
        private static bool ShapeMatches(BuildingConstructionRequest request)
        {
            var p=request.Placement;int turn=((int)Math.Round(p.RotationDegrees/90)%4+4)%4;
            return p.Type==request.Definition.Type && p.RuntimeEstateId.HasValue && p.FootprintWidth==(turn%2==0?request.Definition.Descriptor.Width:request.Definition.Descriptor.Height)
                && p.FootprintHeight==(turn%2==0?request.Definition.Descriptor.Height:request.Definition.Descriptor.Width);
        }
        public sealed class Operation : IDisposable
        {
            private readonly S2MBuildingConstructionService owner;private readonly BuildingConstructionRequest request;
            private readonly List<EstateInventory.GoodsReservation> costs;private readonly float height;
            private IPreparedAiBuilding view;private bool started;
            public BuildingConstructionStatus Status { get; private set; }
            public Exception Error { get; private set; }
            public BuildingPlacement Building=>request.Placement;
            internal Operation(S2MBuildingConstructionService owner,BuildingConstructionRequest request,List<EstateInventory.GoodsReservation> costs,float height)
            {this.owner=owner;this.request=request;this.costs=costs;this.height=height;}
            public void Advance()
            {
                if(Status!=BuildingConstructionStatus.Preparing)return;
                try
                {
                    if(!request.ValidateIntent() || !ShapeMatches(request)){Cancel();return;}
                    var check=owner.Placement.Check(Building,request.TroopExclusionRadius,request.AdditionalPlacementRule);
                    if(!check.Allowed || check.RawHeight!=height){Cancel();return;}
                    if(!started)
                    {
                        started=true;view=request.PreparePresentation?.Invoke(Building);
                        if(view is IStagedAiBuilding)return;
                    }
                    if(view is IStagedAiBuilding staged && !staged.IsPrepared){staged.AdvancePreparation();return;}
                    if(!request.ValidateIntent()){Cancel();return;}
                    Commit();
                }
                catch(Exception error){Error=error;Cancel();Status=BuildingConstructionStatus.Failed;}
            }
            private void Commit()
            {
                int estate=Building.RuntimeEstateId.Value;var before=owner.map.BuildingPlacements;var instance=new Instance{View=view};
                bool store=false,staff=false,published=false;
                try
                {
                    if(request.Definition.Storage.HasValue){owner.inventory.Register(Building.Id,estate,request.Definition.Storage.Value);store=true;}
                    if(owner.navigation!=null)instance.Navigation=S2MBuildingNavigation.Register(owner.navigation,Building);
                    if(request.ValidateAfterNavigation!=null && !request.ValidateAfterNavigation(Building))throw new InvalidOperationException("Building would block required access.");
                    if(owner.labor!=null)
                    {
                        instance.Labor=request.PrepareLabor?.Invoke(Building);
                        if(instance.Labor!=null)
                        {
                            if(instance.Labor.Building.BuildingId!=Building.Id || instance.Labor.Building.EstateId!=estate)throw new InvalidOperationException("Staffing belongs to another building/territory.");
                            owner.labor.RegisterBuilding(instance.Labor.Building);
                        }
                        else if(Building.Type=="SawPit")throw new InvalidOperationException("Sawpit requires a woodcutter job.");
                        else owner.labor.RegisterPassiveBuilding(Building.Id,estate);staff=true;
                    }
                    owner.map.BuildingPlacements=new List<BuildingPlacement>(before){Building}.AsReadOnly();published=true;
                    owner.world.UpsertBuilding(new AiBuilding(Building.Id,estate,Building.Type));
                    instance.Bindings=request.PrepareBindings?.Invoke(Building);
                    view?.Show();instance.Labor?.Show();instance.Bindings?.Show();
                    owner.instances.Add(instance);owner.active.Remove(this);Status=BuildingConstructionStatus.Completed;view=null;
                }
                catch
                {
                    instance.Bindings?.Dispose();instance.Labor?.Dispose();instance.Navigation?.Dispose();
                    owner.map.BuildingPlacements=before;if(published)owner.world.RemoveBuilding(Building.Id);
                    if(staff)owner.labor.RemoveUnstaffedBuilding(Building.Id);if(store)owner.inventory.TryRemoveEmptyBuilding(Building.Id);
                    owner.Placement.Invalidate();throw;
                }
                foreach(var cost in costs)cost.Commit();costs.Clear();
            }
            public void Cancel()
            {
                if(Status!=BuildingConstructionStatus.Preparing)return;
                Status=BuildingConstructionStatus.Cancelled;owner.active.Remove(this);view?.Dispose();view=null;
                foreach(var held in costs)held.Dispose();costs.Clear();
            }
            public void Dispose()=>Cancel();
        }
        private sealed class Instance : IDisposable
        {
            public IPreparedAiBuilding View,Bindings;public IPreparedAiLabor Labor;public IDisposable Navigation;
            public void Dispose(){View?.Dispose();Bindings?.Dispose();Labor?.Dispose();Navigation?.Dispose();}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;foreach(var operation in active.ToArray())operation.Cancel();
            foreach(var instance in instances)instance.Dispose();instances.Clear();
        }
    }
}
