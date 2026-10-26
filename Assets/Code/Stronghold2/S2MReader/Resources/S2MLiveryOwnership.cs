using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Map-local ownership metadata. Numeric conventions stay at their serialization boundary.</summary>
  public sealed class S2MLiveryOwnership
  {
    public sealed class Estate
    {
      public int Number;
      public int Lord;
      public int Color = -1, Crest = -1;
      public int PlayerNumber = -1;
      internal int PlayerReference;
    }
    public sealed class Player
    {
      public int Number;
      public int Lord;
      public bool LocalHuman;
    }
    public Dictionary<int, Estate> Estates { get; } = new();
    public Dictionary<int, Player> Players { get; } = new();
    public int[] ColorOrder { get; } = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    public bool HasSavedPlayers => Players.Count > 0;
    private static readonly int[] PersonalityLords = { 0, 9, 8, 6, 10, 7, 5, 4, 2, 3 };

    public static S2MLiveryOwnership Read(S2MFile map)
    {
      var result = new S2MLiveryOwnership();
      var types = new Dictionary<int, string>();
      foreach (var obj in map.S2GameObjects) types[obj.TypeIndex] = obj.Type;
      var playerReferences = new Dictionary<int, Player>();
      foreach (var obj in map.S2GameObjects)
      {
        if (!(obj is RawS2Object raw)) continue;
        if (obj.Type == "S2Game")
        {
          foreach (var field in Fields(raw.Payload, "Game", types))
            if (field.Section == "S2Game" && field.Id == 7 && field.Length == 44)
              for (int i = 0; i < 11; i++)
              {
                int color = I(raw.Payload, field.Offset + i * 4);
                if (color >= 0 && color < 32) result.ColorOrder[i] = color;
              }
        }
        else if (obj.Type == "S2Estate")
        {
          var estate = new Estate { Number = -1 };
          foreach (var field in Fields(raw.Payload, "Estate", types))
          {
            if (field.Section == "Estate" && field.Id == 0 && field.Length >= 8) estate.Number = I(raw.Payload, field.Offset);
            if (field.Section == "Estate" && field.Id == 2 && field.Length >= 4) estate.PlayerReference = I(raw.Payload, field.Offset);
            if (field.Section == "S2Estate" && field.Id == 8 && field.Length >= 8)
            { estate.Crest = I(raw.Payload, field.Offset); estate.Color = I(raw.Payload, field.Offset + 4); }
          }
          if (estate.Number >= 0 && estate.Number <= 31) result.Estates[estate.Number] = estate;
        }
        else if (obj.Type == "S2Player")
        {
          var player = new Player { Number = -1 };
          foreach (var field in Fields(raw.Payload, "Player", types))
          {
            if (field.Section == "Player" && field.Id == 0 && field.Length >= 10)
            {
              int n = I(raw.Payload, field.Offset + 2);
              long numberOffset = (long)field.Offset + 6 + (long)n * 2;
              if (n >= 0 && numberOffset <= field.Offset + field.Length - 4)
              {
                player.Number = I(raw.Payload, (int)numberOffset);
                player.LocalHuman = raw.Payload[field.Offset] != 0 && raw.Payload[field.Offset + 1] != 0;
              }
            }
            // Relations[31], Home, Code. The AI personality code is not the player number.
            if (field.Section == "S2Player" && field.Id == 0 && field.Length >= 132)
            {
              int code = I(raw.Payload, field.Offset + 128);
              if (code >= 0 && code < PersonalityLords.Length) player.Lord = PersonalityLords[code];
            }
          }
          if (player.Number >= 0 && player.Number <= 10)
          {
            if (player.LocalHuman) player.Lord = 1;
            result.Players[player.Number] = player; playerReferences[obj.Id] = player;
          }
        }
      }
      // Initial maps have no saved players. Their Scenario field 3 holds 21 estate→lord entries.
      foreach (var obj in map.MapHeaderObjects)
      {
        byte[] b = obj is Scenario scenario ? scenario.RawPayload : (obj as RawS2Object)?.Payload;
        if (obj.Type != "Scenario" || b == null || b.Length < 4) continue;
        int bytes = I(b, 0);
        if (bytes < 4 || bytes > b.Length - 4) continue;
        foreach (var field in Fields(b, "Scenario", new Dictionary<int, string>(), bytes + 4))
          if (field.Id == 3 && field.Length % 4 == 0)
            for (int i = 0; i < Math.Min(32, field.Length / 4); i++)
            {
              int lord = I(b, field.Offset + i * 4);
              if (!result.Estates.TryGetValue(i, out var estate)) result.Estates[i] = estate = new Estate { Number = i };
              if (lord >= 0 && lord <= 10) estate.Lord = lord;
            }
      }
      foreach (var estate in result.Estates.Values)
        if (playerReferences.TryGetValue(estate.PlayerReference, out var player))
        { estate.Lord = player.Lord; estate.PlayerNumber = player.Number; }
      return result;
    }

    // Walk length-delimited fields only; never search arbitrary payload bytes for a matching value.
    private readonly struct Field
    {
      public readonly string Section;
      public readonly int Id, Offset, Length;
      public Field(string section, int id, int offset, int length) { Section = section; Id = id; Offset = offset; Length = length; }
    }
    private static IEnumerable<Field> Fields(byte[] b, string section, Dictionary<int, string> types, int p = 0)
    {
      while (p <= b.Length - 8)
      {
        int id = I(b, p); p += 4;
        if (id == -15963)
        {
          int type = I(b, p); p += 4;
          if (p > b.Length - 4) yield break;
          int n = I(b, p);
          if (n > 0 && n <= 64 && n <= b.Length - p - 4 && IsName(b, p + 4, n))
          { section = Encoding.ASCII.GetString(b, p + 4, n); types[type] = section; p += n + 4; }
          else if (!types.TryGetValue(type, out section)) yield break;
          if (p > b.Length - 8) yield break;
          id = I(b, p); p += 4;
        }
        int length = I(b, p); p += 4;
        if (length < 0 || length > b.Length - p) yield break;
        yield return new Field(section, id, p, length);
        p += length;
      }
    }
    private static bool IsName(byte[] b, int p, int n)
    {
      for (int i = 0; i < n; i++)
        if (!(b[p + i] >= 'A' && b[p + i] <= 'Z') && !(b[p + i] >= 'a' && b[p + i] <= 'z')
          && !(b[p + i] >= '0' && b[p + i] <= '9') && b[p + i] != '_') return false;
      return true;
    }
    private static int I(byte[] b, int p) => BitConverter.ToInt32(b, p);
  }
}
