using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Stronghold2.AssetLoading;

namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  public sealed class WoodcutterPresentation : LaborPresentation
  {
    private readonly HorseCartAssemblySettings assembly;
    public WoodcutterPresentation(HorseCartAssemblySettings assembly) { this.assembly=assembly; }
    public override LaborRole Role => LaborRole.Woodcutter;
    public override string TravelingVariant => S2MWoodcutterAssets.Walking;
    public override string WorkingVariant => S2MWoodcutterAssets.Riding;
    protected override string TravelingActorType => "JobWoodcutterWalk";
    protected override string WorkingActorType => "JobWoodcutterRide";
    public override IEnumerable<S2MActorOverride> AssetOverrides() => S2MWoodcutterAssets.Overrides(assembly);
    public override IEnumerable<LaborEquipment> Equipment(LaborJob job)
    {
      var woodcutter=(WoodcutterJob)job;
      yield return new LaborEquipment(woodcutter.CartActorId,"JobWoodcutterCart",S2MWoodcutterAssets.Cart,
        woodcutter.CartPosition,woodcutter.CartHeading,woodcutter.RawHeight);
    }
  }
}
