using System;
using Assets.Code.UI.Assets;
using Assets.Code.UI.Video;
using Assets.Code.UI.Video.EmbeddedFFmpeg;
using Assets.Code.Video;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Screens.MainMenu
{
  /// <summary>Binds original game artwork and video playback to the menu's declarative layout.</summary>
  internal sealed class MainMenuScreen : IFrontendScreen
  {
    private readonly VisualElement root;
    private readonly Action play;
    private readonly RuntimeUiAssets assets = new();
    private IVideoSurface background;
    private IVideoSurface opening;
    private IVideoSurface idle;
    private readonly Font font;
    private bool shown;
    private bool idleRequested;

    public MainMenuScreen(VisualElement root, Action play)
    {
      this.root = root;
      this.play = play;
      root.Q<Label>("build-version").text = $"v{Constants.Version} OpenSH2";
      font = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 18);
      if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
      root.Q<Button>("play").clicked += play;
      root.Q<Button>("exit").clicked += Exit;
    }

    public void Show()
    {
      if (shown) return;
      shown = true;
      root.Q<Image>("logo").image = assets.Load(UIAssets.LogoTexture);
      foreach (var button in root.Query<Button>().ToList())
      {
        SetBackground(button, UIAssets.FancyButtonTexture);
        SetBackground(button.Q(className: "button-highlight"), UIAssets.FancyButtonHighlightTexture);
      }
      root.style.display = DisplayStyle.Flex;
      background = Video("background-video", VideoFilePaths.MainMenuBackground, true);
      opening = Video("curtain-opening", VideoFilePaths.MainMenuCurtainOpen, false);
      // Decode/prepare idle in advance, but reveal it only after it produces its first frame.
      idle = Video("curtain-idle", VideoFilePaths.MainMenuCurtainIdle, true, false);
      idle.FirstFrame += HideOpening;
    }

    private IVideoSurface Video(string element, string path, bool loop, bool autoplay = true)
    {
      var image = root.Q<Image>(element);
      return new BinkVideoSurface(image, path, loop, autoplay);
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

    private static void Exit()
    {
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }

    public void Hide()
    {
      root.style.display = DisplayStyle.None;
      background?.Dispose();
      opening?.Dispose();
      if (idle != null) idle.FirstFrame -= HideOpening;
      idle?.Dispose();
      background = opening = idle = null;
      idleRequested = false;
      shown = false;
    }

    public void Dispose()
    {
      Hide();
      root.Q<Button>("play").clicked -= play;
      root.Q<Button>("exit").clicked -= Exit;
      assets.Dispose();
      if (font != null) UnityEngine.Object.Destroy(font);
    }
  }
}
