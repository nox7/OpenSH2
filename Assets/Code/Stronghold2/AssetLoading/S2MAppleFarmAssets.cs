using System.Collections.Generic;
using UnityEngine;
namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MAppleFarmAssets
  {
    public const string Walk="apple-farmer-walk",Idle="apple-farmer-idle",Plant="apple-farmer-plant",Pick="apple-farmer-pick",Carry="apple-farmer-carry",CarryIdle="apple-farmer-carry-idle";
    public static readonly string[] TreeModels={"apple_tree_sapling","apple_tree_bare","apple_tree_blossom","apple_tree","apple_tree_apples"};
    public static IEnumerable<S2MActorOverride> Overrides()
    {
      foreach(var clip in new[]{(Walk,"WALK"),(Idle,"IDLE CROSSED"),(Plant,"PLANTING"),(Pick,"PICKING"),(Carry,"CARRY APPLES"),(CarryIdle,"STAND APPLES")})
        yield return new S2MActorOverride{ObjectType=clip.Item1,Variant=clip.Item1,DefinitionPath="meshes/units/farmer_apples.uni",AnimationAction=clip.Item2,LocalEulerAngles=new Vector3(0,180,0)};
    }
  }
}
