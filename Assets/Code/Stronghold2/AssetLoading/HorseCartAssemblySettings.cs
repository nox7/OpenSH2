using System;

namespace Assets.Code.Stronghold2.AssetLoading
{
  [Serializable]
  public sealed class HorseCartAssemblySettings
  {
    // Actor-local +Z is the harness-forward direction after the model's authored yaw correction.
    public float HorseForwardOffset = 2.5f;
    public void Validate()
    { if(float.IsNaN(HorseForwardOffset) || float.IsInfinity(HorseForwardOffset))throw new ArgumentOutOfRangeException(nameof(HorseForwardOffset)); }
  }
}
