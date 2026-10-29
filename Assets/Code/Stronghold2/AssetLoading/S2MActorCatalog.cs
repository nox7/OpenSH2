using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.AssetLoading
{
  [Serializable]
  public class S2MActorPart
  {
    public string DefinitionPath;
    public string ModelPath;
    public string AnimationPath;
    public string AnimationAction;
    public bool SynchronizedPlayback;
    public float Scale = 1;
    public Vector3 LocalOffset;
    public Vector3 LocalEulerAngles;
    public Vector3 SpinAxis;
    public bool HoldPose;
    public bool UseBindPose;
    public bool DisableDefinitionAttachments;
    public string Variant;
    [Tooltip("-2 reads the definition; -1 disables default team tint; otherwise a triangle-group index.")]
    public int TeamGroup = -2;
    public S2MActorAttachment[] Attachments = Array.Empty<S2MActorAttachment>();
  }

  [Serializable]
  public sealed class S2MActorOverride : S2MActorPart
  {
    public string ObjectType;
    public S2MActorPart[] Parts = Array.Empty<S2MActorPart>();
  }

  [Serializable]
  public sealed class S2MActorAttachment
  {
    public string ModelPath;
    public int Point = 24;
    public string BoneName;
  }

  public sealed class S2MActorAsset
  {
    public int TeamGroup { get; internal set; } = -1;
    public string ModelPath { get; internal set; }
    public string AnimationPath { get; internal set; }
    public string Action { get; internal set; }
    public string Variant { get; internal set; }
    public float Scale { get; internal set; } = 1;
    public bool HoldPose { get; internal set; }
    public bool UseBindPose { get; internal set; }
    public bool SynchronizedPlayback { get; internal set; }
    public Vector3 LocalOffset { get; internal set; }
    public Vector3 LocalEulerAngles { get; internal set; }
    public Vector3 SpinAxis { get; internal set; }
    public List<S2MActorAttachment> Attachments { get; } = new();
    public string Key { get; internal set; }
  }

  /// <summary>Discovers game definitions; explicit overrides take precedence over aliases and discovery.</summary>
  public sealed class S2MActorCatalog
  {
    private readonly string root;
    private readonly Dictionary<string, string> definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, S2MActorOverride> overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Definition> parsed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string type, int animation, bool mounted), S2MActorAsset> resolved = new();
    private readonly Dictionary<(string type, int animation, bool mounted), IReadOnlyList<S2MActorAsset>> assemblies = new();
    private readonly Dictionary<string, ClipEntry> animations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] Families = { "child", "teenager", "worker", "soldier", "hero" };
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
      ["Dog"] = "hunters_dog", ["Deer"] = "deer_stag", ["WarHound"] = "war_hound",
      ["Lady"] = "noble_lady", ["HorseCart"] = "cartman", ["AxeThrower"] = "axeman",
      ["Assassin"] = "assasin", ["Thief"] = "thief_merc", ["WorkerMonk"] = "monk",
      ["ConstructionMonk"] = "monk", ["Judge"] = "bishop", ["Engineer"] = "engineer_guild",
      ["LightCavalryHorse"] = "horse_light_cavalry", ["HorseArcherHorse"] = "horse_horse_archer",
      ["JoustingHorse"] = "horse_knight", ["JoustingKnight"] = "knight_jousting",
      ["WoodCutter"] = "woodcutter", ["FairgroundActor"] = "actor"
    };

    public S2MActorCatalog(string gameRoot, IEnumerable<S2MActorOverride> custom = null,HorseCartAssemblySettings assembly=null)
    {
      assembly??=new HorseCartAssemblySettings();assembly.Validate();
      root = Path.GetFullPath(gameRoot);
      string folder = Path.Combine(root, "meshes", "units");
      foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        if (Path.GetExtension(file).Equals(".uni", StringComparison.OrdinalIgnoreCase)
          || Path.GetExtension(file).Equals(".aml", StringComparison.OrdinalIgnoreCase))
          {
            string key = Normalize(Path.GetFileNameWithoutExtension(file));
            if (!definitions.ContainsKey(key) || Path.GetExtension(file).Equals(".uni", StringComparison.OrdinalIgnoreCase)) definitions[key] = Relative(file);
          }
      if (custom != null) foreach (var entry in custom)
        if (entry != null && !string.IsNullOrWhiteSpace(entry.ObjectType)) overrides[entry.ObjectType] = entry;
      ReadAnimations(Path.Combine(folder, "AnimList.anl"));
      if (!overrides.ContainsKey("HorseCart")) overrides["HorseCart"] = new S2MActorOverride
      {
        ObjectType = "HorseCart", DefinitionPath = "meshes/units/cartman.uni",
        AnimationPath = "meshes/units/animations/worker/woodcutter_riding_cart.GR2",
        LocalOffset = new Vector3(0,0,-.7f), LocalEulerAngles = new Vector3(0,180,0), HoldPose = true,
        Parts = new[] {
          new S2MActorPart { DefinitionPath = "meshes/units/animals/cart_horse.aml", LocalOffset = new Vector3(0,0,assembly.HorseForwardOffset), LocalEulerAngles = new Vector3(0,180,0), HoldPose = true },
          S2MWoodcutterAssets.CartBody(),
          S2MWoodcutterAssets.CartWheels()
        }
      };
    }

    public S2MActorAsset Resolve(ActorPlacement actor)
    {
      if (resolved.TryGetValue((actor.Type, actor.AnimationId, actor.IsMounted), out var cached)) return cached;
      var custom = OverrideFor(actor);
      var asset = ResolveCore(actor, custom);
      resolved[(actor.Type, actor.AnimationId, actor.IsMounted)] = asset;
      return asset;
    }

    public IReadOnlyList<S2MActorAsset> ResolveParts(ActorPlacement actor)
    {
      if (assemblies.TryGetValue((actor.Type, actor.AnimationId, actor.IsMounted), out var cached)) return cached;
      var parts = new List<S2MActorAsset> { Resolve(actor) };
      var custom = OverrideFor(actor);
      if (custom?.Parts != null)
        for (int i = 0; i < custom.Parts.Length; i++)
        {
          if (custom.Parts[i] == null) continue;
          var part = ResolveCore(actor, custom.Parts[i]); part.Key += "|" + actor.Type + ":part:" + i;
          parts.Add(part);
        }
      assemblies[(actor.Type, actor.AnimationId, actor.IsMounted)] = parts;
      return parts;
    }

    private S2MActorOverride OverrideFor(ActorPlacement actor)
    {
      if (actor.Type == "Knight" && actor.IsMounted)
      {
        if (overrides.TryGetValue("Knight:Mounted", out var mounted)) return mounted;
        if (overrides.TryGetValue("Knight", out var sharedOverride)) return sharedOverride;
        // These models share their ground origin. Riding clips contain the saddle
        // height; applying a separate rider lift would count it twice.
        return new S2MActorOverride {
          ObjectType = "Knight:Mounted", DefinitionPath = "meshes/units/knight.uni",
          AnimationAction = MountedRiderAction(actor.AnimationId), SynchronizedPlayback = true,
          Parts = new[] { new S2MActorPart {
            DefinitionPath = "meshes/units/animals/horse_knight.aml",
            AnimationAction = MountedHorseAction(actor.AnimationId), SynchronizedPlayback = true
          } }
        };
      }
      overrides.TryGetValue(actor.Type, out var custom);
      return custom;
    }

    private static string MountedRiderAction(int id) => id switch
    {
      1 or 461 => "KNIGHT HRS WALK",
      2 or 647 => "KNIGHT HRS CANTER",
      460 or 931 => "KNIGHT HRS IDLE 2",
      456 or 928 => "KNIGHT HRS ATK 1",
      457 or 929 => "KNIGHT HRS ATK 2",
      646 => "KNIGHT HRS ATK LFT",
      648 => "KNIGHT HRS CANTER A",
      462 => "KNIGHT HRS WALK ATK",
      464 => "KNIGHT HRS REARUP",
      458 => "KNIGHT HRS FALL",
      _ => "KNIGHT HRS IDLE"
    };

    private static string MountedHorseAction(int id) => id switch
    {
      1 or 461 or 462 => "WALK",
      2 or 647 or 648 => "RUN",
      464 => "REAR UP",
      458 => "DIE HIT",
      _ => "IDLE"
    };

    private S2MActorAsset ResolveCore(ActorPlacement actor, S2MActorPart custom)
    {
      string path = custom?.DefinitionPath;
      if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(custom?.ModelPath))
      {
        string name = Aliases.TryGetValue(actor.Type, out string alias) ? alias : actor.Type;
        definitions.TryGetValue(Normalize(name), out path);
      }
      Definition definition = null;
      if (!string.IsNullOrWhiteSpace(path))
      {
        if (!parsed.TryGetValue(path, out definition)) parsed[path] = definition = ReadDefinition(path);
      }
      string model = custom?.ModelPath;
      bool siege = definition == null && (actor.Type == "Ballista" || actor.Type == "FireBallista" || actor.Type == "Mangonel");
      if (siege && string.IsNullOrWhiteSpace(model)) model = "meshes/siege_equipment/" + (actor.Type == "Mangonel" ? "mangonel.GR2" : "ballista_one_mesh.GR2");
      if (string.IsNullOrWhiteSpace(model)) model = definition?.Model;
      if (string.IsNullOrWhiteSpace(model)) throw new InvalidDataException("No actor definition/model mapping.");
      string clip = custom?.AnimationPath, action = "override";
      if (definition == null && string.IsNullOrWhiteSpace(clip)) { clip = model; if (siege) action = "placed siege pose"; }
      ClipEntry selected = null;
      if (string.IsNullOrWhiteSpace(clip) && !string.IsNullOrWhiteSpace(custom?.AnimationAction) && definition != null)
      {
        foreach (var entry in definition.Clips)
          if (entry.Path != null && string.Equals(entry.Action, custom.AnimationAction, StringComparison.OrdinalIgnoreCase)) selected = entry;
        if (selected == null) animations.TryGetValue(definition.Family + ":" + custom.AnimationAction, out selected);
        if (selected == null) throw new InvalidDataException("Animation action not available: " + custom.AnimationAction);
        action = selected.Action; clip = selected.Path;
      }
      if (string.IsNullOrWhiteSpace(clip) && definition != null)
      {
        int index = actor.AnimationId - 1;
        if (index >= 0 && index < definition.Clips.Count) selected = definition.Clips[index];
        if (selected != null) { action = selected.Action; clip = selected.Path; }
        if (string.IsNullOrWhiteSpace(clip))
        {
          foreach (var candidate in definition.Clips)
            if (candidate.Action.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 && candidate.Path != null)
            { selected = candidate; action = candidate.Action; clip = candidate.Path; break; }
        }
        if (string.IsNullOrWhiteSpace(clip)) foreach (var candidate in definition.Clips)
          if (candidate.Path != null) { selected = candidate; action = candidate.Action; clip = candidate.Path; break; }
      }
      if (string.IsNullOrWhiteSpace(clip)) throw new InvalidDataException("No animation mapping.");
      SafePath(model); SafePath(clip);
      float scale = custom?.Scale ?? 1;
      if (!(scale > 0) || float.IsInfinity(scale)) throw new InvalidDataException("Actor scale must be finite and positive.");
      var asset = new S2MActorAsset { ModelPath = model, AnimationPath = clip, Action = action, Scale = scale, Variant = custom?.Variant,
        TeamGroup = custom != null && custom.TeamGroup != -2 ? custom.TeamGroup : definition?.TeamGroup ?? -1,
        SynchronizedPlayback = custom?.SynchronizedPlayback ?? false,
        HoldPose = (custom?.HoldPose ?? false) || (siege && string.IsNullOrWhiteSpace(custom?.AnimationPath)),
        UseBindPose = custom?.UseBindPose ?? false,
        LocalOffset = custom?.LocalOffset ?? Vector3.zero, LocalEulerAngles = custom?.LocalEulerAngles ?? Vector3.zero,
        SpinAxis = custom?.SpinAxis ?? Vector3.zero };
      for (int axis = 0; axis < 3; axis++)
        if (float.IsNaN(asset.LocalOffset[axis]) || float.IsInfinity(asset.LocalOffset[axis])
          || float.IsNaN(asset.LocalEulerAngles[axis]) || float.IsInfinity(asset.LocalEulerAngles[axis])
          || float.IsNaN(asset.SpinAxis[axis]) || float.IsInfinity(asset.SpinAxis[axis]))
          throw new InvalidDataException("Actor part transforms must be finite.");
      var key = new StringBuilder(model + "|" + clip + "|team=" + asset.TeamGroup);
      if (selected != null && definition != null && !(custom?.DisableDefinitionAttachments ?? false))
        for (int i = 0; i < definition.Attachments.Count && i < selected.Points.Length; i++)
        {
          if (selected.Points[i] < 0 || definition.Attachments[i].Length == 0) continue;
          string attachment = definition.Prefix + definition.Attachments[i];
          SafePath(attachment);
          asset.Attachments.Add(new S2MActorAttachment { ModelPath = attachment, Point = selected.Points[i] });
          key.Append('|').Append(attachment).Append(':').Append(selected.Points[i]);
        }
      if (custom?.Attachments != null) foreach (var attachment in custom.Attachments)
      {
        if (attachment == null || string.IsNullOrWhiteSpace(attachment.ModelPath)) continue;
        SafePath(attachment.ModelPath); asset.Attachments.Add(attachment);
        key.Append('|').Append(attachment.ModelPath).Append(':').Append(attachment.Point).Append(':').Append(attachment.BoneName);
      }
      key.Append("|variant:").Append(asset.Variant).Append("|sync:").Append(asset.SynchronizedPlayback).Append("|bind:").Append(asset.UseBindPose).Append("|pose:").Append(asset.HoldPose).Append('|').Append(asset.LocalOffset.ToString("R", CultureInfo.InvariantCulture))
        .Append('|').Append(asset.LocalEulerAngles.ToString("R", CultureInfo.InvariantCulture))
        .Append('|').Append(asset.SpinAxis.ToString("R", CultureInfo.InvariantCulture));
      asset.Key = key.ToString();
      return asset;
    }

    public string SafePath(string relative)
    {
      string path = Path.GetFullPath(Path.Combine(root, relative));
      if (Path.IsPathRooted(relative) || !path.StartsWith(root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Actor paths must be relative to the game installation.");
      if (!File.Exists(path)) throw new FileNotFoundException("Actor asset does not exist.", path);
      return path;
    }

    private Definition ReadDefinition(string path)
    {
      bool animal = Path.GetExtension(path).Equals(".aml", StringComparison.OrdinalIgnoreCase);
      using var r = new BinaryReader(File.OpenRead(SafePath(path)), Encoding.ASCII);
      int version = r.ReadInt32();
      if (version < (animal ? 1 : 4) || version > 100) throw new InvalidDataException("Unsupported actor definition version.");
      int family = animal ? 5 : r.ReadInt32();
      if (animal && version > 1) r.ReadInt32();
      string model = Name(r, 32);
      string prefix = animal ? "meshes/units/animals/" : "meshes/units/models/";
      var result = new Definition { Model = prefix + model, Prefix = prefix, Family = family };
      if (animal) for (int i = 0; i < AnimalActions.Length; i++) result.Clips.Add(new ClipEntry { Action = AnimalActions[i] });
      int attachments = Count(r, 128);
      for (int i = 0; i < attachments; i++) result.Attachments.Add(Name(r, 32));
      int slots = Count(r, 10000);
      for (int i = 0; i < slots; i++)
      {
        string action;
        ClipEntry entry = null;
        if (animal)
        {
          string file = Name(r, version == 1 ? 32 : 64);
          action = Name(r, 16);
          var points = new int[attachments];
          for (int a = 0; a < attachments; a++) points[a] = r.ReadInt32();
          entry = new ClipEntry { Action = action, Path = file.Length > 0 ? prefix + file : null, Points = points };
        }
        else
        {
          action = Name(r, 20); int id = r.ReadInt32();
          if (version < 6) Exact(r, attachments);
          if (id >= 0 && action.Length > 0) animations.TryGetValue(family + ":" + action, out entry);
        }
        if (animal)
        {
          int actionIndex = Array.FindIndex(AnimalActions, a => a.Equals(action, StringComparison.OrdinalIgnoreCase));
          if (action == "IDLE LAY") actionIndex = 19;
          if (actionIndex >= 0 && entry != null) result.Clips[actionIndex] = entry;
        }
        else result.Clips.Add(entry ?? new ClipEntry { Action = action });
      }
      // Version-4 definitions (including ox_handler.uni) end after the action slots.
      // The team-group trailer was introduced in version 5.
      if (!animal && version >= 5) result.TeamGroup = r.ReadInt32();
      return result;
    }

    private void ReadAnimations(string path)
    {
      using var r = new BinaryReader(File.OpenRead(path), Encoding.ASCII);
      int version = r.ReadInt32(), groups = Count(r, 32), defaultCount = Count(r, 100000);
      int slots = version > 3 ? Count(r, 128) : 8;
      for (int g = 0; g < groups; g++)
      {
        int count = version > 3 ? Count(r, 100000) : defaultCount;
        for (int i = 0; i < count; i++)
        {
          string file = Name(r, 64), action = Name(r, 20);
          var points = new int[slots];
          for (int s = 0; s < slots; s++) points[s] = r.ReadInt32();
          if (g < Families.Length && file.Length > 0)
            animations[g + ":" + action] = new ClipEntry { Path = "meshes/units/animations/" + Families[g] + "/" + file, Action = action, Points = points };
        }
      }
      if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Unexpected AnimList data.");
    }

    private string Relative(string path) => path.Substring(root.TrimEnd('\\', '/').Length + 1);
    private static string Normalize(string s) => s.Replace("_", "").Replace(" ", "").ToLowerInvariant();
    private static int Count(BinaryReader r, int max) { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("Invalid actor table count."); return n; }
    private static byte[] Exact(BinaryReader r, int n) { byte[] b = r.ReadBytes(n); if (b.Length != n) throw new EndOfStreamException(); return b; }
    private static string Name(BinaryReader r, int n) => Encoding.ASCII.GetString(Exact(r, n)).Split('\0')[0];
    // Saved animal animation identifiers are action numbers, not AML entry order.
    private static readonly string[] AnimalActions = {
      "ALERT", "ATTACK", "BEING MILKED", "DANCE", "DIE ARROW", "DIE HIT", "DIE FALLING", "DIE LANDING", "DIE SWORD",
      "DIVE IN", "DIVE OUT", "EAT", "FLYING", "GOOSE SWIM", "HIDE", "HIDE CHICKEN", "WALK CHICKEN", "IDLE", "IDLE 2",
      "IDLE LAY DOWN", "JUMP", "LANDING", "MUD ROLL", "PEE START", "PEE END", "PROWL", "RUN", "SWIM", "SIT", "STAND SIT",
      "TAKE OFF", "PECK", "WALK", "WALK PHEASANT", "RUN PHEASANT", "IDLE PHEASANT", "TRAD WATER", "SLEEP 2", "FLY",
      "ATTACK FALCONER", "LANDING FALCONE", "IDLE FALCONER", "WALK FALCONER", "IDLE POST", "SHEEP SHEARING", "GRAZING",
      "STAND", "REAR UP", "IDLE LOOK", "IDLE HOOVES", "FLAG", "WALK RABBIT", "FLAP", "LIE DOWN", "SNARL", "PEE"
    };
    private sealed class ClipEntry
    {
      public string Action, Path;
      public int[] Points = Array.Empty<int>();
    }
    private sealed class Definition
    {
      public int TeamGroup = -1;
      public int Family;
      public string Model;
      public string Prefix;
      public readonly List<string> Attachments = new();
      public readonly List<ClipEntry> Clips = new();
    }
  }
}
