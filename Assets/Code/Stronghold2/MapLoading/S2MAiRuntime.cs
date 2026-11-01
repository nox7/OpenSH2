using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Simulation.Economy;
using Assets.Code.Simulation.Labor;
using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading
{
    public readonly struct AiBlueprintFile
    {
        public readonly string Key,Path;
        public readonly long Bytes;
        public AiBlueprintFile(string key,string path,long bytes){Key=key;Path=path;Bytes=bytes;}
    }
    /// <summary>Catalog only: AIC layout decoding and placement are intentionally separate future work.</summary>
    public sealed class S2MAiRuntime : IDisposable
    {
        public AiWorld World { get; }
        public AiScheduler Scheduler { get; }
        public IReadOnlyList<AiBlueprintFile> Blueprints { get; }
        private readonly EstateInventory inventory;
        private readonly LaborManager labor;
        private bool disposed;
        private S2MAiRuntime(AiWorld world,EstateInventory inventory,List<AiBlueprintFile> blueprints,AiSettings settings,double initialTime,LaborManager labor)
        {
            World=world;this.inventory=inventory;Blueprints=blueprints.AsReadOnly();
            // No planner is installed: indexing, scheduling and notifications only.
            Scheduler=new AiScheduler(world,new AiInventoryView(inventory),settings,initialTime:initialTime);
            inventory.Changed+=OnStorageChanged;
            this.labor=labor;
            if(labor!=null){foreach(int id in world.Territories.Keys)world.SetLaborDemand(id,labor.GetLaborDemand(id));labor.StaffingChanged+=OnStaffingChanged;}
        }
        public static S2MAiRuntime Create(S2MFile map, EstateInventory inventory,string installation,AiSettings settings=null,double initialTime=0,LaborManager labor=null)
        {
            if(map==null || inventory==null)throw new ArgumentNullException();
            var world=new AiWorld();var ownership=S2MLiveryOwnership.Read(map);
            var kinds=new Dictionary<int,AiTerritoryKind>();
            if(map.RadarEstateLayer?.EstateIds!=null)foreach(byte id in map.RadarEstateLayer.EstateIds)
                if(id>0 && id<=31)kinds[id]=AiTerritoryKind.Unclassified;
            foreach(var marker in map.EstateMarkers)
                if(marker.EstateId>0 && marker.Type!=EstateType.None)kinds[marker.EstateId]=marker.Type==EstateType.Castle?AiTerritoryKind.Kingdom:AiTerritoryKind.Estate;
            foreach(var keep in map.Keeps)
            {int id=S2MCampfireLayout.EstateAtBuilding(map,keep);if(id>0)kinds[id]=AiTerritoryKind.Kingdom;}
            var profiles=ReadProfiles(map);
            foreach(var pair in kinds.OrderBy(p=>p.Key))
            {
                AiFaction? faction=null;bool human=false,known=false;
                if(ownership.Estates.TryGetValue(pair.Key,out var owner))
                {
                    if(owner.PlayerNumber>=0)
                    {
                        faction=new AiFaction("saved-player",owner.PlayerNumber);
                        known=ownership.Players.TryGetValue(owner.PlayerNumber,out var player);human=known && player.LocalHuman;
                    }
                    else if(owner.Lord>0)
                    {
                        faction=new AiFaction("scenario-lord",owner.Lord);known=true;
                        // Existing ownership adapter's initial-map convention: lord 1 is the human.
                        human=owner.Lord==1;
                    }
                }
                var control=pair.Value==AiTerritoryKind.Estate?AiController.Autonomous:
                    known?(human?AiController.Human:AiController.Autonomous):AiController.Unknown;
                profiles.TryGetValue(pair.Key,out var profile);
                world.Add(new AiTerritory(pair.Key,pair.Value,control,faction,profile.Production,profile.Policy));
            }
            // A shared owner can have multiple kingdoms. Never guess a parent in that case.
            foreach(var t in world.Territories.Values)
                if(t.Kind==AiTerritoryKind.Estate && t.Owner.HasValue)
                {
                    var parents=world.Territories.Values.Where(k=>k.Kind==AiTerritoryKind.Kingdom && k.Owner.Equals(t.Owner)).ToArray();
                    if(parents.Length==1)world.SetParentKingdom(t.Id,parents[0].Id);
                }
            foreach(var b in map.BuildingPlacements)
            {int id=S2MCampfireLayout.EstateAtBuilding(map,b);if(world.TryGet(id,out _))world.UpsertBuilding(new AiBuilding(b.Id,id,b.Type));}
            var blueprints=new List<AiBlueprintFile>();
            string folder=System.IO.Path.Combine(installation,"castles");
            if(Directory.Exists(folder))foreach(string path in Directory.EnumerateFiles(folder,"*.aic").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
            {var file=new FileInfo(path);blueprints.Add(new AiBlueprintFile(file.Name,file.FullName,file.Length));}
            return new S2MAiRuntime(world,inventory,blueprints,settings,initialTime,labor);
        }
        private void OnStorageChanged(BuildingStorage storage)=>World.Notify(storage.EstateId,AiChange.Resources);
        private void OnStaffingChanged(int estate,LaborDemand demand)=>World.SetLaborDemand(estate,demand);
        public void Dispose()
        {if(disposed)return;disposed=true;inventory.Changed-=OnStorageChanged;if(labor!=null)labor.StaffingChanged-=OnStaffingChanged;Scheduler.Dispose();}

        // Modern Scenario fields are counted arrays indexed by estate, not the legacy
        // reader's fixed 16-element convenience arrays. Keep native production codes intact.
        internal static Dictionary<int,(int? Production,AiConstructionPolicy Policy)> ReadProfiles(S2MFile map)
        {
            var result=new Dictionary<int,(int?,AiConstructionPolicy)>();
            foreach(var obj in map.MapHeaderObjects)
            {
                byte[] bytes=obj is Scenario s?s.RawPayload:(obj as RawS2Object)?.Payload;
                if(obj.Type!="Scenario" || bytes==null || bytes.Length<4)continue;
                int first=BitConverter.ToInt32(bytes,0);
                if(first<0 || first>bytes.Length-4)continue;
                int offset=first+4;
                while(offset<=bytes.Length-8)
                {
                    int field=BitConverter.ToInt32(bytes,offset),length=BitConverter.ToInt32(bytes,offset+4);offset+=8;
                    if(length<0 || length>bytes.Length-offset)break;
                    if(field==4 && length%4==0)for(int id=0;id<Math.Min(32,length/4);id++)
                    {
                        result.TryGetValue(id,out var previous);int code=BitConverter.ToInt32(bytes,offset+id*4);
                        result[id]=(code>=0 && code<=19?(int?)code:null,previous.Item2);
                    }
                    if(field==5)for(int id=0;id<Math.Min(32,length);id++)
                    {
                        result.TryGetValue(id,out var previous);byte code=bytes[offset+id];
                        result[id]=(previous.Item1,code==0?AiConstructionPolicy.Allowed:code==1?AiConstructionPolicy.UseMapBuildings:AiConstructionPolicy.Unknown);
                    }
                    offset+=length;
                }
            }
            return result;
        }
    }
}
