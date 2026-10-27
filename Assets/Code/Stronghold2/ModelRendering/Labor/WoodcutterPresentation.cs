using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.S2MReader.Resources;

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
    public override void ConfigureJob(LaborJob job,S2MActorData data)
    {
      var wood=(WoodcutterJob)job;if(!wood.HarvestEnabled)return;
      float fell=0,chop=0;
      foreach(var batch in data.Batches)
      {if(batch.Asset.Variant==S2MWoodcutterAssets.Felling)fell=batch.Motion.Duration;if(batch.Asset.Variant==S2MWoodcutterAssets.Chopping)chop=batch.Motion.Duration;}
      if(!(fell>0) || !(chop>0))throw new System.InvalidOperationException("Woodcutter harvesting clips are unavailable.");
      wood.SetClipDurations(fell,chop);
    }
    public override IEnumerable<ActorPlacement> WorkerPlacements(Peasant actor)
    {
      foreach(var type in new[]{"JobWoodcutterWalk","JobWoodcutterRide","JobWoodcutterFell","JobWoodcutterChop","JobWoodcutterSaw"})
        yield return new ActorPlacement{Id=actor.Id,Type=type,OnMap=true,RawX=actor.Spawn.X*1024,RawZ=actor.Spawn.Z*1024,EstateId=actor.EstateId,AnimationLoops=true};
    }
    public override IEnumerable<ActorPlacement> EquipmentPlacements(LaborJob job)
    {
      var wood=(WoodcutterJob)job;
      yield return GetEquipment(job,1).Placement();
      foreach(var type in new[]{"JobWoodcutterCart",S2MWoodcutterAssets.MovingCart,S2MWoodcutterAssets.LoadedCart,S2MWoodcutterAssets.LoadedIdleCart,S2MWoodcutterAssets.PlankCart,S2MWoodcutterAssets.PlankIdleCart})
        yield return new ActorPlacement{Id=wood.CartActorId,Type=type,OnMap=true,RawX=wood.CartPosition.X*1024,RawZ=wood.CartPosition.Z*1024,
          RawY=wood.RawHeight,HeadingDegrees=wood.CartHeading,EstateId=wood.EstateId,AnimationLoops=true};
    }
    public override IEnumerable<LaborEquipment> Equipment(LaborJob job)
    { yield return GetEquipment(job,0);yield return GetEquipment(job,1); }
    public override int EquipmentCount => 2;
    public override LaborEquipment GetEquipment(LaborJob job,int index)
    {
      if(index==1)
      {var wood=(WoodcutterJob)job;return new LaborEquipment(wood.SawLogActorId,S2MWoodcutterAssets.SawLog,S2MWoodcutterAssets.SawLog,wood.BuildingPosition,wood.SawHeading,wood.RawHeight,visible:wood.Phase==WoodcuttingPhase.Sawing,buildingScale:true);}
      if(index!=0)throw new System.ArgumentOutOfRangeException(nameof(index));
      var woodcutter=(WoodcutterJob)job;
      string variant=woodcutter.CarryingPlanks ? woodcutter.IsCartMoving?S2MWoodcutterAssets.PlankCart:S2MWoodcutterAssets.PlankIdleCart
        :woodcutter.Loaded ? woodcutter.IsCartMoving?S2MWoodcutterAssets.LoadedCart:S2MWoodcutterAssets.LoadedIdleCart
        :woodcutter.IsCartMoving?S2MWoodcutterAssets.MovingCart:S2MWoodcutterAssets.Cart;
      float rate=woodcutter.IsCartMoving?woodcutter.CartAnimationRate:1;
      return new LaborEquipment(woodcutter.CartActorId,"JobWoodcutterCart",variant,
        woodcutter.CartPosition,woodcutter.CartHeading,woodcutter.RawHeight,woodcutter.WheelDegrees,woodcutter.IsCartMoving?woodcutter.CartAnimationTime:woodcutter.PhaseTime,rate,
        woodcutter.HarvestEnabled && woodcutter.Phase!=WoodcuttingPhase.WaitingForResource && woodcutter.Phase!=WoodcuttingPhase.Returned);
    }
    public override string Variant(WorkerComponent worker)
    {
      var wood=(WoodcutterJob)worker.Job;
      if(worker.State==WorkerState.Traveling)return TravelingVariant;
      if(!wood.HarvestEnabled || wood.WorkerOnCart)return WorkingVariant;
      return wood.Phase==WoodcuttingPhase.Sawing?S2MWoodcutterAssets.Sawing
        :wood.Phase==WoodcuttingPhase.Chopping?S2MWoodcutterAssets.Chopping
        :wood.Phase==WoodcuttingPhase.Felling || wood.Phase==WoodcuttingPhase.Falling?S2MWoodcutterAssets.Felling:TravelingVariant;
    }
    public override int AnimationRevision(WorkerComponent worker)=>((WoodcutterJob)worker.Job).Revision;
    public override bool AnimationLoops(WorkerComponent worker)=>((WoodcutterJob)worker.Job).Phase!=WoodcuttingPhase.Falling;
    public override float AnimationRate(WorkerComponent worker,float walkSpeed)
    {
      var wood=(WoodcutterJob)worker.Job;
      if(wood.Phase==WoodcuttingPhase.Falling || wood.WaitingForPath)return 0;
      return wood.WorkerOnCart && wood.IsCartMoving?wood.CartAnimationRate:Variant(worker)==TravelingVariant?walkSpeed:1;
    }
    public override float AnimationTime(WorkerComponent worker,float rate)
    {
      var wood=(WoodcutterJob)worker.Job;
      if(wood.HarvestEnabled && wood.WorkerOnCart && wood.IsCartMoving)return wood.CartAnimationTime;
      return !wood.HarvestEnabled || worker.State==WorkerState.Traveling ? worker.StateTime*rate
        :wood.Phase==WoodcuttingPhase.Falling?wood.FellClipSeconds:wood.PhaseTime*rate;
    }
    public override float BuildingRawHeight(WorkerComponent worker)
    {var wood=(WoodcutterJob)worker.Job;return wood.Phase==WoodcuttingPhase.Sawing?wood.SawRawHeight:wood.RawHeight;}
    public override bool UseBuildingHeight(WorkerComponent worker)
    {
      var wood=(WoodcutterJob)worker.Job;
      return !wood.HarvestEnabled || wood.Phase==WoodcuttingPhase.WaitingForResource || wood.Phase==WoodcuttingPhase.Returned || wood.Phase==WoodcuttingPhase.Sawing;
    }
  }
}
