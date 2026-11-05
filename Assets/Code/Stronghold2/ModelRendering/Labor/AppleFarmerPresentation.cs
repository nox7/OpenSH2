using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  public sealed class AppleFarmerPresentation : LaborPresentation
  {
    public override LaborRole Role=>LaborRole.AppleFarmer;
    public override string TravelingVariant=>S2MAppleFarmAssets.Walk;
    public override string WorkingVariant=>S2MAppleFarmAssets.Pick;
    protected override string TravelingActorType=>TravelingVariant;
    protected override string WorkingActorType=>WorkingVariant;
    public override IEnumerable<S2MActorOverride> AssetOverrides()=>S2MAppleFarmAssets.Overrides();
    public override IEnumerable<ActorPlacement> WorkerPlacements(Peasant actor)
    {
      foreach(string type in new[]{S2MAppleFarmAssets.Walk,S2MAppleFarmAssets.Idle,S2MAppleFarmAssets.Plant,S2MAppleFarmAssets.Pick,S2MAppleFarmAssets.Carry,S2MAppleFarmAssets.CarryIdle})
        yield return new ActorPlacement{Id=actor.Id,Type=type,EstateId=actor.EstateId,OnMap=true,RawX=actor.Spawn.X*1024,RawZ=actor.Spawn.Z*1024,AnimationLoops=true};
    }
    public override void ConfigureJob(LaborJob job,S2MActorData data)
    {
      float pick=0,plant=0;foreach(var batch in data.Batches)
      {if(batch.Asset.Variant==S2MAppleFarmAssets.Pick)pick=batch.Motion.Duration;if(batch.Asset.Variant==S2MAppleFarmAssets.Plant)plant=batch.Motion.Duration;}
      if(!(pick>0) || !(plant>0))throw new InvalidOperationException("Apple farmer planting/picking clips are unavailable.");
      ((AppleFarmerJob)job).SetDurations(pick,plant);
    }
    public override bool UseBuildingHeight(WorkerComponent worker)=>false;
    public override string Variant(WorkerComponent worker)
    {
      if(worker.State==WorkerState.Traveling)return TravelingVariant;var farmer=(AppleFarmerJob)worker.Job;
      if(farmer.Phase==AppleFarmerPhase.Planting)return S2MAppleFarmAssets.Plant;
      if(farmer.Phase==AppleFarmerPhase.Picking)return S2MAppleFarmAssets.Pick;
      return farmer.PickedTrees>0?(farmer.Moving?S2MAppleFarmAssets.Carry:S2MAppleFarmAssets.CarryIdle):(farmer.Moving?TravelingVariant:S2MAppleFarmAssets.Idle);
    }
    public override float AnimationRate(WorkerComponent worker,float speed)
    {
      if(worker.State==WorkerState.Traveling)return speed;var farmer=(AppleFarmerJob)worker.Job;
      if(farmer.Phase==AppleFarmerPhase.Planting || farmer.Phase==AppleFarmerPhase.Picking)return farmer.Settings.WorkAnimationSpeed;
      return farmer.Moving?farmer.Settings.WalkSpeedCellsPerSecond:1;
    }
    public override float AnimationTime(WorkerComponent worker,float rate)=>worker.State==WorkerState.Traveling?worker.StateTime*rate:((AppleFarmerJob)worker.Job).PhaseTime*rate;
    public override int AnimationRevision(WorkerComponent worker)=>((AppleFarmerJob)worker.Job).Revision;
    public override bool AnimationLoops(WorkerComponent worker)=>worker.State!=WorkerState.Working || ((AppleFarmerJob)worker.Job).Phase!=AppleFarmerPhase.Planting && ((AppleFarmerJob)worker.Job).Phase!=AppleFarmerPhase.Picking;
  }
}
