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
    private readonly Dictionary<int,(LaborRole role,string variant,int revision)> shownWork = new();
    private S2MActorRenderer actors;
    private SimulationClock clock;
    private S2MFile map;
    private S2MTerrainSettings terrain;
    private float scale;
    private PopulationSettings settings;
    private LaborPresentationCatalog presentations;
    private readonly HashSet<int> shownEquipment = new();

    public void Initialize(S2MFile map,NavigationGrid navigation,IReadOnlyList<IReadOnlyList<Peasant>> plans,
      S2MActorData data,S2MTerrainSettings terrain,S2MActorRenderSettings renderSettings,PopulationSettings settings,
      SimulationClock clock,float unitScale=1,IReadOnlyList<LaborJob> jobs=null,HorseCartAssemblySettings assembly=null)
    {
      if(data.Issues.Count>0)throw new InvalidOperationException("Recruitment animation assets failed: "+string.Join("; ",data.Issues));
      this.map=map; this.terrain=terrain; this.clock=clock; scale=renderSettings.ModelScale*unitScale;
      this.settings=settings;
      presentations=new LaborPresentationCatalog(assembly);
      float standDuration=0;
      foreach(var batch in data.Batches)
        if(string.Equals(batch.Asset.AnimationPath,S2MCampfirePopulationLoader.StandClip,StringComparison.OrdinalIgnoreCase))standDuration=batch.Motion.Duration;
      if(!(standDuration>0) && plans.Count>0)throw new InvalidOperationException("Stand-up animation is unavailable.");
      actors=S2MActorRenderer.Render(data,transform,terrain,renderSettings,unitScale); actors.name="Recruiting Peasants"; actors.Clock=clock;
      foreach(var plan in plans)
      {
        kingdoms.Add(new PeasantRecruitment(plan,navigation,settings,standDuration));
        foreach(var p in plan)actors.HideActor(p.Id);
      }
      if(jobs!=null)
      {
        Labor=new LaborManager(jobs,kingdoms,navigation,settings);
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
          if(p.State==PeasantState.Unspawned)continue;
          var presentation=p.Job==null ? null : presentations.Get(p.Job.Role);
          string variant=presentation?.Variant(p.Worker);
          var signature=(p.Job?.Role??default,variant,presentation?.AnimationRevision(p.Worker)??0);
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
            float rate=presentation?.AnimationRate(p.Worker,settings.WalkSpeedCellsPerSecond)??1;
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
            height=p.Job.RawHeight;
            ShowEquipment(p.Job);
          }
          // Hovel interiors use ground height, rather than the roof/stair navigation surface.
          if(p.State==PeasantState.HovelSeated || p.State==PeasantState.StandingUp || p.State==PeasantState.LeavingHovel)
            height=S2MCampfireLayout.GroundHeight(map,p.Position.X,p.Position.Z);
          actors.SetTransform(p.Id,new Vector3(x*terrain.HorizontalCellSize,height*terrain.HeightUnitScale,z*terrain.HorizontalCellSize),
            Quaternion.Euler(0,yaw,0),Vector3.one*scale);
        }
      }
    }
    private void ShowEquipment(LaborJob job)
    {
      foreach(var equipment in presentations.Get(job.Role).Equipment(job))
      {
        if(shownEquipment.Add(equipment.ActorId) && !actors.SetVariant(equipment.ActorId,equipment.Variant))
          throw new InvalidOperationException("Missing labor equipment "+equipment.Variant);
        float x=equipment.Position.X,z=equipment.Position.Z,yaw=equipment.Heading;
        if(terrain.FlipX){x=map.MapSize-x;yaw=-yaw;}if(terrain.FlipZ){z=map.MapSize-z;yaw=180-yaw;}
        actors.SetTransform(equipment.ActorId,new Vector3(x*terrain.HorizontalCellSize,equipment.RawHeight*terrain.HeightUnitScale,z*terrain.HorizontalCellSize),Quaternion.Euler(0,yaw,0),Vector3.one*scale);
      }
    }
  }
}
