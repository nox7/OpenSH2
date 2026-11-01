using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;

namespace Assets.Code.Simulation.AI
{
    public enum AiTerritoryKind { Unclassified, Estate, Kingdom }
    public enum AiController { Unknown, Autonomous, Human }
    public enum AiConstructionPolicy { Unknown, Allowed, UseMapBuildings }
    public enum AiPopulationLevel { Unknown=0,Small=1,Medium=2,Large=3 }
    [Flags] public enum AiChange { None=0, Buildings=1, Resources=2, Ownership=4, Threats=8, Diplomacy=16, Profile=32, Removed=64, All=63 }

    /// <summary>A map-local faction identity, independent of colours and territory IDs.</summary>
    public readonly struct AiFaction : IEquatable<AiFaction>
    {
        public readonly string Domain;
        public readonly int Number;
        public AiFaction(string domain, int number) { Domain=domain ?? throw new ArgumentNullException(nameof(domain)); Number=number; }
        public bool Equals(AiFaction other)=>Domain==other.Domain && Number==other.Number;
        public override bool Equals(object obj)=>obj is AiFaction other && Equals(other);
        public override int GetHashCode()=>((Domain?.GetHashCode() ?? 0)*397)^Number;
    }
    public readonly struct AiBuilding
    {
        public readonly int Id, TerritoryId;
        public readonly string Type;
        public AiBuilding(int id,int territoryId,string type) { Id=id;TerritoryId=territoryId;Type=type??throw new ArgumentNullException(nameof(type)); }
    }
    public readonly struct AiThreat
    {
        public readonly int TerritoryId, SourceId;
        public readonly double ExpiresAt;
        public AiThreat(int territoryId,int sourceId,double expiresAt) { TerritoryId=territoryId;SourceId=sourceId;ExpiresAt=expiresAt; }
    }
    public sealed class AiTerritory
    {
        public int Id { get; }
        public AiTerritoryKind Kind { get; }
        public AiController Controller { get; internal set; }
        public AiFaction? Owner { get; internal set; }
        public int? ParentKingdomId { get; internal set; }
        public int? ProductionCode { get; internal set; }
        public AiPopulationLevel PopulationLevel=>!ProductionCode.HasValue?AiPopulationLevel.Unknown:
            ProductionCode.Value>=0 && ProductionCode.Value<=7?AiPopulationLevel.Small:
            ProductionCode.Value<=14 && ProductionCode.Value>=8?AiPopulationLevel.Medium:
            ProductionCode.Value>=15 && ProductionCode.Value<=19?AiPopulationLevel.Large:AiPopulationLevel.Unknown;
        public int MaximumHovels=>Kind==AiTerritoryKind.Estate?(int)PopulationLevel:0;
        public int DesiredHovels { get; internal set; }
        public bool ProducesWood=>ProductionCode==3 || ProductionCode==9 || ProductionCode==12 || ProductionCode==16;
        public int SawpitTarget=>Kind!=AiTerritoryKind.Estate?0:ProductionCode==9?2:ProductionCode==12 || ProductionCode==16?3:1;
        public int DesiredSawpits { get; internal set; }
        public int PlannedLaborers { get; internal set; }
        internal readonly Dictionary<string,int> plannedLabor=new(StringComparer.Ordinal);
        public bool HasInitialProduction=>BuildingCount("SawPit")>0;
        public LaborDemand Labor { get; internal set; }
        public AiConstructionPolicy ConstructionPolicy { get; internal set; }
        public string BlueprintKey { get; internal set; }
        public long Revision { get; internal set; }
        public bool CanPlan => Controller==AiController.Autonomous && Kind!=AiTerritoryKind.Unclassified;
        internal AiWorld World;
        public bool IsUnderAttack { get { foreach(var t in threats.Values) if(t.TerritoryId==Id && t.ExpiresAt>(World?.Now??0))return true; return false; } }
        public IReadOnlyDictionary<int,AiBuilding> Buildings { get; }
        public IReadOnlyDictionary<(int Territory,int Source),AiThreat> Threats { get; }
        internal readonly Dictionary<int,AiBuilding> buildings=new();
        internal readonly Dictionary<string,int> counts=new(StringComparer.Ordinal);
        internal readonly Dictionary<(int,int),AiThreat> threats=new();
        public AiTerritory(int id,AiTerritoryKind kind,AiController controller,AiFaction? owner=null,
            int? productionCode=null,AiConstructionPolicy constructionPolicy=AiConstructionPolicy.Unknown)
        {
            if(id<0)throw new ArgumentOutOfRangeException(nameof(id));
            Id=id;Kind=kind;Controller=controller;Owner=owner;ProductionCode=productionCode;ConstructionPolicy=constructionPolicy;
            Buildings=new ReadOnlyDictionary<int,AiBuilding>(buildings);
            Threats=new ReadOnlyDictionary<(int,int),AiThreat>(threats);
        }
        public int BuildingCount(string type)=>counts.TryGetValue(type,out int count)?count:0;
        internal double NextThreatExpiry { get { double next=double.PositiveInfinity;foreach(var t in threats.Values)if(t.ExpiresAt>(World?.Now??0))next=Math.Min(next,t.ExpiresAt);return next; } }
    }

    /// <summary>Simulation-thread knowledge index. Events update facts once; planners never scan the scene.</summary>
    public sealed class AiWorld
    {
        private readonly Dictionary<int,AiTerritory> territories=new();
        private readonly Dictionary<int,AiBuilding> buildings=new();
        private int nextRuntimeBuildingId=int.MaxValue;
        private readonly Dictionary<AiFaction,HashSet<int>> factions=new();
        private readonly SortedSet<(double At,int Receiver,int Target,int Source)> expiry=new();
        public double Now { get; private set; }
        public IReadOnlyDictionary<int,AiTerritory> Territories { get; }
        public event Action<int,AiChange> Changed;
        public AiWorld()=>Territories=new ReadOnlyDictionary<int,AiTerritory>(territories);
        /// <summary>One allocation sequence for all concurrently prepared policies, including unpublished buildings.</summary>
        public int AllocateBuildingId(ISet<int> occupied)
        {
            while(buildings.ContainsKey(nextRuntimeBuildingId) || occupied.Contains(nextRuntimeBuildingId))nextRuntimeBuildingId--;
            return nextRuntimeBuildingId--;
        }
        public bool TryGet(int id,out AiTerritory territory)=>territories.TryGetValue(id,out territory);
        public bool TryGetBuilding(int id,out AiBuilding building)=>buildings.TryGetValue(id,out building);
        public void Add(AiTerritory territory)
        {
            if(territory==null)throw new ArgumentNullException(nameof(territory));
            if(territory.World!=null)throw new ArgumentException("Territory already belongs to a world.");
            territories.Add(territory.Id,territory);territory.World=this;IndexOwner(territory,true);Notify(territory.Id,AiChange.All);
        }
        private void IndexOwner(AiTerritory t,bool add)
        {
            if(!t.Owner.HasValue)return;
            if(!factions.TryGetValue(t.Owner.Value,out var members))
            {if(!add)return;factions.Add(t.Owner.Value,members=new());}
            if(add)members.Add(t.Id);else {members.Remove(t.Id);if(members.Count==0)factions.Remove(t.Owner.Value);}
        }
        public void SetControl(int id,AiFaction? owner,AiController controller)
        {
            var t=territories[id];t.plannedLabor.Clear();t.PlannedLaborers=0;DetachParent(t);IndexOwner(t,false);t.Owner=owner;t.Controller=controller;IndexOwner(t,true);
            // Aid relationships must be explicitly rebuilt after a capture.
            foreach(var child in territories.Values)if(child.ParentKingdomId==id){DetachParent(child);Notify(child.Id,AiChange.Ownership);}
            foreach(var threat in t.threats.Values)expiry.Remove((threat.ExpiresAt,t.Id,threat.TerritoryId,threat.SourceId));
            t.threats.Clear();Notify(id,AiChange.Ownership|AiChange.Threats);
        }
        public void SetParentKingdom(int estateId,int? kingdomId)
        {
            var t=territories[estateId];
            if(t.Kind!=AiTerritoryKind.Estate)throw new ArgumentException("Only estates have a parent kingdom.");
            if(kingdomId.HasValue)
            {
                var parent=territories[kingdomId.Value];
                if(parent.Kind!=AiTerritoryKind.Kingdom || !parent.Owner.HasValue || !parent.Owner.Equals(t.Owner))
                    throw new ArgumentException("Parent must be a kingdom of the same owner.");
            }
            DetachParent(t);t.ParentKingdomId=kingdomId;Notify(estateId,AiChange.Ownership);
            if(kingdomId.HasValue)foreach(var threat in t.threats.Values)
                if(threat.ExpiresAt>Now)StoreThreat(territories[kingdomId.Value],threat);
        }
        private void DetachParent(AiTerritory t)
        {
            if(!t.ParentKingdomId.HasValue)return;
            if(territories.TryGetValue(t.ParentKingdomId.Value,out var parent))
            {
                var keys=new List<(int,int)>();
                foreach(var key in parent.threats.Keys)if(key.Item1==t.Id)keys.Add(key);
                foreach(var key in keys)ClearThreat(parent,key.Item1,key.Item2);
            }
            t.ParentKingdomId=null;
        }
        public void SetBlueprint(int id,string key) { territories[id].BlueprintKey=key;Notify(id,AiChange.Profile); }
        public void SetLaborDemand(int id,LaborDemand value){if(territories.TryGetValue(id,out var t)){t.Labor=value;Notify(id,AiChange.Buildings);}}
        public void SetPlannedLabor(int id,string goal,int workers)
        {
            if(workers<0 || string.IsNullOrEmpty(goal))throw new ArgumentOutOfRangeException(nameof(workers));
            var t=territories[id];t.plannedLabor.TryGetValue(goal,out int old);if(old==workers)return;
            if(workers==0)t.plannedLabor.Remove(goal);else t.plannedLabor[goal]=workers;
            t.PlannedLaborers+=workers-old;Notify(id,AiChange.Profile);
        }
        public void SetSawpitGoal(int id,int value){var t=territories[id];if(t.DesiredSawpits==value)return;t.DesiredSawpits=value;Notify(id,AiChange.Profile);}
        public void SetHovelGoal(int id,int value){var t=territories[id];if(t.DesiredHovels==value)return;t.DesiredHovels=value;Notify(id,AiChange.Profile);}
        public void UpsertBuilding(AiBuilding building)
        {
            if(!territories.ContainsKey(building.TerritoryId))throw new ArgumentException("Unknown territory.");
            if(buildings.TryGetValue(building.Id,out var previous))
            {
                if(previous.TerritoryId==building.TerritoryId && previous.Type==building.Type)return;
                RemoveBuilding(building.Id);
            }
            buildings.Add(building.Id,building);var t=territories[building.TerritoryId];t.buildings.Add(building.Id,building);
            t.counts[building.Type]=t.BuildingCount(building.Type)+1;Notify(t.Id,AiChange.Buildings);
        }
        public bool RemoveBuilding(int id)
        {
            if(!buildings.TryGetValue(id,out var b))return false;
            buildings.Remove(id);var t=territories[b.TerritoryId];t.buildings.Remove(id);
            int remaining=t.counts[b.Type]-1;if(remaining==0)t.counts.Remove(b.Type);else t.counts[b.Type]=remaining;
            Notify(t.Id,AiChange.Buildings);return true;
        }
        /// <summary>Combat/visibility authority supplies incidents; AI does not infer attacks from colours.</summary>
        public void ReportAttack(int territoryId,int sourceId,double expiresAt)
        {
            if(double.IsNaN(expiresAt)||double.IsInfinity(expiresAt)||expiresAt<0)throw new ArgumentOutOfRangeException(nameof(expiresAt));
            var t=territories[territoryId];var threat=new AiThreat(territoryId,sourceId,expiresAt);
            StoreThreat(t,threat);
            if(t.ParentKingdomId.HasValue)
            {
                StoreThreat(territories[t.ParentKingdomId.Value],threat);
            }
        }
        private void StoreThreat(AiTerritory receiver,AiThreat threat)
        {
            var key=(threat.TerritoryId,threat.SourceId);
            if(receiver.threats.TryGetValue(key,out var old))expiry.Remove((old.ExpiresAt,receiver.Id,old.TerritoryId,old.SourceId));
            receiver.threats[key]=threat;expiry.Add((threat.ExpiresAt,receiver.Id,threat.TerritoryId,threat.SourceId));Notify(receiver.Id,AiChange.Threats);
        }
        public void ClearAttack(int territoryId,int sourceId)
        {
            ClearThreat(territories[territoryId],territoryId,sourceId);
            if(territories[territoryId].ParentKingdomId is int parent)ClearThreat(territories[parent],territoryId,sourceId);
        }
        private void ClearThreat(AiTerritory receiver,int territoryId,int sourceId)
        {
            if(!receiver.threats.TryGetValue((territoryId,sourceId),out var old))return;
            receiver.threats.Remove((territoryId,sourceId));expiry.Remove((old.ExpiresAt,receiver.Id,territoryId,sourceId));Notify(receiver.Id,AiChange.Threats);
        }
        internal void AdvanceTime(double now,int maximumExpiries)
        {
            Now=now;
            while(maximumExpiries-->0 && expiry.Count>0 && expiry.Min.At<=now)
            {
                var e=expiry.Min;expiry.Remove(e);
                if(territories.TryGetValue(e.Receiver,out var t) && t.threats.TryGetValue((e.Target,e.Source),out var threat) && threat.ExpiresAt==e.At)
                {t.threats.Remove((e.Target,e.Source));Notify(t.Id,AiChange.Threats);}
            }
        }
        public void NotifyDiplomacyChanged(AiFaction faction)
        {if(factions.TryGetValue(faction,out var members))foreach(int id in members)Notify(id,AiChange.Diplomacy);}
        public void Notify(int territoryId,AiChange change)
        {if(territories.TryGetValue(territoryId,out var t)){t.Revision++;Changed?.Invoke(territoryId,change);}}
        public void Remove(int id)
        {
            if(!territories.TryGetValue(id,out var t))return;
            DetachParent(t);
            foreach(var threat in t.threats.Values)expiry.Remove((threat.ExpiresAt,id,threat.TerritoryId,threat.SourceId));
            foreach(var b in t.buildings.Values)buildings.Remove(b.Id);
            IndexOwner(t,false);territories.Remove(id);
            t.threats.Clear();t.buildings.Clear();t.counts.Clear();t.Controller=AiController.Unknown;t.World=null;
            foreach(var child in territories.Values)if(child.ParentKingdomId==id){child.ParentKingdomId=null;Notify(child.Id,AiChange.Ownership);}
            Changed?.Invoke(id,AiChange.Removed);
        }
    }
    public interface IAiEconomyView { long Amount(int territoryId,Goods goods); }
    public sealed class AiInventoryView : IAiEconomyView
    {
        private readonly EstateInventory inventory;
        public AiInventoryView(EstateInventory inventory)=>this.inventory=inventory??throw new ArgumentNullException(nameof(inventory));
        public long Amount(int territoryId,Goods goods)=>inventory.Amount(territoryId,goods);
    }
}
