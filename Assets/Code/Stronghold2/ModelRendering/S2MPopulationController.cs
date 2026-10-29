using System;
using System.Collections.Generic;
using Assets.Code.Simulation;
using Assets.Code.Simulation.Navigation;
using Assets.Code.Simulation.Population;
using Assets.Code.Simulation.Labor;
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
    public LaborManager Labor { get; private set; }
    private readonly List<PeasantRecruitment> kingdoms = new();
    private readonly Dictionary<int,PeasantState> shown = new();
    private readonly Dictionary<int,(LaborRole role,string variant,int revision,float rate)> shownWork = new();
    private S2MActorRenderer actors;
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
      this.map=map; this.terrain=terrain; this.clock=clock; buildingEquipmentScale=terrain.HorizontalCellSize/(S2MBuildingRenderer.ModelUnitsPerCell*renderSettings.ModelUnitScale); scale=renderSettings.ModelScale*unitScale;
      this.settings=settings;
      presentations=new LaborPresentationCatalog(assembly);
      foliage=transform.parent!=null?transform.parent.GetComponentInChildren<S2MFoliageHarvestView>():null;
      float standDuration=0;
      foreach(var batch in data.Batches)
        if(string.Equals(batch.Asset.AnimationPath,S2MCampfirePopulationLoader.StandClip,StringComparison.OrdinalIgnoreCase))standDuration=batch.Motion.Duration;
      if(!(standDuration>0) && plans.Count>0)throw new InvalidOperationException("Stand-up animation is unavailable.");
      actors=S2MActorRenderer.Render(data,transform,terrain,renderSettings,unitScale); actors.name="Recruiting Peasants"; actors.Clock=clock;
      foreach(var plan in plans)
      {
        kingdoms.Add(new PeasantRecruitment(plan,navigation,settings,standDuration));
        foreach(var p in plan){actors.HideActor(p.Id);p.Worker.Removed+=OnWorkerRemoved;}
      }
      if(jobs!=null)
      {
        foreach(var job in jobs)presentations.Get(job.Role).ConfigureJob(job,data);
        Labor=new LaborManager(jobs,kingdoms,navigation,settings,laborBuildings);
        foreach(var job in jobs)foreach(var equipment in presentations.Get(job.Role).Equipment(job))actors.HideActor(equipment.ActorId);
      }
    }
    private void Update() { if(actors!=null)Advance(clock.DeltaTime); }
    public void Advance(float seconds)
    {
      if(Labor!=null)Labor.Advance(seconds);
      foreach(var kingdom in kingdoms)
      {
        if(Labor==null)kingdom.Advance(seconds);
        foreach(var p in kingdom.Peasants)
        {
          if(p.State==PeasantState.Unspawned || p.State==PeasantState.Removed)continue;
          var presentation=p.Job==null ? null : presentations.Get(p.Job.Role);
          string variant=presentation?.Variant(p.Worker);
          float rate=presentation?.AnimationRate(p.Worker,settings.WalkSpeedCellsPerSecond)??1;
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
    {foreach(var kingdom in kingdoms)foreach(var peasant in kingdom.Peasants)peasant.Worker.Removed-=OnWorkerRemoved;}
  }
}
