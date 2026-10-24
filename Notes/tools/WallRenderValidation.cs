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
      Check(map.WallLayer.Cells.Count(c => c.HasWall) == 1076, "Chapter 8 shape count");
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
      }
      Physics.SyncTransforms();
      Check(Hit(walls, new Ray(new Vector3(198.5f, 1f, 14.5f), Vector3.forward), out _), "North exterior side missing/backfacing");
      Check(Hit(walls, new Ray(new Vector3(198.5f, 10f, 15.5f), Vector3.down), out float top) && Mathf.Abs(top - 3.33f) < 0.01f, "Raised stone walk/collider height");
      Check(!Hit(walls, new Ray(new Vector3(198.4f, 10f, 14.975f), Vector3.down), out _), "Merlon still leans outside wall face");
      Check(Hit(walls, new Ray(new Vector3(198.4f, 10f, 15.025f), Vector3.down), out float outerCap) && outerCap > 3.85f, "Merlon outer cap missing");
      Check(Hit(walls, new Ray(new Vector3(198.4f, 10f, 15.24f), Vector3.down), out float innerCap) && innerCap > outerCap, "Sloping cap missing from vertical merlon");
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
        && Mathf.Abs(flippedTop - 6.66f) < 0.01f, "Scaled/flipped raised walk collider");
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
      Preview(camera, new Vector3(204f, 2f, 20f), new Vector3(-9f, 11f, -15f), 6f, "stone-walls.png");
      Preview(camera, new Vector3(200f, 3.3f, 16f), new Vector3(-5f, 6f, -8f), 2.8f, "stone-parapet-detail.png");
      Preview(camera, new Vector3(71.5f, 5f, 18f), new Vector3(6f, 5f, 9f), 3f, "map-height-step-detail.png");
      Preview(camera, new Vector3(88f, 2f, 186f), new Vector3(-11f, 13f, 16f), 9f, "wooden-walls.png");
      Preview(camera, new Vector3(212f, 2f, 154f), new Vector3(-5f, 6f, -8f), 2.4f, "wood-spike-detail.png");
      Preview(camera, new Vector3(128f, 1f, 128f), new Vector3(0f, 230f, -90f), 140f, "wall-map-overview.png");
      File.WriteAllText(Path.Combine(Output, "validation.txt"), $"PASS: parser block relocation/length rejection, 1076 wall cells, {filters.Length} chunks, {triangleCount} triangles, texture/shader checks, winding, colliders, raised walk, vertical merlons with sloped caps, pointed wood spikes, diagonal halves, gate approach gap, cross-chunk height-step faces, crossing slopes, mixed stone/wood heights, equal-height/void negative controls, axis flips/scaling, blank control, URP renders.\n");
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

      RenderPair(Cell(2048, 2048, 2048, 2048, 13), Cell(2048, 2048, 2048, 2048, 7, 2));
      Check(ShortHit(step, new Ray(new Vector3(32.5f, 2.04f, 20.5f), Vector3.left), 1f), "Stone coping / timber height difference missing");
      RenderPair(Cell(2048, 2048, 2048, 2048, 13), Cell(2048, 2048, 2048, 2048, 7));
      Check(!ShortHit(step, new Ray(new Vector3(31.5f, 2.04f, 20.5f), Vector3.right), 1f), "Equal-height seam has duplicate face");
      RenderPair(Cell(2048, 2048, 2048, 2048, 13), default);
      Check(!ShortHit(step, new Ray(new Vector3(32.5f, 1f, 20.5f), Vector3.left), 1f), "Saved opening against absent wall was sealed");

      RenderPair(Cell(4096, 3072, 4096, 3072, 13), Cell(3072, 2048, 3072, 2048, 7));
      Check(Hit(step, new Ray(new Vector3(31.75f, 10f, 20.5f), Vector3.down), out float alongX)
        && Mathf.Abs(alongX - 3.33f) < 0.01f, "X-axis ramp interpolation changed");
      Check(Hit(step, new Ray(new Vector3(32.25f, 10f, 20.5f), Vector3.down), out float nextX)
        && Mathf.Abs(nextX - 2.83f) < 0.01f, "X-axis ramp seam is discontinuous");

      Array.Clear(cells, 0, cells.Length);
      int north = 31 * map.WallLayer.Width + 20, south = north + map.WallLayer.Width;
      cells[north] = Cell(4096, 4096, 3072, 3072, 14, tops: 8);
      cells[south] = Cell(3072, 3072, 2048, 2048, 11, tops: 8);
      RenderCells();
      Check(Hit(step, new Ray(new Vector3(20.5f, 10f, 31.75f), Vector3.down), out float alongZ)
        && Mathf.Abs(alongZ - 3.33f) < 0.01f, "Z-axis ramp slopes in the wrong direction");
      Check(Hit(step, new Ray(new Vector3(20.5f, 10f, 32.25f), Vector3.down), out float nextZ)
        && Mathf.Abs(nextZ - 2.83f) < 0.01f, "Z-axis ramp seam is discontinuous");
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

  private static bool Hit(GameObject root, Ray ray, out float y)
  {
    float closest = float.PositiveInfinity; y = 0f;
    foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>())
      if (collider.Raycast(ray, out RaycastHit hit, 50f) && hit.distance < closest)
      { closest = hit.distance; y = hit.point.y; }
    return !float.IsPositiveInfinity(closest);
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
