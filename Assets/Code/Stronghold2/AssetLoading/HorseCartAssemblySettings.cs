using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  [Serializable]
  public sealed class HorseCartAssemblySettings
  {
    // Actor-local +Z is the harness-forward direction after the model's authored yaw correction.
    public float HorseForwardOffset = 2.5f;
    [Tooltip("Centers the upper saw action on the work log cutting plane. Actor-local units, before unit scaling.")]
    public Vector3 SawingSeatOffset = new(.02f,-.8f,-1.1f);
    public Vector3 SawingSeatEulerAngles = Vector3.zero;
    public Vector3 LogLoadOffset = new(0,0,-.7f);
    public void Validate()
    {
      if(float.IsNaN(HorseForwardOffset) || float.IsInfinity(HorseForwardOffset))throw new ArgumentOutOfRangeException(nameof(HorseForwardOffset));
      for(int i=0;i<3;i++)if(float.IsNaN(SawingSeatOffset[i]) || float.IsInfinity(SawingSeatOffset[i]))throw new ArgumentOutOfRangeException(nameof(SawingSeatOffset));
      for(int i=0;i<3;i++)if(float.IsNaN(SawingSeatEulerAngles[i]) || float.IsInfinity(SawingSeatEulerAngles[i]))throw new ArgumentOutOfRangeException(nameof(SawingSeatEulerAngles));
      for(int i=0;i<3;i++)if(float.IsNaN(LogLoadOffset[i]) || float.IsInfinity(LogLoadOffset[i]))throw new ArgumentOutOfRangeException(nameof(LogLoadOffset));
    }
  }
}
