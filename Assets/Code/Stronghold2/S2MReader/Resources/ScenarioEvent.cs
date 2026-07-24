using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  internal class ScenarioEvent : S2Object
  {
    public int EventDelay { get; set; }
    /// <summary>
    /// The "+ Months" value in the map editor.
    /// AFAIK only used in "Time until final invasion" action to
    /// display how long the event will last.
    /// </summary>
    public int EventLengthInMonths { get; set; }
    public int ActionRepeatCount { get; set; }
    public int ActionRepeatDelay { get; set; }
    public int ActionObjectId { get; set; }
    /// <summary>
    /// All the triggers that belong to this ScenarioEvent
    /// </summary>
    public List<int> TriggerObjectIds { get; set; } = new List<int>();
  }
}
