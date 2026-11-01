using System;
using System.Collections.Generic;

namespace Assets.Code.UI.Screens
{
  /// <summary>Owns frontend pages; navigation hides the old page before showing the next.</summary>
  internal sealed class FrontendScreenRouter : IDisposable
  {
    private readonly Dictionary<FrontendScreenId, IFrontendScreen> screens = new();
    private readonly Dictionary<FrontendScreenId, Func<IFrontendScreen>> factories = new();
    private IFrontendScreen current;
    public FrontendScreenId? CurrentScreen { get; private set; }

    public void Register(FrontendScreenId id, Func<IFrontendScreen> factory) => factories.Add(id, factory);

    public void Navigate(FrontendScreenId id)
    {
      if (!screens.TryGetValue(id, out var next))
      {
        next = factories[id]();
        screens.Add(id, next);
      }
      if (current == next) return;
      current?.Hide();
      current = next;
      CurrentScreen = id;
      current.Show();
    }

    public void Tick() => current?.Tick();

    public void Dispose()
    {
      foreach (var screen in screens.Values) screen.Dispose();
      screens.Clear();
      factories.Clear();
      current = null;
      CurrentScreen = null;
    }
  }
}
