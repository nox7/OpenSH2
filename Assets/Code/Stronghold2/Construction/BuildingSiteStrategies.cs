using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.Castles;
namespace Assets.Code.Stronghold2.Construction
{
    public enum BuildingSiteStep { Candidate,Skipped,Complete }
    public readonly struct BuildingSiteContext
    {
        public readonly int EstateId,Rotation;
        public readonly MapPoint Origin;
        public readonly S2MBuildingDefinition Definition;
        public BuildingSiteContext(int estate,S2MBuildingDefinition definition,MapPoint origin,int rotation)
        {EstateId=estate;Definition=definition;Origin=origin;Rotation=rotation;}
    }
    public readonly struct BuildingSiteCandidate
    {
        public readonly int X,Z,Rotation;
        public readonly string BlueprintKey;
        public readonly int? BlueprintItemIndex;
        public BuildingSiteCandidate(int x,int z,int rotation,string blueprintKey=null,int? itemIndex=null)
        {X=x;Z=z;Rotation=rotation;BlueprintKey=blueprintKey;BlueprintItemIndex=itemIndex;}
    }
    /// <summary>Each Step is budgeted, including skipped/occupied blueprint slots. Selection never spends resources.</summary>
    public interface IBuildingSiteSearch : IDisposable { BuildingSiteStep Step(out BuildingSiteCandidate candidate); }
    public interface IBuildingSiteStrategy { IBuildingSiteSearch Begin(in BuildingSiteContext context); }

    public sealed class GeneralBuildingSiteStrategy : IBuildingSiteStrategy
    {
        private readonly IReadOnlyList<(int X,int Z)> offsets;
        public GeneralBuildingSiteStrategy(IReadOnlyList<(int X,int Z)> offsets){this.offsets=offsets??throw new ArgumentNullException(nameof(offsets));}
        public IBuildingSiteSearch Begin(in BuildingSiteContext context)=>new Search(context,offsets);
        private sealed class Search : IBuildingSiteSearch
        {
            private readonly BuildingSiteContext context;private readonly IReadOnlyList<(int X,int Z)> offsets;private int cursor;
            public Search(BuildingSiteContext context,IReadOnlyList<(int X,int Z)> offsets){this.context=context;this.offsets=offsets;}
            public BuildingSiteStep Step(out BuildingSiteCandidate candidate)
            {
                candidate=default;if(cursor==offsets.Count)return BuildingSiteStep.Complete;
                var p=offsets[cursor++];candidate=new BuildingSiteCandidate((int)Math.Round(context.Origin.X)+p.X,(int)Math.Round(context.Origin.Z)+p.Z,context.Rotation);return BuildingSiteStep.Candidate;
            }
            public void Dispose(){}
        }
    }
    public sealed class BlueprintBuildingSiteStrategy : IBuildingSiteStrategy
    {
        private readonly AicCastleLayout layout;private readonly byte kind;private readonly Func<int,bool> available;
        public BlueprintBuildingSiteStrategy(AicCastleLayout layout,byte nativeKind,Func<int,bool> slotAvailable=null)
        {this.layout=layout??throw new ArgumentNullException(nameof(layout));kind=nativeKind;available=slotAvailable;}
        public IBuildingSiteSearch Begin(in BuildingSiteContext context)=>new Search(context,layout,kind,available);
        private sealed class Search : IBuildingSiteSearch
        {
            private readonly BuildingSiteContext context;private readonly AicCastleLayout layout;
            private readonly IReadOnlyList<int> indices;private readonly Func<int,bool> available;private int cursor;
            public Search(BuildingSiteContext context,AicCastleLayout layout,byte kind,Func<int,bool> available)
            {this.context=context;this.layout=layout;indices=layout.Blueprint.ItemsOfKind(kind);this.available=available;}
            public BuildingSiteStep Step(out BuildingSiteCandidate candidate)
            {
                candidate=default;if(cursor==indices.Count)return BuildingSiteStep.Complete;int index=indices[cursor++];
                var item=layout.Blueprint.Items[index];if(item.RotationCode>3 || available!=null && !available(index))return BuildingSiteStep.Skipped;
                var site=layout.Project(item,context.Definition.Descriptor.Width,context.Definition.Descriptor.Height);
                candidate=new BuildingSiteCandidate(site.ModelX,site.ModelZ,site.RotationQuarterTurns,layout.Blueprint.Key,index);return BuildingSiteStep.Candidate;
            }
            public void Dispose(){}
        }
    }
    /// <summary>Try preferred sites, then alternate placement. The caller may replace this policy on any goal at runtime.</summary>
    public sealed class FallbackBuildingSiteStrategy : IBuildingSiteStrategy
    {
        private readonly IBuildingSiteStrategy preferred,fallback;
        public FallbackBuildingSiteStrategy(IBuildingSiteStrategy preferred,IBuildingSiteStrategy fallback)
        {this.preferred=preferred??throw new ArgumentNullException(nameof(preferred));this.fallback=fallback??throw new ArgumentNullException(nameof(fallback));}
        public IBuildingSiteSearch Begin(in BuildingSiteContext context)=>new Search(preferred.Begin(context),fallback,context);
        private sealed class Search : IBuildingSiteSearch
        {
            private IBuildingSiteSearch current;private IBuildingSiteStrategy fallback;private readonly BuildingSiteContext context;
            public Search(IBuildingSiteSearch preferred,IBuildingSiteStrategy fallback,BuildingSiteContext context){current=preferred;this.fallback=fallback;this.context=context;}
            public BuildingSiteStep Step(out BuildingSiteCandidate candidate)
            {
                var result=current.Step(out candidate);if(result!=BuildingSiteStep.Complete || fallback==null)return result;
                current.Dispose();current=fallback.Begin(context);fallback=null;return BuildingSiteStep.Skipped;
            }
            public void Dispose()=>current.Dispose();
        }
    }
}
