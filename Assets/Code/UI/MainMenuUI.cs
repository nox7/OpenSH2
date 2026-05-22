using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// Responsible for showing and hiding the main menu UI as well as handling
  /// any interactions with it.
  /// </summary>
  class MainMenuUI
  {
    public MainMenuVideoUiManager VideoUiManager = new();
    public bool IsShown = false;
    private GameObject uiRoot;
    private RectTransform contentRoot;
    private RectTransform videoRoot;
    private RectTransform menuColumn;
    private AspectRatioBars AspectRatioBars;
    private int lastScreenWidth;
    private int lastScreenHeight;

    /// <summary>
    /// Creates all UI elements for the main menu
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      AspectRatioBars = new();
      AspectRatioBars.Show();
      EnsureEventSystem();
      CreateUiRoot();
      CreateLogo();
      CreateMenuButtons();
      UpdateLayout();
      VideoUiManager.Show(videoRoot);
      IsShown = true;
    }

    public void Hide()
    {

      if (uiRoot != null)
      {
        VideoUiManager.Hide();
        AspectRatioBars?.Hide();
        Object.Destroy(uiRoot);
        uiRoot = null;
        contentRoot = null;
        videoRoot = null;
        menuColumn = null;
        lastScreenWidth = 0;
        lastScreenHeight = 0;
      }

      IsShown = false;
    }

    /// <summary>
    /// Creates the logo that appears above the menu buttons along the curtains video
    /// </summary>
    private void CreateLogo()
    {
      GameObject logoObject = new("MainMenuLogo");
      logoObject.transform.SetParent(menuColumn, false);

      Image logoImage = logoObject.AddComponent<Image>();
      logoImage.sprite = SpriteLoader.LoadSprite(UIAssets.LogoTexture);
      logoImage.preserveAspect = true;

      LayoutElement layoutElement = logoObject.AddComponent<LayoutElement>();
      layoutElement.preferredWidth = 340f;
      layoutElement.preferredHeight = 170f;
      layoutElement.minHeight = 170f;
    }

    /// <summary>
    /// Creates the menu buttons that appear along the curtains video
    /// </summary>
    private void CreateMenuButtons()
    {
      CreateMenuButton("Play");
      CreateMenuButton("Multiplayer");
      CreateMenuButton("Map Editor");
      CreateMenuButton("Options");
      CreateMenuButton("Load");
      CreateMenuButton("Exit");
    }

    private void CreateUiRoot()
    {
      uiRoot = new GameObject("MainMenuUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1100;

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("MainMenuContentRoot");
      contentObject.transform.SetParent(uiRoot.transform, false);
      contentRoot = contentObject.AddComponent<RectTransform>();
      contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
      contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
      contentRoot.pivot = new Vector2(0.5f, 0.5f);
      contentRoot.sizeDelta = new Vector2(Screen.height * (4f / 3f), Screen.height);
      contentRoot.anchoredPosition = new Vector2(0f, 0f);

      GameObject videoObject = new("MainMenuVideoRoot");
      videoObject.transform.SetParent(contentRoot, false);
      videoRoot = videoObject.AddComponent<RectTransform>();
      videoRoot.anchorMin = Vector2.zero;
      videoRoot.anchorMax = Vector2.one;
      videoRoot.offsetMin = Vector2.zero;
      videoRoot.offsetMax = Vector2.zero;

      GameObject columnObject = new("MainMenuColumn");
      columnObject.transform.SetParent(contentRoot, false);
      menuColumn = columnObject.AddComponent<RectTransform>();
      menuColumn.anchorMin = new Vector2(0f, 0.5f);
      menuColumn.anchorMax = new Vector2(0f, 0.5f);
      menuColumn.pivot = new Vector2(0f, 0.5f);
      menuColumn.anchoredPosition = new Vector2(30f, 0f);

      VerticalLayoutGroup layoutGroup = columnObject.AddComponent<VerticalLayoutGroup>();
      layoutGroup.childAlignment = TextAnchor.UpperCenter;
      layoutGroup.childControlWidth = true;
      layoutGroup.childControlHeight = false;
      layoutGroup.childForceExpandWidth = true;
      layoutGroup.childForceExpandHeight = false;
      layoutGroup.spacing = 18f;
      layoutGroup.padding = new RectOffset(10, 10, 30, 30);
    }

    private void CreateMenuButton(string buttonText)
    {
      GameObject buttonObject = new(buttonText + "Button");
      buttonObject.transform.SetParent(menuColumn, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(UIAssets.FancyButtonTexture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;

      LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
      layoutElement.preferredWidth = 320f;
      layoutElement.preferredHeight = 60f;
      layoutElement.minHeight = 60f;

      RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
      buttonRect.sizeDelta = new Vector2(320f, 60f);

      GameObject highlightObject = new("Highlight");
      highlightObject.transform.SetParent(buttonObject.transform, false);
      Image highlightImage = highlightObject.AddComponent<Image>();
      highlightImage.sprite = SpriteLoader.LoadSprite(UIAssets.FancyButtonHighlightTexture);
      highlightImage.raycastTarget = false;
      highlightImage.enabled = false;

      RectTransform highlightRect = highlightObject.GetComponent<RectTransform>();
      highlightRect.anchorMin = Vector2.zero;
      highlightRect.anchorMax = Vector2.one;
      highlightRect.offsetMin = Vector2.zero;
      highlightRect.offsetMax = Vector2.zero;

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = buttonText;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = new Color32(255, 244, 214, 255);
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.fontSize = 24;
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = Vector2.zero;
      textRect.offsetMax = Vector2.zero;

      HoverHighlightToggle hoverHighlightToggle = buttonObject.AddComponent<HoverHighlightToggle>();
      hoverHighlightToggle.SetHighlightGraphic(highlightImage);
    }

    public void UpdateLayout()
    {
      if (contentRoot != null)
      {
        contentRoot.sizeDelta = new Vector2(Screen.height * (4f / 3f), Screen.height);
      }

      if (AspectRatioBars != null)
      {
        AspectRatioBars.UpdateLayout();
      }
    }

    private static void EnsureEventSystem()
    {
      if (Object.FindAnyObjectByType<EventSystem>() != null)
      {
        return;
      }

      GameObject eventSystemObject = new("EventSystem");
      eventSystemObject.AddComponent<EventSystem>();
      eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private class HoverHighlightToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
      private Graphic highlightGraphic;

      public void SetHighlightGraphic(Graphic graphic)
      {
        highlightGraphic = graphic;
      }

      public void OnPointerEnter(PointerEventData eventData)
      {
        if (highlightGraphic != null)
        {
          highlightGraphic.enabled = true;
        }
      }

      public void OnPointerExit(PointerEventData eventData)
      {
        if (highlightGraphic != null)
        {
          highlightGraphic.enabled = false;
        }
      }
    }
  }
}
