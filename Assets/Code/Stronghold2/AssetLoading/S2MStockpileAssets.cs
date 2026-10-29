using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Economy;
namespace Assets.Code.Stronghold2.AssetLoading
{
  [Serializable] public sealed class StockpileGoodsVisual
  {
    public Goods Goods;public string ModelPath;
    public int Columns=3,Rows=3;public float Scale=1;
  }
  [Serializable] public sealed class S2MStockpileVisualSettings
  {
    public bool Enabled=true;
    public StockpileGoodsVisual[] Overrides=Array.Empty<StockpileGoodsVisual>();
  }
  public sealed class S2MStockpileAssets
  {
    public const string Folder="meshes/buildings/stockpile/";
    public const string PalletPath=Folder+"palette.GR2";
    public readonly Dictionary<string,Granny2ModelData> Models=new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<Goods,StockpileGoodsVisual> Visuals=new();
    public readonly HashSet<Goods> Custom=new();
    public static S2MStockpileAssets Load(string install,string converter=null,S2MStockpileVisualSettings settings=null)
    {
      var result=new S2MStockpileAssets();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase){PalletPath};
      void Visual(Goods goods,string file,int columns,int rows)
      {result.Visuals.Add(goods,new StockpileGoodsVisual{Goods=goods,ModelPath=Folder+file+".GR2",Columns=columns,Rows=rows});}
      Visual(Goods.Wood,"wood_1",3,2);Visual(Goods.Stone,"stone_1",4,4);Visual(Goods.Iron,"iron_1",4,3);
      Visual(Goods.Wheat,"wheat_1",4,4);Visual(Goods.Flour,"flour_1",4,2);Visual(Goods.Hops,"hopsack_1",3,3);
      Visual(Goods.Ale,"barrel_1",3,3);Visual(Goods.Grapes,"grapes_1",3,2);Visual(Goods.Pitch,"oil_jug_01",4,2);
      Visual(Goods.Candles,"candles_1",2,2);Visual(Goods.Wool,"woolsack_1",2,2);Visual(Goods.Cloth,"cloth_1",5,5);
      if(settings?.Overrides!=null)foreach(var visual in settings.Overrides)
      {
        if(visual==null)continue;
        if(!result.Visuals.ContainsKey(visual.Goods) || visual.Columns<1 || visual.Rows<1 || !(visual.Scale>0) || float.IsInfinity(visual.Scale)
          || string.IsNullOrWhiteSpace(visual.ModelPath))throw new ArgumentException("Invalid stockpile visual override.");
        result.Visuals[visual.Goods]=new StockpileGoodsVisual{Goods=visual.Goods,ModelPath=visual.ModelPath,Columns=visual.Columns,Rows=visual.Rows,Scale=visual.Scale};result.Custom.Add(visual.Goods);
      }
      foreach(var visual in result.Visuals.Values)paths.Add(visual.ModelPath);
      void Series(string stem,int count){for(int i=1;i<=count;i++)paths.Add(Folder+stem+i+".GR2");}
      Series("wood_",5);Series("wood_layer_",6);Series("iron_",11);Series("iron_layer_",3);Series("cloth_",25);
      foreach(var pair in Granny2AssetConverter.LoadOrConvertMany(install,paths,converter))
      {if(pair.Value.Error!=null)throw new InvalidOperationException("Stockpile asset "+pair.Key,pair.Value.Error);result.Models.Add(pair.Key,pair.Value.Model);}
      return result;
    }
  }
}
