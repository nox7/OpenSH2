using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Assets.Code.Simulation.AI;
using Assets.Code.Stronghold2.Castles;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading
{
    public enum CastleBlueprintOrigin { LordCandidates,Campaign,Override,RuntimeSelection,Unresolved }
    public sealed class KingdomCastleBlueprints
    {
        public int EstateId { get; }
        public int? Lord { get; }
        public IReadOnlyList<AicCastleBlueprint> Candidates { get; }
        public AicCastleBlueprint Selected { get; }
        public int? RotationQuarterTurns { get; }
        public CastleBlueprintOrigin Origin { get; }
        public string Issue { get; }
        internal KingdomCastleBlueprints(int estate,int? lord,List<AicCastleBlueprint> candidates,AicCastleBlueprint selected,int? rotation,CastleBlueprintOrigin origin,string issue)
        {EstateId=estate;Lord=lord;Candidates=candidates.AsReadOnly();Selected=selected;RotationQuarterTurns=rotation;Origin=origin;Issue=issue;}
    }

    /// <summary>One map lifetime: read files once, share immutable designs, resolve AI kingdoms without scene scans or gameplay I/O.</summary>
    public sealed class S2MCastleBlueprints : IDisposable
    {
        private readonly AiWorld world;
        private readonly S2MLiveryOwnership ownership;
        private readonly Dictionary<string,AicCastleBlueprint> loaded=new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int,KingdomCastleBlueprints> kingdoms=new();
        private readonly List<AiBlueprintFile> files=new();private readonly List<string> issues=new();
        private readonly Dictionary<int,string[]> lordFiles=new();
        private readonly Dictionary<int,(string Key,int? Rotation)> overrides=new();
        private readonly bool useCampaign;
        private bool disposed;
        public int? WarChapter { get; }
        public IReadOnlyDictionary<string,AicCastleBlueprint> Loaded { get; }
        public IReadOnlyDictionary<int,KingdomCastleBlueprints> Kingdoms { get; }
        public IReadOnlyList<AiBlueprintFile> Files { get; }
        public IReadOnlyList<string> Issues { get; }

        public S2MCastleBlueprints(S2MFile map,AiWorld world,string installation,CastleBlueprintSettings settings=null,int? warChapter=null)
        {
            if(map==null || world==null || installation==null)throw new ArgumentNullException();
            settings??=new CastleBlueprintSettings();this.world=world;ownership=S2MLiveryOwnership.Read(map);useCampaign=settings.UseCampaignAssignments;
            WarChapter=ResolveWarChapter(map.SourcePath,installation,warChapter);
            Loaded=new ReadOnlyDictionary<string,AicCastleBlueprint>(loaded);Kingdoms=new ReadOnlyDictionary<int,KingdomCastleBlueprints>(kingdoms);Files=files.AsReadOnly();Issues=issues.AsReadOnly();
            foreach(var entry in settings.LordDesigns??new List<LordCastleDesigns>())
            {
                if(entry==null || entry.Lord<0 || lordFiles.ContainsKey(entry.Lord))throw new ArgumentException("Lord blueprint sets must have distinct nonnegative lord IDs.");
                lordFiles.Add(entry.Lord,(entry.Files??Array.Empty<string>()).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }
            foreach(var entry in settings.KingdomOverrides??new List<KingdomCastleDesignOverride>())
            {
                if(entry==null || entry.EstateId<0 || entry.RotationQuarterTurns< -1 || entry.RotationQuarterTurns>3 || string.IsNullOrWhiteSpace(entry.File))throw new ArgumentException("Invalid kingdom blueprint override.");
                if(!string.IsNullOrEmpty(entry.MapFileName) && !string.Equals(entry.MapFileName,Path.GetFileName(map.SourcePath),StringComparison.OrdinalIgnoreCase))continue;
                if(overrides.ContainsKey(entry.EstateId))throw new ArgumentException("Duplicate kingdom blueprint override for "+entry.EstateId);
                overrides.Add(entry.EstateId,(entry.File,entry.RotationQuarterTurns<0?(int?)null:entry.RotationQuarterTurns));
            }
            LoadFiles(Path.Combine(installation,"castles"));
            foreach(var territory in world.Territories.Values)Refresh(territory.Id);
            world.Changed+=OnWorldChanged;
        }
        private void LoadFiles(string folder)
        {
            if(!Directory.Exists(folder)){issues.Add("Castle blueprint folder not found: "+folder);return;}
            string[] paths;
            try
            {paths=Directory.EnumerateFiles(folder).Where(p=>string.Equals(Path.GetExtension(p),".aic",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p,StringComparer.Ordinal).ToArray();}
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException)
            {issues.Add("Unable to list castle blueprints: "+error.Message);return;}
            foreach(string path in paths)
            {
                string key=Path.GetFileName(path);
                try
                {
                    var file=new FileInfo(path);files.Add(new AiBlueprintFile(key,file.FullName,file.Length));
                    if(loaded.ContainsKey(key)){issues.Add("Duplicate castle blueprint filename: "+key);continue;}
                    loaded.Add(key,AicCastleReader.ReadFile(path));
                }
                catch(Exception error) when(error is IOException || error is InvalidDataException || error is UnauthorizedAccessException)
                {issues.Add(key+": "+error.Message);}
            }
        }
        private int? Lord(AiTerritory territory)
        {
            if(!territory.Owner.HasValue)return null;
            var owner=territory.Owner.Value;
            if(owner.Domain=="scenario-lord")return owner.Number;
            if(owner.Domain=="saved-player" && ownership.Players.TryGetValue(owner.Number,out var player))return player.Lord;
            return null;
        }
        private void OnWorldChanged(int estate,AiChange changes)
        {if((changes&(AiChange.Ownership|AiChange.Removed))!=0)Refresh(estate);}
        private void Refresh(int estate)
        {
            if(!world.TryGet(estate,out var territory)){kingdoms.Remove(estate);return;}
            if(territory.Kind!=AiTerritoryKind.Kingdom || territory.Controller!=AiController.Autonomous)
            {kingdoms.Remove(estate);SetKey(territory,null);return;}
            int? lord=Lord(territory);var candidates=new List<AicCastleBlueprint>();
            if(lord.HasValue && lordFiles.TryGetValue(lord.Value,out var names))foreach(var name in names)
                if(loaded.TryGetValue(name,out var blueprint) && !candidates.Contains(blueprint))candidates.Add(blueprint);
            string requested=null;int? rotation=null;var origin=CastleBlueprintOrigin.LordCandidates;
            if(overrides.TryGetValue(estate,out var configured)){requested=configured.Key;rotation=configured.Rotation;origin=CastleBlueprintOrigin.Override;}
            else if(useCampaign && CampaignDesign(WarChapter,estate,lord,out var campaign,out var turn)){requested=campaign;rotation=turn;origin=CastleBlueprintOrigin.Campaign;}
            AicCastleBlueprint selected=null;string issue=null;
            if(requested!=null)
            {
                if(loaded.TryGetValue(requested,out selected)){if(!candidates.Contains(selected))candidates.Insert(0,selected);}
                else issue="Assigned castle blueprint is missing or invalid: "+requested;
            }
            if(candidates.Count==0 && issue==null)issue=lord.HasValue?"No valid castle blueprints configured for lord "+lord:"Kingdom lord identity is unresolved.";
            if(candidates.Count==0)origin=CastleBlueprintOrigin.Unresolved;
            kingdoms[estate]=new KingdomCastleBlueprints(estate,lord,candidates,selected,rotation,origin,issue);SetKey(territory,selected?.Key);
        }
        private void SetKey(AiTerritory territory,string key)
        {if(!string.Equals(territory.BlueprintKey,key,StringComparison.Ordinal))world.SetBlueprint(territory.Id,key);}
        /// <summary>Future fit/strategy selection can choose an already loaded candidate without disk access.</summary>
        public bool TrySelect(int estate,string key,int? rotationQuarterTurns=null)
        {
            if(disposed)throw new ObjectDisposedException(nameof(S2MCastleBlueprints));
            if(rotationQuarterTurns.HasValue && (rotationQuarterTurns<0 || rotationQuarterTurns>3))throw new ArgumentOutOfRangeException(nameof(rotationQuarterTurns));
            if(key==null || !kingdoms.TryGetValue(estate,out var current) || !loaded.TryGetValue(key,out var selected) || !current.Candidates.Contains(selected))return false;
            kingdoms[estate]=new KingdomCastleBlueprints(estate,current.Lord,current.Candidates.ToList(),selected,rotationQuarterTurns,CastleBlueprintOrigin.RuntimeSelection,null);
            if(world.TryGet(estate,out var territory))SetKey(territory,selected.Key);return true;
        }
        private static int? ResolveWarChapter(string source,string installation,int? explicitChapter)
        {
            if(explicitChapter.HasValue){if(explicitChapter<1 || explicitChapter>12)throw new ArgumentOutOfRangeException(nameof(explicitChapter));return explicitChapter;}
            if(string.IsNullOrEmpty(source))return null;
            string full=Path.GetFullPath(source);
            for(int chapter=1;chapter<=12;chapter++)if(string.Equals(full,Path.GetFullPath(Path.Combine(installation,"maps",$"war_chapter{chapter}.s2m")),StringComparison.OrdinalIgnoreCase))return chapter;
            return null;
        }
        private static bool CampaignDesign(int? chapter,int estate,int? lord,out string key,out int turn)
        {
            key=null;turn=0;
            if(chapter==4 && estate==11 && lord==5)key="AIVChap4_Bull.aic";
            else if(chapter==4 && estate==12 && lord==10)key="AIVChap4_SirGrey.aic";
            else if(chapter==5 && estate==1 && lord==2){key="AIVChap5_Olaf.aic";turn=1;}
            else if(chapter==6 && estate==2 && lord==2)key="AIVChap6_Olaf.aic";
            else if(chapter==6 && estate==3 && lord==7)key="AIVChap6_Edwin.aic";
            return key!=null;
        }
        public void Dispose()
        {if(disposed)return;disposed=true;world.Changed-=OnWorldChanged;kingdoms.Clear();loaded.Clear();}
    }
}
