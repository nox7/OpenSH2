using System;using System.Collections.Generic;using UnityEngine;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  public sealed class QuarryPresentation : LaborPresentation
  {
    public override LaborRole Role=>LaborRole.Stonemason;
    public override string TravelingVariant=>S2MQuarryAssets.Walk;
    public override string WorkingVariant=>S2MQuarryAssets.Hammer;
    protected override string TravelingActorType=>"JobQuarryWalk";
    protected override string WorkingActorType=>"JobQuarryHammer";
    public override IEnumerable<S2MActorOverride> AssetOverrides()=>S2MQuarryAssets.Overrides();
    public override bool BuildingEquipment=>true;
    public override int EquipmentCountFor(LaborJob job)=>((QuarryJob)job).SlotIndex==0?55:0;
    public override IEnumerable<LaborEquipment> Equipment(LaborJob job)
    {for(int i=0;i<EquipmentCountFor(job);i++)yield return GetEquipment(job,i);}
    public override IEnumerable<ActorPlacement> WorkerPlacements(Peasant actor)
    {
      foreach(var type in new[]{"JobQuarryWalk","JobQuarryIdle","JobQuarryHammer","JobQuarryLift","JobQuarryHook"})
        yield return new ActorPlacement{Id=actor.Id,Type=type,OnMap=true,RawX=actor.Spawn.X*1024,RawZ=actor.Spawn.Z*1024,EstateId=actor.EstateId,AnimationLoops=false};
    }
    public override LaborEquipment GetEquipment(LaborJob job,int index)
    {
      var q=(QuarryJob)job;var state=q.Production;var settings=state.Settings;
      if(index<0 || index>=55 || q.SlotIndex!=0)throw new ArgumentOutOfRangeException(nameof(index));
      if(index>=50)
      {
        int stage=index-50;string pose=S2MQuarryAssets.RockPose(stage);
        return new LaborEquipment(q.EquipmentBaseId+index,pose,pose,q.BuildingPosition,q.BuildingHeading,q.RawHeight,
          animationRate:0,visible:state.BenchRockStage==stage,buildingScale:true,animationLoops:false);
      }
      if(index>=48)
      {
        string variant=index==48?S2MQuarryAssets.Rig:S2MQuarryAssets.Rock;
        return new LaborEquipment(q.EquipmentBaseId+index,variant,variant,q.BuildingPosition,q.BuildingHeading,q.RawHeight,
          animationTime:index==49?state.RockAnimationTime:state.AnimationTime,animationRate:state.IsWorking?(index==49?state.RockAnimationRate:state.TransportAnimationRate):0,visible:index==48 || state.WorkingRockVisible,buildingScale:true,animationLoops:false,revision:state.Revision);
      }
      var layout=settings.StoneLayout;int per=layout.GridColumns*layout.GridRows;
      layout.CellAt(index,out int column,out int row);
      var delta=Quaternion.Euler(0,q.BuildingHeading,0)*new Vector3(layout.OffsetX+column*layout.BlockSpacing,0,layout.OffsetZ+(row+(layout.GridRows-1)*.5f)*layout.BlockSpacing);
      return new LaborEquipment(q.EquipmentBaseId+index,S2MQuarryAssets.Stone,S2MQuarryAssets.Stone,
        new MapPoint(q.BuildingPosition.X+delta.x,q.BuildingPosition.Z+delta.z),q.BuildingHeading,q.RawHeight+(layout.OffsetY+(index/per)*layout.LayerHeight)*1024,
        motionDegrees:S2MQuarryAssets.FinishedStoneYaw,visible:index<state.StoredStone,buildingScale:true);
    }
    public override string Variant(WorkerComponent worker)
    {
      if(worker.State==WorkerState.Traveling)return TravelingVariant;
      var q=(QuarryJob)worker.Job;
      return q.SlotIndex==0?S2MQuarryAssets.Hammer:q.SlotIndex==1?S2MQuarryAssets.Lift:S2MQuarryAssets.Hook;
    }
    public override int AnimationRevision(WorkerComponent worker)=>((QuarryJob)worker.Job).Production.Revision;
    public override bool AnimationLoops(WorkerComponent worker)=>true;
    public override bool UseBuildingScale(WorkerComponent worker)=>worker.State==WorkerState.Working;
    public override float AnimationRate(WorkerComponent worker,float speed)
    {var q=(QuarryJob)worker.Job;return worker.State==WorkerState.Traveling?speed:(q.SlotIndex==0?q.Production.IsChiseling:q.Production.IsWorking)?(q.SlotIndex==0?q.Production.HammerAnimationRate:q.Production.TransportAnimationRate):0;}
    public override float AnimationTime(WorkerComponent worker,float rate)
    {var q=(QuarryJob)worker.Job;return worker.State==WorkerState.Traveling?worker.StateTime*rate:q.SlotIndex==0?q.Production.HammerAnimationTime:q.Production.AnimationTime;}
  }
}
