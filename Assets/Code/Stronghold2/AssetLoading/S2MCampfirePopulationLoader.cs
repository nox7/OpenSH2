using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Simulation.Population;
using Assets.Code.Simulation.Labor;
using Assets.Code.Stronghold2.ModelRendering.Labor;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class S2MCampfirePopulationLoader
  {
    public const string AnimationFolder = "meshes/units/animations/worker/";
    public const string HovelClip = AnimationFolder + "hovel_peasant.GR2";
    public const string StandClip = AnimationFolder + "church_standup.GR2";
    public const string RunClip = AnimationFolder + "worker_run.GR2";
    public const string WaitClip = AnimationFolder + "worker_idle.GR2";
    public static string SeatClip(int id) => AnimationFolder + $"peasant_campfire{(-id-1)%3+1}.GR2";

    public static Task<S2MActorData> LoadRecruitsAsync(S2MFile map, string installation,
      IReadOnlyList<IReadOnlyList<Peasant>> plans, int samplesPerSecond = 15, string converterPath = null, string cacheDirectory = null,
      IReadOnlyList<LaborJob> jobs = null,HorseCartAssemblySettings assembly = null)
    {
      return new S2MActorAssetLoader().LoadAsync(map,installation,Overrides(assembly),samplesPerSecond,converterPath,cacheDirectory,CreateRecruitPlacements(plans,jobs,assembly));
    }

    public static IReadOnlyList<ActorPlacement> CreateRecruitPlacements(IReadOnlyList<IReadOnlyList<Peasant>> plans,IReadOnlyList<LaborJob> jobs=null,HorseCartAssemblySettings assembly=null)
    {
      var result=new List<ActorPlacement>();
      foreach(var plan in plans)foreach(var p in plan)
        foreach(string type in new[] { "CampfirePeasant"+(-p.Id-1)%3,"HovelRecruit","StandingRecruit","RunningRecruit","WaitingRecruit" })
          result.Add(new ActorPlacement { Id=p.Id,Type=type,RawX=p.Spawn.X*1024,RawZ=p.Spawn.Z*1024,OnMap=true,AnimationLoops=true });
      if(jobs!=null && jobs.Count>0)
      {
        var catalog=new LaborPresentationCatalog(assembly);var roles=new HashSet<LaborRole>();
        foreach(var job in jobs)roles.Add(job.Role);
        foreach(var role in roles)foreach(var plan in plans)foreach(var p in plan)
          result.AddRange(catalog.Get(role).WorkerPlacements(p));
        foreach(var job in jobs)foreach(var equipment in catalog.Get(job.Role).Equipment(job))result.Add(equipment.Placement());
      }
      return result;
    }
    /// <summary>Static seated preview for asset validation. Gameplay uses LoadRecruitsAsync.</summary>
    public static Task<S2MActorData> LoadAsync(S2MFile map, string installation, int samplesPerSecond = 15,
      string converterPath = null, string cacheDirectory = null)
    {
      return new S2MActorAssetLoader().LoadAsync(map, installation, Overrides(), samplesPerSecond,
        converterPath, cacheDirectory, CreatePlacements(map));
    }

    public static IReadOnlyList<ActorPlacement> CreatePlacements(S2MFile map)
    {
      var actors = new List<ActorPlacement>();
      foreach (var camp in S2MCampfireLayout.Create(map))
        foreach (var seat in camp.Population.Seats)
        {
          float x = camp.Center.x + seat.X, z = camp.Center.y + seat.Z;
          if (x < 0 || z < 0 || x >= map.MapSize || z >= map.MapSize) continue;
          actors.Add(new ActorPlacement { Id = -1 - actors.Count, Type = "CampfirePeasant" + actors.Count % 3,
            RawX = x * 1024, RawZ = z * 1024, RawY = S2MCampfireLayout.GroundHeight(map, x, z),
            HeadingDegrees = seat.HeadingDegrees, OnMap = true, AnimationLoops = true });
        }
      return actors;
    }

    public static IEnumerable<S2MActorOverride> Overrides(HorseCartAssemblySettings assembly=null)
    {
      for (int i = 0; i < 3; i++) yield return new S2MActorOverride { ObjectType = "CampfirePeasant" + i,
        ModelPath = "meshes/units/models/peasant.GR2",
        // Original seated models face local -Z; simulation headings use +Z forward.
        LocalEulerAngles = new UnityEngine.Vector3(0, 180, 0),
        AnimationPath = $"meshes/units/animations/worker/peasant_campfire{i + 1}.GR2" };
      foreach(var pair in new[] { ("HovelRecruit",HovelClip),("StandingRecruit",StandClip),("RunningRecruit",RunClip),("WaitingRecruit",WaitClip) })
        yield return new S2MActorOverride { ObjectType=pair.Item1,ModelPath="meshes/units/models/peasant.GR2",
          AnimationPath=pair.Item2,LocalEulerAngles=new UnityEngine.Vector3(0,180,0) };
      foreach(var definition in new LaborPresentationCatalog(assembly).Definitions)
        foreach(var asset in definition.AssetOverrides())yield return asset;
    }
  }
}
