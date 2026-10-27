using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MWoodcutterAssets
  {
    public const string Walking = "woodcutter-walking";
    public const string Riding = "woodcutter-riding";
    public const string Cart = "woodcutter-cart";
    public const string MovingCart = "woodcutter-cart-moving";
    public const string LoadedCart = "woodcutter-cart-loaded";
    public const string LoadedIdleCart = "woodcutter-cart-loaded-idle";
    public const string Felling = "woodcutter-felling";
    public const string SawLog = "woodcutter-saw-log";
    public const string Sawing = "woodcutter-sawing";
    public const string PlankCart = "woodcutter-cart-planks";
    public const string PlankIdleCart = "woodcutter-cart-planks-idle";
    public const string Chopping = "woodcutter-chopping";
    public const string WorkerModel = "meshes/units/models/woodcutter.GR2";
    // These rigid exports contain authoring animations that lower/rotate their parts out of assembly space.
    public static S2MActorPart CartBody(string variant=null) => new S2MActorPart {
      ModelPath="meshes/units/animals/cart_horse_cart.GR2",Variant=variant,HoldPose=true,UseBindPose=true,
      LocalOffset=new Vector3(0,0,-.7f),LocalEulerAngles=new Vector3(0,180,0) };
    public static S2MActorPart CartWheels(string variant=null) => new S2MActorPart {
      ModelPath="meshes/units/animals/cart_horse_wheels.GR2",Variant=variant,HoldPose=true,UseBindPose=true,
      SpinAxis=Vector3.left,
      LocalOffset=new Vector3(0,1.04f,-.7f),LocalEulerAngles=new Vector3(0,180,0) };
    public static IEnumerable<S2MActorOverride> Overrides(HorseCartAssemblySettings assembly=null)
    {
      assembly??=new HorseCartAssemblySettings();assembly.Validate();
      yield return new S2MActorOverride { ObjectType="JobWoodcutterWalk",DefinitionPath="meshes/units/woodcutter.uni",
        AnimationAction="WALK SHOULDER",Variant=Walking,LocalEulerAngles=new Vector3(0,180,0) };
      yield return new S2MActorOverride { ObjectType="JobWoodcutterRide",DefinitionPath="meshes/units/woodcutter.uni",
        AnimationPath=S2MCampfirePopulationLoader.AnimationFolder+"woodcutter_riding_cart.GR2",Variant=Riding,
        LocalOffset=new Vector3(0,0,-.7f),LocalEulerAngles=new Vector3(0,180,0) };
      yield return new S2MActorOverride { ObjectType="JobWoodcutterFell",DefinitionPath="meshes/units/woodcutter.uni",
        AnimationAction="FELL",Variant=Felling,LocalEulerAngles=new Vector3(0,180,0) };
      yield return new S2MActorOverride { ObjectType="JobWoodcutterChop",DefinitionPath="meshes/units/woodcutter.uni",
        AnimationAction="CHOP",Variant=Chopping,LocalEulerAngles=new Vector3(0,180,0) };
      yield return new S2MActorOverride { ObjectType=SawLog,ModelPath="meshes/buildings/saw_pit_wood.GR2",
        Variant=SawLog,HoldPose=true,UseBindPose=true,TeamGroup=-1 };
      yield return new S2MActorOverride { ObjectType="JobWoodcutterSaw",DefinitionPath="meshes/units/woodcutter.uni",
        AnimationAction="SAW TOP",Variant=Sawing,LocalOffset=assembly.SawingSeatOffset,LocalEulerAngles=assembly.SawingSeatEulerAngles };
      yield return new S2MActorOverride { ObjectType="JobWoodcutterCart",DefinitionPath="meshes/units/animals/cart_horse.aml",
        AnimationPath="meshes/units/animals/cart_horse_grazing.GR2",Variant=Cart,LocalOffset=new Vector3(0,0,assembly.HorseForwardOffset),LocalEulerAngles=new Vector3(0,180,0),
        Parts=new[] {
          CartBody(Cart),
          CartWheels(Cart)
        } };
      foreach(var mode in new[]{(MovingCart,false,true),(LoadedCart,true,true),(LoadedIdleCart,true,false),(PlankCart,true,true),(PlankIdleCart,true,false)})
      {
        var parts=new List<S2MActorPart>{CartBody(mode.Item1),CartWheels(mode.Item1)};
        if(mode.Item2)parts.Add(new S2MActorPart{ModelPath="meshes/units/animals/"+(mode.Item1==PlankCart || mode.Item1==PlankIdleCart?"cart_horse_planks.GR2":"cart_horse_wood_1.GR2"),Variant=mode.Item1,
          HoldPose=true,UseBindPose=true,LocalOffset=assembly.LogLoadOffset,LocalEulerAngles=new Vector3(0,180,0)});
        yield return new S2MActorOverride{ObjectType=mode.Item1,DefinitionPath="meshes/units/animals/cart_horse.aml",
          AnimationPath="meshes/units/animals/cart_horse_"+(mode.Item3?"walk":"grazing")+".GR2",Variant=mode.Item1,
          LocalOffset=new Vector3(0,0,assembly.HorseForwardOffset),LocalEulerAngles=new Vector3(0,180,0),Parts=parts.ToArray()};
      }
    }
  }
}
