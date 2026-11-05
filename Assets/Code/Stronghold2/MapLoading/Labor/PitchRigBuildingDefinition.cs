using System;
using System.Collections.Generic;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.MapLoading.Labor
{
  public sealed class PitchRigBuildingDefinition : LaborBuildingDefinition
  {
    private readonly PitchRigSettings settings;
    public PitchRigBuildingDefinition(PitchRigSettings settings=null){this.settings=settings??new PitchRigSettings();this.settings.Validate();}
    public override string BuildingType=>"PitchRig";
    public override LaborJob Create(S2MFile map,BuildingPlacement building,NavigationGrid navigation,int equipmentActorId,ISet<int> suppressed)
    {
      var entrance=S2MNavigationBuilder.BuildingPoint(building,1.125f,-.5f);
      if(!navigation.TryNearest(entrance,8,out var approach))throw new InvalidOperationException($"Pitch rig {building.Id} has no ground approach.");
      return new PitchWorkerJob(building.Id,S2MCampfireLayout.EstateAtBuilding(map,building),equipmentActorId,approach,
        S2MNavigationBuilder.BuildingPoint(building,1.125f,0),S2MNavigationBuilder.BuildingPoint(building,1.125f,1.125f),
        new MapPoint(building.MapX,building.MapZ),building.RotationDegrees,S2MPitchRigLayout.RawHeight(map,building),
        S2MPitchRigLayout.HasPitch(map,building),navigation,settings,S2MNavigationBuilder.SampleHeight(map,approach.X,approach.Z));
    }
    public override bool IsWorkerPlaceholder(ActorPlacement actor,LaborJob job)=>actor.Type=="PitchWorker"
      && MapPoint.Distance(new MapPoint(actor.RawX/1024,actor.RawZ/1024),job.WorkPosition)<3;
  }
}
