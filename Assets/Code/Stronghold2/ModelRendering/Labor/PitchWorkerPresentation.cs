using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  public sealed class PitchWorkerPresentation : LaborPresentation
  {
    public override LaborRole Role=>LaborRole.PitchWorker;
    public override string TravelingVariant=>S2MPitchRigAssets.Walk;
    public override string WorkingVariant=>S2MPitchRigAssets.Pump;
    protected override string TravelingActorType=>TravelingVariant;
    protected override string WorkingActorType=>WorkingVariant;
    public override IEnumerable<S2MActorOverride> AssetOverrides()=>S2MPitchRigAssets.Overrides();
    public override IEnumerable<ActorPlacement> WorkerPlacements(Peasant actor)
    {
      foreach(string type in new[]{S2MPitchRigAssets.Walk,S2MPitchRigAssets.Idle,S2MPitchRigAssets.Pump,S2MPitchRigAssets.Carry,S2MPitchRigAssets.CarryIdle})
        yield return new ActorPlacement{Id=actor.Id,Type=type,OnMap=true,EstateId=actor.EstateId,RawX=actor.Spawn.X*1024,RawZ=actor.Spawn.Z*1024,AnimationLoops=true};
    }
    public override void ConfigureJob(LaborJob job,S2MActorData data)
    {
      foreach(var batch in data.Batches)if(batch.Asset.Variant==S2MPitchRigAssets.Pump)
      {((PitchWorkerJob)job).SetPumpDuration(batch.Motion.Duration);return;}
      throw new InvalidOperationException("Pitch worker scoop animation is unavailable.");
    }
    public override bool BuildingEquipment=>true;
    public override int EquipmentCount=>1;
    public override IEnumerable<LaborEquipment> Equipment(LaborJob job){yield return GetEquipment(job,0);}
    public override LaborEquipment GetEquipment(LaborJob job,int index)
    {
      if(index!=0)throw new ArgumentOutOfRangeException(nameof(index));var pitch=(PitchWorkerJob)job;
      bool pumping=pitch.IsOccupied && pitch.ExtractionSiteValid && pitch.Phase==PitchWorkerPhase.Pumping;
      return new LaborEquipment(pitch.EquipmentId,S2MPitchRigAssets.Scoop,S2MPitchRigAssets.Scoop,pitch.BuildingPosition,pitch.BuildingHeading,pitch.RigRawHeight,
        animationTime:pumping?pitch.PhaseTime*pitch.Settings.PumpAnimationSpeed:0,animationRate:pumping?pitch.Settings.PumpAnimationSpeed:0,buildingScale:true,revision:pitch.Revision);
    }
    public override string Variant(WorkerComponent worker)
    {
      if(worker.State==WorkerState.Traveling)return TravelingVariant;
      var pitch=(PitchWorkerJob)worker.Job;
      if(pitch.Phase==PitchWorkerPhase.Pumping && pitch.ExtractionSiteValid)return WorkingVariant;
      return pitch.Cargo>0?(pitch.Moving?S2MPitchRigAssets.Carry:S2MPitchRigAssets.CarryIdle):(pitch.Moving?TravelingVariant:S2MPitchRigAssets.Idle);
    }
    public override float AnimationRate(WorkerComponent worker,float walkSpeed)
    {
      if(worker.State==WorkerState.Traveling)return worker.WaitingForPath?0:walkSpeed;
      var pitch=(PitchWorkerJob)worker.Job;
      if(pitch.Phase==PitchWorkerPhase.Pumping && pitch.ExtractionSiteValid)return pitch.Settings.PumpAnimationSpeed;
      return pitch.Moving?pitch.Settings.WalkSpeedCellsPerSecond:1;
    }
    public override float AnimationTime(WorkerComponent worker,float rate)=>worker.State==WorkerState.Traveling?worker.StateTime*rate:((PitchWorkerJob)worker.Job).PhaseTime*rate;
    public override int AnimationRevision(WorkerComponent worker)=>((PitchWorkerJob)worker.Job).Revision;
    private static bool Pumping(WorkerComponent worker)=>worker.State==WorkerState.Working && ((PitchWorkerJob)worker.Job).Phase==PitchWorkerPhase.Pumping;
    // This clip is authored in the rig assembly's coordinates, like the quarry clips.
    // Keep the logical worker at its interaction point while sampling the visual at the rig origin.
    public override Assets.Code.Simulation.Navigation.MapPoint Position(WorkerComponent worker)=>Pumping(worker)?((PitchWorkerJob)worker.Job).BuildingPosition:base.Position(worker);
    public override float Heading(WorkerComponent worker)=>Pumping(worker)?((PitchWorkerJob)worker.Job).BuildingHeading:base.Heading(worker);
    public override bool UseBuildingScale(WorkerComponent worker)=>Pumping(worker);
    public override bool UseBuildingHeight(WorkerComponent worker)
    {var phase=((PitchWorkerJob)worker.Job).Phase;return phase==PitchWorkerPhase.Entering || phase==PitchWorkerPhase.Pumping || phase==PitchWorkerPhase.Exiting;}
    public override float BuildingRawHeight(WorkerComponent worker)
    {
      var pitch=(PitchWorkerJob)worker.Job;
      if(pitch.Phase==PitchWorkerPhase.Pumping)return pitch.RigRawHeight;
      // Blend the short ramp to the platform without snapping to swamp ground.
      float distance=Assets.Code.Simulation.Navigation.MapPoint.Distance(pitch.Approach,pitch.WorkPosition);
      float t=distance>0?Math.Clamp(1-Assets.Code.Simulation.Navigation.MapPoint.Distance(worker.Movement.Position,pitch.WorkPosition)/distance,0,1):1;
      return pitch.ApproachRawHeight+(pitch.RawHeight-pitch.ApproachRawHeight)*t;
    }
  }
}
