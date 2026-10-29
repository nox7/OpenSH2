using System;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Size calibration by scenery category; never changes placement coordinates.</summary>
  public sealed class S2MSceneryModelScales
  {
    public float Vegetation = 1f;
    public float Rocks = 1f;
    public float NaturalStone = 1f;
    public float Grass = 1f;
    public float Crops = 1f;

    internal void Validate()
    {
      Check(Vegetation, nameof(Vegetation)); Check(Rocks, nameof(Rocks));
      Check(NaturalStone, nameof(NaturalStone)); Check(Grass, nameof(Grass)); Check(Crops, nameof(Crops));
    }

    private static void Check(float value, string name)
    {
      if (!(value > 0f) || float.IsInfinity(value))
        throw new ArgumentOutOfRangeException(name, "Model scale must be finite and positive.");
    }

    internal float For(SceneryKind kind) => kind switch
    {
      SceneryKind.Shrubs => Vegetation,
      SceneryKind.Rocks => Rocks,
      SceneryKind.IronOre => Rocks,
      SceneryKind.NaturalStone => NaturalStone,
      SceneryKind.AppleTree or SceneryKind.Hops or SceneryKind.Wheat => Crops,
      SceneryKind.Grass => Grass,
      _ => 1f
    };
  }
}
