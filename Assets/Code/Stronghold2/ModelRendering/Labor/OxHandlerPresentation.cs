using System.Collections.Generic;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Economy;using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  public sealed class OxHandlerPresentation : LaborPresentation
  {
    public override LaborRole Role=>LaborRole.OxHandler;
    public override string TravelingVariant=>S2MOxHaulingAssets.HandlerWalk;
    public override string WorkingVariant=>S2MOxHaulingAssets.HandlerIdle;
    protected override string TravelingActorType=>TravelingVariant;
    protected override string WorkingActorType=>WorkingVariant;
    public override IEnumerable<S2MActorOverride> AssetOverrides()=>S2MOxHaulingAssets.Overrides();
    public override bool BuildingEquipment=>true;
    public override bool KeepEquipmentOnRemoval=>false;
    public override int EquipmentCount=>1;
    public override IEnumerable<LaborEquipment> Equipment(LaborJob job)
    {yield return GetEquipment(job,0);}
    public override IEnumerable<ActorPlacement> EquipmentPlacements(LaborJob job)
    {
      var ox=(OxHandlerJob)job;
      foreach(var goods in new[]{Goods.Stone,Goods.Iron})for(int moving=0;moving<2;moving++)for(int blocks=goods==Goods.Iron?1:0;blocks<=8;blocks++)
        yield return Placement(ox,0,S2MOxHaulingAssets.OxVariant(moving==1,blocks,goods));
    }
    private static ActorPlacement Placement(OxHandlerJob ox,int part,string type)=>new(){Id=ox.EquipmentBaseId+part,Type=type,OnMap=true,
      RawX=ox.TetherPosition.X*1024,RawZ=ox.TetherPosition.Z*1024,RawY=ox.RawHeight,EstateId=ox.EstateId,AnimationLoops=true};
    public override LaborEquipment GetEquipment(LaborJob job,int index)
    {
      if(index!=0)throw new System.ArgumentOutOfRangeException(nameof(index));var ox=(OxHandlerJob)job;
      string variant=S2MOxHaulingAssets.OxVariant(ox.OxMoving,ox.VisibleLoadBlocks,ox.CargoGoods);
      return new LaborEquipment(ox.EquipmentBaseId,variant,variant,ox.Ox.Position,ox.Ox.Heading,ox.RawHeight,
        animationTime:ox.OxMoving?ox.OxAnimationTime:ox.PhaseTime,animationRate:ox.OxMoving?ox.Settings.OxSpeedCellsPerSecond/1.5f:1,
        sampleGround:true,visible:ox.OxVisible,revision:ox.Revision,modelScale:ox.Settings.OxModelScale);
    }
    public override string Variant(WorkerComponent worker)=>worker.State==WorkerState.Traveling || ((OxHandlerJob)worker.Job).HandlerMoving?TravelingVariant:WorkingVariant;
    public override float AnimationRate(WorkerComponent worker,float speed)=>worker.State==WorkerState.Traveling?speed:((OxHandlerJob)worker.Job).HandlerMoving?((OxHandlerJob)worker.Job).Settings.HandlerSpeedCellsPerSecond:1;
    public override int AnimationRevision(WorkerComponent worker)=>((OxHandlerJob)worker.Job).Revision;
    public override float AnimationTime(WorkerComponent worker,float rate)=>worker.State==WorkerState.Traveling?worker.StateTime*rate:((OxHandlerJob)worker.Job).PhaseTime*rate;
  }
}
