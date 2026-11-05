using System.Collections.Generic;
using UnityEngine;
namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MPitchRigAssets
  {
    public const string Walk="pitch-worker-walk",Idle="pitch-worker-idle",Pump="pitch-worker-pump",
      Carry="pitch-worker-carry",CarryIdle="pitch-worker-carry-idle",Scoop="pitch-rig-scoop";
    public static IEnumerable<S2MActorOverride> Overrides()
    {
      foreach(var clip in new[]{(Walk,"WALK"),(Idle,"IDLE CROSSED"),(Pump,"PITCH SCOOP"),(Carry,"CARRY PITCH FUL"),(CarryIdle,"STAND PITCH")})
        yield return new S2MActorOverride{ObjectType=clip.Item1,Variant=clip.Item1,DefinitionPath="meshes/units/pitch_worker.uni",
          AnimationAction=clip.Item2,LocalEulerAngles=clip.Item1==Pump?Vector3.zero:new Vector3(0,180,0)};
      yield return new S2MActorOverride{ObjectType=Scoop,Variant=Scoop,ModelPath="meshes/buildings/pitch_rig_scooping.GR2"};
    }
  }
}
