using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Code.UI.Screens.Shared
{
  /// <summary>Pointer context overrides focus; leaving restores the focused control or the default.</summary>
  internal sealed class ScreenContext : IDisposable
  {
    private readonly Label description;
    private readonly Image preview;
    private readonly string defaultText;
    private readonly Dictionary<Button, (string Text, Texture2D Preview)> entries = new();
    private Button hovered, focused;
    private bool shown;

    public ScreenContext(Label description, string defaultText = "", Image preview = null)
    {
      this.description = description;
      this.defaultText = defaultText;
      this.preview = preview;
    }

    public void Bind(Button button, string text, Texture2D image = null)
    {
      entries.Add(button, (text, image));
      button.RegisterCallback<PointerEnterEvent>(Enter);
      button.RegisterCallback<PointerLeaveEvent>(Leave);
      button.RegisterCallback<FocusInEvent>(Focus);
      button.RegisterCallback<FocusOutEvent>(Blur);
    }

    private void Enter(PointerEnterEvent evt) { hovered = (Button)evt.currentTarget; Refresh(); }
    private void Leave(PointerLeaveEvent evt) { if (hovered == evt.currentTarget) hovered = null; Refresh(); }
    private void Focus(FocusInEvent evt) { focused = (Button)evt.currentTarget; Refresh(); }
    private void Blur(FocusOutEvent evt) { if (focused == evt.currentTarget) focused = null; Refresh(); }
    private void Refresh()
    {
      var active = hovered ?? focused;
      bool hasContext = shown && active != null && entries.ContainsKey(active);
      description.text = hasContext ? entries[active].Text : defaultText;
      if (preview == null) return;
      preview.image = hasContext ? entries[active].Preview : null;
      preview.style.display = preview.image == null ? DisplayStyle.None : DisplayStyle.Flex;
    }

    public void Show() { shown = true; hovered = focused = null; Refresh(); }
    public void Hide() { shown = false; hovered = focused = null; Refresh(); }
    public void Dispose()
    {
      Hide();
      foreach (var button in entries.Keys)
      {
        button.UnregisterCallback<PointerEnterEvent>(Enter);
        button.UnregisterCallback<PointerLeaveEvent>(Leave);
        button.UnregisterCallback<FocusInEvent>(Focus);
        button.UnregisterCallback<FocusOutEvent>(Blur);
      }
      entries.Clear();
    }
  }
}
