using System.Collections;
using Assets.Code.UI.Video;
using Assets.Code.UI.Video.EmbeddedFFmpeg;
using Assets.Code.Video;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Frontend
{
  /// <summary>Owns startup UI and its lifetime. Main only needs to call Begin.</summary>
  public sealed class FrontendController : MonoBehaviour
  {
    private Game game;
    private UIDocument document;
    private PanelSettings panel;
    private VisualElement loading;
    private VisualElement intro;
    private VisualElement menu;
    private IVideoSurface introVideo;
    private MainMenuView mainMenu;
    private bool skipRequested;

    internal void Begin(Game game)
    {
      this.game = game;
      panel = ScriptableObject.CreateInstance<PanelSettings>();
      panel.name = "OpenSH2 Frontend Panel";
      panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("OpenSH2/Frontend/FrontendTheme");
      panel.scaleMode = PanelScaleMode.ConstantPixelSize;
      panel.sortingOrder = 1000;
      document = gameObject.AddComponent<UIDocument>();
      document.panelSettings = panel;
      document.visualTreeAsset = Resources.Load<VisualTreeAsset>("OpenSH2/Frontend/Frontend");
      if (document.visualTreeAsset == null)
        throw new System.InvalidOperationException("OpenSH2 frontend UXML is missing.");
      var root = document.rootVisualElement;
      root.style.flexGrow = 1;
      root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      loading = root.Q("loading-screen");
      intro = root.Q("intro-screen");
      menu = root.Q("menu-screen");
      intro.RegisterCallback<PointerDownEvent>(OnIntroPointerDown);
      mainMenu = new MainMenuView(menu);
      StartCoroutine(Startup());
    }

    private IEnumerator Startup()
    {
      game.State = GameState.LOADING_ASSETS;
      Show(loading);
      // Videos stream directly from their original Bink files; no conversion or disk cache is needed.
      yield return null;

      game.State = GameState.INTRO_VIDEOS;
      Show(intro);
      yield return PlayIntro(VideoFilePaths.FireflyLogo);
      yield return PlayIntro(VideoFilePaths.Intro);

      game.State = GameState.MAIN_MENU;
      Show(menu);
      mainMenu.Show();
    }

    private IEnumerator PlayIntro(string originalPath)
    {
      skipRequested = false;
      introVideo = new BinkVideoSurface(intro.Q<Image>("intro-video"), originalPath, false,
        audioParent: transform, withAudio: true);
      while (!introVideo.IsFinished && !skipRequested) yield return null;
      introVideo.Dispose();
      introVideo = null;
      // A click skips only the current video, never the next video or a menu control.
      yield return null;
    }

    private void OnIntroPointerDown(PointerDownEvent evt)
    {
      if (evt.button != 0 || introVideo == null) return;
      skipRequested = true;
      evt.StopPropagation();
    }

    private void Show(VisualElement screen)
    {
      loading.style.display = loading == screen ? DisplayStyle.Flex : DisplayStyle.None;
      intro.style.display = intro == screen ? DisplayStyle.Flex : DisplayStyle.None;
      menu.style.display = menu == screen ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Update()
    {
      introVideo?.Tick();
      mainMenu?.Tick();
    }

    private void OnDisable()
    {
      StopAllCoroutines();
      introVideo?.Dispose();
      introVideo = null;
      mainMenu?.Dispose();
      mainMenu = null;
      intro?.UnregisterCallback<PointerDownEvent>(OnIntroPointerDown);
      if (document != null) Destroy(document);
      if (panel != null) Destroy(panel);
    }
  }
}
