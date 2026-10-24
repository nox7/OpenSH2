using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class S2MPlacedBuildingBatch
  {
    public S2MBuildingDescriptor Descriptor { get; internal set; }
    public IReadOnlyList<BuildingPlacement> Placements { get; internal set; }
    public Granny2ModelData Closed { get; internal set; }
    public Granny2ModelData Open { get; internal set; }
    public Granny2ModelData Collision { get; internal set; }
    public IReadOnlyList<Granny2ModelData> StaticParts { get; internal set; } = Array.Empty<Granny2ModelData>();
  }

  public sealed class S2MBuildingAssetIssue
  {
    public int ObjectId { get; internal set; }
    public string ObjectType { get; internal set; }
    public string Reason { get; internal set; }
  }

  /// <summary>One data-driven loader for all ordinary BDG-backed placed buildings.</summary>
  internal sealed class S2MPlacedBuildingAssetLoader
  {
    private sealed class Plan
    {
      public S2MBuildingDescriptor Descriptor;
      public List<BuildingPlacement> Placements;
    }

    public IReadOnlyList<S2MPlacedBuildingBatch> Load(S2MFile map, string gameInstallPath,
      string converterPath, string cacheDirectory, List<S2MBuildingAssetIssue> issues)
    {
      var payloads = new Dictionary<int, byte[]>();
      var placementsById = new Dictionary<int, BuildingPlacement>();
      foreach (BuildingPlacement placement in map.BuildingPlacements) placementsById[placement.Id] = placement;
      foreach (S2Object obj in map.S2GameObjects)
        if (obj is RawS2Object raw) payloads[obj.Id] = raw.Payload;
      var groups = new Dictionary<string, List<BuildingPlacement>>(StringComparer.OrdinalIgnoreCase);
      foreach (BuildingPlacement placement in map.BuildingPlacements)
      {
        if (placement.Type == "Keep1" || placement.Type == "Keep2"
          || placement.Type == "Keep3" || placement.Type == "Keep4") continue;
        payloads.TryGetValue(placement.Id, out byte[] payload);
        if (!S2MBuildingAssetCatalog.TryGetDescriptor(placement, payload, out string path, out string reason))
        {
          issues.Add(Issue(placement, reason));
          continue;
        }
        if (!groups.TryGetValue(path, out List<BuildingPlacement> group))
          groups.Add(path, group = new List<BuildingPlacement>());
        group.Add(placement);
      }
      var plans = new List<Plan>();
      var paths = new List<string>();
      foreach (KeyValuePair<string, List<BuildingPlacement>> group in groups)
      {
        try
        {
          S2MBuildingDescriptor descriptor = S2MBuildingDescriptorReader.Read(gameInstallPath, group.Key);
          var placements = new List<BuildingPlacement>();
          foreach (BuildingPlacement placement in group.Value)
          {
            int w = descriptor.Width, h = descriptor.Height;
            int quarter = (int)Math.Round(placement.RotationDegrees / 90d);
            int mapWidth = quarter % 2 != 0 ? placement.FootprintHeight : placement.FootprintWidth;
            int mapHeight = quarter % 2 != 0 ? placement.FootprintWidth : placement.FootprintHeight;
            // The map's reserved placement area can exceed the visible model
            // (saw pit 1x4 versus 1x2). Composite buildings can also reserve
            // space for a linked half. Preserve dimensions and model scale.
            // A kitchen's pantry has a 1x1 logical marker, but its 3x4 mesh
            // occupies the remaining portion of the kitchen's full 7x4 area.
            // Only accept this exception for a decoded reciprocal association.
            bool linkedPantry = placement.Type == "ServantsQuarters"
              && placement.LinkedBuildingId.HasValue
              && placementsById.TryGetValue(placement.LinkedBuildingId.Value, out BuildingPlacement kitchen)
              && kitchen.Type == "Kitchen" && kitchen.LinkedBuildingId == placement.Id;
            if ((mapWidth >= w && mapHeight >= h) || linkedPantry) placements.Add(placement);
            else if (placement.Type == "ServantsQuarters")
              issues.Add(Issue(placement, "ServantsQuarters has no verified kitchen association; its 1x1 marker does not define a standalone pantry placement."));
            else issues.Add(Issue(placement, $"Descriptor footprint {w}x{h} differs from map footprint {placement.FootprintWidth}x{placement.FootprintHeight}: {group.Key}."));
          }
          if (placements.Count == 0) continue;
          plans.Add(new Plan { Descriptor = descriptor, Placements = placements });
          paths.Add(descriptor.ClosedModel);
          if (descriptor.OpenModel != null) paths.Add(descriptor.OpenModel);
          if (descriptor.CollisionModel != null) paths.Add(descriptor.CollisionModel);
          foreach (string part in S2MBuildingAssetCatalog.GetStaticParts(placements[0].Type))
            paths.Add("meshes/buildings/" + part);
        }
        catch (Exception exception) when (IsAssetError(exception))
        {
          foreach (BuildingPlacement placement in group.Value)
            issues.Add(Issue(placement, $"{group.Key}: {exception.Message}"));
        }
      }
      var models = Granny2AssetConverter.LoadOrConvertMany(gameInstallPath, paths, converterPath, cacheDirectory);
      var batches = new List<S2MPlacedBuildingBatch>();
      foreach (Plan plan in plans)
      {
        S2MBuildingDescriptor descriptor = plan.Descriptor;
        List<BuildingPlacement> placements = plan.Placements;
        try
        {
          var batch = new S2MPlacedBuildingBatch
          {
            Descriptor = descriptor, Placements = placements, Closed = LoadModel(descriptor.ClosedModel)
          };
          batch.Open = LoadOptional(descriptor.OpenModel, placements);
          batch.Collision = LoadOptional(descriptor.CollisionModel, placements);
          var parts = new List<Granny2ModelData>();
          foreach (string part in S2MBuildingAssetCatalog.GetStaticParts(placements[0].Type))
          {
            Granny2ModelData model = LoadOptional("meshes/buildings/" + part, placements);
            if (model != null) parts.Add(model);
          }
          batch.StaticParts = parts;
          batches.Add(batch);
        }
        catch (Exception exception) when (IsAssetError(exception))
        {
          foreach (BuildingPlacement placement in placements)
            issues.Add(Issue(placement, $"{descriptor.RelativePath}: {exception.Message}"));
        }
      }
      return batches;

      Granny2ModelData LoadModel(string path)
      {
        Granny2AssetLoadResult result = models[path];
        if (result.Error != null) throw result.Error;
        if (result.Model.Meshes.Count == 0) throw new InvalidDataException($"No geometry in {path}.");
        return result.Model;
      }

      Granny2ModelData LoadOptional(string path, List<BuildingPlacement> placements)
      {
        if (path == null) return null;
        try { return LoadModel(path); }
        catch (Exception exception) when (IsAssetError(exception))
        {
          foreach (BuildingPlacement placement in placements)
            issues.Add(Issue(placement, $"Optional asset {path}: {exception.Message}"));
          return null;
        }
      }
    }

    private static bool IsAssetError(Exception exception) => exception is IOException || exception is InvalidDataException
      || exception is InvalidOperationException || exception is ArgumentException || exception is TimeoutException;

    private static S2MBuildingAssetIssue Issue(BuildingPlacement placement, string reason) => new()
    {
      ObjectId = placement.Id, ObjectType = placement.Type, Reason = reason
    };
  }
}
