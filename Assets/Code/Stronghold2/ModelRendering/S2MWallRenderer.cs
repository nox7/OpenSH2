using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.ModelRendering
{
  [Serializable]
  public sealed class S2MWallRenderSettings
  {
    [Tooltip("Generate parapets and merlons along the saved top-edge mask. Dimensions are preliminary visual calibration.")]
    public bool RenderBattlements = true;
    [Min(0f)] public float StoneParapetHeight = 0.22f;
    [Min(0f)] public float StoneMerlonHeight = 0.35f;
    [Tooltip("Stone parapet thickness across the wall edge, in cells.")]
    [Min(0.01f)] public float ParapetWidth = 0.30f;
    [Tooltip("Inner edge of the coping is slightly higher than its outer edge.")]
    [Min(0f)] public float StoneCapSlope = 0.035f;
    [Min(0.01f)] public float WoodParapetWidth = 0.25f;
    [Min(0f)] public float WoodParapetHeight = 0.18f;
    [Tooltip("Pointed end-grain tops, calibrated against the wooden gate's six-sided spikes.")]
    [Min(0f)] public float WoodSpikeHeight = 0.50f;
  }

  /// <summary>
  /// Builds walls from Map tag 14 cell polygons and edge masks, retaining the saved
  /// four heights. Bodies and battlements are generated separately; destruction
  /// animation/health is not inferred from the shape enum.
  /// </summary>
  public static class S2MWallRenderer
  {
    private static readonly int[][] Polygons =
    {
      Array.Empty<int>(), new[] { 2, 0, 1, 3 }, new[] { 2, 0, 1 },
      new[] { 2, 1, 3 }, new[] { 2, 0, 3 }, new[] { 0, 1, 3 }
    };
    // Winding is upward for tops, outward for sides (X right, Z forward).
    private static readonly int[] EdgeStart = { 0, 1, 3, 2, 1, 2, 0, 3 };
    private static readonly int[] EdgeEnd =   { 1, 3, 2, 0, 2, 1, 3, 0 };
    private static readonly Vector2[] Corners =
    {
      new(0f, 1f), new(1f, 1f), new(0f, 0f), new(1f, 0f)
    };
    private static readonly string[] TextureNames =
    {
      "wall.dds", "walltop.DDS", "crenelations.dds",
      "wood_walls.DDS", "walltop_wood.DDS", "wood_walls_top.dds"
    };

    public static GameObject Render(S2MFile map, string gameInstallPath, Transform parent = null,
      S2MTerrainSettings terrainSettings = null, S2MWallRenderSettings settings = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      terrainSettings ??= new S2MTerrainSettings();
      settings ??= new S2MWallRenderSettings();
      if (!Positive(terrainSettings.HorizontalCellSize) || !Positive(terrainSettings.HeightUnitScale)
        || terrainSettings.CellsPerChunk <= 0 || !Positive(settings.ParapetWidth)
        || !Positive(settings.WoodParapetWidth)
        || !Nonnegative(settings.StoneCapSlope) || !Nonnegative(settings.WoodSpikeHeight)
        || !Nonnegative(settings.WoodParapetHeight)
        || !Nonnegative(settings.StoneParapetHeight) || !Nonnegative(settings.StoneMerlonHeight))
        throw new ArgumentOutOfRangeException(nameof(settings), "Invalid wall render dimensions.");
      var root = new GameObject("S2M Walls");
      if (parent != null) root.transform.SetParent(parent, false);
      if (map.WallLayer == null) return root;
      try
      {
        HeightLayer heights = map.HeightLayer ?? throw new InvalidDataException("Walls require a ground height grid.");
        if (heights.Width > map.WallLayer.Width || heights.Height > map.WallLayer.Height)
          throw new InvalidDataException("Wall backing grid is smaller than playable terrain.");
        int count = 0, unsupported = 0;
        for (int z = 0; z < heights.Height; z++)
          for (int x = 0; x < heights.Width; x++)
          {
            WallCell cell = map.WallLayer.GetCell(x, z);
            if (cell.HasWall) count++;
            else if (cell.Shape != 0) unsupported++;
          }
        if (unsupported > 0) Debug.LogWarning($"Skipped {unsupported} wall cells with undecoded shapes.");
        if (count == 0) return root; // Do not load assets for an empty layer.

        var resources = root.AddComponent<Granny2GeneratedModelResources>();
        Material[] materials = CreateMaterials(gameInstallPath, resources);
        var parapets = settings.RenderBattlements ? new ParapetLayout(map.WallLayer, heights, terrainSettings, settings) : null;
        for (int z = 0; z < heights.Height; z += terrainSettings.CellsPerChunk)
          for (int x = 0; x < heights.Width; x += terrainSettings.CellsPerChunk)
          {
            var mesh = new WallMesh();
            for (int cz = z; cz < Math.Min(z + terrainSettings.CellsPerChunk, heights.Height); cz++)
              for (int cx = x; cx < Math.Min(x + terrainSettings.CellsPerChunk, heights.Width); cx++)
              {
                WallCell cell = map.WallLayer.GetCell(cx, cz);
                if (cell.HasWall) AddCell(mesh, cell, map.WallLayer, heights, cx, cz, terrainSettings, settings, parapets);
              }
            if (mesh.Vertices.Count == 0) continue;
            Mesh generated = mesh.Build($"Wall chunk {x},{z}");
            resources.Add(generated);
            var chunk = new GameObject(generated.name);
            chunk.transform.SetParent(root.transform, false);
            chunk.AddComponent<MeshFilter>().sharedMesh = generated;
            chunk.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (terrainSettings.GenerateMeshColliders) chunk.AddComponent<MeshCollider>().sharedMesh = generated;
          }
        // Same playable bounds and axis reflection as terrain/buildings/bridges.
        root.transform.localPosition = new Vector3(
          terrainSettings.FlipX ? heights.Width * terrainSettings.HorizontalCellSize : 0f, 0f,
          terrainSettings.FlipZ ? heights.Height * terrainSettings.HorizontalCellSize : 0f);
        root.transform.localScale = new Vector3(terrainSettings.FlipX ? -1f : 1f, 1f, terrainSettings.FlipZ ? -1f : 1f);
        Debug.Log($"Rendered {count} wall cells from Map tag 14.");
        return root;
      }
      catch
      {
        if (Application.isPlaying) UnityEngine.Object.Destroy(root);
        else UnityEngine.Object.DestroyImmediate(root);
        throw;
      }
    }

    private static Material[] CreateMaterials(string installation, Granny2GeneratedModelResources resources)
    {
      Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
      if (shader == null) throw new InvalidOperationException("No lit wall shader available.");
      var materials = new Material[TextureNames.Length];
      for (int i = 0; i < materials.Length; i++)
      {
        // Own wall copies because the original WallSides shader mirrors V; do
        // not change sampler state on textures shared by unrelated GR2 models.
        Texture2D texture = DdsTextureLoader.Load(Path.Combine(installation, "meshes", "buildings", TextureNames[i]));
        resources.Add(texture);
        if (i == 0 || i == 3) texture.wrapModeV = TextureWrapMode.Mirror;
        var material = new Material(shader) { name = $"S2M Wall {TextureNames[i]}" };
        resources.Add(material);
        Granny2ModelRenderer.ConfigureImportedOpaqueLighting(material);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
        materials[i] = material;
      }
      return materials;
    }

    private static void AddCell(WallMesh mesh, WallCell cell, WallLayer walls, HeightLayer ground, int x, int z,
      S2MTerrainSettings terrain, S2MWallRenderSettings settings, ParapetLayout parapets)
    {
      float pitch = terrain.HorizontalCellSize;
      float vertical = terrain.HeightUnitScale * 1024f;
      var top = new Vector3[4];
      var bottom = new Vector3[4];
      for (int c = 0; c < 4; c++)
      {
        int vx = x + (int)Corners[c].x, vz = z + (int)Corners[c].y;
        // Saved heights are already the walking surface, including building
        // joins. An extra slab/rise floats above their existing model decks.
        top[c] = new Vector3(vx * pitch, cell.GetHeight(c) * terrain.HeightUnitScale, vz * pitch);
        // The primary grid is the ground under the wall; secondary planes can
        // contain the wall's own walking surface and must not be used as a base.
        float baseHeight = ground.Heights[Math.Min(vz, ground.Height - 1) * ground.Width + Math.Min(vx, ground.Width - 1)];
        bottom[c] = new Vector3(top[c].x, Math.Min(top[c].y, baseHeight * terrain.HeightUnitScale), top[c].z);
      }
      int sideMaterial = cell.IsWood ? 3 : 0, topMaterial = cell.IsWood ? 4 : 1;
      int[] polygon = Polygons[cell.Shape];
      for (int i = 1; i + 1 < polygon.Length; i++)
        mesh.Triangle(top[polygon[0]], top[polygon[i]], top[polygon[i+1]], topMaterial, pitch);
      for (int e = 0; e < 8; e++)
      {
        int a = EdgeStart[e], b = EdgeEnd[e];
        // Only edges of this polygon are meaningful. Preserve masks rather than
        // filling every neighbor gap (which would seal gatehouse openings).
        if (!IsEdge(polygon, a, b)) continue;
        if ((cell.SideEdges & (1 << e)) != 0)
        {
          // Masonry/timber runs directly to the saved walk. Walkway textures
          // belong only on horizontal tops, never on an exterior riser band.
          mesh.Side(bottom[a], top[a], top[b], bottom[b], sideMaterial, pitch, vertical);
        }
        else if (e < 4)
        {
          // Saved masks omit shared faces, but adjacent cells can have distinct
          // corner heights. Close only the exposed portion above a real wall
          // neighbor; an absent wall can be an intentional gate/building join.
          int nx = x + (e == 1 ? 1 : e == 3 ? -1 : 0);
          int nz = z + (e == 0 ? 1 : e == 2 ? -1 : 0);
          if (nx >= 0 && nx < ground.Width && nz >= 0 && nz < ground.Height)
          {
            WallCell neighbor = walls.GetCell(nx, nz);
            int opposite = (e + 2) % 4;
            if (neighbor.HasWall && IsEdge(Polygons[neighbor.Shape], EdgeStart[opposite], EdgeEnd[opposite]))
            {
              Vector3 lowerA = top[a], lowerB = top[b];
              // Follow the neighbor's actual linear profile. Clamping each end
              // to ground would create an artificial ramp between endpoints and
              // can leave a hole where a sloping wall crosses the ground plane.
              lowerA.y = neighbor.GetHeight(EdgeEnd[opposite]) * terrain.HeightUnitScale;
              lowerB.y = neighbor.GetHeight(EdgeStart[opposite]) * terrain.HeightUnitScale;
              mesh.HeightDifference(lowerA, top[a], top[b], lowerB, sideMaterial, pitch, vertical);
            }
          }
        }
        if (settings.RenderBattlements && (cell.TopEdges & (1 << e)) != 0)
          AddBattlements(mesh, top[a], top[b], cell.IsWood, pitch, vertical, settings, parapets.Get(x, z, e));
      }
    }

    private static bool IsEdge(int[] polygon, int a, int b)
    {
      for (int i = 0; i < polygon.Length; i++)
        if (polygon[i] == a && polygon[(i + 1) % polygon.Length] == b) return true;
      return false;
    }

    private static void AddBattlements(WallMesh mesh, Vector3 a, Vector3 b, bool wood,
      float pitch, float vertical, S2MWallRenderSettings settings, ParapetEdge edge)
    {
      Vector3 along = b - a;
      Vector3 inwardDirection = Vector3.Cross(Vector3.up, along).normalized;
      Vector3 inward = inwardDirection * ((wood ? settings.WoodParapetWidth : settings.ParapetWidth) * pitch);
      float low = (wood ? settings.WoodParapetHeight : settings.StoneParapetHeight) * vertical;
      if (wood) mesh.Prism(a, b, inward, low, 3, 5, pitch, vertical);
      int pieces = Math.Max(1, Mathf.RoundToInt(new Vector2(along.x, along.z).magnitude / (pitch * (wood ? 0.25f : 0.5f))));
      for (int i = 0; i < pieces; i++)
      {
        if (wood)
        {
          Vector3 center = Vector3.Lerp(a, b, (i + 0.5f) / pieces) + inward * 0.5f + Vector3.up * low;
          float spacing = new Vector2(along.x, along.z).magnitude / pieces;
          float radius = Math.Min(inward.magnitude * 0.5f, spacing * 0.5f);
          // A repeatable small variation avoids a mechanically level sawtooth.
          float variation = 0.92f + 0.08f * Mathf.Sin(center.x * 13.3f / pitch + center.z * 7.1f / pitch);
          mesh.Spike(center, radius, settings.WoodSpikeHeight * vertical * variation,
            new Vector3(along.x, 0f, along.z).normalized);
        }
        else
        {
          Vector3 courseA = Vector3.Lerp(a, b, (float)i / pieces);
          Vector3 courseB = Vector3.Lerp(a, b, (float)(i + 1) / pieces);
          float courseHeight = Math.Max(courseA.y, courseB.y) + low;
          bool startCorner = i == 0 && edge?.StartCorner != null;
          bool endCorner = i == pieces - 1 && edge?.EndCorner != null;
          if (startCorner) courseHeight = edge.StartCorner.CourseHeight;
          if (endCorner) courseHeight = edge.EndCorner.CourseHeight;
          if (low > 0f)
            mesh.Ribbon(courseA, courseB,
              courseA + InnerOffset((float)i / pieces), courseB + InnerOffset((float)(i + 1) / pieces),
              courseHeight, 0f, 0, 2, pitch, vertical, !startCorner, !endCorner);
          if (settings.StoneMerlonHeight <= 0f) continue;

          // Keep the regular merlon's inward endpoint on each run. Extend only
          // toward the corner, so joining the cap does not enlarge its next gap.
          float startT = startCorner ? 0f : (i + 0.05f) / pieces;
          float endT = endCorner ? 1f : (i + 0.75f) / pieces;
          Vector3 start = Vector3.Lerp(a, b, startT);
          Vector3 end = Vector3.Lerp(a, b, endT);
          start.y = end.y = courseHeight;
          mesh.Ribbon(start, end, start + InnerOffset(startT), end + InnerOffset(endT),
            courseHeight + settings.StoneMerlonHeight * vertical, settings.StoneCapSlope * vertical,
            0, 2, pitch, vertical, !startCorner, !endCorner);
        }
      }

      Vector3 InnerOffset(float t)
      {
        Vector3 offset = inward;
        if (edge?.StartCorner != null)
        {
          Vector3 delta = edge.StartCorner.InnerPoint - new Vector3(a.x, 0f, a.z) - inward;
          offset += delta * Mathf.Clamp01(1f - t * edge.Length / inward.magnitude);
        }
        if (edge?.EndCorner != null)
        {
          Vector3 delta = edge.EndCorner.InnerPoint - new Vector3(b.x, 0f, b.z) - inward;
          offset += delta * Mathf.Clamp01(1f - (1f - t) * edge.Length / inward.magnitude);
        }
        return offset;
      }
    }

    private sealed class ParapetCorner
    {
      public Vector3 InnerPoint;
      public float CourseHeight;
    }

    private sealed class ParapetEdge
    {
      public Vector3 A, B, Direction, Inward;
      public float Length;
      public ParapetCorner StartCorner, EndCorner;
    }

    /// <summary>Global endpoint connectivity keeps joints consistent across cell/chunk boundaries.</summary>
    private sealed class ParapetLayout
    {
      private readonly Dictionary<(int, int, int), ParapetEdge> edges = new();
      public ParapetEdge Get(int x, int z, int e) => edges.TryGetValue((x, z, e), out var edge) ? edge : null;

      public ParapetLayout(WallLayer walls, HeightLayer ground, S2MTerrainSettings terrain, S2MWallRenderSettings settings)
      {
        // Corner blocks must fit inside one half-cell merlon interval.
        if (settings.ParapetWidth >= 0.5f) return;
        float pitch = terrain.HorizontalCellSize, width = settings.ParapetWidth * pitch;
        float low = settings.StoneParapetHeight * terrain.HeightUnitScale * 1024f;
        var nodes = new Dictionary<(int, int), List<(ParapetEdge Edge, bool Start)>>();
        for (int z = 0; z < ground.Height; z++)
          for (int x = 0; x < ground.Width; x++)
          {
            WallCell cell = walls.GetCell(x, z);
            if (!cell.HasWall || cell.IsWood) continue;
            for (int e = 0; e < 8; e++)
            {
              int a = EdgeStart[e], b = EdgeEnd[e];
              if ((cell.TopEdges & (1 << e)) == 0 || !IsEdge(Polygons[cell.Shape], a, b)) continue;
              var edge = new ParapetEdge
              {
                A = new Vector3((x + Corners[a].x) * pitch, cell.GetHeight(a) * terrain.HeightUnitScale, (z + Corners[a].y) * pitch),
                B = new Vector3((x + Corners[b].x) * pitch, cell.GetHeight(b) * terrain.HeightUnitScale, (z + Corners[b].y) * pitch)
              };
              Vector3 delta = edge.B - edge.A; delta.y = 0f;
              edge.Length = delta.magnitude; edge.Direction = delta.normalized;
              edge.Inward = Vector3.Cross(Vector3.up, edge.Direction);
              edges.Add((x, z, e), edge);
              Add((x + (int)Corners[a].x, z + (int)Corners[a].y), edge, true);
              Add((x + (int)Corners[b].x, z + (int)Corners[b].y), edge, false);
            }
          }
        foreach (var entries in nodes.Values)
        {
          // Do not fuse touching-but-independent boundaries or ambiguous branches.
          if (entries.Count != 2 || entries[0].Start == entries[1].Start) continue;
          var incoming = entries[0].Start ? entries[1].Edge : entries[0].Edge;
          var outgoing = entries[0].Start ? entries[0].Edge : entries[1].Edge;
          if (Math.Abs(incoming.B.y - outgoing.A.y) > terrain.HeightUnitScale
            || Math.Abs(Vector3.Dot(incoming.Direction, outgoing.Direction)) > 0.001f) continue;
          Vector3 vertex = incoming.B; vertex.y = 0f;
          float IncomingCourse(ParapetEdge edge, bool start)
          {
            int pieces = Math.Max(1, Mathf.RoundToInt(edge.Length / (pitch * 0.5f)));
            return start ? Math.Max(edge.A.y, Vector3.Lerp(edge.A, edge.B, 1f / pieces).y)
              : Math.Max(edge.B.y, Vector3.Lerp(edge.A, edge.B, 1f - 1f / pieces).y);
          }
          var corner = new ParapetCorner
          {
            InnerPoint = vertex + (incoming.Inward + outgoing.Inward) * width,
            CourseHeight = Math.Max(IncomingCourse(incoming, false), IncomingCourse(outgoing, true)) + low
          };
          // A concave turn needs supporting wall in its inner quadrant. Two
          // independent walls touching only at a vertex must not grow a bridge.
          int ix = Mathf.FloorToInt(corner.InnerPoint.x / pitch), iz = Mathf.FloorToInt(corner.InnerPoint.z / pitch);
          if (ix < 0 || iz < 0 || ix >= ground.Width || iz >= ground.Height) continue;
          WallCell support = walls.GetCell(ix, iz);
          if (!support.HasWall) continue;
          Vector2 point = new(corner.InnerPoint.x / pitch - ix, corner.InnerPoint.z / pitch - iz);
          int[] polygon = Polygons[support.Shape];
          bool inside = true;
          for (int i = 0; i < polygon.Length; i++)
          {
            Vector2 a = Corners[polygon[i]], b = Corners[polygon[(i + 1) % polygon.Length]];
            if ((b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x) > 0.0001f) inside = false;
          }
          if (!inside) continue;
          incoming.EndCorner = outgoing.StartCorner = corner;
        }

        void Add((int, int) key, ParapetEdge edge, bool start)
        {
          if (!nodes.TryGetValue(key, out var entries)) nodes.Add(key, entries = new());
          entries.Add((edge, start));
        }
      }
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    private static bool Nonnegative(float value) => value >= 0f && !float.IsInfinity(value);

    private sealed class WallMesh
    {
      public readonly List<Vector3> Vertices = new();
      private readonly List<Vector3> normals = new();
      private readonly List<Vector2> uv = new();
      private readonly List<int>[] triangles = { new(), new(), new(), new(), new(), new() };

      public void Triangle(Vector3 a, Vector3 b, Vector3 c, int material, float pitch)
        => Triangle(a, b, c, new Vector2(a.x, a.z) / (pitch * 2f),
          new Vector2(b.x, b.z) / (pitch * 2f), new Vector2(c.x, c.z) / (pitch * 2f), material);

      private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, int material)
      {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (normal.sqrMagnitude < 1e-12f) return;
        // Vector3.Normalize zeros vectors below Unity's length epsilon. Clipped
        // height transitions can form small but valid triangles; normalize by
        // the area already checked above so these retain a unit face normal.
        normal /= Mathf.Sqrt(normal.sqrMagnitude);
        int offset = Vertices.Count;
        Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
        normals.Add(normal); normals.Add(normal); normals.Add(normal);
        uv.Add(ua); uv.Add(ub); uv.Add(uc);
        triangles[material].Add(offset); triangles[material].Add(offset + 1); triangles[material].Add(offset + 2);
      }

      public void Side(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, float pitch, float vertical)
      {
        Vector3 direction = new Vector3(c.x - b.x, 0f, c.z - b.z).normalized;
        Vector2 UV(Vector3 p) => new(Vector3.Dot(p, direction) / (2f * pitch), 1f - p.y / (3.25f * vertical));
        Triangle(a, c, b, UV(a), UV(c), UV(b), material);
        Triangle(a, d, c, UV(a), UV(d), UV(c), material);
      }

      public void Prism(Vector3 a, Vector3 b, Vector3 inward, float height,
        int side, int cap, float pitch, float vertical, float capRise = 0f, bool levelTop = false)
      {
        if (height <= 0f) return;
        Vector3 c = b + inward, d = a + inward;
        Vector3 up = Vector3.up * height;
        Vector3 ta = a + up, tb = b + up;
        if (levelTop) ta.y = tb.y = Math.Max(a.y, b.y) + height;
        Vector3 tc = tb + inward + Vector3.up * capRise, td = ta + inward + Vector3.up * capRise;
        Triangle(ta, tb, tc, cap, pitch);
        Triangle(ta, tc, td, cap, pitch);
        Side(a, ta, tb, b, side, pitch, vertical);
        Side(b, tb, tc, c, side, pitch, vertical);
        Side(c, tc, td, d, side, pitch, vertical);
        Side(d, td, ta, a, side, pitch, vertical);
      }

      public void HeightDifference(Vector3 lowerA, Vector3 upperA, Vector3 upperB, Vector3 lowerB,
        int material, float pitch, float vertical)
      {
        float da = upperA.y - lowerA.y, db = upperB.y - lowerB.y;
        if (da <= 0f && db <= 0f) return;
        // Sloping tops may cross along an edge. Clip at their intersection so
        // each cell emits only its higher portion, without overlapping faces.
        if (da < 0f)
        {
          float t = da / (da - db);
          lowerA = Vector3.Lerp(lowerA, lowerB, t);
          upperA = Vector3.Lerp(upperA, upperB, t);
        }
        else if (db < 0f)
        {
          float t = da / (da - db);
          lowerB = Vector3.Lerp(lowerA, lowerB, t);
          upperB = Vector3.Lerp(upperA, upperB, t);
        }
        Side(lowerA, upperA, upperB, lowerB, material, pitch, vertical);
      }

      public void Ribbon(Vector3 a, Vector3 b, Vector3 d, Vector3 c, float topY, float capRise,
        int side, int cap, float pitch, float vertical, bool startFace, bool endFace)
      {
        // Inner endpoints are mitered at turns. Two corner halves share their
        // diagonal and cap heights, without overlapping blocks or internal faces.
        Vector3 ta = a, tb = b, tc = c, td = d;
        ta.y = tb.y = topY; tc.y = td.y = topY + capRise;
        Triangle(ta, tb, tc, cap, pitch);
        Triangle(ta, tc, td, cap, pitch);
        Side(a, ta, tb, b, side, pitch, vertical);
        if (endFace) Side(b, tb, tc, c, side, pitch, vertical);
        Side(c, tc, td, d, side, pitch, vertical);
        if (startFace) Side(d, td, ta, a, side, pitch, vertical);
      }

      public void Spike(Vector3 center, float radius, float height, Vector3 along)
      {
        if (height <= 0f) return;
        // The wooden gate uses a hexagonal pointed log and this same end-grain
        // texture. Generate a local equivalent, rather than instancing the gate.
        Vector3 across = Vector3.Cross(along, Vector3.up);
        Vector3 tip = center + Vector3.up * height;
        for (int i = 0; i < 6; i++)
        {
          float angleA = i * Mathf.PI / 3f, angleB = (i + 1) * Mathf.PI / 3f;
          Vector2 ra = new(Mathf.Cos(angleA), Mathf.Sin(angleA));
          Vector2 rb = new(Mathf.Cos(angleB), Mathf.Sin(angleB));
          Vector3 a = center + (along * ra.x + across * ra.y) * radius;
          Vector3 b = center + (along * rb.x + across * rb.y) * radius;
          Triangle(a, tip, b, Vector2.one * 0.5f + ra * 0.5f,
            Vector2.one * 0.5f, Vector2.one * 0.5f + rb * 0.5f, 5);
        }
      }

      public Mesh Build(string name)
      {
        var mesh = new Mesh { name = name, indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(Vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
        mesh.subMeshCount = triangles.Length;
        for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateBounds();
        return mesh;
      }
    }
  }
}
