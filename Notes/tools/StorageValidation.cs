// Copy alongside WoodcuttingValidation into the isolated Unity project's Assets/Editor.
using System;using System.IO;using System.Linq;
using Assets.Code.Simulation.Economy;using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Movement;
using UnityEditor;
public static class StorageValidation
{
  const string Output="C:/Users/garet/Documents/GitHub/OpenSH2/Temp/StorageValidation";
  static void Check(bool value,string text){if(!value)throw new Exception(text);}
  public static void Run(){try{Core();IncrementalDeliveryValidation.Map();EditorApplication.Exit(0);}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());EditorApplication.Exit(1);}}
  public static void Core()
  {
    Directory.CreateDirectory(Output);File.Delete(Output+"/failure.txt");File.Delete(Output+"/core-pass.txt");
    var inventory=new EstateInventory();var catalog=inventory.Catalog;
    Check(catalog.All.Count==29 && (int)Goods.Eels==13 && (int)Goods.Bows==29,"GoodsList indices or unknown filtering changed");
    Check(catalog[Goods.Vegetables].Storage==StorageKind.Pantry && catalog[Goods.Cheese].Storage==StorageKind.Granary && catalog[Goods.LeatherArmor].Storage==StorageKind.Armory,"Goods storage classification failed");
    foreach(var g in catalog.All.Where(g=>g.Storage==StorageKind.Stockpile))Check(g.PalletCapacity>0,"Missing pallet limit");
    Check(catalog[Goods.Wood].PalletCapacity==36 && catalog[Goods.Stone].PalletCapacity==96,"Native pallet capacities changed");
    bool invalid=false;try{_=catalog[(Goods)12];}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"Unknown goods accepted");
    var a=inventory.Register(1,1,StorageKind.Stockpile,2);var b=inventory.Register(2,1,StorageKind.Stockpile,1);var other=inventory.Register(3,2,StorageKind.Stockpile,1);
    int changes=0;inventory.Changed+=s=>{changes++;Check(inventory.Estate(s.EstateId)[Goods.Wood]==inventory.Estate(s.EstateId).Buildings.Sum(t=>t.Amount(Goods.Wood)),"Event saw stale estate totals");};
    Check(inventory.TryDeposit(1,1,Goods.Wood,16) && inventory.TryDeposit(1,1,Goods.Wood,56),"Partial pallets did not pack");
    Check(a.GetPallet(0).Amount==36 && a.GetPallet(1).Amount==36 && inventory.Estate(1)[Goods.Wood]==72,"Pallet contents/totals diverged");
    int before=changes;Check(!inventory.TryDeposit(1,1,Goods.Wood,1) && changes==before && inventory.Amount(1,Goods.Wood)==72,"Over-capacity deposit was partial");
    Check(!inventory.TryDeposit(3,1,Goods.Wood,16) && inventory.Amount(2,Goods.Wood)==0,"Cross-estate worker deposit accepted");
    Check(inventory.TryWithdraw(1,Goods.Wood,40) && !a.GetPallet(1).Goods.HasValue && a.GetPallet(0).Amount==32,"Withdrawal did not release empty pallet");
    Check(inventory.TryDeposit(1,1,Goods.Stone,96) && !inventory.TryDeposit(1,1,Goods.Iron,1),"Pallet mixed resource types");
    Check(inventory.TryTransfer(1,2,Goods.Wood,16) && inventory.Amount(1,Goods.Wood)==32 && b.Amount(Goods.Wood)==16,"Within-estate transfer changed totals");
    Check(inventory.TryTransfer(2,3,Goods.Wood,16) && inventory.Amount(1,Goods.Wood)==16 && inventory.Amount(2,Goods.Wood)==16,"Explicit cross-estate transfer lost goods");
    Check(!inventory.TryRemoveEmptyBuilding(1) && inventory.TryRemoveEmptyBuilding(2),"Store removal destroyed goods");
    inventory.Register(4,1,StorageKind.Pantry);inventory.Register(5,1,StorageKind.Granary);inventory.Register(6,1,StorageKind.Armory);
    Check(inventory.TryDeposit(4,1,Goods.Vegetables,11) && inventory.TryDeposit(5,1,Goods.Apples,22) && inventory.TryDeposit(6,1,Goods.Bows,33),"Store-specific bins failed");
    Check(!inventory.TryDeposit(5,1,Goods.Wood,1) && inventory.Estate(1)[Goods.Vegetables]==11 && inventory.Estate(1)[Goods.Bows]==33,"Wrong building accepted goods or estate query failed");
    var custom=new EstateInventory(new StorageSettings{StockpilePallets=1,PalletCapacities=new[]{new PalletCapacityOverride{Goods=Goods.Wood,Capacity=20}}});
    Check(custom.Register(1,1,StorageKind.Stockpile).PalletCount==1 && custom.TryDeposit(1,1,Goods.Wood,20) && !custom.TryDeposit(1,1,Goods.Wood,1),"Capacity overrides ignored");
    for(int i=0;i<1000;i++){inventory.Register(100+i,10+i%4,StorageKind.Stockpile,1);Check(inventory.TryDeposit(100+i,10+i%4,Goods.Wood,16),"Scaled deposit failed");}
    for(int e=10;e<14;e++)Check(inventory.Amount(e,Goods.Wood)==4000,"Estate totals failed at scale");
    Deliveries();File.WriteAllText(Output+"/core-pass.txt","PASS: 29 goods; exact storage responsibilities; atomic capacity/mixing/withdrawal/transfer; cached estate totals across 1000 stores; incremental competing arrivals and partial-cargo retry; automatic cycles and exactly-once plank commits.");
  }
  static void Deliveries()=>IncrementalDeliveryValidation.Core();
}
