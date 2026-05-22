using Assets.Code.Video;
using UnityEngine;

namespace Assets.Code.UI
{
  class MainMenuVideoUiManager
  {
    private const float BackgroundWidthPercent = 0.71875f;
    private const float CurtainPercent = 0.325f;

    private VideoPlayerUI backgroundPlayer;
    private VideoPlayerUI curtainPlayer;
    private bool isVisible;
    private RectTransform parent;
    private float lastParentWidth;
    private float lastParentHeight;

    public void Show(RectTransform parentRect)
    {
      if (isVisible)
      {
        return;
      }

      parent = parentRect;

      backgroundPlayer = VideoPlayerUI.Play(
        GetCachedVideoPath(VideoFilePaths.MainMenuBackground),
        onFinished: null,
        fadeOutMs: 0,
        sortingOrder: 900,
        isLooping: true,
        width: parent.rect.width * BackgroundWidthPercent,
        anchorX: 1f,
        pivotX: 1f,
        parent: parent);

      curtainPlayer = VideoPlayerUI.Play(
        GetCachedVideoPath(VideoFilePaths.MainMenuCurtainOpen),
        onFinished: PlayCurtainIdleLoop,
        fadeOutMs: 0,
        sortingOrder: 1000,
        isLooping: false,
        width: parent.rect.width * CurtainPercent,
        anchorX: 0f,
        pivotX: 0f,
        parent: parent);

      isVisible = true;
      UpdateLayout(force: true);
    }

    public void UpdateLayout(bool force = false)
    {
      if (!isVisible || parent == null)
      {
        return;
      }

      float parentWidth = parent.rect.width;
      float parentHeight = parent.rect.height;
      if (!force && Mathf.Approximately(lastParentWidth, parentWidth) && Mathf.Approximately(lastParentHeight, parentHeight))
      {
        return;
      }

      lastParentWidth = parentWidth;
      lastParentHeight = parentHeight;

      backgroundPlayer?.UpdateLayout(
        width: parentWidth * BackgroundWidthPercent,
        anchorX: 1f,
        pivotX: 1f);

      curtainPlayer?.UpdateLayout(
        width: parent.rect.width * CurtainPercent,
        anchorX: 0f,
        pivotX: 0f);
    }

    public void Hide()
    {
      if (!isVisible)
      {
        return;
      }

      if (curtainPlayer != null)
      {
        curtainPlayer.Stop();
        curtainPlayer = null;
      }

      if (backgroundPlayer != null)
      {
        backgroundPlayer.Stop();
        backgroundPlayer = null;
      }

      isVisible = false;
      parent = null;
      lastParentWidth = 0f;
      lastParentHeight = 0f;
    }

    private void PlayCurtainIdleLoop()
    {
      if (!isVisible)
      {
        return;
      }

      curtainPlayer = VideoPlayerUI.Play(
        GetCachedVideoPath(VideoFilePaths.MainMenuCurtainIdle),
        onFinished: null,
        fadeOutMs: 0,
        sortingOrder: 1000,
        isLooping: true,
        width: parent.rect.width * CurtainPercent,
        anchorX: 0f,
        pivotX: 0f,
        parent: parent);

      UpdateLayout(force: true);
    }

    private string GetCachedVideoPath(string originalBikPath)
    {
      return VideoFilePaths.GetCachedVideoPath(originalBikPath);
    }
  }
}
