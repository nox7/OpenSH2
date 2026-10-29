using System.Collections.Generic;using UnityEngine;
namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MQuarryAssets
  {
    // Finished blocks are smaller than the worked rock; this scale fits the front plank platform.
    public const float FinishedStoneScale=.75f;
    // Native block edges are authored about 28.7 degrees from the platform axes.
    public const float FinishedStoneYaw=28.72f;
    public const string Walk="quarry-walk",Idle="quarry-idle",Hammer="quarry-hammer",Lift="quarry-lift",Hook="quarry-hook",Rig="quarry-rig",Rock="quarry-rock",Stone="quarry-stone";
    private static readonly string[] RockPoses={"quarry-rock-pose-0","quarry-rock-pose-1","quarry-rock-pose-2","quarry-rock-pose-3","quarry-rock-pose-4"};
    public static string RockPose(int stage)=>RockPoses[stage];
    public static IEnumerable<S2MActorOverride> Overrides()
    {
      foreach(var entry in new[]{("JobQuarryWalk",Walk,"WALK"),("JobQuarryIdle",Idle,"IDLE LOOK"),("JobQuarryHammer",Hammer,"HAMMERING"),("JobQuarryLift",Lift,"LIFTING"),("JobQuarryHook",Hook,"HOOK")})
        yield return new S2MActorOverride{ObjectType=entry.Item1,DefinitionPath="meshes/units/stone_mason.uni",AnimationAction=entry.Item3,Variant=entry.Item2,
          LocalEulerAngles=entry.Item2==Walk?new Vector3(0,180,0):Vector3.zero};
      for(int stage=0;stage<5;stage++)
        yield return new S2MActorOverride{ObjectType=RockPose(stage),Variant=RockPose(stage),
          ModelPath=$"meshes/buildings/quarry_rock_pose_{stage:00}.GR2",AnimationPath=$"meshes/buildings/quarry_rock_pose_{stage:00}.GR2",
          HoldPose=true,UseBindPose=true,TeamGroup=-1};
      foreach(var entry in new[]{(Rig,"quarry_parts.GR2"),(Rock,"quarry_parts_rock.GR2"),(Stone,"quarry_stone.GR2")})
        yield return new S2MActorOverride{ObjectType=entry.Item1,ModelPath="meshes/buildings/"+entry.Item2,AnimationPath="meshes/buildings/"+entry.Item2,
          Variant=entry.Item1,SpinAxis=entry.Item1==Stone ? Vector3.up : Vector3.zero,Scale=entry.Item1==Stone ? FinishedStoneScale : 1f,HoldPose=entry.Item1==Stone,UseBindPose=entry.Item1==Stone,TeamGroup=-1};
    }
  }
}
