using System;
using Assets.Code.UI.Video;
using Assets.Code.UI.Video.EmbeddedFFmpeg;
using Assets.Code.Video;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Frontend
{
  /// <summary>Binds original game artwork and video playback to the menu's declarative layout.</summary>
  internal sealed class MainMenuView : IDisposable
  {
    private readonly Transform host;
    private readonly VisualElement root;
    private readonly VisualElement frame;
    private readonly RuntimeUiAssets assets = new();
    private readonly bool useEmbeddedBink;
    private IVideoSurface background;
    private IVideoSurface opening;
    private IVideoSurface idle;
    private bool shown;
    private bool idleRequested;

    public MainMenuView(Transform host, VisualElement root, bool useEmbeddedBink = false)
    {
      this.host = host;
      this.root = root;
      this.useEmbeddedBink = useEmbeddedBink;
      frame = root.Q("menu-frame");
      root.RegisterCallback<GeometryChangedEvent>(Resize);
    }

    public void Show()
    {
      if (shown) return;
      shown = true;
      root.Q<Image>("logo").image = assets.Load(UIAssets.LogoTexture);
      SetBackground(root.Q("left-sidebar"), UIAssets.StoneSidebar2KTexture);
      SetBackground(root.Q("right-sidebar"), UIAssets.StoneSidebar2KTexture);
      foreach (var button in root.Query<Button>().ToList())
      {
        SetBackground(button, UIAssets.FancyButtonTexture);
        SetBackground(button.Q(className: "button-highlight"), UIAssets.FancyButtonHighlightTexture);
      }
      root.Q<Button>("exit").clicked += Exit;
      background = Video("background-video", VideoFilePaths.MainMenuBackground, true);
      opening = Video("curtain-opening", VideoFilePaths.MainMenuCurtainOpen, false);
      // Decode/prepare idle in advance, but reveal it only after it produces its first frame.
      idle = Video("curtain-idle", VideoFilePaths.MainMenuCurtainIdle, true, false);
      idle.FirstFrame += HideOpening;
      ResizeFrame(root.contentRect.width, root.contentRect.height);
    }

    private IVideoSurface Video(string element, string path, bool loop, bool autoplay = true)
    {
      var image = root.Q<Image>(element);
      return useEmbeddedBink
        ? new BinkVideoSurface(image, path, loop, autoplay)
        : new VideoSurface(host, image, VideoFilePaths.GetCachedVideoPath(path), loop, autoplay);
    }

    private void SetBackground(VisualElement element, string path)
    {
      var texture = assets.Load(path);
      if (texture != null) element.style.backgroundImage = new StyleBackground(texture);
    }

    public void Tick()
    {
      if (!shown) return;
      background?.Tick();
      opening?.Tick();
      idle?.Tick();
      if (!idleRequested && opening != null && opening.IsFinished)
      {
        idleRequested = true;
        idle.Play();
      }
    }

    private void HideOpening()
    {
      opening?.Dispose();
      opening = null;
    }

    private void Resize(GeometryChangedEvent evt) => ResizeFrame(evt.newRect.width, evt.newRect.height);

    private void ResizeFrame(float width, float height)
    {
      if (width <= 0 || height <= 0) return;
      float scale = Mathf.Min(width / 1024f, height / 768f);
      frame.style.scale = new Scale(new Vector3(scale, scale, 1));
      frame.style.left = (width - 1024f * scale) / 2f;
      frame.style.top = (height - 768f * scale) / 2f;
      float gutter = Mathf.Max(0, (width - 1024f * scale) / 2f);
      root.Q("left-sidebar").style.width = gutter;
      root.Q("right-sidebar").style.width = gutter;
    }

    private static void Exit()
    {
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }

    public void Dispose()
    {
      root.UnregisterCallback<GeometryChangedEvent>(Resize);
      root.Q<Button>("exit").clicked -= Exit;
      background?.Dispose();
      opening?.Dispose();
      if (idle != null) idle.FirstFrame -= HideOpening;
      idle?.Dispose();
      assets.Dispose();
      shown = false;
    }
  }
}
