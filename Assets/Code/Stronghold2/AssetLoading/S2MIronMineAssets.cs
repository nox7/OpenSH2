using System.Collections.Generic;using UnityEngine;
namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MIronMineAssets
  {
    public const string Walk="iron-miner-walk",Climb="iron-miner-climb",Lever="iron-miner-lever",Carry="iron-miner-carry",Return="iron-miner-return";
    public const string Lift="iron-mine-lift",Rock="iron-mine-rock",Molten="iron-mine-molten";
    private static readonly string[] piles={"iron-mine-pile-1","iron-mine-pile-2","iron-mine-pile-3","iron-mine-pile-4","iron-mine-pile-5","iron-mine-pile-6","iron-mine-pile-7","iron-mine-pile-8","iron-mine-pile-9","iron-mine-pile-10"};
    public static string Pile(int count)=>piles[count-1];
    public static IEnumerable<S2MActorOverride> Overrides()
    {
      yield return new S2MActorOverride{ObjectType=Walk,Variant=Walk,DefinitionPath="meshes/units/iron_miner.uni",AnimationAction="WALK",LocalEulerAngles=new Vector3(0,180,0)};
      foreach(var entry in new[]{(Climb,"CLIMBING"),(Lever,"TURNIN LEVER"),(Carry,"MOVING IRON"),(Return,"WALK BACK")})
        yield return new S2MActorOverride{ObjectType=entry.Item1,Variant=entry.Item1,DefinitionPath="meshes/units/iron_miner.uni",AnimationAction=entry.Item2};
      foreach(var part in new[]{(Lift,"lift"),(Rock,"rock"),(Molten,"molten")})
        yield return new S2MActorOverride{ObjectType=part.Item1,Variant=part.Item1,ModelPath="meshes/buildings/iron_mine_parts_"+part.Item2+".GR2"};
      for(int i=1;i<=10;i++)yield return new S2MActorOverride{ObjectType=Pile(i),Variant=Pile(i),ModelPath=$"meshes/buildings/stockpile/iron_{i}.GR2",HoldPose=true,UseBindPose=true,TeamGroup=-1};
    }
  }
}
