namespace Assets.Code.UserData
{
  /// <summary>Progress policy; the temporary local testing override never writes fake completion data.</summary>
  public sealed class CampaignProgress
  {
    // Requested for current local Unity testing. Set false when real progression is enabled.
    public static bool CompleteAllForLocalTesting { get; set; } = true;
    private readonly UserProfileStore store;
    public CampaignProgress(UserProfileStore store) { this.store = store; }
    public int LastAttempted => store.Profile.lastAttemptedWarChapter;
    public bool IsCompleted(int chapter) => CompleteAllForLocalTesting || store.Profile.completedWarChapters.Contains(chapter);
    public bool IsVisible(int chapter) => IsCompleted(chapter) || chapter == LastAttempted || chapter == 1;
  }
}
