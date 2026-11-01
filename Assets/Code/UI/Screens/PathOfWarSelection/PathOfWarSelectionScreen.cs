using System;
using Assets.Code.UI.Assets;
using Assets.Code.UI.Screens.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Screens.PathOfWarSelection
{
  internal sealed class PathOfWarSelectionScreen : IFrontendScreen
  {
    private const string Art = Constants.GamePath + "/ui/war_peace_menu/";
    private const string General = Constants.GamePath + "/ui/general/";
    private readonly VisualElement root;
    private readonly Action back;
    private readonly Action campaign;
    private readonly RuntimeUiAssets assets = new();
    private readonly ScreenContext context;
    private readonly Font font;

    public PathOfWarSelectionScreen(VisualElement root, Action back, Action campaign)
    {
      this.root = root;
      this.back = back;
      this.campaign = campaign;
      root.Q<Button>("war-campaign").clicked += campaign;
      SetArtwork(root.Q("war-selection-background"), Art + "background_lion_side.jpg");
      context = new ScreenContext(root.Q<Label>("war-selection-description"), preview: root.Q<Image>("war-selection-preview"));
      BindMode("war-campaign", Art + "war_campaign.jpg", "Play the 'Lost King' campaign");
      BindMode("war-kingmaker", Art + "war_kingmaker.jpg", "Skirmish for the crown, in a Kingmaker\nchallenge");
      BindMode("war-siege", Art + "war_siege.jpg", "Relive history. Siege a real castle!");
      BindMode("war-conquest-trail", Constants.GamePath + "/ui/gold_edition/war_conquest.jpg", "Seige and defend the Strongholds of Europe as\nyou conquer the continent.");
      BindMode("war-custom", Art + "warpeace_custom.jpg", "Load a user created scenario");
      var previous = root.Q<Button>("war-previous-menu");
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
      context.Bind(button, description, assets.Load(preview));
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
      root.Q<Button>("war-previous-menu").clicked -= back;
      root.Q<Button>("war-campaign").clicked -= campaign;
      context.Dispose();
      assets.Dispose();
      if (font != null) UnityEngine.Object.Destroy(font);
    }
  }
}
