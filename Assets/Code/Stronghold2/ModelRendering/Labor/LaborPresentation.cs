using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  public readonly struct LaborEquipment
  {
    public int ActorId { get; }
    public string ActorType { get; }
    public string Variant { get; }
    public MapPoint Position { get; }
    public float Heading { get; }
    public float RawHeight { get; }
    public float MotionDegrees { get; }
    public float AnimationTime { get; }
    public float AnimationRate { get; }
    public bool SampleGround { get; }
    public bool Visible { get; }
    public bool BuildingScale { get; }
    public LaborEquipment(int actorId,string actorType,string variant,MapPoint position,float heading,float rawHeight,
      float motionDegrees=0,float animationTime=0,float animationRate=1,bool sampleGround=false,bool visible=true,bool buildingScale=false)
    { ActorId=actorId;ActorType=actorType;Variant=variant;Position=position;Heading=heading;RawHeight=rawHeight;
      MotionDegrees=motionDegrees;AnimationTime=animationTime;AnimationRate=animationRate;SampleGround=sampleGround;Visible=visible;BuildingScale=buildingScale; }
    public ActorPlacement Placement() => new() { Id=ActorId,Type=ActorType,OnMap=true,RawX=Position.X*1024,
      RawZ=Position.Z*1024,RawY=RawHeight,HeadingDegrees=Heading,AnimationLoops=true };
  }

  /// <summary>Per-role assets and presentation. Simulation jobs contain no model or Unity references.</summary>
  public abstract class LaborPresentation
  {
    public abstract LaborRole Role { get; }
    public abstract string TravelingVariant { get; }
    public abstract string WorkingVariant { get; }
    protected abstract string TravelingActorType { get; }
    protected abstract string WorkingActorType { get; }
    public abstract IEnumerable<S2MActorOverride> AssetOverrides();
    public virtual void ConfigureJob(LaborJob job,S2MActorData data) { }
    public virtual IEnumerable<LaborEquipment> Equipment(LaborJob job) { yield break; }
    public virtual int EquipmentCount => 0;
    public virtual LaborEquipment GetEquipment(LaborJob job,int index) => throw new System.ArgumentOutOfRangeException(nameof(index));
    public virtual IEnumerable<ActorPlacement> EquipmentPlacements(LaborJob job)
    { foreach(var equipment in Equipment(job))yield return equipment.Placement(); }
    public virtual IEnumerable<ActorPlacement> WorkerPlacements(Peasant actor)
    {
      foreach(string type in new[]{TravelingActorType,WorkingActorType})
        yield return new ActorPlacement { Id=actor.Id,Type=type,OnMap=true,RawX=actor.Spawn.X*1024,RawZ=actor.Spawn.Z*1024,AnimationLoops=true };
    }
    public virtual float WalkingAnimationRate(float walkSpeed) => walkSpeed;
    // Jobs with harvesting, hauling, forging, etc. can supply phase-specific clips/timers here.
    public virtual string Variant(WorkerComponent worker) => worker.State==WorkerState.Traveling ? TravelingVariant : WorkingVariant;
    public virtual int AnimationRevision(WorkerComponent worker) => 0;
    public virtual bool AnimationLoops(WorkerComponent worker) => true;
    public virtual float AnimationRate(WorkerComponent worker,float walkSpeed)
      => worker.State==WorkerState.Traveling ? WalkingAnimationRate(walkSpeed*worker.Job.TravelSpeedMultiplier) : 1;
    public virtual float AnimationTime(WorkerComponent worker,float rate) => worker.StateTime*rate;
    public virtual float BuildingRawHeight(WorkerComponent worker) => worker.Job.RawHeight;
    public virtual bool UseBuildingHeight(WorkerComponent worker) => true;
  }
}
