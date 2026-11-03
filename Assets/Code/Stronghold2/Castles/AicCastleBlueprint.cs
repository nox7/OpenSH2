using System;
using System.Collections.Generic;
namespace Assets.Code.Stronghold2.Castles
{
    public enum AicKeepKind { Wooden=20, Small=21, Medium=22, Large=23 }

    /// <summary>Native build order entry. Unknown types and unused bits remain available to future executors.</summary>
    public readonly struct AicBuildItem
    {
        public uint PlacementWord { get; }
        public uint GroupWord { get; }
        public byte Kind=>(byte)(PlacementWord&255);
        public byte LocalX=>(byte)((PlacementWord>>8)&255);
        public byte LocalZ=>(byte)((PlacementWord>>16)&255);
        public byte RotationCode=>(byte)(PlacementWord>>24);
        public ushort Group=>(ushort)(GroupWord&65535);
        public byte Parameter=>(byte)((GroupWord>>16)&255);
        public byte ReservedByte=>(byte)(GroupWord>>24);
        public AicKeepKind? KeepKind=>Kind>=20 && Kind<=23?(AicKeepKind?)Kind:null;
        public AicBuildItem(uint placementWord,uint groupWord){PlacementWord=placementWord;GroupWord=groupWord;}
    }

    /// <summary>Zero denotes an unused point. The original encoded value is always retained.</summary>
    public readonly struct AicPoint
    {
        public int Encoded { get; }
        public bool HasPosition=>Encoded!=0;
        public int X=>Encoded%100;
        public int Z=>Encoded/100;
        public AicPoint(int encoded){Encoded=encoded;}
    }

    /// <summary>Immutable map-load data; contains no rendering objects or mutable AI construction progress.</summary>
    public sealed class AicCastleBlueprint
    {
        public const int ValueCount=50,PointListCount=24,PointsPerList=10,PointGroupCount=10,PointsPerGroup=20;
        public string Key { get; }
        public int ByteLength { get; }
        public IReadOnlyList<AicBuildItem> Items { get; }
        public IReadOnlyList<int> Values { get; }
        public int UnknownValue { get; }
        public IReadOnlyList<AicPoint> ListPoints { get; }
        public IReadOnlyList<AicPoint> GroupPoints { get; }
        public IReadOnlyList<int> ListPointFlags { get; }
        public IReadOnlyList<byte> TrailingBytes { get; }
        public IReadOnlyList<AicBuildItem> Keeps { get; }
        private readonly Dictionary<byte,IReadOnlyList<int>> kindIndices=new();
        public IReadOnlyList<int> ItemsOfKind(byte kind)=>kindIndices.TryGetValue(kind,out var indices)?indices:Array.Empty<int>();
        public AicBuildItem? PrimaryKeep=>Keeps.Count==0?(AicBuildItem?)null:Keeps[Keeps.Count-1];
        internal AicCastleBlueprint(string key,int length,AicBuildItem[] items,int[] values,int unknown,AicPoint[] lists,AicPoint[] groups,int[] flags,byte[] trailing)
        {
            Key=key;ByteLength=length;Items=Array.AsReadOnly(items);Values=Array.AsReadOnly(values);UnknownValue=unknown;
            ListPoints=Array.AsReadOnly(lists);GroupPoints=Array.AsReadOnly(groups);ListPointFlags=Array.AsReadOnly(flags);TrailingBytes=Array.AsReadOnly(trailing);
            var keeps=new List<AicBuildItem>();foreach(var item in items)if(item.KeepKind.HasValue)keeps.Add(item);Keeps=keeps.AsReadOnly();
            var index=new Dictionary<byte,List<int>>();
            for(int i=0;i<items.Length;i++){byte kind=items[i].Kind;if(!index.TryGetValue(kind,out var list))index.Add(kind,list=new List<int>());list.Add(i);}
            foreach(var pair in index)kindIndices.Add(pair.Key,pair.Value.AsReadOnly());
        }
        public AicPoint GetListPoint(int list,int point)
        {if((uint)list>=PointListCount || (uint)point>=PointsPerList)throw new ArgumentOutOfRangeException();return ListPoints[list*PointsPerList+point];}
        public AicPoint GetGroupPoint(int group,int point)
        {if((uint)group>=PointGroupCount || (uint)point>=PointsPerGroup)throw new ArgumentOutOfRangeException();return GroupPoints[group*PointsPerGroup+point];}
    }
}
