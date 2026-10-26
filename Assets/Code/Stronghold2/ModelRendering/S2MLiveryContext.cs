using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  [Serializable]
  public sealed class S2MLiveryOverride
  {
    [Tooltip("Native color/crest choice, 1=player, 2=Olaf, through 10=Grey.")]
    [Range(0, 31)] public int Choice = 1;
    public bool OverrideColor = true;
    public Color Color = Color.red;
    [Tooltip("Optional readable 128×128 crest, using original DDS/GR2 UV orientation.")]
    public Texture2D Crest;
  }

  [Serializable]
  public sealed class S2MEstateLiveryOverride
  {
    [Range(0, 31)] public int Estate;
    [Range(0, 10)] public int Lord = 1;
  }

  [Serializable]
  public sealed class S2MLiverySettings
  {
    public bool Enabled = true;
    public bool UseSavedLivery = true;
    public bool UseSavedColorOrder = true;
    [Tooltip("The local human uses the player/red palette entry even when saved player numbers differ.")]
    public bool LocalPlayerUsesRed = true;
    public Color UnassignedColor = new(.6f, .6f, .65f, 1);
    [Tooltip("PNG rows must match the DDS loader's top-first pixels and the original model UVs.")]
    public bool FlipNativeCrestVertically = true;
    public S2MLiveryOverride[] PaletteOverrides = Array.Empty<S2MLiveryOverride>();
    public S2MEstateLiveryOverride[] EstateOverrides = Array.Empty<S2MEstateLiveryOverride>();
  }

  public readonly struct S2MLivery
  {
    public readonly Color Color;
    public readonly int Crest;
    public S2MLivery(Color color, int crest) { Color = color; Crest = crest; }
    public Vector4 Data => new(Color.r, Color.g, Color.b, Crest);
  }

  internal enum S2MLiveryMaterial { None, Color, AlphaMask, Crest }

  /// <summary>One asset library per map. Shared crest array, cached building variants and O(1) ownership lookup.</summary>
  [ExecuteAlways]
  public sealed class S2MLiveryContext : MonoBehaviour
  {
    public S2MLiveryOwnership Ownership { get; private set; }
    public S2MLiverySettings Settings { get; private set; }
    public Texture2DArray CrestArray { get; private set; }
    public int BuildingMaterialVariantCount => variants.Count;
    private S2MFile map;
    private readonly Dictionary<string, S2MLiveryMaterial> mappings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Material, S2MLiveryMaterial> kinds = new();
    private readonly Dictionary<(Material, Color, int), Material> variants = new();
    private readonly List<Texture2D> crestTextures = new();
    private readonly Color[] colors = new Color[32];
    private const int NeutralCrest = 13;
    private static readonly int[] NativeRgb = {
      0x000000,0xFF0000,0xFF8200,0x00A900,0x00D3D3,0x2D2DFF,0xF353E4,0xF5F500,0x6A1489,0x8D0000,0x727272,
      0xB42828,0x8C7814,0x281450,0x641E3C,0x282878,0x787828,0x647828,0x647864,0x648C00,0x465A00,
      0x737518,0x737564,0x739064,0x7390A4,0x9090BC,0xAF2B11,0x782B3C,0xB42B1E,0x787832,0x7878BE,0xFFFFFF };

    public static S2MLiveryContext Create(S2MFile map, string installation, Transform parent, S2MLiverySettings settings = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (parent == null) throw new ArgumentNullException(nameof(parent));
      var root = new GameObject("Team Colors and Crests"); root.transform.SetParent(parent, false);
      var result = root.AddComponent<S2MLiveryContext>();
      try { result.Initialize(map, installation, settings ?? new S2MLiverySettings()); return result; }
      catch { if (Application.isPlaying) Destroy(root); else DestroyImmediate(root); throw; }
    }

    internal static S2MLiveryContext Find(Transform parent)
    {
      // The context is a sibling of render roots; search each ancestor's direct map context.
      for (var p = parent; p != null; p = p.parent)
        for (int i = 0; i < p.childCount; i++)
          if (p.GetChild(i).TryGetComponent<S2MLiveryContext>(out var context)) return context;
      return null;
    }

    private void Initialize(S2MFile source, string installation, S2MLiverySettings settings)
    {
      map = source; Settings = settings; Ownership = S2MLiveryOwnership.Read(map);
      for (int i = 0; i < colors.Length; i++)
      {
        int rgb = NativeRgb[i]; colors[i] = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
      }
      foreach (var entry in settings.PaletteOverrides ?? Array.Empty<S2MLiveryOverride>())
        if (entry != null && entry.Choice >= 0 && entry.Choice < colors.Length && entry.OverrideColor) colors[entry.Choice] = entry.Color;
      foreach (var entry in settings.EstateOverrides ?? Array.Empty<S2MEstateLiveryOverride>())
        if (entry != null && Ownership.Estates.TryGetValue(entry.Estate, out var estate))
        { estate.Lord = entry.Lord; estate.PlayerNumber = -1; }
      ReadMaterials(installation);
      if (settings.Enabled) LoadCrests(installation);
    }

    private int PlayerChoice(int number)
    {
      if (Ownership.Players.TryGetValue(number, out var player))
      {
        if (Settings.LocalPlayerUsesRed && player.LocalHuman) return 1;
        if (Settings.UseSavedColorOrder && number >= 0 && number < 11) return Ownership.ColorOrder[number];
        if (player.Lord > 0) return player.Lord;
      }
      return number >= 0 && number < 11 && Settings.UseSavedColorOrder ? Ownership.ColorOrder[number] : number;
    }

    private S2MLivery Choice(int color, int crest, bool playerSlots)
    {
      if (playerSlots)
      {
        if (color >= 0 && color < 11) color = PlayerChoice(color);
        if (crest >= 0 && crest < 11) crest = PlayerChoice(crest);
      }
      Color rgb = color >= 0 && color < colors.Length ? colors[color] : Settings.UnassignedColor;
      // MPB vectors bypass Unity's color conversion. Shader math operates in the active color space.
      if (QualitySettings.activeColorSpace == ColorSpace.Linear) rgb = rgb.linear;
      return new S2MLivery(rgb, crest >= 0 && crest <= 12 ? crest : NeutralCrest);
    }

    public S2MLivery ResolveActor(ActorPlacement actor)
    {
      if (!Settings.Enabled) return new S2MLivery(Color.white, NeutralCrest);
      if (Settings.UseSavedLivery && actor.SavedLivery != -1)
      {
        int color = actor.SavedLivery & 65535, crest = (int)((uint)actor.SavedLivery >> 16);
        if (color < 32)
        {
          if (crest == 0 || crest == 65535) crest = color;
          return Choice(color, crest, true);
        }
      }
      // PendingPlayer is an initial-placement player choice, not the terrain's estate number.
      if (actor.PendingPlayer > 0 && actor.PendingPlayer <= 10) return Choice(actor.PendingPlayer, actor.PendingPlayer, true);
      if (Ownership.HasSavedPlayers && actor.PendingPlayer == 0 && Ownership.Players.TryGetValue(0, out var player) && player.LocalHuman)
        return Choice(0, 0, true);
      return ResolveEstate(actor.EstateId);
    }

    public S2MLivery ResolveEstate(int id)
    {
      if (!Settings.Enabled) return new S2MLivery(Color.white, NeutralCrest);
      if (!Ownership.Estates.TryGetValue(id, out var estate)) return Choice(-1, -1, false);
      int color = estate.PlayerNumber >= 0 ? PlayerChoice(estate.PlayerNumber) : estate.Lord > 0 ? estate.Lord : -1, crest = color;
      if (Settings.UseSavedLivery)
      {
        if (estate.Color >= 0 && estate.Color < 32) color = estate.Color < 11 ? PlayerChoice(estate.Color) : estate.Color;
        crest = estate.Crest >= 0 && estate.Crest <= 12 ? estate.Crest < 11 ? PlayerChoice(estate.Crest) : estate.Crest : color;
      }
      return Choice(color, crest, false);
    }

    public S2MLivery ResolveBuilding(BuildingPlacement building) => ResolveEstate(S2MCampfireLayout.EstateAtBuilding(map, building));

    internal void Configure(Material material, string model, string texture, int group, int teamGroup)
    {
      var kind = Classify(model, texture, group, teamGroup);
      if (!Settings.Enabled) kind = S2MLiveryMaterial.None;
      kinds[material] = kind;
      if (material.HasProperty("_LiveryMode"))
      {
        material.SetFloat("_LiveryMode", (int)kind);
        if (CrestArray != null) material.SetTexture("_CrestArray", CrestArray);
      }
    }

    internal S2MLiveryMaterial Classify(string model, string texture, int group, int teamGroup)
    {
      string path = model.Replace('\\', '/');
      int meshes = path.LastIndexOf("/meshes/", StringComparison.OrdinalIgnoreCase);
      if (meshes >= 0) path = path.Substring(meshes + 1);
      string key = path + "|" + Path.GetFileNameWithoutExtension((texture ?? "").Replace('\\', '/'));
      if (mappings.TryGetValue(key, out var kind)) return kind;
      return group == teamGroup ? S2MLiveryMaterial.Color : S2MLiveryMaterial.None;
    }

    private void ReadMaterials(string installation)
    {
      var effects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      string folder = Path.Combine(installation, "materials");
      foreach (string path in Directory.EnumerateFiles(folder, "*.materials.xml"))
        foreach (var node in ReadXml(path).Descendants("material"))
        {
          string name = (string)node.Attribute("name"), effect = (string)node.Attribute("effect");
          if (name != null && effect != null) effects[name] = effect;
        }
      foreach (string path in Directory.EnumerateFiles(folder, "*.materialmappings.xml"))
        foreach (var node in ReadXml(path).Descendants("map"))
        {
          string model = (string)node.Attribute("model"), texture = (string)node.Attribute("texture"), material = (string)node.Attribute("material");
          if (model == null || texture == null || material == null) continue;
          string effect = effects.TryGetValue(material, out var found) ? found : material;
          var kind = effect.IndexOf("TeamCrest", StringComparison.OrdinalIgnoreCase) >= 0 ? S2MLiveryMaterial.Crest
            : effect.IndexOf("TeamColour", StringComparison.OrdinalIgnoreCase) >= 0 ? S2MLiveryMaterial.Color
            : effect.IndexOf("UberLow", StringComparison.OrdinalIgnoreCase) >= 0 ? S2MLiveryMaterial.AlphaMask : S2MLiveryMaterial.None;
          mappings[model.Replace('\\', '/') + "|" + Path.GetFileNameWithoutExtension(texture.Replace('\\', '/'))] = kind;
        }
    }
    private static XDocument ReadXml(string path)
    {
      using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
      return XDocument.Load(reader);
    }

    private void LoadCrests(string installation)
    {
      if (!SystemInfo.supports2DArrayTextures) throw new NotSupportedException("Team crests require texture arrays.");
      CrestArray = new Texture2DArray(128, 128, NeutralCrest + 1, TextureFormat.RGBA32, true, false)
      { name = "Shared Lord Crests", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
      for (int i = 0; i <= NeutralCrest; i++)
      {
        int number = i == 6 ? 8 : i == 8 ? 9 : i == 9 ? 6 : i;
        string file = i == 11 ? "white_knight_crest.png" : i == 12 ? "black_knight_crest.png" : $"crest{number}.PNG";
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, true, false) { name = file, wrapMode = TextureWrapMode.Repeat };
        crestTextures.Add(texture);
        Color32[] pixels;
        Texture2D custom = null;
        foreach (var entry in Settings.PaletteOverrides ?? Array.Empty<S2MLiveryOverride>())
          if (entry != null && entry.Choice == i && entry.Crest != null) custom = entry.Crest;
        if (custom != null)
        {
          if (custom.width != 128 || custom.height != 128 || !custom.isReadable) throw new ArgumentException("Custom crests must be readable 128×128 textures.");
          pixels = custom.GetPixels32();
        }
        else if (i < NeutralCrest && File.Exists(Path.Combine(installation, "crests", file)))
        {
          if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Path.Combine(installation, "crests", file)), false)
            || texture.width != 128 || texture.height != 128) throw new InvalidDataException("Invalid crest: " + file);
          pixels = texture.GetPixels32();
          if (Settings.FlipNativeCrestVertically)
            for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
              (pixels[y * 128 + x], pixels[(127 - y) * 128 + x]) = (pixels[(127 - y) * 128 + x], pixels[y * 128 + x]);
        }
        else
        {
          pixels = new Color32[128 * 128];
          for (int p = 0; p < pixels.Length; p++) pixels[p] = i == NeutralCrest ? (Color32)Settings.UnassignedColor : new Color32(255, 255, 255, 255);
          if (i < NeutralCrest) Debug.LogWarning("Missing lord crest: " + file);
        }
        texture.SetPixels32(pixels); texture.Apply(true, false);
        CrestArray.SetPixels32(pixels, i);
      }
      CrestArray.Apply(true, true);
    }

    internal void ApplyBuildings(GameObject root, IReadOnlyList<BuildingPlacement> placements)
    {
      for (int i = 0; i < placements.Count; i++) Apply(root.transform.GetChild(i), ResolveBuilding(placements[i]));
      if (root.TryGetComponent<S2MBuildingAmbientAnimation>(out var animation)) animation.RefreshMaterials();
    }

    private void Apply(Transform instance, S2MLivery livery)
    {
      foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
      {
        Material[] shared = renderer.sharedMaterials;
        for (int i = 0; i < shared.Length; i++)
        {
          Material source = shared[i];
          if (!kinds.TryGetValue(source, out var kind) || kind == S2MLiveryMaterial.None) continue;
          var key = (source, livery.Color, kind == S2MLiveryMaterial.Crest ? livery.Crest : -1);
          if (!variants.TryGetValue(key, out var variant))
          {
            variant = new Material(source) { name = source.name + " Livery", enableInstancing = true };
            // Standard building Lit materials use shared variants, independent of the actor shader.
            if (variant.HasProperty("_LiveryMode")) variant.SetFloat("_LiveryMode", 0);
            if (kind == S2MLiveryMaterial.Crest)
            { variant.SetTexture("_BaseMap", crestTextures[livery.Crest]); variant.SetColor("_BaseColor", Color.white); }
            else variant.SetColor("_BaseColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? livery.Color.gamma : livery.Color);
            variants.Add(key, variant);
          }
          shared[i] = variant;
        }
        renderer.sharedMaterials = shared;
      }
    }

    private void OnDestroy()
    {
      foreach (var material in variants.Values) Release(material);
      foreach (var texture in crestTextures) Release(texture);
      Release(CrestArray);
    }
    private static void Release(UnityEngine.Object obj)
    { if (obj == null) return; if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
  }
}
