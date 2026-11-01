using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.Construction
{
    public readonly struct BuildingCost
    {public readonly Goods Goods;public readonly int Amount;public BuildingCost(Goods goods,int amount){if(amount<0)throw new ArgumentOutOfRangeException(nameof(amount));Goods=goods;Amount=amount;}}
    /// <summary>Loaded native dimensions and simulation rules, independent of the player/AI placement strategy.</summary>
    public sealed class S2MBuildingDefinition
    {
        public string Type { get; }
        public S2MBuildingDescriptor Descriptor { get; }
        public IReadOnlyList<BuildingCost> Costs { get; }
        public StorageKind? Storage { get; }
        public S2MBuildingDefinition(string type,S2MBuildingDescriptor descriptor,IReadOnlyList<BuildingCost> costs,StorageKind? storage=null)
        {Type=type;Descriptor=descriptor;Costs=new List<BuildingCost>(costs).AsReadOnly();Storage=storage;}
        public static S2MBuildingDefinition Load(string installation,string type)
        {
            if(type!="StockPile" && type!="Granary" && type!="Hovel" && type!="SawPit")throw new NotSupportedException("Register explicit construction rules for "+type+" before placing it.");
            if(!S2MBuildingAssetCatalog.TryGetDescriptor(new BuildingPlacement{Type=type,HovelAppearance=0},null,out var path,out var error))throw new ArgumentException(error);
            return new S2MBuildingDefinition(type,S2MBuildingDescriptorReader.Read(installation,path),
                type=="Hovel"?new[]{new BuildingCost(Goods.Wood,6)}:type=="SawPit"?new[]{new BuildingCost(Goods.Wood,3)}:Array.Empty<BuildingCost>(),
                type=="StockPile"?StorageKind.Stockpile:type=="Granary"?StorageKind.Granary:(StorageKind?)null);
        }
        public BuildingPlacement Propose(int territory,int x,int z,int quarterTurns=0)
        {
            int turn=(quarterTurns%4+4)%4;
            return new BuildingPlacement{Type=Type,HovelAppearance=Type=="Hovel"?0:(int?)null,RuntimeEstateId=territory,
                RawX=x*1024f,RawZ=z*1024f,RotationDegrees=turn*90,
                FootprintWidth=turn%2==0?Descriptor.Width:Descriptor.Height,FootprintHeight=turn%2==0?Descriptor.Height:Descriptor.Width};
        }
    }
}
