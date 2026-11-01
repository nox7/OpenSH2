using System;

namespace Assets.Code.UI.Screens
{
  internal enum FrontendScreenId { MainMenu, WarPeace, PathOfPeaceSelection, PathOfWarSelection, CampaignSelection }

  internal interface IFrontendScreen : IDisposable
  {
    void Show();
    void Hide();
    void Tick();
  }
}
