namespace Assets.Code.Stronghold2.S2MReader.Enums
{
  /// <summary>
  /// Used to deliminate if a feature, building, etc. is available in a mission
  /// or if it has requirements before it becomes available.
  /// </summary>
  internal enum MissionFeatureAvailability
  {
    Disabled = 0,
    Enabled = 1,
    Requires1Quest = 2,
    Requires2Quests = 3,
    Requires3Quests = 4,
  }
}
