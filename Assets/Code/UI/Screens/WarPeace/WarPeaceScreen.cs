using System;
using Assets.Code.UI.Assets;
using Assets.Code.UI.Screens.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Screens.WarPeace
{
  /// <summary>The original path selection artwork and pointer/keyboard context descriptions.</summary>
  internal sealed class WarPeaceScreen : IFrontendScreen
  {
    private const string Art = Constants.GamePath + "/ui/war_peace_menu/";
    private const string General = Constants.GamePath + "/ui/general/";
    private readonly VisualElement root;
    private readonly Label description;
    private readonly ScreenContext context;
    private readonly Action peace;
    private readonly Action war;
    private readonly Action back;
    private readonly RuntimeUiAssets assets = new();
    private Font font;

    public WarPeaceScreen(VisualElement root, Action back, Action peace, Action war)
    {
      this.root = root;
      this.back = back;
      this.peace = peace;
      this.war = war;
      description = root.Q<Label>("path-description");
      context = new ScreenContext(description, "Choose Your Path");
      SetArtwork(root.Q("war-peace-background"), Art + "war_peace_menu_back.jpg");
      Bind("peace", Art + "peace_off.jpg", Art + "peace_on.jpg",
        "The Path of Peace\n\nGentler games modes, focusing mainly on castle life.");
      Bind("war", Art + "war_off.jpg", Art + "war_on.jpg",
        "The Path of War\n\nCombat intensive styles of play.");
      Bind("previous-menu", General + "nav_left.tga", General + "nav_left_highlight.tga", "Previous Menu");
      Bind("tutorial", Art + "tutorial_button_off.tga", Art + "tutorial_button_on.tga", "Play the Tutorial");
      root.Q<Button>("previous-menu").clicked += back;
      root.Q<Button>("peace").clicked += peace;
      root.Q<Button>("war").clicked += war;
      // Use an installed serif font; no proprietary font is copied into the port.
      font = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 16);
      if (font != null) description.style.unityFontDefinition = FontDefinition.FromFont(font);
    }

    private void Bind(string name, string off, string on, string text)
    {
      var button = root.Q<Button>(name);
      context.Bind(button, text);
      SetArtwork(button.Q(className: "normal-art"), off);
      SetArtwork(button.Q(className: "highlight-art"), on);
    }

    private void SetArtwork(VisualElement element, string path)
    {
      var texture = assets.Load(path);
      if (texture != null) element.style.backgroundImage = new StyleBackground(texture);
    }

    public void Show()
    {
      context.Show();
      root.style.display = DisplayStyle.Flex;
      root.Focus();
    }

    public void Hide()
    {
      context.Hide();
      root.style.display = DisplayStyle.None;
    }

    public void Tick() { }

    public void Dispose()
    {
      Hide();
      root.Q<Button>("previous-menu").clicked -= back;
      root.Q<Button>("peace").clicked -= peace;
      root.Q<Button>("war").clicked -= war;
      context.Dispose();
      assets.Dispose();
      if (font != null) UnityEngine.Object.Destroy(font);
    }
  }
}
