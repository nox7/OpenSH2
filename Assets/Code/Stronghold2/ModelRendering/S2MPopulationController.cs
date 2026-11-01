using System;
using System.Collections.Generic;
using Assets.Code.Simulation;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Simulation.Labor;
using Assets.Code.Simulation.Economy;
using Assets.Code.Stronghold2.MapLoading.Labor;
using Assets.Code.Stronghold2.ModelRendering.Labor;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Updates instanced actor presentation from the simulation's recruitment states.</summary>
  [DefaultExecutionOrder(100)]
  public sealed class S2MPopulationController : MonoBehaviour
  {
    public IReadOnlyList<PeasantRecruitment> Kingdoms => kingdoms;
    public IReadOnlyList<PeasantRecruitment> Territories => kingdoms;
    private NavigationGrid navigation;
    private bool[] recruitmentSeats,occupiedWorkerCells;
    private readonly List<int> occupiedWorkerIndices=new();
    private readonly HashSet<int> authorizedHovels=new();
    public LaborManager Labor { get; private set; }
    private readonly List<PeasantRecruitment> kingdoms = new();
    private readonly Dictionary<int,PeasantState> shown = new();
    private readonly Dictionary<int,(LaborRole role,string variant,int revision,float rate)> shownWork = new();
    private S2MActorRenderer actors;
    private S2MActorData presentationData;
    private SimulationClock clock;
    private S2MFile map;
    private S2MTerrainSettings terrain;
    private float scale;
    private PopulationSettings settings;
    private LaborPresentationCatalog presentations;
    private readonly Dictionary<int,(string variant,float rate,int revision)> shownEquipment = new();
    private S2MFoliageHarvestView foliage;

    public void Initialize(S2MFile map,NavigationGrid navigation,IReadOnlyList<IReadOnlyList<Peasant>> plans,
      S2MActorData data,S2MTerrainSettings terrain,S2MActorRenderSettings renderSettings,PopulationSettings settings,
      SimulationClock clock,float unitScale=1,IReadOnlyList<LaborJob> jobs=null,HorseCartAssemblySettings assembly=null,IReadOnlyList<LaborBuilding> laborBuildings=null)
    {
      if(data.Issues.Count>0)throw new InvalidOperationException("Recruitment animation assets failed: "+string.Join("; ",data.Issues));
      presentationData=data;this.navigation=navigation;this.map=map; this.terrain=terrain; this.clock=clock; buildingEquipmentScale=terrain.HorizontalCellSize/(S2MBuildingRenderer.ModelUnitsPerCell*renderSettings.ModelUnitScale); scale=renderSettings.ModelScale*unitScale;
      this.settings=settings;
      recruitmentSeats=new bool[map.MapSize*map.MapSize];occupiedWorkerCells=new bool[recruitmentSeats.Length];
      foreach(var plan in plans)foreach(var p in plan)
      {
        int x=(int)Math.Floor(p.Seat.X),z=(int)Math.Floor(p.Seat.Z);
        if(x>=0 && z>=0 && x<map.MapSize && z<map.MapSize)recruitmentSeats[z*map.MapSize+x]=true;
      }
      presentations=new LaborPresentationCatalog(assembly);
      foliage=transform.parent!=null?transform.parent.GetComponentInChildren<S2MFoliageHarvestView>():null;
      float standDuration=0;
      foreach(var batch in data.Batches)
        if(string.Equals(batch.Asset.AnimationPath,S2MCampfirePopulationLoader.StandClip,StringComparison.OrdinalIgnoreCase))standDuration=batch.Motion.Duration;
      if(!(standDuration>0) && plans.Count>0)throw new InvalidOperationException("Stand-up animation is unavailable.");
      actors=S2MActorRenderer.Render(data,transform,terrain,renderSettings,unitScale); actors.name="Recruiting Peasants"; actors.Clock=clock;
      foreach(var plan in plans)
      {
        var recruitment=new PeasantRecruitment(plan,navigation,settings,standDuration);
        if(plan.Count>0 && plan[0].IsEstateResident)recruitment.SetSpawnLimit(0);
        kingdoms.Add(recruitment);
        foreach(var p in plan){actors.HideActor(p.Id);p.Worker.Removed+=OnWorkerRemoved;}
      }
      actors.HideActor(S2MCampfirePopulationLoader.RuntimeCartTemplateId);actors.HideActor(S2MCampfirePopulationLoader.RuntimeSawLogTemplateId);
      foreach(var building in map.BuildingPlacements)if(building.Type=="Hovel")AuthorizeHovel(building);
      if(jobs!=null)
      {
        foreach(var job in jobs)presentations.Get(job.Role).ConfigureJob(job,data);
        Labor=new LaborManager(jobs,kingdoms,navigation,settings,laborBuildings);
        foreach(var job in jobs)foreach(var equipment in presentations.Get(job.Role).Equipment(job))actors.HideActor(equipment.ActorId);
      }
    }
    public IPreparedAiLabor PrepareSawpit(BuildingPlacement placement,FoliageResources resources,WoodcuttingSettings woodSettings,EstateInventory inventory)
    {
      int id=actors.AllocateEquipmentPair();
      var job=(WoodcutterJob)new WoodcutterBuildingDefinition().Create(map,placement,navigation,id,new HashSet<int>());
      bool reachable=false;
      foreach(var group in kingdoms)
      {
        if(group.Peasants.Count==0 || group.Peasants[0].EstateId!=job.EstateId)continue;
        for(int i=0;i<group.SpawnedCount;i++)
        {
          var peasant=group.Peasants[i];
          if(peasant.Job==null && peasant.State!=PeasantState.Removed && navigation.CanReach(peasant.Seat,job.Approach)){reachable=true;break;}
        }
        if(reachable)break;
      }
      if(!reachable)throw new InvalidOperationException("Sawpit has no route from its estate's recruitment area.");
      bool canDeliver=false;
      foreach(var point in job.DeliveryPoints)if(navigation.CanReach(job.Approach,point.Position)){canDeliver=true;break;}
      if(!canDeliver)throw new InvalidOperationException("Sawpit has no route to a same-estate stockpile.");
      job.ConfigureHarvest(resources,woodSettings);job.ConfigureStorage(inventory);
      presentations.Get(job.Role).ConfigureJob(job,presentationData);
      try
      {
        actors.CloneActor(S2MCampfirePopulationLoader.RuntimeCartTemplateId,job.CartActorId,job.EstateId);
        actors.CloneActor(S2MCampfirePopulationLoader.RuntimeSawLogTemplateId,job.SawLogActorId,job.EstateId);
        return new PreparedSawpit(this,new LaborBuilding(placement.Id,job.EstateId,new[]{job}),job);
      }
      catch{actors.RemoveClonedActor(job.CartActorId);actors.RemoveClonedActor(job.SawLogActorId);throw;}
    }
    private sealed class PreparedSawpit : IPreparedAiLabor
    {
      public LaborBuilding Building { get; }
      private S2MPopulationController owner;
      private readonly WoodcutterJob job;
      public PreparedSawpit(S2MPopulationController owner,LaborBuilding building,WoodcutterJob job){this.owner=owner;Building=building;this.job=job;}
      public void Show(){} // Equipment appears only after the worker reaches its job.
      public void Dispose()
      {
        if(owner==null)return;
        owner.actors.RemoveClonedActor(job.CartActorId);owner.actors.RemoveClonedActor(job.SawLogActorId);
        owner.shownEquipment.Remove(job.CartActorId);owner.shownEquipment.Remove(job.SawLogActorId);owner=null;
      }
    }
    public bool PreservesRecruitmentSeats(BuildingPlacement building)
    {
      var footprint=S2MBuildingFootprint.FromPlacement(building);
      for(int z=footprint.MinZ;z<footprint.MaxZExclusive;z++)for(int x=footprint.MinX;x<footprint.MaxXExclusive;x++)
        if(x>=0 && z>=0 && x<map.MapSize && z<map.MapSize && (recruitmentSeats[z*map.MapSize+x] || occupiedWorkerCells[z*map.MapSize+x]))return false;
      return true;
    }
    public void AuthorizeHovel(BuildingPlacement building)
    {
      if(building.Type!="Hovel" || authorizedHovels.Contains(building.Id))return;
      int estate=S2MCampfireLayout.EstateAtBuilding(map,building);
      foreach(var group in kingdoms)
      {
        if(group.Peasants.Count==0 || !group.Peasants[0].IsEstateResident || group.Peasants[0].EstateId!=estate)continue;
        int start=group.SpawnLimit,end=Math.Min(group.Peasants.Count,start+CampfirePopulation.PeasantsPerHovel);
        for(int i=start;i<end;i++)S2MRecruitmentPlan.ConfigureHovel(group.Peasants[i],building,navigation,settings);
        group.SetSpawnLimit(end);authorizedHovels.Add(building.Id);return;
      }
    }
    private void Update() { if(actors!=null)Advance(clock.DeltaTime); }
    public void Advance(float seconds)
    {
      if(Labor!=null)Labor.Advance(seconds);
      foreach(int cell in occupiedWorkerIndices)occupiedWorkerCells[cell]=false;occupiedWorkerIndices.Clear();
      foreach(var kingdom in kingdoms)
      {
        if(Labor==null)kingdom.Advance(seconds);
        foreach(var p in kingdom.Peasants)
        {
          if(p.State==PeasantState.Unspawned || p.State==PeasantState.Removed)continue;
          int px=(int)Math.Floor(p.Position.X),pz=(int)Math.Floor(p.Position.Z);
          if(px>=0 && pz>=0 && px<map.MapSize && pz<map.MapSize)
          {int cell=pz*map.MapSize+px;if(!occupiedWorkerCells[cell]){occupiedWorkerCells[cell]=true;occupiedWorkerIndices.Add(cell);}}
          var presentation=p.Job==null ? null : presentations.Get(p.Job.Role);
          string variant=presentation?.Variant(p.Worker);
          float rate=p.Worker.WaitingForPath?0:presentation?.AnimationRate(p.Worker,settings.WalkSpeedCellsPerSecond)??1;
          var signature=(p.Job?.Role??default,variant,presentation?.AnimationRevision(p.Worker)??0,rate);
          bool workChanged=presentation!=null && (!shownWork.TryGetValue(p.Id,out var previousWork) || !previousWork.Equals(signature));
          if(!shown.TryGetValue(p.Id,out var previous) || previous!=p.State || workChanged)
          {
            string clip=p.State switch {
              PeasantState.HovelSeated=>S2MCampfirePopulationLoader.HovelClip,
              PeasantState.StandingUp=>S2MCampfirePopulationLoader.StandClip,
              PeasantState.LeavingHovel or PeasantState.Running=>S2MCampfirePopulationLoader.RunClip,
              PeasantState.Seated=>S2MCampfirePopulationLoader.SeatClip(p.Id),
              _=>S2MCampfirePopulationLoader.WaitClip };
            bool loop=p.State!=PeasantState.StandingUp;
            bool selected=variant!=null ? actors.SetVariant(p.Id,variant,presentation.AnimationTime(p.Worker,rate),presentation.AnimationLoops(p.Worker),rate)
              : actors.SetAnimation(p.Id,clip,p.StateTime,loop);
            if(!selected)throw new InvalidOperationException("Missing actor state "+(variant??clip));
            shown[p.Id]=p.State;
            if(presentation!=null)shownWork[p.Id]=signature;
            if(p.State==PeasantState.WaitingForPath)Debug.LogWarning($"Peasant {p.Id} from building {p.SourceBuildingId} has no route to its campfire seat; retrying in simulation time.");
          }
          float x=p.Position.X,z=p.Position.Z,yaw=p.Heading;
          // Reflect movement vectors, preserving the same map-space simulation under either terrain flip.
          if(terrain.FlipX) { x=map.MapSize-x; yaw=-yaw; }
          if(terrain.FlipZ) { z=map.MapSize-z; yaw=180-yaw; }
          float height=S2MNavigationBuilder.SampleHeight(map,p.Position.X,p.Position.Z);
          if(p.Worker.State==WorkerState.Working)
          {
            if(presentation.UseBuildingHeight(p.Worker))height=presentation.BuildingRawHeight(p.Worker);
            if(!presentation.BuildingEquipment)ShowEquipment(p.Job);
          }
          // Hovel interiors use ground height, rather than the roof/stair navigation surface.
          if(p.State==PeasantState.HovelSeated || p.State==PeasantState.StandingUp || p.State==PeasantState.LeavingHovel)
            height=S2MCampfireLayout.GroundHeight(map,p.Position.X,p.Position.Z);
          actors.SetTransform(p.Id,new Vector3(x*terrain.HorizontalCellSize,height*terrain.HeightUnitScale,z*terrain.HorizontalCellSize),
            Quaternion.Euler(0,yaw,0),presentation!=null && presentation.UseBuildingScale(p.Worker)?new Vector3(buildingEquipmentScale,buildingEquipmentScale*terrain.HeightUnitScale*1024/terrain.HorizontalCellSize,buildingEquipmentScale):Vector3.one*scale);
        }
      }
      if(Labor!=null)for(int i=0;i<Labor.Jobs.Count;i++){var job=Labor.Jobs[i];if(presentations.Get(job.Role).BuildingEquipment)ShowEquipment(job);}
      foliage?.Synchronize();
    }
    private float buildingEquipmentScale;
    private void ShowEquipment(LaborJob job)
    {
      var presentation=presentations.Get(job.Role);
      for(int i=0;i<presentation.EquipmentCountFor(job);i++)
      {
        var equipment=presentation.GetEquipment(job,i);
        if(!equipment.Visible) {if(shownEquipment.Remove(equipment.ActorId))actors.HideActor(equipment.ActorId);continue;}
        if(!shownEquipment.TryGetValue(equipment.ActorId,out var previous) || previous!=(equipment.Variant,equipment.AnimationRate,equipment.Revision))
        {
          if(!actors.SetVariant(equipment.ActorId,equipment.Variant,equipment.AnimationTime,equipment.AnimationLoops,equipment.AnimationRate))
            throw new InvalidOperationException("Missing labor equipment "+equipment.Variant);
          shownEquipment[equipment.ActorId]=(equipment.Variant,equipment.AnimationRate,equipment.Revision);
        }
        float x=equipment.Position.X,z=equipment.Position.Z,yaw=equipment.Heading;
        if(terrain.FlipX){x=map.MapSize-x;yaw=-yaw;}if(terrain.FlipZ){z=map.MapSize-z;yaw=180-yaw;}
        float height=equipment.SampleGround?S2MNavigationBuilder.SampleHeight(map,equipment.Position.X,equipment.Position.Z):equipment.RawHeight;
        actors.SetTransform(equipment.ActorId,new Vector3(x*terrain.HorizontalCellSize,height*terrain.HeightUnitScale,z*terrain.HorizontalCellSize),Quaternion.Euler(0,yaw,0),(equipment.BuildingScale?new Vector3(buildingEquipmentScale,buildingEquipmentScale*terrain.HeightUnitScale*1024/terrain.HorizontalCellSize,buildingEquipmentScale):Vector3.one*scale)*equipment.ModelScale,equipment.MotionDegrees);
      }
    }
    /// <summary>Shared death/despawn entry point; releases the worker's pile and hides all visual variants.</summary>
    public bool RemoveActor(int id)
    {
      foreach(var kingdom in kingdoms)foreach(var peasant in kingdom.Peasants)
        if(peasant.Id==id){peasant.Worker.Remove();foliage?.Synchronize();return true;}
      return false;
    }
    private void OnWorkerRemoved(WorkerComponent worker,LaborJob former)
    {
      actors.HideActor(worker.ActorId);shown.Remove(worker.ActorId);shownWork.Remove(worker.ActorId);
      if(former!=null)
      {
        var presentation=presentations.Get(former.Role);
        if(presentation.KeepEquipmentOnRemoval)return;
        for(int i=0;i<presentation.EquipmentCountFor(former);i++)
        {int id=presentation.GetEquipment(former,i).ActorId;actors.HideActor(id);shownEquipment.Remove(id);}
      }
    }
    private void OnDestroy()
    {Labor?.Dispose();foreach(var kingdom in kingdoms)foreach(var peasant in kingdom.Peasants)peasant.Worker.Removed-=OnWorkerRemoved;}
  }
}
