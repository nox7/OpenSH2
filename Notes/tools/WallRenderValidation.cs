// Copy into an isolated Unity project's Assets/Editor with the OpenSH2 sources.
// Run with -batchmode -executeMethod WallRenderValidation.Run -quit.
using System;
using System.IO;
using System.Linq;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.ObjectReaders;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WallRenderValidation
{
  private const string Installation = "C:/Steam/steamapps/common/Stronghold 2";
  private static string Output => Environment.GetEnvironmentVariable("OPENSH2_WALL_VALIDATION_OUTPUT")
    ?? Path.GetFullPath("../WallProbe/Rendered");

  public static void Run()
  {
    Directory.CreateDirectory(Output);
    try
    {
      ParserChecks();
      var map = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/war_chapter8.s2m");
      Check(map.WallLayer.Width == 256, "Backing stride");
      int wallCount = map.WallLayer.Cells.Count(c => c.HasWall);
      Check(wallCount > 0, "Chapter 8 has no wall shapes");
      Check(map.WallLayer.GetCell(198, 15).Height0 == 3328f, "Stone sample height");
      Check(map.WallLayer.GetCell(211, 154).IsWood, "Wood sample flag");
      var world = new GameObject("Wall validation");
      var settings = new S2MTerrainSettings();
      GameObject walls = S2MWallRenderer.Render(map, Installation, world.transform, settings);
      MeshFilter[] filters = walls.GetComponentsInChildren<MeshFilter>();
      File.WriteAllText(Path.Combine(Output, "materials.txt"), string.Join("\n", filters.Select(f => f.name + ": " +
        string.Join(", ", f.GetComponent<MeshRenderer>().sharedMaterials.Select((m, i) =>
          i + "=" + m.mainTexture.name + " indices=" + f.sharedMesh.GetIndexCount(i))))));
      Check(filters.Length > 0, "No wall meshes");
      int triangleCount = 0;
      foreach (MeshFilter filter in filters)
      {
        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices, normals = mesh.normals;
        int[] indices = mesh.triangles;
        triangleCount += indices.Length / 3;
        Check(mesh.bounds.min.x >= 0f && mesh.bounds.max.x <= 256f, "Footprint X exceeds map");
        Check(mesh.bounds.min.z >= 0f && mesh.bounds.max.z <= 256f, "Footprint Z exceeds map");
        Check(filter.GetComponent<MeshCollider>().sharedMesh == mesh, "Collider does not share geometry");
        foreach (Material material in filter.GetComponent<MeshRenderer>().sharedMaterials)
        {
          Check(material.mainTexture != null, "Missing installed wall texture");
          Check(!ShaderUtil.ShaderHasError(material.shader), "Material shader error");
        }
        for (int i = 0; i < indices.Length; i += 3)
        {
          int a = indices[i], b = indices[i + 1], c = indices[i + 2];
          Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
          Check(cross.sqrMagnitude > 1e-12f, "Degenerate wall triangle");
          Check(Vector3.Dot(cross / Mathf.Sqrt(cross.sqrMagnitude), normals[a]) > 0.99f, "Incorrect winding/normal");
        }
        // Walkway material must never be used on an exterior vertical band.
        foreach (int index in mesh.GetTriangles(1))
          Check(normals[index].y > 0f, "Wall-top material leaked onto a vertical exterior face");
      }
      Physics.SyncTransforms();
      Check(Hit(walls, new Ray(new Vector3(198.5f, 1f, 14.5f), Vector3.forward), out _), "North exterior side missing/backfacing");
      Check(Hit(walls, new Ray(new Vector3(198.5f, 10f, 15.5f), Vector3.down), out float top) && Mathf.Abs(top - 3.25f) < 0.01f, "Saved stone walk/collider height");
      Check(!Hit(walls, new Ray(new Vector3(198.8f, 10f, 14.975f), Vector3.down), out _), "Merlon still leans outside wall face");
      Check(Hit(walls, new Ray(new Vector3(198.8f, 10f, 15.025f), Vector3.down), out float outerCap) && outerCap > 3.75f, "Merlon outer cap missing");
      Check(Hit(walls, new Ray(new Vector3(198.8f, 10f, 15.24f), Vector3.down), out float innerCap) && innerCap > outerCap, "Sloping cap missing from vertical merlon");
      Check(Hit(walls, new Ray(new Vector3(211.635f, 10f, 154.135f), Vector3.down), out float spike) && spike > 2.5f, "Pointed wood spike missing");
      Check(Hit(walls, new Ray(new Vector3(211.70f, 10f, 154.125f), Vector3.down), out float spikeEdge) && spike > spikeEdge + 0.1f, "Wood spike is flat rather than tapered");
      Check(Hit(walls, new Ray(new Vector3(199.15f, 10f, 15.85f), Vector3.down), out _), "Diagonal occupied half missing");
      Check(!Hit(walls, new Ray(new Vector3(199.85f, 10f, 15.15f), Vector3.down), out _), "Diagonal empty half filled");
      // No generated body should fill the saved opening immediately outside
      // this gate's footprint. A short horizontal ray tests the passage height.
      Check(!ShortHit(walls, new Ray(new Vector3(200.5f, 1f, 42f), Vector3.forward), 2f), "Stone gate approach sealed");

      // Exercise reflections and non-default scale, including collider transforms.
      var flipped = S2MWallRenderer.Render(map, Installation, null,
        new S2MTerrainSettings { FlipX = true, FlipZ = true, HorizontalCellSize = 2f, HeightUnitScale = 2f / 1024f });
      Physics.SyncTransforms();
      Check(Hit(flipped, new Ray(new Vector3((256f - 198.5f) * 2f, 20f, (256f - 15.5f) * 2f), Vector3.down), out float flippedTop)
        && Mathf.Abs(flippedTop - 6.5f) < 0.01f, "Scaled/flipped saved walk collider");
      UnityEngine.Object.DestroyImmediate(flipped);
      var flat = S2MFileLoader.Load("C:/Users/garet/Documents/Stronghold 2/Maps/terrain-flat.s2m");
      GameObject empty = S2MWallRenderer.Render(flat, "unused-on-empty-layer");
      Check(empty.transform.childCount == 0, "Blank control created geometry");
      UnityEngine.Object.DestroyImmediate(empty);

      // Render through the same URP pipeline and terrain implementation as Main.
      var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
      Check(pipeline != null, "Missing validation URP asset");
      GraphicsSettings.defaultRenderPipeline = pipeline;
      QualitySettings.renderPipeline = pipeline;
      RenderSettings.ambientMode = AmbientMode.Flat;
      RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
      RenderSettings.ambientIntensity = 1f;
      var sun = new GameObject("Sun").AddComponent<Light>();
      sun.type = LightType.Directional; sun.intensity = 1.1f;
      sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
      sun.shadows = LightShadows.Soft;
      GameObject terrain = S2MTerrainRenderer.Render(map, world.transform, settings);
      var textureData = new S2MTerrainTextureAssetLoader().Load(map, Installation);
      S2MTerrainTextureRenderer.Apply(terrain, textureData);
      Camera camera = new GameObject("Wall preview").AddComponent<Camera>();
      camera.enabled = false; camera.orthographic = true;
      camera.nearClipPlane = 0.1f; camera.farClipPlane = 600f;
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color(0.12f, 0.15f, 0.18f);
      camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
      StepChecks(map, camera);
      CornerChecks(map, camera);
      SolarChecks(map, walls, camera, world.transform);
      Preview(camera, new Vector3(204f, 2f, 20f), new Vector3(-9f, 11f, -15f), 6f, "stone-walls.png");
      Preview(camera, new Vector3(200f, 3.3f, 16f), new Vector3(-5f, 6f, -8f), 2.8f, "stone-parapet-detail.png");
      Preview(camera, new Vector3(71.5f, 5f, 18f), new Vector3(6f, 5f, 9f), 3f, "map-height-step-detail.png");
      Preview(camera, new Vector3(88f, 2f, 186f), new Vector3(-11f, 13f, 16f), 9f, "wooden-walls.png");
      Preview(camera, new Vector3(212f, 2f, 154f), new Vector3(-5f, 6f, -8f), 2.4f, "wood-spike-detail.png");
      Preview(camera, new Vector3(128f, 1f, 128f), new Vector3(0f, 230f, -90f), 140f, "wall-map-overview.png");
      File.WriteAllText(Path.Combine(Output, "validation.txt"), $"PASS: parser block relocation/length rejection, {wallCount} wall cells, {filters.Length} chunks, {triangleCount} triangles, texture/shader checks, winding, colliders, saved walk height, no vertical walkway-texture band, bedchamber mesh/deck alignment, four convex corner orientations, single corner cap/no internal face, concave corner across chunks, vertical merlons with sloped caps, pointed wood spikes, diagonal halves, gate approach gap, cross-chunk height-step faces, crossing slopes, mixed stone/wood heights, equal-height/void negative controls, X/Z ramps, axis flips/scaling, blank control, URP renders.\n");
      Debug.Log("WALL_VALIDATION_PASS");
    }
    catch (Exception e)
    {
      File.WriteAllText(Path.Combine(Output, "validation.txt"), "FAIL: " + e);
      Debug.LogException(e);
      EditorApplication.Exit(1);
    }
  }

  private static void ParserChecks()
  {
    byte[] Payload(int referenceBytes, bool insertBlock)
    {
      using var stream = new MemoryStream();
      using var writer = new BinaryWriter(stream);
      writer.Write(referenceBytes); writer.Write(new byte[referenceBytes]);
      if (insertBlock) { writer.Write(99); writer.Write(7); writer.Write(new byte[7]); }
      writer.Write(14); writer.Write(24 * 4);
      for (int i = 0; i < 4; i++)
      {
        for (int c = 0; c < 4; c++) writer.Write(1024f + i * 20 + c);
        writer.Write(new byte[] { 1, 2, 8, 8, 7, 6, 5, 4 });
      }
      return stream.ToArray();
    }
    WallLayer a = WallLayerReader.Read(Payload(4, false));
    WallLayer b = WallLayerReader.Read(Payload(108, true));
    Check(a.Width == 2 && b.GetCell(1, 1).Height3 == 1087f, "Reader assumes fixed stride/offset");
    Check(b.GetCell(1, 1).GetHeight(0) == 1086f && b.GetCell(1, 1).GetHeight(2) == 1084f,
      "Wall record north/south pairs must differ from HeightLayer geometric order");
    Check(b.GetCell(1, 1).Auxiliary == 7 && b.GetCell(1, 1).Unknown21 == 6, "Unknown wall bytes lost");
    byte[] invalid = Payload(4, false);
    Array.Resize(ref invalid, invalid.Length - 1);
    bool rejected = false;
    try { WallLayerReader.Read(invalid); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Truncated layer accepted");
  }

  private static void StepChecks(S2MFile map, Camera camera)
  {
    WallCell[] cells = map.WallLayer.Cells;
    WallCell[] original = (WallCell[])cells.Clone();
    GameObject step = null;
    int left = 20 * map.WallLayer.Width + 31, right = left + 1;
    WallCell Cell(float h0, float h1, float h2, float h3, byte sides, byte flags = 0, byte tops = 0)
      => new(h0, h1, h2, h3, 1, flags, tops, sides, 0, 0, 0, 0);
    void RenderCells(bool battlements = false)
    {
      if (step != null) UnityEngine.Object.DestroyImmediate(step);
      step = S2MWallRenderer.Render(map, Installation, null, null, new S2MWallRenderSettings { RenderBattlements = battlements });
      Physics.SyncTransforms();
    }
    void RenderPair(WallCell a, WallCell b)
    {
      cells[left] = a; cells[right] = b;
      RenderCells();
    }
    try
    {
      Array.Clear(cells, 0, cells.Length);
      // X=31/32 straddles the default chunk boundary. The shared face is absent
      // from both saved masks and must be reconstructed only above the low top.
      RenderPair(Cell(2048, 2048, 2048, 2048, 13), Cell(4096, 4096, 4096, 4096, 7));
      Check(ShortHit(step, new Ray(new Vector3(31.5f, 3f, 20.5f), Vector3.right), 1f), "Cross-chunk height step is see-through");
      Check(!ShortHit(step, new Ray(new Vector3(31.5f, 1f, 20.5f), Vector3.right), 1f), "Hidden face below low wall was filled");
      Preview(camera, new Vector3(32f, 2f, 20.5f), new Vector3(-4f, 4f, -5f), 3f, "height-step-detail.png");

      RenderPair(Cell(2048, 2048, 2048, 2048, 13), Cell(4096, 4096, 1024, 1024, 7));
      Check(ShortHit(step, new Ray(new Vector3(31.5f, 2.75f, 20.25f), Vector3.right), 1f), "Higher portion of crossing slope missing");
      Check(ShortHit(step, new Ray(new Vector3(32.5f, 1.75f, 20.9f), Vector3.left), 1f), "Opposite portion of crossing slope missing");

      RenderPair(Cell(3072, 3072, 3072, 3072, 13), Cell(2048, 2048, 2048, 2048, 7, 2));
      Check(ShortHit(step, new Ray(new Vector3(32.5f, 2.5f, 20.5f), Vector3.left), 1f), "Saved masonry / timber height difference missing");
      RenderPair(Cell(2048, 2048, 2048, 2048, 13), Cell(2048, 2048, 2048, 2048, 7));
      Check(!ShortHit(step, new Ray(new Vector3(31.5f, 2.04f, 20.5f), Vector3.right), 1f), "Equal-height seam has duplicate face");
      RenderPair(Cell(2048, 2048, 2048, 2048, 13), default);
      Check(!ShortHit(step, new Ray(new Vector3(32.5f, 1f, 20.5f), Vector3.left), 1f), "Saved opening against absent wall was sealed");

      RenderPair(Cell(4096, 3072, 4096, 3072, 13), Cell(3072, 2048, 3072, 2048, 7));
      Check(Hit(step, new Ray(new Vector3(31.75f, 10f, 20.5f), Vector3.down), out float alongX)
        && Mathf.Abs(alongX - 3.25f) < 0.01f, "X-axis ramp interpolation changed");
      Check(Hit(step, new Ray(new Vector3(32.25f, 10f, 20.5f), Vector3.down), out float nextX)
        && Mathf.Abs(nextX - 2.75f) < 0.01f, "X-axis ramp seam is discontinuous");

      Array.Clear(cells, 0, cells.Length);
      int north = 31 * map.WallLayer.Width + 20, south = north + map.WallLayer.Width;
      cells[north] = Cell(4096, 4096, 3072, 3072, 14, tops: 8);
      cells[south] = Cell(3072, 3072, 2048, 2048, 11, tops: 8);
      RenderCells();
      Check(Hit(step, new Ray(new Vector3(20.5f, 10f, 31.75f), Vector3.down), out float alongZ)
        && Mathf.Abs(alongZ - 3.25f) < 0.01f, "Z-axis ramp slopes in the wrong direction");
      Check(Hit(step, new Ray(new Vector3(20.5f, 10f, 32.25f), Vector3.down), out float nextZ)
        && Mathf.Abs(nextZ - 2.75f) < 0.01f, "Z-axis ramp seam is discontinuous");
      Check(Hit(step, new Ray(new Vector3(20.5f, 10f, 31.999f), Vector3.down), out float seamA)
        && Hit(step, new Ray(new Vector3(20.5f, 10f, 32.001f), Vector3.down), out float seamB)
        && Mathf.Abs(seamA - seamB) < 0.01f, "Z ramp does not meet across chunk boundary");

      RenderCells(battlements: true);
      Check(Hit(step, new Ray(new Vector3(20.05f, 10f, 31.1f), Vector3.down), out float merlonA)
        && Hit(step, new Ray(new Vector3(20.05f, 10f, 31.3f), Vector3.down), out float merlonB)
        && Mathf.Abs(merlonA - merlonB) < 0.001f, "Individual merlon tilts along the Z ramp");
      Check(Hit(step, new Ray(new Vector3(20.05f, 10f, 31.6f), Vector3.down), out float nextMerlon)
        && merlonA - nextMerlon > 0.1f, "Separate merlons should still step with the grade");
      Preview(camera, new Vector3(20.5f, 3f, 32f), new Vector3(-4f, 5f, 4f), 3f, "z-ramp-detail.png");
    }
    finally
    {
      if (step != null) UnityEngine.Object.DestroyImmediate(step);
      Array.Copy(original, cells, cells.Length);
    }
  }

  private static void CornerChecks(S2MFile map, Camera camera)
  {
    WallCell[] cells = map.WallLayer.Cells;
    WallCell[] original = (WallCell[])cells.Clone();
    GameObject corner = null;
    WallCell Cell(byte top) => new(3328, 3328, 3328, 3328, 1, 0, top, 15, 0, 0, 0, 0);
    void Render()
    {
      if (corner != null) UnityEngine.Object.DestroyImmediate(corner);
      corner = S2MWallRenderer.Render(map, Installation);
      Physics.SyncTransforms();
    }
    try
    {
      // All four convex turns: one corner block covers the former sliver of
      // walkway, with regular gaps on both arms and no internal diagonal faces.
      byte[] masks = { 12, 6, 3, 9 };
      Vector2[] vertices = { new(31, 31), new(32, 31), new(32, 32), new(31, 32) };
      Vector2[] inward = { new(1, 1), new(-1, 1), new(-1, -1), new(1, -1) };
      for (int turn = 0; turn < masks.Length; turn++)
      {
        Array.Clear(cells, 0, cells.Length);
        cells[31 * map.WallLayer.Width + 31] = Cell(masks[turn]);
        Render();
        Vector2 p = vertices[turn] + inward[turn] * 0.01f;
        Check(Hit(corner, new Ray(new Vector3(p.x, 10f, p.y), Vector3.down), out float y) && y > 3.8f,
          "Right-angle corner leaves an uncovered walkway sliver");
        for (int axis = 0; axis < 2; axis++)
        {
          int gapSamples = 0;
          bool foundGap = false;
          for (int sample = 10; sample < 990; sample++)
          {
            float distance = sample / 1000f;
            Vector2 point = vertices[turn] + new Vector2(inward[turn].x * (axis == 0 ? distance : 0.05f),
              inward[turn].y * (axis == 1 ? distance : 0.05f));
            bool merlon = Hit(corner, new Ray(new Vector3(point.x, 10f, point.y), Vector3.down), out float top) && top > 3.8f;
            if (!merlon) { foundGap = true; gapSamples++; }
            else if (foundGap) break;
          }
          Check(Math.Abs(gapSamples - 150) <= 2, $"Corner arm {axis} has irregular gap: {gapSamples / 1000f}");
        }
        Vector2 other = vertices[turn] + inward[turn] * 0.49f;
        float minX = Mathf.Min(vertices[turn].x, other.x), maxX = Mathf.Max(vertices[turn].x, other.x);
        float minZ = Mathf.Min(vertices[turn].y, other.y), maxZ = Mathf.Max(vertices[turn].y, other.y);
        int caps = 0, internalFaces = 0;
        foreach (MeshFilter filter in corner.GetComponentsInChildren<MeshFilter>())
        {
          Mesh mesh = filter.sharedMesh;
          Vector3[] v = mesh.vertices, n = mesh.normals;
          int[] cap = mesh.GetTriangles(2);
          for (int i = 0; i < cap.Length; i += 3)
            if (Enumerable.Range(0, 3).All(j => v[cap[i+j]].y > 3.8f
              && v[cap[i+j]].x >= minX - 0.0001f && v[cap[i+j]].x <= maxX + 0.0001f
              && v[cap[i+j]].z >= minZ - 0.0001f && v[cap[i+j]].z <= maxZ + 0.0001f)) caps++;
          foreach (int index in mesh.GetTriangles(0))
            if (Mathf.Abs(n[index].y) < 0.01f && Mathf.Abs(n[index].x) > 0.3f && Mathf.Abs(n[index].z) > 0.3f) internalFaces++;
        }
        Check(caps == 4, $"Extended corner cap should have two joined quads, got {caps} triangles");
        Check(internalFaces == 0, "Corner contains an internal diagonal wall face");
        if (turn == 0) Preview(camera, new Vector3(31.2f, 3.6f, 31.2f), new Vector3(-3f, 4f, -4f), 1f, "merged-corner-detail.png");
      }

      // Concave turn using two edge owners on opposite sides of X=32, with
      // supporting masonry in the inner quadrant. The layout must join across
      // the chunk boundary rather than treating the two runs independently.
      Array.Clear(cells, 0, cells.Length);
      cells[31 * map.WallLayer.Width + 31] = Cell(2);
      cells[30 * map.WallLayer.Width + 32] = Cell(1);
      cells[30 * map.WallLayer.Width + 31] = Cell(0);
      Render();
      Check(Hit(corner, new Ray(new Vector3(31.99f, 10f, 30.99f), Vector3.down), out float joined) && joined > 3.8f,
        "Parapet corner was not joined across chunks");
      Preview(camera, new Vector3(32f, 3.6f, 31f), new Vector3(3f, 4f, 4f), 1.6f, "concave-corner-detail.png");
    }
    finally
    {
      if (corner != null) UnityEngine.Object.DestroyImmediate(corner);
      Array.Copy(original, cells, cells.Length);
    }
  }

  private static bool Hit(GameObject root, Ray ray, out float y)
  {
    float closest = float.PositiveInfinity; y = 0f;
    foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>())
      if (collider.Raycast(ray, out RaycastHit hit, 50f) && hit.distance < closest)
      { closest = hit.distance; y = hit.point.y; }
    return !float.IsPositiveInfinity(closest);
  }

  private static void SolarChecks(S2MFile map, GameObject walls, Camera camera, Transform parent)
  {
    // Inspect/render the installed bedchamber, rather than patching its height
    // or inventing an extra building-owned wall-top mesh.
    string converter = "C:/Users/garet/Documents/GitHub/OpenSH2/Tools/Granny2MeshConverter/bin/Release/net48/OpenSH2.Granny2MeshConverter.exe";
    Granny2ModelData model = Granny2AssetConverter.LoadOrConvert(Installation,
      "meshes/buildings/queens_solar_closed.GR2", converter, Path.Combine(Output, "model-cache"));
    int compared = 0, preview = 0;
    float maximumGap = 0f;
    foreach (BuildingPlacement building in map.BuildingPlacements.Where(p => p.Type == "Solar"))
    {
      GameObject solar = Granny2ModelRenderer.Render(model, parent,
        new Granny2ModelRenderSettings { UnitScale = 1f / 204.8f, UseFoliageMaterials = false });
      solar.transform.localPosition = new Vector3(building.MapX, building.RawY / 1024f, building.MapZ);
      solar.transform.localRotation = Quaternion.Euler(0f, building.RotationDegrees, 0f);
      foreach (Granny2MeshData mesh in model.Meshes)
        foreach (Granny2TriangleGroup group in mesh.TriangleGroups)
        {
          string path = mesh.Materials[group.MaterialIndex].TexturePath.Replace('\\', '/');
          if (!path.EndsWith("/walltop.DDS", StringComparison.OrdinalIgnoreCase)) continue;
          for (int tri = group.TriangleFirst; tri < group.TriangleFirst + group.TriangleCount; tri++)
          {
            Vector3 point = Vector3.zero;
            for (int j = 0; j < 3; j++)
            {
              int index = mesh.Indices[tri * 3 + j] * 8;
              point += new Vector3(mesh.VertexData[index], mesh.VertexData[index + 2], mesh.VertexData[index + 1]) / (204.8f * 3f);
            }
            point = solar.transform.TransformPoint(point);
            int x = Mathf.FloorToInt(point.x), z = Mathf.FloorToInt(point.z);
            if (x < 0 || z < 0 || x >= map.MapSize || z >= map.MapSize) continue;
            WallCell cell = map.WallLayer.GetCell(x, z);
            if (!cell.HasWall) continue;
            float tx = point.x - x, tz = point.z - z;
            float saved = Mathf.Lerp(Mathf.Lerp(cell.GetHeight(2), cell.GetHeight(3), tx),
              Mathf.Lerp(cell.GetHeight(0), cell.GetHeight(1), tx), tz) / 1024f;
            float gap = Mathf.Abs(saved - point.y);
            Check(gap < 0.02f, "Saved wall height does not meet existing bedchamber deck");
            maximumGap = Mathf.Max(maximumGap, gap);
            if (Hit(walls, new Ray(new Vector3(point.x, saved + 2f, point.z), Vector3.down), out float rendered)
              && Mathf.Abs(rendered - saved) < 0.005f)
            {
              compared++;
              Check(Mathf.Abs(rendered - point.y) < 0.02f, "Rendered wall deck floats over the building deck");
            }
          }
        }
      Vector3 target = solar.transform.TransformPoint(new Vector3(2f, 3.25f, 2f));
      Preview(camera, target, new Vector3(-7f, 4f, -8f), 4.2f, $"bedchamber-join-{preview}.png");
      Preview(camera, target, new Vector3(7f, 0.2f, 8f), 3f, $"bedchamber-back-{preview}.png");
      preview++;
    }
    Check(compared > 0, "No bedchamber deck samples compared to rendered wall");
    File.WriteAllText(Path.Combine(Output, "building-join.txt"), $"{compared} deck triangle samples compared; maximum saved/model difference {maximumGap:F6} map units.\n");
  }

  private static void Preview(Camera camera, Vector3 target, Vector3 offset, float size, string name)
  {
    camera.transform.position = target + offset;
    camera.transform.LookAt(target);
    camera.orthographicSize = size;
    var render = new RenderTexture(1200, 900, 24);
    // Initialize URP's renderer/material state before capturing the first view.
    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
    RenderTexture.active = render;
    var image = new Texture2D(1200, 900, TextureFormat.RGB24, false);
    image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); image.Apply();
    File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
    RenderTexture.active = null;
    UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(render);
  }

  private static void Check(bool condition, string reason)
  {
    if (!condition) throw new Exception(reason);
  }

  private static bool ShortHit(GameObject root, Ray ray, float distance)
    => root.GetComponentsInChildren<MeshCollider>().Any(c => c.Raycast(ray, out _, distance));
}
