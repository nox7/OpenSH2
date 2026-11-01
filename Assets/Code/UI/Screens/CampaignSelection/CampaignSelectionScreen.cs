using System;
using System.Collections.Generic;
using Assets.Code.UI.Assets;
using Assets.Code.UserData;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Screens.CampaignSelection
{
  internal sealed class CampaignSelectionScreen : IFrontendScreen
  {
    private const string Art = Constants.GamePath + "/ui/campaign_screen/";
    private const string General = Constants.GamePath + "/ui/general/";
    private static readonly string[] Titles = {
      "The Search for the King", "First Command", "Edwin's Rescue", "The Bull in the Borderlands",
      "Return to the Monastery", "Olaf Grimtooth, Your Time Has Come", "Defense of the Abbey",
      "Hawks Nest", "Lord Barclay's Castle", "Siege of the Abbey", "Treachery", "To Capture a King"
    };
    private readonly VisualElement root;
    private readonly UserProfileStore profile;
    private readonly CampaignProgress progress;
    private readonly Action back;
    private readonly Action<int> launch;
    private readonly RuntimeUiAssets assets = new();
    private readonly List<Action> unbind = new();
    private readonly Font font;
    private int selected = 1;
    private bool nextHovered;

    public CampaignSelectionScreen(VisualElement root, Action back, UserProfileStore profile, Action<int> launch = null)
    {
      this.root = root; this.back = back; this.profile = profile; this.launch = launch;
      progress = new CampaignProgress(profile);
      for (int chapter = 1; chapter <= 12; chapter++)
      {
        int number = chapter;
        var button = root.Q<Button>("campaign-chapter-" + chapter);
        SetArt(button.Q(className: "normal-art"), Art + "campaign_button.bmp");
        SetArt(button.Q(className: "highlight-art"), Art + "campaign_button_highlight.tga");
        Action select = () => Select(number);
        button.clicked += select;
        unbind.Add(() => button.clicked -= select);
      }
      BindNavigation("campaign-back", "nav_left", back);
      BindNavigation("campaign-continue", "nav_right", Continue);
      // The enabled hover region receives pointer events even while mission launch is disabled.
      var nextHover = root.Q("campaign-continue-hover");
      nextHover.RegisterCallback<PointerEnterEvent>(OnNextEnter);
      nextHover.RegisterCallback<PointerLeaveEvent>(OnNextLeave);
      unbind.Add(() => nextHover.UnregisterCallback<PointerEnterEvent>(OnNextEnter));
      unbind.Add(() => nextHover.UnregisterCallback<PointerLeaveEvent>(OnNextLeave));
      var next = root.Q<Button>("campaign-continue");
      next.SetEnabled(launch != null);
      next.tooltip = launch == null ? "Campaign mission loading is not implemented yet." : "Play selected chapter";
      font = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 18);
      if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
    }

    private void BindNavigation(string name, string art, Action action)
    {
      var button = root.Q<Button>(name);
      SetArt(button.Q(className: "normal-art"), General + art + ".tga");
      SetArt(button.Q(className: "highlight-art"), General + art + "_highlight.tga");
      button.clicked += action;
      unbind.Add(() => button.clicked -= action);
    }

    private void SetArt(VisualElement element, string path)
    {
      var texture = assets.Load(path);
      if (texture != null) element.style.backgroundImage = new StyleBackground(texture);
    }

    private void RefreshProgress()
    {
      for (int chapter = 1; chapter <= 12; chapter++)
      {
        var button = root.Q<Button>("campaign-chapter-" + chapter);
        bool visible = progress.IsVisible(chapter);
        button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) continue;
        string faction = chapter <= 6 ? "red" : chapter <= 9 ? "good" : "bad";
        string state = chapter == progress.LastAttempted || !progress.IsCompleted(chapter) ? "current" : "complete";
        SetArt(button.Q(className: "chapter-symbol"), Art + "symbol_" + faction + "_" + state + ".tga");
      }
    }

    private void Select(int chapter)
    {
      if (!progress.IsVisible(chapter)) return;
      selected = chapter;
      SetArt(root.Q("campaign-background"), Art + "campaign_chapter" + chapter + ".jpg");
      RefreshTitle();
      for (int i = 1; i <= 12; i++)
        root.Q<Button>("campaign-chapter-" + i).EnableInClassList("selected", i == chapter);
    }

    private void OnNextEnter(PointerEnterEvent evt) { nextHovered = true; RefreshTitle(); }
    private void OnNextLeave(PointerLeaveEvent evt) { nextHovered = false; RefreshTitle(); }
    private void RefreshTitle()
    {
      root.Q<Label>("campaign-title").text = nextHovered ? $"Play Mission {selected}" : Titles[selected - 1];
    }

    private void Continue()
    {
      if (launch == null) return;
      profile.RecordWarChapterAttempt(selected);
      launch(selected);
    }

    public void Show()
    {
      nextHovered = false;
      RefreshProgress();
      if (!progress.IsVisible(selected)) selected = 1;
      Select(selected);
      root.style.display = DisplayStyle.Flex;
      root.Focus();
    }
    public void Hide() { nextHovered = false; RefreshTitle(); root.style.display = DisplayStyle.None; }
    public void Tick() { }
    public void Dispose()
    {
      Hide();
      foreach (var remove in unbind) remove();
      unbind.Clear(); assets.Dispose();
      if (font != null) UnityEngine.Object.Destroy(font);
    }
  }
}
