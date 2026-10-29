using System;using System.Collections.Generic;using UnityEngine;
using Assets.Code.Simulation.Labor;using Assets.Code.Simulation.Navigation;using Assets.Code.Simulation.Population;
using Assets.Code.Stronghold2.AssetLoading;using Assets.Code.Stronghold2.S2MReader.Resources;
namespace Assets.Code.Stronghold2.ModelRendering.Labor
{
  public sealed class IronMinerPresentation : LaborPresentation
  {
    public override LaborRole Role=>LaborRole.IronMiner;
    public override string TravelingVariant=>S2MIronMineAssets.Walk;public override string WorkingVariant=>S2MIronMineAssets.Climb;
    protected override string TravelingActorType=>TravelingVariant;protected override string WorkingActorType=>WorkingVariant;
    public override IEnumerable<S2MActorOverride> AssetOverrides()=>S2MIronMineAssets.Overrides();
    public override bool BuildingEquipment=>true;
    public override int EquipmentCountFor(LaborJob job)=>((IronMinerJob)job).SlotIndex==0?4:0;
    public override IEnumerable<ActorPlacement> WorkerPlacements(Peasant actor)
    {foreach(var type in new[]{S2MIronMineAssets.Walk,S2MIronMineAssets.Climb,S2MIronMineAssets.Lever,S2MIronMineAssets.Carry,S2MIronMineAssets.Return})yield return new ActorPlacement{Id=actor.Id,Type=type,OnMap=true,RawX=actor.Spawn.X*1024,RawZ=actor.Spawn.Z*1024,EstateId=actor.EstateId,AnimationLoops=false};}
    public override IEnumerable<LaborEquipment> Equipment(LaborJob job)
    {for(int i=0;i<EquipmentCountFor(job);i++)yield return GetEquipment(job,i);}
    public override IEnumerable<ActorPlacement> EquipmentPlacements(LaborJob job)
    {
      if(((IronMinerJob)job).SlotIndex!=0)yield break;
      for(int i=0;i<3;i++)yield return GetEquipment(job,i).Placement();
      var miner=(IronMinerJob)job;for(int n=1;n<=10;n++)yield return new ActorPlacement{Id=miner.EquipmentBaseId+3,Type=S2MIronMineAssets.Pile(n),OnMap=true,RawX=miner.BuildingPosition.X*1024,RawZ=miner.BuildingPosition.Z*1024,RawY=miner.RawHeight,EstateId=miner.EstateId};
    }
    public override LaborEquipment GetEquipment(LaborJob job,int index)
    {
      var miner=(IronMinerJob)job;var production=miner.Production;float t=production.NativeTime,speed=production.AnimationRate;
      if(miner.SlotIndex!=0 || index<0 || index>=4)throw new ArgumentOutOfRangeException(nameof(index));
      if(index==3)
      {
        var settings=production.Settings;var offset=Quaternion.Euler(0,miner.BuildingHeading,0)*new Vector3(settings.PileOffsetX,0,settings.PileOffsetZ);
        var position=new MapPoint(miner.BuildingPosition.X+offset.x,miner.BuildingPosition.Z+offset.z);string variant=S2MIronMineAssets.Pile(Math.Max(1,production.StoredIron));
        return new LaborEquipment(miner.EquipmentBaseId+index,variant,variant,position,miner.BuildingHeading,miner.RawHeight+settings.PileOffsetY*1024,animationRate:0,visible:production.StoredIron>0,buildingScale:true,animationLoops:false,revision:production.Revision);
      }
      string part=index==0?S2MIronMineAssets.Lift:index==1?S2MIronMineAssets.Rock:S2MIronMineAssets.Molten;
      bool visible=index==0 || index==1 && t>=IronMineProduction.LeverUp && t<828f/40f || index==2 && t>=IronMineProduction.WalkBack && t<IronMineProduction.OutputSeconds;
      float time,rate;
      if(index==0){time=t<IronMineProduction.ClimbDown?3.5f-t:t<IronMineProduction.LeverUp?0:Math.Min(3.5f,t-IronMineProduction.LeverUp);rate=t<IronMineProduction.ClimbDown?-speed:t>=IronMineProduction.LeverUp && t<828f/40f?speed:0;}
      else{time=t-(index==1?IronMineProduction.LeverUp:IronMineProduction.WalkBack);rate=speed;}
      return new LaborEquipment(miner.EquipmentBaseId+index,part,part,miner.BuildingPosition,miner.BuildingHeading,miner.RawHeight,animationTime:Math.Max(0,time),animationRate:production.IsWorking?rate:0,visible:visible,buildingScale:true,animationLoops:false,revision:production.Revision);
    }
    public override string Variant(WorkerComponent worker)
    {
      if(worker.State==WorkerState.Traveling)return TravelingVariant;var miner=(IronMinerJob)worker.Job;float t=miner.Production.NativeTime;
      if(miner.SlotIndex==1)return S2MIronMineAssets.Lever;
      return t>=IronMineProduction.WalkBack?S2MIronMineAssets.Return:t>=IronMineProduction.MoveIron?S2MIronMineAssets.Carry:S2MIronMineAssets.Climb;
    }
    public override float AnimationTime(WorkerComponent worker,float rate)
    {
      if(worker.State==WorkerState.Traveling)return worker.StateTime*rate;var miner=(IronMinerJob)worker.Job;float t=miner.Production.NativeTime;
      if(miner.SlotIndex==1)return Math.Max(0,t<IronMineProduction.LeverUp?3.5f-t:t-IronMineProduction.LeverUp);
      if(t>=IronMineProduction.WalkBack)return t-IronMineProduction.WalkBack;
      if(t>=IronMineProduction.MoveIron)return t-IronMineProduction.MoveIron;
      if(t>=IronMineProduction.ClimbUp)return Math.Max(0,2.8666668f-(t-IronMineProduction.ClimbUp));
      return Math.Max(0,t-IronMineProduction.ClimbDown);
    }
    public override float AnimationRate(WorkerComponent worker,float speed)
    {
      if(worker.State==WorkerState.Traveling)return speed;var miner=(IronMinerJob)worker.Job;var production=miner.Production;float t=production.NativeTime;
      if(!production.IsWorking || miner.SlotIndex==0 && t<IronMineProduction.ClimbDown)return 0;
      bool reverse=miner.SlotIndex==1?t<IronMineProduction.LeverUp:t>=IronMineProduction.ClimbUp && t<IronMineProduction.MoveIron;
      return (reverse?-1:1)*production.AnimationRate;
    }
    public override int AnimationRevision(WorkerComponent worker)=>((IronMinerJob)worker.Job).Production.Revision;
    public override bool AnimationLoops(WorkerComponent worker)=>worker.State==WorkerState.Traveling;
    public override bool UseBuildingScale(WorkerComponent worker)=>worker.State==WorkerState.Working;
  }
}
