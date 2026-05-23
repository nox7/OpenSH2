using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Code.Stronghold2;

namespace Assets.Code.Stronghold2.FormatReaders
{
  public static class S2MDebugDumper
  {
    public static string DumpSummary(S2MFile file)
    {
      if (file == null)
      {
        return "S2M file is null.";
      }

      var lines = new List<string>
      {
        "=== S2M Summary ===",
        $"Source: {file.Source}",
        $"FileSize: {file.FileSize}",
        $"HeaderEndOffset: {file.HeaderEndOffset}",
        $"SegmentA: start={file.SegmentA.StartOffset} compressed={file.SegmentA.CompressedLength} decompressed={file.SegmentA.DecompressedLength}",
        $"SegmentA TokenRecords: {file.SegmentA.TokenRecords.Count}",
        $"ScenarioEvents: {file.SegmentA.ScenarioEvents.Count}",
        string.Empty,
        "=== Scenario Events ===",
      };

      foreach (var ev in file.SegmentA.ScenarioEvents)
      {
        lines.Add($"Event[{ev.EventIndex}] id={ev.RecordId} name={ev.RecordName} month={ev.Month} delay={ev.Delay} range={ev.RecordStart}..{ev.RecordEndExclusive}");

        if (ev.Actions.Count == 0)
        {
          lines.Add("  Actions: (none)");
        }
        else
        {
          lines.Add("  Actions:");
          foreach (var action in ev.Actions)
          {
            lines.Add($"    - {action.GetType().Name} name={action.RecordName} id={action.RecordId} tag={action.Tag}");
          }
        }

        if (ev.Triggers.Count == 0)
        {
          lines.Add("  Triggers: (none)");
        }
        else
        {
          lines.Add("  Triggers:");
          foreach (var trigger in ev.Triggers)
          {
            lines.Add("    - " + FormatTriggerLine(trigger));
          }
        }

        lines.Add(string.Empty);
      }

      if (file.SegmentA.ParseIssues.Count > 0)
      {
        lines.Add("=== Segment A Parse Issues ===");
        foreach (var issue in file.SegmentA.ParseIssues)
        {
          lines.Add("- " + issue);
        }

        lines.Add(string.Empty);
      }

      lines.Add("=== World Payload ===");
      lines.Add($"ZlibCandidates: {file.WorldPayload.ZlibCandidates.Count}");
      lines.Add($"Dominant: offset={file.WorldPayload.DominantCandidate.Offset} decompressed={file.WorldPayload.DominantCandidate.DecompressedLength} anchors={file.WorldPayload.DominantCandidate.AnchorHits}");
      lines.Add($"HeightLayer: found={file.WorldPayload.HeightLayer.Found} tiles={file.WorldPayload.HeightLayer.TileWidth}x{file.WorldPayload.HeightLayer.TileHeight}");
      if (!string.IsNullOrEmpty(file.WorldPayload.HeightLayer.ParseIssue))
      {
        lines.Add($"HeightLayerIssue: {file.WorldPayload.HeightLayer.ParseIssue}");
      }

      return string.Join(Environment.NewLine, lines);
    }

    private static string FormatTriggerLine(S2MScenarioTrigger trigger)
    {
      var prefix = $"{trigger.GetType().Name} name={trigger.RecordName} id={trigger.RecordId} code={trigger.TriggerCode} mode={trigger.TriggerModeCode} value={trigger.TriggerValue}";

      if (trigger is S2MGoodsAcquiredTrigger goods)
      {
        return prefix + " " + FormatGoodsAmounts(goods.GoodsAmounts);
      }

      if (trigger is S2MEnemyGoodsAcquiredTrigger enemyGoods)
      {
        return prefix + " " + FormatGoodsAmounts(enemyGoods.GoodsAmounts);
      }

      if (trigger is S2MGoldAcquiredTrigger gold)
      {
        return prefix + $" requiredGold={gold.RequiredGoldAmount}";
      }

      if (trigger is S2MHonourAcquiredTrigger honour)
      {
        return prefix + $" requiredHonour={honour.RequiredHonourAmount}";
      }

      if (trigger is S2MNoFoodInGranaryTrigger noFood)
      {
        return prefix + $" flagColorCode={noFood.FlagColorCode} flagSelectionValue={noFood.FlagSelectionValue}";
      }

      return prefix;
    }

    private static string FormatGoodsAmounts(Dictionary<GoodsAcquiredEnum, int> amounts)
    {
      if (amounts == null || amounts.Count == 0)
      {
        return "goods=(none)";
      }

      var nonZero = amounts
        .Where(kvp => kvp.Value != 0)
        .OrderBy(kvp => (int)kvp.Key)
        .Select(kvp => $"{kvp.Key}:{kvp.Value}")
        .ToList();

      if (nonZero.Count == 0)
      {
        return "goods=(all-zero)";
      }

      return "goods=" + string.Join(", ", nonZero);
    }
  }
}
