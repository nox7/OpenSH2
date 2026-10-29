using System;
namespace Assets.Code.Simulation.Economy
{
  /// <summary>Shared by all workers delivering to this map's stockpiles; uses simulation seconds.</summary>
  [Serializable] public sealed class StockpileDeliverySettings
  {
    public float SecondsPerItem=.15f;
    public double IntervalSeconds
    {get{Validate();return SecondsPerItem;}}
    public void Validate()
    {if(!(SecondsPerItem>0) || float.IsInfinity(SecondsPerItem))throw new ArgumentOutOfRangeException(nameof(SecondsPerItem));}
  }
}
