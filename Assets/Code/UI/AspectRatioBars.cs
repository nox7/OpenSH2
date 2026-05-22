using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// Manages the stone bars that appear on the left and right of the screen when game UI are shown but were designed for a different
  /// aspect ratio than the user's display.
  /// </summary>
  class AspectRatioBars
  {
    public GameObject UIRoot = null;
    private RectTransform leftBarRect;
    private RectTransform rightBarRect;

    public void Show()
    {
      if (UIRoot != null) {
        return;
      }

      UIRoot = new GameObject("AspectRatioBars");

      Canvas canvas = UIRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 5;

      UIRoot.AddComponent<CanvasScaler>();
      UIRoot.AddComponent<GraphicRaycaster>();

      GameObject leftBar = new("LeftAspectRatioBar");
      leftBar.transform.SetParent(UIRoot.transform, false);

      AspectRatioFitter leftARFitter = leftBar.AddComponent<AspectRatioFitter>();
      leftARFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
      leftARFitter.aspectRatio = 211f / 900f;

      Image leftBarImage = leftBar.AddComponent<Image>();
      leftBarImage.sprite = SpriteLoader.LoadSprite(UIAssets.StoneSidebar2KTexture);
      leftBarImage.raycastTarget = false;

      leftBarRect = leftBar.GetComponent<RectTransform>();
      leftBarRect.anchorMin = new Vector2(0f, 1f);
      leftBarRect.anchorMax = new Vector2(0f, 1f);
      leftBarRect.pivot = new Vector2(0f, 1f);

      GameObject rightBar = new("RightAspectRatioBar");
      rightBar.transform.SetParent(UIRoot.transform, false);

      AspectRatioFitter rightARFitter = rightBar.AddComponent<AspectRatioFitter>();
      rightARFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
      rightARFitter.aspectRatio = 211f / 900f;

      Image rightBarImage = rightBar.AddComponent<Image>();
      rightBarImage.sprite = SpriteLoader.LoadSprite(UIAssets.StoneSidebar2KTexture);
      rightBarImage.raycastTarget = false;

      rightBarRect = rightBar.GetComponent<RectTransform>();
      rightBarRect.anchorMin = new Vector2(1f, 0f);
      rightBarRect.anchorMax = new Vector2(1f, 1f);
      rightBarRect.pivot = new Vector2(1f, 1f);
      rightBarRect.anchoredPosition = Vector2.zero;

      UpdateLayout();
    }

    public void UpdateLayout()
    {
      if (leftBarRect == null || rightBarRect == null)
      {
        return;
      }

      float contentWidth = Screen.height * (4f / 3f);
      float gutterWidth = Mathf.Max(0f, (Screen.width - contentWidth) * 0.5f);
      leftBarRect.sizeDelta = new Vector2(gutterWidth, 0f);
      rightBarRect.sizeDelta = new Vector2(gutterWidth, 0f);
    }

    public void Hide()
    {
      if (UIRoot != null)
      {
        Object.Destroy(UIRoot);
        UIRoot = null;
        leftBarRect = null;
        rightBarRect = null;
      }
    }
  }
}
