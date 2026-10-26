using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MWoodcutterAssets
  {
    public const string Walking = "woodcutter-walking";
    public const string Riding = "woodcutter-riding";
    public const string Cart = "woodcutter-cart";
    public const string WorkerModel = "meshes/units/models/woodcutter.GR2";
    // These rigid exports contain authoring animations that lower/rotate their parts out of assembly space.
    public static S2MActorPart CartBody(string variant=null) => new S2MActorPart {
      ModelPath="meshes/units/animals/cart_horse_cart.GR2",Variant=variant,HoldPose=true,UseBindPose=true,
      LocalOffset=new Vector3(0,0,-.7f),LocalEulerAngles=new Vector3(0,180,0) };
    public static S2MActorPart CartWheels(string variant=null) => new S2MActorPart {
      ModelPath="meshes/units/animals/cart_horse_wheels.GR2",Variant=variant,HoldPose=true,UseBindPose=true,
      LocalOffset=new Vector3(0,1.04f,-.7f),LocalEulerAngles=new Vector3(0,180,0) };
    public static IEnumerable<S2MActorOverride> Overrides(HorseCartAssemblySettings assembly=null)
    {
      assembly??=new HorseCartAssemblySettings();assembly.Validate();
      yield return new S2MActorOverride { ObjectType="JobWoodcutterWalk",ModelPath=WorkerModel,
        AnimationPath=S2MCampfirePopulationLoader.AnimationFolder+"worker_walk.GR2",Variant=Walking,LocalEulerAngles=new Vector3(0,180,0) };
      yield return new S2MActorOverride { ObjectType="JobWoodcutterRide",ModelPath=WorkerModel,
        AnimationPath=S2MCampfirePopulationLoader.AnimationFolder+"woodcutter_riding_cart.GR2",Variant=Riding,
        LocalOffset=new Vector3(0,0,-.7f),LocalEulerAngles=new Vector3(0,180,0) };
      yield return new S2MActorOverride { ObjectType="JobWoodcutterCart",DefinitionPath="meshes/units/animals/cart_horse.aml",
        AnimationPath="meshes/units/animals/cart_horse_grazing.GR2",Variant=Cart,LocalOffset=new Vector3(0,0,assembly.HorseForwardOffset),LocalEulerAngles=new Vector3(0,180,0),
        Parts=new[] {
          CartBody(Cart),
          CartWheels(Cart)
        } };
    }
  }
}
