using System.Buffers.Binary;
using System.Text;

const int SegmentPrefixSize = 8;
const int EndOfSegmentMarker = unchecked((int)0xFFFFDEAD);
const int ObjectTrailerMarker = unchecked((int)0xFFFF1EAF);
const int LandscapeTag = 27;
const int TextureRotationTag = 30;
const int GridSide = 256;
const int CellCount = GridSide * GridSide;

if (args.Length != 3)
{
  Console.Error.WriteLine("Usage: CompareLandscapeBlocks <flat-s2game.bin> <painted-s2game.bin> <tinted-s2game.bin>");
  return 2;
}

LandscapeBlocks flat = ReadLandscape(args[0]);
LandscapeBlocks painted = ReadLandscape(args[1]);
LandscapeBlocks tinted = ReadLandscape(args[2]);
Compare("flat -> painted", flat, painted);
Compare("painted -> tinted", painted, tinted);
return 0;

static LandscapeBlocks ReadLandscape(string path)
{
  byte[] data = File.ReadAllBytes(path);
  int offset = SegmentPrefixSize;
  var types = new Dictionary<int, string>();

  while (offset < data.Length)
  {
    int id = ReadInt32(data, ref offset);
    if (id == EndOfSegmentMarker) break;

    int typeIndex = ReadInt32(data, ref offset);
    if (!types.TryGetValue(typeIndex, out string? typeName))
    {
      typeName = ReadAscii(data, ref offset);
      types.Add(typeIndex, typeName);
    }

    int parentTypeIndex = ReadInt32(data, ref offset);
    if (parentTypeIndex == typeIndex)
    {
      int optionalFieldOffset = offset;
      int optionalField = ReadInt32(data, ref offset);
      if (optionalField == ObjectTrailerMarker) offset = optionalFieldOffset;
    }
    else if (parentTypeIndex != 0 && !types.ContainsKey(parentTypeIndex))
    {
      types.Add(parentTypeIndex, ReadAscii(data, ref offset));
    }

    int payloadStart = offset;
    while (offset + sizeof(int) <= data.Length && ReadInt32At(data, offset) != ObjectTrailerMarker) offset++;
    if (offset + sizeof(int) > data.Length)
      throw new InvalidDataException($"{path}: object {typeName} ({id}) has no trailer.");

    int payloadLength = offset - payloadStart;
    if (typeName == "Landscape") return ParseLandscape(data.AsSpan(payloadStart, payloadLength));
    offset += sizeof(int);
  }

  throw new InvalidDataException($"{path}: Landscape object not found.");
}

static LandscapeBlocks ParseLandscape(ReadOnlySpan<byte> payload)
{
  byte[] bytes = payload.ToArray();
  int offset = bytes.Length >= 16 && ReadInt32At(bytes, 12) == 4 ? 12 : 0;
  if (ReadInt32(bytes, ref offset) != 4)
    throw new InvalidDataException("Landscape does not begin with tag 4.");

  _ = ReadInt32(bytes, ref offset); // logical map size
  _ = ReadInt32(bytes, ref offset); // unknown
  int heightByteCount = ReadInt32(bytes, ref offset);
  offset += heightByteCount;

  var blocks = new Dictionary<int, byte[]>();
  while (offset < bytes.Length)
  {
    int tag = ReadInt32(bytes, ref offset);
    int byteCount = ReadInt32(bytes, ref offset);
    if (byteCount < 0 || offset + byteCount > bytes.Length)
      throw new InvalidDataException($"Landscape tag {tag} has an invalid length.");

    blocks[tag] = bytes.AsSpan(offset, byteCount).ToArray();
    offset += byteCount;
  }
  return new LandscapeBlocks(blocks);
}

static void Compare(string name, LandscapeBlocks before, LandscapeBlocks after)
{
  Console.WriteLine($"{Environment.NewLine}=== {name} ===");
  foreach ((int tag, byte[] oldData) in before.Blocks.OrderBy(pair => pair.Key))
  {
    if (!after.Blocks.TryGetValue(tag, out byte[]? newData))
    {
      Console.WriteLine($"tag {tag}: missing from comparison map");
      continue;
    }
    if (oldData.Length != newData.Length)
    {
      Console.WriteLine($"tag {tag}: length {oldData.Length} -> {newData.Length}");
      continue;
    }

    List<int> changes = Enumerable.Range(0, oldData.Length)
      .Where(index => oldData[index] != newData[index])
      .ToList();
    if (changes.Count == 0) continue;

    Console.WriteLine($"tag {tag}: {changes.Count} changed byte(s), {DescribeRanges(changes)}");
    foreach (int index in changes.Take(20))
    {
      string cell = tag == LandscapeTag && index < CellCount
        ? $" cell (x={index / GridSide}, z={index % GridSide})"
        : tag == TextureRotationTag && index < CellCount
          ? $" candidate rotation cell (x={index / GridSide}, z={index % GridSide})"
          : tag == LandscapeTag && index >= CellCount && index < CellCount * 2
            ? $" tag-27-tail cell (x={(index - CellCount) / GridSide}, z={(index - CellCount) % GridSide})"
            : string.Empty;
      Console.WriteLine($"  [{index}]{cell}: {oldData[index]} -> {newData[index]}");
    }
  }
}

static string DescribeRanges(List<int> changes)
{
  var ranges = new List<string>();
  int start = changes[0];
  int previous = start;
  foreach (int index in changes.Skip(1))
  {
    if (index == previous + 1) { previous = index; continue; }
    ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
    start = previous = index;
  }
  ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
  return "ranges " + string.Join(", ", ranges.Take(12)) + (ranges.Count > 12 ? ", ..." : string.Empty);
}

static string ReadAscii(byte[] data, ref int offset)
{
  int length = ReadInt32(data, ref offset);
  string value = Encoding.ASCII.GetString(data, offset, length);
  offset += length;
  return value;
}

static int ReadInt32(byte[] data, ref int offset)
{
  int value = ReadInt32At(data, offset);
  offset += sizeof(int);
  return value;
}

static int ReadInt32At(byte[] data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, sizeof(int)));

sealed record LandscapeBlocks(Dictionary<int, byte[]> Blocks);
