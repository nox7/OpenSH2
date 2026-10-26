using System;
using System.Collections.Generic;
using System.Text;
using Assets.Code.Stronghold2.S2MReader.Resources;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal static class ActorPlacementReader
  {
    public static IReadOnlyList<ActorPlacement> ReadAll(IReadOnlyList<S2Object> objects)
    {
      var types = new Dictionary<int, string>();
      var result = new List<ActorPlacement>();
      foreach (S2Object obj in objects)
      {
        // Concrete sections can reuse a type registered in the outer object header.
        types[obj.TypeIndex] = obj.Type;
        if (!(obj is RawS2Object raw)) continue;
        byte[] b = raw.Payload;
        // Entity field 0 is a packed 59-byte record, distinct from Building.
        if (b.Length < 67 || I(b, 0) != 0 || I(b, 4) != 59) continue;
        var actor = new ActorPlacement { Id = obj.Id, Type = obj.Type,
          RawX = F(b, 29), RawY = F(b, 33), RawZ = F(b, 37), OnMap = b[20] != 0,
          PlacedType = I(b, 8), NavigationLayer = b[21], EstateId = I(b, 61),
          TileX = BitConverter.ToInt16(b, 25), TileZ = BitConverter.ToInt16(b, 27) };
        bool hasActor = false;
        int p = 0;
        string section = "Entity";
        while (p <= b.Length - 8)
        {
          int field = I(b, p); p += 4;
          if (field == S2MReaderUtils.DataPayloadMarker)
          {
            int type = I(b, p); p += 4;
            if (p > b.Length - 4) break;
            int n = I(b, p);
            if (n > 0 && n <= 64 && p + 4 + n <= b.Length && IsName(b, p + 4, n))
            { section = Encoding.ASCII.GetString(b, p + 4, n); types[type] = section; p += 4 + n; }
            else if (!types.TryGetValue(type, out section)) break;
            if (p > b.Length - 8) break;
            field = I(b, p); p += 4;
          }
          int length = I(b, p); p += 4;
          if (length < 0 || length > b.Length - p) break;
          if (section == "Entity" && field == 10000 && length >= 25)
          {
            actor.AnimationId = I(b, p); actor.AnimationRate = F(b, p + 8);
            actor.AnimationTime = F(b, p + 12); actor.AnimationLoops = b[p + 16] != 0;
          }
          if (section == "Soldier" && field == 0 && length >= 65)
            actor.PendingPlayer = I(b, p + 61);
          if (section == "S2Actor" && field == 0 && length >= 29)
            actor.SavedLivery = I(b, p + 25);
          if (obj.Type == "Knight" && section == "Knight" && field == 0 && length == 11)
          {
            actor.HasKnightState = true;
            actor.SavedMounted = b[p] != 0;
            actor.SavedHorseReference = BitConverter.ToUInt32(b, p + 1);
            actor.HorseAnimationTime = F(b, p + 5);
          }
          if (section == "Actor" && (field == 0 || field == 2) && length >= 80)
          {
            int paths = I(b, p + 48);
            if (paths >= 0 && paths <= (length - 80) / 8)
            {
              int flags = p + 60 + paths * 8;
              actor.OnMap = b[flags + 1] != 0;
              int actionsOffset = flags + (field == 2 ? 11 : 27);
              if (actionsOffset <= p + length - 4)
              {
                int actions = I(b, actionsOffset);
                long heading = (long)actionsOffset + 4 + (long)actions * 4 + 4 + (field == 0 ? 1 : 0);
                if (actions >= 0 && heading <= p + length - 4)
                { actor.HeadingDegrees = F(b, (int)heading); hasActor = true; }
              }
            }
          }
          p += length;
        }
        if (hasActor && Finite(actor.RawX) && Finite(actor.RawY) && Finite(actor.RawZ)
          && Finite(actor.HeadingDegrees) && Finite(actor.AnimationTime) && Finite(actor.AnimationRate)) result.Add(actor);
      }
      ReadWalkingClasses(objects, result);
      return result;
    }

    private static void ReadWalkingClasses(IReadOnlyList<S2Object> objects, List<ActorPlacement> actors)
    {
      foreach (var obj in objects)
      {
        if (obj.Type != "ContextLayer" || !(obj is RawS2Object raw)) continue;
        byte[] b = raw.Payload;
        int p = 0;
        while (p <= b.Length - 8)
        {
          int field = I(b, p); p += 4;
          if (field == S2MReaderUtils.DataPayloadMarker)
          {
            int type = I(b, p); p += 4;
            if (type != obj.TypeIndex || p > b.Length - 8) break;
            int n = I(b, p);
            if (n > 0 && n <= 64 && p + 4 + n <= b.Length && IsName(b, p + 4, n)) p += 4 + n;
            if (p > b.Length - 8) break;
            field = I(b, p); p += 4;
          }
          int length = I(b, p); p += 4;
          if (length < 0 || length > b.Length - p) break;
          // Three counted grids of packed class pairs, with a stride independent
          // of playable terrain cropping. Read only the actors' occupied tiles.
          if (field >= 0 && field <= 3 && length >= 12 && (length - 12) % 6 == 0)
          {
            int count = (length - 12) / 6, side = (int)Math.Sqrt(count);
            int stride = 4 + count * 2;
            if (count > 0 && side * side == count && I(b, p) == count
              && I(b, p + stride) == count && I(b, p + stride * 2) == count)
            {
              foreach (var actor in actors)
                if (actor.NavigationLayer < 3 && actor.TileX >= 0 && actor.TileX < side && actor.TileZ >= 0 && actor.TileZ < side)
                  actor.SavedWalkingClass = b[p + stride * actor.NavigationLayer + 4 + (actor.TileZ * side + actor.TileX) * 2];
              return;
            }
          }
          p += length;
        }
      }
    }

    private static bool IsName(byte[] b, int p, int n)
    {
      for (int i = 0; i < n; i++)
      {
        byte c = b[p + i];
        if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
      }
      return true;
    }
    private static int I(byte[] b, int p) => BitConverter.ToInt32(b, p);
    private static float F(byte[] b, int p) => BitConverter.ToSingle(b, p);
    private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
  }
}
