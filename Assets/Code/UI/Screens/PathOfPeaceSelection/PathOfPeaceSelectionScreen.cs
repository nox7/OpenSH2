using System;
using Assets.Code.UI.Assets;
using Assets.Code.UI.Screens.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Screens.PathOfPeaceSelection
{
  internal sealed class PathOfPeaceSelectionScreen : IFrontendScreen
  {
    private const string Art = Constants.GamePath + "/ui/war_peace_menu/";
    private const string General = Constants.GamePath + "/ui/general/";
    private readonly VisualElement root;
    private readonly Action back;
    private readonly RuntimeUiAssets assets = new();
    private readonly ScreenContext context;
    private readonly Font font;

    public PathOfPeaceSelectionScreen(VisualElement root, Action back)
    {
      this.root = root;
      this.back = back;
      SetArtwork(root.Q("peace-selection-background"), Art + "background_lion_side.jpg");
      context = new ScreenContext(root.Q<Label>("peace-selection-description"), preview: root.Q<Image>("peace-selection-preview"));
      BindMode("peace-sim-campaign", "peace_simcampaign.jpg", "Help the King rebuild the county, in this gentler\nSim campaign");
      BindMode("peace-free-build", "peace_freebuild.jpg", "Build peacefully in a setting of your own\nchoosing");
      BindMode("peace-custom-maps", "warpeace_custom.jpg", "Load a user created sim mission");
      var previous = root.Q<Button>("peace-previous-menu");
      SetArtwork(previous.Q(className: "normal-art"), General + "nav_left.tga");
      SetArtwork(previous.Q(className: "highlight-art"), General + "nav_left_highlight.tga");
      context.Bind(previous, "Previous menu");
      previous.clicked += back;
      font = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 28);
      if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
    }

    private void BindMode(string name, string preview, string description)
    {
      var button = root.Q<Button>(name);
      SetArtwork(button.Q(className: "normal-art"), UIAssets.FancyButtonTexture);
      SetArtwork(button.Q(className: "highlight-art"), UIAssets.FancyButtonGreenHighlightTexture);
      context.Bind(button, description, assets.Load(Art + preview));
    }

    private void SetArtwork(VisualElement element, string path)
    {
      var texture = assets.Load(path);
      if (texture != null) element.style.backgroundImage = new StyleBackground(texture);
    }

    public void Show() { root.style.display = DisplayStyle.Flex; context.Show(); root.Focus(); }
    public void Hide() { context.Hide(); root.style.display = DisplayStyle.None; }
    public void Tick() { }
    public void Dispose()
    {
      Hide();
      root.Q<Button>("peace-previous-menu").clicked -= back;
      context.Dispose();
      assets.Dispose();
      if (font != null) UnityEngine.Object.Destroy(font);
    }
  }
}
