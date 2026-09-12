using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>Confirmed tag-27 terrain material IDs and their installed DDS assets.</summary>
  public static class S2MTerrainTextureAssetCatalog
  {
    private static readonly IReadOnlyDictionary<byte, string> AssetPaths =
      new Dictionary<byte, string>
      {
        [0x08] = "terrain/mountain.dds",
        [0x09] = "terrain/mountain_edge.dds",
        [0x0A] = "terrain/grass_rough.dds",
        [0x0B] = "terrain/grass_dark.dds",
        [0x0C] = "terrain/grass_lush.dds",
        [0x0D] = "terrain/grass_yellow_3.dds",
        [0x0E] = "terrain/grass_yellow_2.dds",
        [0x0F] = "terrain/grass_yellow.dds",
        [0x10] = "terrain/mud.dds",
        [0x11] = "terrain/cracked_peat.dds",
        [0x12] = "terrain/building_blend.dds",
        [0x13] = "terrain/mud_edge.dds",
        [0x14] = "terrain/sand.dds",
        [0x15] = "terrain/sand_dunes.dds",
        [0x16] = "terrain/sea_bed.dds",
        [0x17] = "terrain/sand_edge.dds",
        [0x18] = "terrain/heath.dds",
        [0x19] = "terrain/heath_2.dds",
        [0x1A] = "terrain/heath_edge.dds",
        [0x1B] = "terrain/grass_flowers.dds",
        [0x1C] = "terrain/stoney_ground.dds",
        [0x1D] = "terrain/rubble.dds",
        [0x1E] = "terrain/rock_1.dds",
        [0x1F] = "terrain/forest_floor.dds",
        [0x27] = "terrain/cliff_face.dds"
      };

    public static IReadOnlyDictionary<byte, string> ConfirmedAssetPaths => AssetPaths;

    public static bool TryGetAssetPath(byte materialId, out string assetPath) =>
      AssetPaths.TryGetValue(materialId, out assetPath);
  }
}
