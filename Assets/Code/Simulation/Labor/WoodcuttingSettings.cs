using System;

namespace Assets.Code.Simulation.Labor
{
  [Serializable]
  public sealed class WoodcuttingSettings
  {
    // Zero searches the whole loaded map; positive values limit the work area.
    public float SearchRadiusCells=0;
    public float RetrySeconds=5;
    // Live map cells per simulation second, independent of model size and foot speed.
    public float CartSpeedCellsPerSecond=.75f;
    public float CartParkingDistance=6f;
    public float DismountDistance=1.25f;
    public float ChopDistance=1.1f;
    public float TreeTrunkRadius=.3f;
    public float BushRadius=.15f;
    public float FallSeconds=1.5f;
    public float SawingSeconds=18f;
    public int FellingCycles=3;
    public int ChoppingCycles=2;
    public int LogsPerTree=6;
    public int LogsPerBush=1;
    public float LogPileScale=1;
    public float WheelRadius=1.03f;
    [NonSerialized] public float UnitScale=1;
    public void Validate()
    {
      foreach(float value in new[]{RetrySeconds,CartSpeedCellsPerSecond,CartParkingDistance,DismountDistance,ChopDistance,TreeTrunkRadius,BushRadius,FallSeconds,SawingSeconds,WheelRadius,UnitScale,LogPileScale})
        if(!(value>0) || float.IsInfinity(value))throw new ArgumentOutOfRangeException(nameof(WoodcuttingSettings));
      if(!(SearchRadiusCells>=0) || float.IsInfinity(SearchRadiusCells) || FellingCycles<1 || ChoppingCycles<1
        || LogsPerTree<1 || LogsPerTree>6 || LogsPerBush<1 || LogsPerBush>6
        || CartParkingDistance<=ChopDistance)throw new ArgumentOutOfRangeException(nameof(WoodcuttingSettings));
    }
  }
}
