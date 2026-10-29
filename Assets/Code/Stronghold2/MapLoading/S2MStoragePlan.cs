using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.MapLoading
{
  public static class S2MStoragePlan
  {
    public static EstateInventory Create(S2MFile map,StorageSettings settings=null,StockpileDeliverySettings deliverySettings=null)
    {
      settings??=new StorageSettings();if(settings.StockpilePallets<1 || settings.StockpilePallets>30)throw new System.ArgumentOutOfRangeException(nameof(settings.StockpilePallets));var inventory=new EstateInventory(settings,deliverySettings);
      if(map.RadarEstateLayer?.EstateIds!=null)foreach(var id in map.RadarEstateLayer.EstateIds)if(id>0 && id<=31)inventory.Estate(id);
      foreach(var building in map.BuildingPlacements)
      {
        StorageKind? kind=building.Type switch {"StockPile"=>StorageKind.Stockpile,"Granary"=>StorageKind.Granary,
          "Armoury"=>StorageKind.Armory,"ServantsQuarters"=>StorageKind.Pantry,_=>null};
        int estate=S2MCampfireLayout.EstateAtBuilding(map,building);
        if(kind.HasValue && estate>=0)inventory.Register(building.Id,estate,kind.Value,settings.StockpilePallets);
      }
      return inventory;
    }
  }
}
