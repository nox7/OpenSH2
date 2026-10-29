using Assets.Code.Simulation.Economy;
namespace Assets.Code.Simulation.Labor
{
  /// <summary>Typed, exclusively reserved local production buffers collected by ox handlers.</summary>
  public interface IOxProduction
  {
    Goods Resource { get; }
    int StoredGoods { get; }
    int? HaulerId { get; }
    bool TryTakeGoods(int count);
    bool TryReserveHauler(int handler);
    void ReleaseHauler(int? handler);
  }
}
