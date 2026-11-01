using System;
using Assets.Code.UI.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Screens.Shared
{
  /// <summary>Shared 1024x768 reference frame and original stone gutters for every frontend page.</summary>
  internal sealed class FrontendViewport : IDisposable
  {
    private readonly VisualElement root, frame, left, right;
    private readonly RuntimeUiAssets assets = new();

    public FrontendViewport(VisualElement root)
    {
      this.root = root;
      frame = root.Q("screen-frame");
      left = root.Q("left-sidebar"); right = root.Q("right-sidebar");
      var stone = assets.Load(UIAssets.StoneSidebar2KTexture);
      if (stone != null)
      {
        left.style.backgroundImage = new StyleBackground(stone);
        right.style.backgroundImage = new StyleBackground(stone);
      }
      root.RegisterCallback<GeometryChangedEvent>(Resize);
      Fit(root.contentRect.width, root.contentRect.height);
    }

    private void Resize(GeometryChangedEvent evt) => Fit(evt.newRect.width, evt.newRect.height);

    private void Fit(float width, float height)
    {
      if (width <= 0 || height <= 0) return;
      float scale = Mathf.Min(width / 1024f, height / 768f);
      float gutter = Mathf.Max(0, (width - 1024f * scale) / 2f);
      frame.style.scale = new Scale(new Vector3(scale, scale, 1));
      frame.style.left = gutter;
      frame.style.top = (height - 768f * scale) / 2f;
      left.style.width = right.style.width = gutter;
    }

    public void Dispose()
    {
      root.UnregisterCallback<GeometryChangedEvent>(Resize);
      assets.Dispose();
    }
  }
}
