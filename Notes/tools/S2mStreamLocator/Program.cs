using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

const int MinLargeStreamDecompressedBytes = 100_000;
const int MinAsciiSequenceLength = 5;
const int CarveWindowSize = 4096;

var rootArg = args.Length > 0 ? args[0] : @"c:\Steam\steamapps\common\Stronghold 2";
var rootPath = Path.GetFullPath(rootArg);
var mapsPath = Path.Combine(rootPath, "maps");
var reportsPath = Path.Combine(rootPath, "reports");

if (!Directory.Exists(mapsPath))
{
	Console.Error.WriteLine($"Maps directory not found: {mapsPath}");
	Environment.Exit(1);
}

Directory.CreateDirectory(reportsPath);

var mapFiles = Directory
	.EnumerateFiles(mapsPath, "*.s2m", SearchOption.TopDirectoryOnly)
	.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
	.ToList();

if (mapFiles.Count == 0)
{
	Console.Error.WriteLine($"No .s2m files found under: {mapsPath}");
	Environment.Exit(1);
}

var analyses = new List<MapAnalysis>();
foreach (var file in mapFiles)
{
	analyses.Add(AnalyzeMap(file));
}

var csvPath = Path.Combine(reportsPath, "s2m_stream_locator_report.csv");
WriteCsv(csvPath, analyses);

var summaryPath = Path.Combine(reportsPath, "s2m_stream_locator_summary.md");
WriteSummary(summaryPath, analyses, rootPath);

var carvingCsvPath = Path.Combine(reportsPath, "s2m_structure_carving_report.csv");
WriteCarvingCsv(carvingCsvPath, analyses);

var carvingSummaryPath = Path.Combine(reportsPath, "s2m_structure_carving_summary.md");
WriteCarvingSummary(carvingSummaryPath, analyses, rootPath);

var regionsCsvPath = Path.Combine(reportsPath, "s2m_region_slices.csv");
WriteRegionSlicesCsv(regionsCsvPath, analyses);

var regionsSummaryPath = Path.Combine(reportsPath, "s2m_region_slices_summary.md");
WriteRegionSlicesSummary(regionsSummaryPath, analyses, rootPath);

var heightCsvPath = Path.Combine(reportsPath, "s2m_height_probe.csv");
WriteHeightProbeCsv(heightCsvPath, analyses);

var heightSummaryPath = Path.Combine(reportsPath, "s2m_height_probe_summary.md");
WriteHeightProbeSummary(heightSummaryPath, analyses, rootPath);

Console.WriteLine($"Analyzed {analyses.Count} map files.");
Console.WriteLine($"CSV report: {csvPath}");
Console.WriteLine($"Summary:    {summaryPath}");
Console.WriteLine($"Carving CSV: {carvingCsvPath}");
Console.WriteLine($"Carving MD:  {carvingSummaryPath}");
Console.WriteLine($"Regions CSV: {regionsCsvPath}");
Console.WriteLine($"Regions MD:  {regionsSummaryPath}");
Console.WriteLine($"Height CSV:  {heightCsvPath}");
Console.WriteLine($"Height MD:   {heightSummaryPath}");

static MapAnalysis AnalyzeMap(string filePath)
{
	var bytes = File.ReadAllBytes(filePath);
	var analysis = new MapAnalysis
	{
		FileName = Path.GetFileName(filePath),
		FileSize = bytes.Length,
		MapType = "unknown"
	};

	HeaderParseResult header;
	try
	{
		header = ParseHeader(bytes);
	}
	catch (Exception ex)
	{
		analysis.Error = $"Header parse failed: {ex.Message}";
		return analysis;
	}

	analysis.HeaderEnd = header.HeaderEnd;
	if (header.StringOptions.TryGetValue("type", out var mapType) && !string.IsNullOrWhiteSpace(mapType))
	{
		analysis.MapType = mapType;
	}

	if (header.HeaderEnd + 1 >= bytes.Length)
	{
		analysis.Error = "Header end exceeds file length.";
		return analysis;
	}

	analysis.SegmentAStart = header.HeaderEnd;

	if (bytes[header.HeaderEnd] == 0x78 && ZlibHeaderChecksumOk(bytes[header.HeaderEnd], bytes[header.HeaderEnd + 1]))
	{
		var segA = TryInflate(bytes, header.HeaderEnd, skipTwoZlibHeaderBytes: true, capturePayload: true);
		if (segA.Success)
		{
			analysis.SegmentACompressedLength = segA.EndOffset - header.HeaderEnd;
			analysis.SegmentADecompressedLength = segA.DecompressedLength;
			var segATokens = ExtractTokens(segA.Payload!, MinAsciiSequenceLength);
			analysis.SegmentATopTokens = string.Join("|", segATokens.Take(12).Select(kv => kv.Key));
			analysis.SegmentAClassification = ClassifyByTokens(segATokens.Select(kv => kv.Key));
			analysis.SegmentAEnd = segA.EndOffset;
		}
		else
		{
			analysis.SegmentAStatus = $"inflate-failed: {segA.Error}";
		}
	}
	else
	{
		analysis.SegmentAStatus = "missing-zlib-header";
	}

	if (analysis.SegmentAEnd.HasValue && analysis.SegmentAEnd.Value < bytes.Length)
	{
		analysis.SegmentBStart = analysis.SegmentAEnd.Value;
		var segB = TryInflate(bytes, analysis.SegmentBStart.Value, skipTwoZlibHeaderBytes: false, capturePayload: true);
		if (segB.Success)
		{
			analysis.SegmentBCompressedLength = segB.EndOffset - analysis.SegmentBStart.Value;
			analysis.SegmentBDecompressedLength = segB.DecompressedLength;
			var segBTokens = ExtractTokens(segB.Payload!, MinAsciiSequenceLength);
			analysis.SegmentBTopTokens = string.Join("|", segBTokens.Take(8).Select(kv => kv.Key));
			analysis.SegmentBClassification = ClassifyByTokens(segBTokens.Select(kv => kv.Key));
			analysis.SegmentBStatus = "ok";
			analysis.SegmentBEnd = segB.EndOffset;
		}
		else
		{
			analysis.SegmentBStatus = $"inflate-failed: {segB.Error}";
		}
	}

	var searchStart = analysis.SegmentAEnd ?? header.HeaderEnd;
	var dominant = FindDominantLargeStream(bytes, searchStart);
	if (dominant != null)
	{
		analysis.DominantLargeStart = dominant.Start;
		analysis.DominantLargeCompressedLength = dominant.EndOffset - dominant.Start;
		analysis.DominantLargeDecompressedLength = dominant.DecompressedLength;
		var dominantTokens = ExtractTokens(dominant.Payload!, MinAsciiSequenceLength);
		analysis.DominantLargeTopTokens = string.Join("|", dominantTokens.Take(12).Select(kv => kv.Key));
		analysis.DominantLargeClassification = ClassifyByTokens(dominantTokens.Select(kv => kv.Key));

		var carve = CarvePayloadStructure(dominant.Payload!);
		analysis.CarveBoundaryCount = carve.BoundaryOffsets.Count;
		analysis.CarveBoundaryFirst = carve.BoundaryOffsets.Count > 0 ? carve.BoundaryOffsets[0] : null;
		analysis.CarveAnchorHits = string.Join("|", carve.AnchorHits.Take(12).Select(a => $"{a.Token}@{a.Offset}"));
		analysis.CarveLenPrefixedTop = string.Join("|", carve.LengthPrefixedTokenHits.Take(12).Select(a => $"{a.Token}:{a.Count}"));
		analysis.CarveDimensionPairs = string.Join("|", carve.DimensionPairs.Take(8).Select(a => $"{a.Pair}:{a.Count}"));

		var slices = BuildRegionSlices(dominant.Payload!, carve);
		analysis.RegionSlices.AddRange(slices);
		analysis.RegionSliceCount = slices.Count;
		analysis.RegionTopLabels = string.Join("|", slices
			.GroupBy(s => s.Label)
			.OrderByDescending(g => g.Count())
			.ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
			.Take(6)
			.Select(g => $"{g.Key}:{g.Count()}"));

		var heightProbe = AnalyzeHeightLayerCandidates(dominant.Payload!);
		analysis.HeightLayerOffset = heightProbe.HeightLayerOffset;
		analysis.HeightDimensionPairs = string.Join("|", heightProbe.DimensionPairHints.Take(8));
		analysis.HeightCandidates.AddRange(heightProbe.Candidates.Take(16));
	}

	return analysis;
}

static HeaderParseResult ParseHeader(byte[] bytes)
{
	using var ms = new MemoryStream(bytes, writable: false);
	using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

	var stringCount = br.ReadInt32();
	if (stringCount < 0 || stringCount > 10_000)
	{
		throw new InvalidDataException($"Invalid string option count: {stringCount}");
	}

	var stringOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	for (var i = 0; i < stringCount; i++)
	{
		var key = ReadAsciiString(br);
		var value = ReadUtf16LeString(br);
		stringOptions[key] = value;
	}

	var intCount = br.ReadInt32();
	if (intCount < 0 || intCount > 10_000)
	{
		throw new InvalidDataException($"Invalid int option count: {intCount}");
	}

	var intOptions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	for (var i = 0; i < intCount; i++)
	{
		var key = ReadAsciiString(br);
		var value = br.ReadInt32();
		intOptions[key] = value;
	}

	return new HeaderParseResult((int)ms.Position, stringOptions, intOptions);
}

static string ReadAsciiString(BinaryReader br)
{
	var len = br.ReadInt32();
	if (len < 0 || len > 1_000_000)
	{
		throw new InvalidDataException($"Invalid ASCII string length: {len}");
	}

	var bytes = br.ReadBytes(len);
	if (bytes.Length != len)
	{
		throw new EndOfStreamException("Unexpected end of stream while reading ASCII string.");
	}

	return Encoding.ASCII.GetString(bytes);
}

static string ReadUtf16LeString(BinaryReader br)
{
	var len = br.ReadInt32();
	if (len < 0 || len > 1_000_000)
	{
		throw new InvalidDataException($"Invalid UTF-16 string length: {len}");
	}

	var byteLen = checked(len * 2);
	var bytes = br.ReadBytes(byteLen);
	if (bytes.Length != byteLen)
	{
		throw new EndOfStreamException("Unexpected end of stream while reading UTF-16 string.");
	}

	return Encoding.Unicode.GetString(bytes);
}

static bool ZlibHeaderChecksumOk(byte cmf, byte flg)
{
	return ((cmf << 8) + flg) % 31 == 0;
}

static InflateResult TryInflate(byte[] bytes, int startOffset, bool skipTwoZlibHeaderBytes, bool capturePayload)
{
	var payload = capturePayload ? new MemoryStream() : null;

	try
	{
		using var ms = new MemoryStream(bytes, writable: false);
		var bodyStart = startOffset + (skipTwoZlibHeaderBytes ? 2 : 0);
		if (bodyStart < 0 || bodyStart >= bytes.Length)
		{
			return InflateResult.Fail("start out of bounds");
		}

		ms.Position = bodyStart;
		using var ds = new DeflateStream(ms, CompressionMode.Decompress, leaveOpen: true);
		var buffer = new byte[16 * 1024];
		var total = 0;

		while (true)
		{
			var read = ds.Read(buffer, 0, buffer.Length);
			if (read <= 0)
			{
				break;
			}

			total += read;
			payload?.Write(buffer, 0, read);
		}

		return InflateResult.Ok((int)ms.Position, total, payload?.ToArray());
	}
	catch (Exception ex)
	{
		payload?.Dispose();
		return InflateResult.Fail(ex.Message);
	}
}

static DominantStreamResult? FindDominantLargeStream(byte[] bytes, int searchStart)
{
	DominantStreamResult? best = null;

	var start = Math.Max(searchStart, 0);
	for (var i = start; i < bytes.Length - 1; i++)
	{
		if (bytes[i] != 0x78)
		{
			continue;
		}

		if (!ZlibHeaderChecksumOk(bytes[i], bytes[i + 1]))
		{
			continue;
		}

		var result = TryInflate(bytes, i, skipTwoZlibHeaderBytes: true, capturePayload: false);
		if (!result.Success)
		{
			continue;
		}

		if (result.DecompressedLength < MinLargeStreamDecompressedBytes)
		{
			continue;
		}

		if (best == null || result.DecompressedLength > best.DecompressedLength)
		{
			var fullResult = TryInflate(bytes, i, skipTwoZlibHeaderBytes: true, capturePayload: true);
			if (fullResult.Success && fullResult.Payload != null)
			{
				best = new DominantStreamResult(i, fullResult.EndOffset, fullResult.DecompressedLength, fullResult.Payload);
			}
		}
	}

	return best;
}

static IReadOnlyList<KeyValuePair<string, int>> ExtractTokens(byte[] payload, int minAsciiSequenceLength)
{
	var tokenRegex = new Regex("[A-Za-z][A-Za-z0-9_]{3,}", RegexOptions.Compiled);
	var counts = new Dictionary<string, int>(StringComparer.Ordinal);

	foreach (var seq in EnumeratePrintableAsciiSequences(payload, minAsciiSequenceLength))
	{
		foreach (Match m in tokenRegex.Matches(seq))
		{
			var token = m.Value;
			if (!counts.TryGetValue(token, out var existing))
			{
				counts[token] = 1;
			}
			else
			{
				counts[token] = existing + 1;
			}
		}
	}

	return counts
		.OrderByDescending(kv => kv.Value)
		.ThenBy(kv => kv.Key, StringComparer.Ordinal)
		.ToList();
}

static IEnumerable<string> EnumeratePrintableAsciiSequences(byte[] payload, int minLen)
{
	var sb = new StringBuilder();
	foreach (var b in payload)
	{
		if (b >= 32 && b <= 126)
		{
			sb.Append((char)b);
			continue;
		}

		if (sb.Length >= minLen)
		{
			yield return sb.ToString();
		}

		sb.Clear();
	}

	if (sb.Length >= minLen)
	{
		yield return sb.ToString();
	}
}

static string ClassifyByTokens(IEnumerable<string> tokens)
{
	var set = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
	if (set.Count == 0)
	{
		return "binary-only";
	}

	if (set.Contains("Scenario") || set.Contains("Mission") || set.Contains("ScenarioEvent") || set.Contains("Trigger") || set.Contains("MapHeader"))
	{
		return "scenario-like";
	}

	if (set.Contains("Simulation") || set.Contains("Landscape") || set.Contains("S2Game") || set.Any(t => t.StartsWith("FX_ENVIRONMENT_", StringComparison.OrdinalIgnoreCase)))
	{
		return "world-like";
	}

	return "mixed-or-unknown";
}

static PayloadCarvingResult CarvePayloadStructure(byte[] payload)
{
	var boundaries = FindBoundaryCandidates(payload, CarveWindowSize);
	var anchors = FindAnchorTokens(payload);
	var lenPrefixed = FindLengthPrefixedTokenRecords(payload);
	var dimensionPairs = FindDimensionPairs(payload);

	return new PayloadCarvingResult(boundaries, anchors, lenPrefixed, dimensionPairs);
}

static List<int> FindBoundaryCandidates(byte[] payload, int windowSize)
{
	var boundaries = new List<int>();
	if (payload.Length < windowSize * 2)
	{
		return boundaries;
	}

	var prev = ComputeWindowMetrics(payload, 0, windowSize);
	for (var start = windowSize; start + windowSize <= payload.Length; start += windowSize)
	{
		var cur = ComputeWindowMetrics(payload, start, windowSize);
		var printableJump = Math.Abs(cur.PrintableRatio - prev.PrintableRatio);
		var zeroJump = Math.Abs(cur.ZeroRatio - prev.ZeroRatio);
		var uniqueJump = Math.Abs(cur.UniqueByteCount - prev.UniqueByteCount);

		if (printableJump >= 0.10 || zeroJump >= 0.06 || uniqueJump >= 22)
		{
			boundaries.Add(start);
		}

		prev = cur;
	}

	return boundaries;
}

static WindowMetrics ComputeWindowMetrics(byte[] payload, int start, int len)
{
	var end = start + len;
	var printable = 0;
	var zero = 0;
	Span<bool> seen = stackalloc bool[256];
	var unique = 0;

	for (var i = start; i < end; i++)
	{
		var b = payload[i];
		if (b >= 32 && b <= 126)
		{
			printable++;
		}

		if (b == 0)
		{
			zero++;
		}

		if (!seen[b])
		{
			seen[b] = true;
			unique++;
		}
	}

	return new WindowMetrics(printable / (double)len, zero / (double)len, unique);
}

static List<AnchorHit> FindAnchorTokens(byte[] payload)
{
	var anchors = new List<AnchorHit>();
	var anchorTokens = new[]
	{
		"S2Game",
		"Simulation",
		"Landscape",
		"Floaters",
		"Scenario",
		"MapHeader",
		"FX_ENVIRONMENT_"
	};

	var text = Encoding.ASCII.GetString(payload);
	foreach (var token in anchorTokens)
	{
		var idx = 0;
		while (idx < text.Length)
		{
			idx = text.IndexOf(token, idx, StringComparison.Ordinal);
			if (idx < 0)
			{
				break;
			}

			anchors.Add(new AnchorHit(token, idx));
			idx += token.Length;
		}
	}

	return anchors.OrderBy(a => a.Offset).Take(64).ToList();
}

static List<TokenCount> FindLengthPrefixedTokenRecords(byte[] payload)
{
	var counts = new Dictionary<string, int>(StringComparer.Ordinal);

	for (var i = 0; i + 12 < payload.Length; i += 4)
	{
		var len = BitConverter.ToInt32(payload, i);
		if (len < 3 || len > 48)
		{
			continue;
		}

		var strStart = i + 4;
		var strEnd = strStart + len;
		if (strEnd + 4 > payload.Length)
		{
			continue;
		}

		if (!IsAsciiWord(payload, strStart, len))
		{
			continue;
		}

		var token = Encoding.ASCII.GetString(payload, strStart, len);
		if (!Regex.IsMatch(token, "^[A-Za-z][A-Za-z0-9_]{2,}$"))
		{
			continue;
		}

		if (!counts.TryGetValue(token, out var c))
		{
			counts[token] = 1;
		}
		else
		{
			counts[token] = c + 1;
		}
	}

	return counts
		.OrderByDescending(kv => kv.Value)
		.ThenBy(kv => kv.Key, StringComparer.Ordinal)
		.Take(32)
		.Select(kv => new TokenCount(kv.Key, kv.Value))
		.ToList();
}

static bool IsAsciiWord(byte[] payload, int start, int len)
{
	for (var i = start; i < start + len; i++)
	{
		var b = payload[i];
		var isOk = (b >= (byte)'A' && b <= (byte)'Z') ||
		           (b >= (byte)'a' && b <= (byte)'z') ||
		           (b >= (byte)'0' && b <= (byte)'9') ||
		           b == (byte)'_';
		if (!isOk)
		{
			return false;
		}
	}

	return true;
}

static List<PairCount> FindDimensionPairs(byte[] payload)
{
	var counts = new Dictionary<string, int>(StringComparer.Ordinal);
	for (var i = 0; i + 8 <= payload.Length; i += 4)
	{
		var a = BitConverter.ToInt32(payload, i);
		var b = BitConverter.ToInt32(payload, i + 4);
		if (a < 32 || a > 2048 || b < 32 || b > 2048)
		{
			continue;
		}

		if (Math.Abs(a - b) > 512)
		{
			continue;
		}

		var key = $"{a}x{b}";
		if (!counts.TryGetValue(key, out var c))
		{
			counts[key] = 1;
		}
		else
		{
			counts[key] = c + 1;
		}
	}

	return counts
		.Where(kv => kv.Value >= 3)
		.OrderByDescending(kv => kv.Value)
		.ThenBy(kv => kv.Key, StringComparer.Ordinal)
		.Take(24)
		.Select(kv => new PairCount(kv.Key, kv.Value))
		.ToList();
}

static List<RegionSlice> BuildRegionSlices(byte[] payload, PayloadCarvingResult carve)
{
	var starts = new SortedSet<int> { 0 };

	foreach (var b in CompactOffsets(carve.BoundaryOffsets, minGap: 262_144))
	{
		if (b > 0 && b < payload.Length)
		{
			starts.Add(b);
		}
	}

	foreach (var token in new[] { "S2Game", "Simulation", "Floaters", "Landscape" })
	{
		var hit = carve.AnchorHits.FirstOrDefault(a => string.Equals(a.Token, token, StringComparison.OrdinalIgnoreCase));
		if (hit != null && hit.Offset > 0 && hit.Offset < payload.Length)
		{
			starts.Add(hit.Offset);
		}
	}

	var fxHits = carve.AnchorHits
		.Where(a => a.Token.StartsWith("FX_ENVIRONMENT_", StringComparison.OrdinalIgnoreCase))
		.Select(a => a.Offset)
		.OrderBy(v => v)
		.ToList();
	for (var i = 0; i < fxHits.Count; i += 32)
	{
		var off = fxHits[i];
		if (off > 0 && off < payload.Length)
		{
			starts.Add(off);
		}
	}

	var compactStarts = CompactOffsets(starts.ToList(), minGap: 32_768);
	if (compactStarts.Count == 0 || compactStarts[0] != 0)
	{
		compactStarts.Insert(0, 0);
	}

	var slices = new List<RegionSlice>();
	for (var i = 0; i < compactStarts.Count; i++)
	{
		var start = compactStarts[i];
		var end = i + 1 < compactStarts.Count ? compactStarts[i + 1] : payload.Length;
		if (end - start < 2048)
		{
			continue;
		}

		var region = new byte[end - start];
		Buffer.BlockCopy(payload, start, region, 0, region.Length);

		var tokens = ExtractTokens(region, MinAsciiSequenceLength).Take(24).ToList();
		var tokenSet = tokens.Select(t => t.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var dimPairs = FindDimensionPairs(region).Take(6).ToList();
		var label = LabelRegion(tokenSet, dimPairs);
		var confidence = ScoreRegionConfidence(label, tokenSet, dimPairs);

		var anchorTokens = carve.AnchorHits
			.Where(a => a.Offset >= start && a.Offset < end)
			.Select(a => a.Token)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(6)
			.ToList();

		slices.Add(new RegionSlice
		{
			Start = start,
			EndExclusive = end,
			Length = end - start,
			Label = label,
			Confidence = confidence,
			AnchorTokens = string.Join("|", anchorTokens),
			TopTokens = string.Join("|", tokens.Take(8).Select(t => t.Key)),
			DimensionPairs = string.Join("|", dimPairs.Select(d => $"{d.Pair}:{d.Count}"))
		});
	}

	return slices;
}

static List<int> CompactOffsets(List<int> offsets, int minGap)
{
	var outList = new List<int>();
	foreach (var off in offsets.Distinct().OrderBy(v => v))
	{
		if (outList.Count == 0 || off - outList[^1] >= minGap)
		{
			outList.Add(off);
		}
	}

	return outList;
}

static string LabelRegion(HashSet<string> tokenSet, List<PairCount> dimPairs)
{
	if (tokenSet.Contains("S2Game") || tokenSet.Contains("Simulation") || tokenSet.Contains("Landscape") || tokenSet.Contains("Floaters"))
	{
		return "core-simulation";
	}

	if (tokenSet.Any(t => t.StartsWith("FX_ENVIRONMENT_", StringComparison.OrdinalIgnoreCase)))
	{
		return "environment-fx";
	}

	if (dimPairs.Any(d => d.Pair is "255x255" or "256x256" or "128x128" or "32x32" or "512x512" or "1024x1024"))
	{
		return "grid-or-terrain";
	}

	if (tokenSet.Any(t => t.EndsWith("Layer", StringComparison.OrdinalIgnoreCase) ||
		                  t.EndsWith("Mgr", StringComparison.OrdinalIgnoreCase) ||
		                  t.Contains("Actor", StringComparison.OrdinalIgnoreCase) ||
		                  t.Contains("Building", StringComparison.OrdinalIgnoreCase) ||
		                  t.Contains("Pathfinder", StringComparison.OrdinalIgnoreCase)))
	{
		return "entities-and-systems";
	}

	if (tokenSet.Count == 0)
	{
		return "binary-opaque";
	}

	return "mixed-unknown";
}

static int ScoreRegionConfidence(string label, HashSet<string> tokenSet, List<PairCount> dimPairs)
{
	var score = 35;
	if (label == "core-simulation")
	{
		score += 45;
	}
	else if (label == "environment-fx")
	{
		score += 40;
	}
	else if (label == "grid-or-terrain")
	{
		score += 35;
	}
	else if (label == "entities-and-systems")
	{
		score += 30;
	}

	score += Math.Min(12, tokenSet.Count / 2);
	score += Math.Min(8, dimPairs.Count);
	return Math.Clamp(score, 10, 99);
}

static HeightProbeResult AnalyzeHeightLayerCandidates(byte[] payload)
{
	var text = Encoding.ASCII.GetString(payload);
	var idx = text.IndexOf("HeightLayer", StringComparison.Ordinal);
	if (idx < 0)
	{
		return new HeightProbeResult(null, new List<string>(), new List<HeightCandidate>());
	}

	var pairHints = new List<string>();
	var pairOffsets = new List<(int W, int H, int Offset)>();
	var expected = new HashSet<string>(StringComparer.Ordinal)
	{
		"255x255", "256x256", "128x128", "32x32", "512x512", "1024x1024", "1024x256"
	};

	var scanStart = Math.Max(0, idx - 1024);
	var scanEnd = Math.Min(payload.Length - 8, idx + 2048);
	for (var o = scanStart; o <= scanEnd; o += 4)
	{
		var w = BitConverter.ToInt32(payload, o);
		var h = BitConverter.ToInt32(payload, o + 4);
		if (w < 16 || w > 4096 || h < 16 || h > 4096)
		{
			continue;
		}

		var key = $"{w}x{h}";
		if (!expected.Contains(key))
		{
			continue;
		}

		pairHints.Add($"{key}@{o}");
		pairOffsets.Add((w, h, o));
	}

	pairHints = pairHints.Distinct(StringComparer.Ordinal).ToList();
	pairOffsets = pairOffsets
		.GroupBy(p => (p.W, p.H, p.Offset))
		.Select(g => g.First())
		.ToList();

	var candidates = new List<HeightCandidate>();
	foreach (var p in pairOffsets)
	{
		foreach (var startDelta in new[] { 8, 12, 16, 20, 24, 28, 32, 36, 40 })
		{
			var dataStart = p.Offset + startDelta;
			TryAddCandidate(payload, p.W, p.H, p.Offset, dataStart, "u8", 1, signed: false, candidates);
			TryAddCandidate(payload, p.W, p.H, p.Offset, dataStart, "u16", 2, signed: false, candidates);
			TryAddCandidate(payload, p.W, p.H, p.Offset, dataStart, "i16", 2, signed: true, candidates);
		}
	}

	candidates = candidates
		.OrderByDescending(c => c.ContinuityScore)
		.ThenByDescending(c => c.UniqueSampleValues)
		.Take(32)
		.ToList();

	return new HeightProbeResult(idx, pairHints, candidates);
}

static void TryAddCandidate(
	byte[] payload,
	int width,
	int height,
	int pairOffset,
	int dataStart,
	string format,
	int bytesPerValue,
	bool signed,
	List<HeightCandidate> outCandidates)
{
	if (dataStart < 0 || dataStart >= payload.Length)
	{
		return;
	}

	long totalValues = (long)width * height;
	long totalBytes = totalValues * bytesPerValue;
	if (totalValues <= 0 || totalBytes <= 0 || dataStart + totalBytes > payload.Length)
	{
		return;
	}

	var sampleW = Math.Min(width, 128);
	var sampleH = Math.Min(height, 128);
	if (sampleW < 4 || sampleH < 4)
	{
		return;
	}

	var sample = new double[sampleW * sampleH];
	for (var y = 0; y < sampleH; y++)
	{
		for (var x = 0; x < sampleW; x++)
		{
			var idx = y * width + x;
			var off = dataStart + idx * bytesPerValue;
			sample[y * sampleW + x] = ReadSampleValue(payload, off, format, signed);
		}
	}

	var unique = new HashSet<int>();
	double horizontalDiffSum = 0;
	double verticalDiffSum = 0;
	var hCount = 0;
	var vCount = 0;

	for (var y = 0; y < sampleH; y++)
	{
		for (var x = 0; x < sampleW; x++)
		{
			var v = sample[y * sampleW + x];
			unique.Add((int)Math.Round(v));

			if (x + 1 < sampleW)
			{
				horizontalDiffSum += Math.Abs(v - sample[y * sampleW + (x + 1)]);
				hCount++;
			}
			if (y + 1 < sampleH)
			{
				verticalDiffSum += Math.Abs(v - sample[(y + 1) * sampleW + x]);
				vCount++;
			}
		}
	}

	if (hCount == 0 || vCount == 0)
	{
		return;
	}

	var meanDiff = (horizontalDiffSum / hCount + verticalDiffSum / vCount) / 2.0;
	var uniqueCount = unique.Count;

	var smoothness = 100.0 / (1.0 + (meanDiff / 64.0));
	var variety = Math.Min(uniqueCount, 8192) / 81.92;
	var continuityScore = Math.Clamp(smoothness * 0.70 + variety * 0.30, 0.0, 100.0);

	if (uniqueCount <= 2)
	{
		continuityScore *= 0.2;
	}
	else if (uniqueCount <= 8)
	{
		continuityScore *= 0.45;
	}

	outCandidates.Add(new HeightCandidate
	{
		Width = width,
		Height = height,
		PairOffset = pairOffset,
		DataStart = dataStart,
		Format = format,
		BytesPerValue = bytesPerValue,
		MeanNeighborDiff = meanDiff,
		UniqueSampleValues = uniqueCount,
		ContinuityScore = continuityScore
	});
}

static double ReadSampleValue(byte[] payload, int off, string format, bool signed)
{
	return format switch
	{
		"u8" => payload[off],
		"u16" => BitConverter.ToUInt16(payload, off),
		"i16" => BitConverter.ToInt16(payload, off),
		_ => signed ? BitConverter.ToInt16(payload, off) : BitConverter.ToUInt16(payload, off)
	};
}

static void WriteCsv(string csvPath, List<MapAnalysis> analyses)
{
	using var sw = new StreamWriter(csvPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	sw.WriteLine(string.Join(',', new[]
	{
		"FileName",
		"MapType",
		"FileSize",
		"HeaderEnd",
		"SegmentAStart",
		"SegmentACompressedLength",
		"SegmentADecompressedLength",
		"SegmentAClassification",
		"SegmentAStatus",
		"SegmentBStart",
		"SegmentBCompressedLength",
		"SegmentBDecompressedLength",
		"SegmentBClassification",
		"SegmentBStatus",
		"DominantLargeStart",
		"DominantLargeCompressedLength",
		"DominantLargeDecompressedLength",
		"DominantLargeClassification",
		"DominantLargeTopTokens",
		"Error"
	}));

	foreach (var a in analyses)
	{
		sw.WriteLine(string.Join(',', new[]
		{
			Csv(a.FileName),
			Csv(a.MapType),
			Csv(a.FileSize.ToString(CultureInfo.InvariantCulture)),
			Csv(a.HeaderEnd?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.SegmentAStart?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.SegmentACompressedLength?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.SegmentADecompressedLength?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.SegmentAClassification),
			Csv(a.SegmentAStatus),
			Csv(a.SegmentBStart?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.SegmentBCompressedLength?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.SegmentBDecompressedLength?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.SegmentBClassification),
			Csv(a.SegmentBStatus),
			Csv(a.DominantLargeStart?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.DominantLargeCompressedLength?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.DominantLargeDecompressedLength?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.DominantLargeClassification),
			Csv(a.DominantLargeTopTokens),
			Csv(a.Error)
		}));
	}

	static string Csv(string? s)
	{
		var text = s ?? string.Empty;
		if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
		{
			return text;
		}

		return '"' + text.Replace("\"", "\"\"") + '"';
	}
}

static void WriteSummary(string summaryPath, List<MapAnalysis> analyses, string rootPath)
{
	var sb = new StringBuilder();
	sb.AppendLine("# S2M Stream Locator Summary");
	sb.AppendLine();
	sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
	sb.AppendLine($"Root: {rootPath}");
	sb.AppendLine($"Maps analyzed: {analyses.Count}");
	sb.AppendLine();

	sb.AppendLine("## Key findings");
	sb.AppendLine();

	var scenarioLikeCount = analyses.Count(a => string.Equals(a.SegmentAClassification, "scenario-like", StringComparison.OrdinalIgnoreCase));
	var worldLikeLargeCount = analyses.Count(a => string.Equals(a.DominantLargeClassification, "world-like", StringComparison.OrdinalIgnoreCase));
	var segmentBOk = analyses.Count(a => string.Equals(a.SegmentBStatus, "ok", StringComparison.OrdinalIgnoreCase));

	sb.AppendLine($"- Segment A classified as scenario-like: {scenarioLikeCount}/{analyses.Count}");
	sb.AppendLine($"- Segment B raw-deflate decode success: {segmentBOk}/{analyses.Count}");
	sb.AppendLine($"- Dominant large stream classified as world-like: {worldLikeLargeCount}/{analyses.Count}");
	sb.AppendLine();

	sb.AppendLine("## By map type");
	sb.AppendLine();
	sb.AppendLine("| MapType | Count | SegmentA scenario-like | SegmentB decode ok | Large world-like | Large start min | Large start max | Large dec avg |");
	sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");

	foreach (var grp in analyses.GroupBy(a => a.MapType).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
	{
		var rows = grp.ToList();
		var largeStarts = rows.Where(r => r.DominantLargeStart.HasValue).Select(r => r.DominantLargeStart!.Value).ToList();
		var largeDec = rows.Where(r => r.DominantLargeDecompressedLength.HasValue).Select(r => r.DominantLargeDecompressedLength!.Value).ToList();

		var segAS = rows.Count(r => string.Equals(r.SegmentAClassification, "scenario-like", StringComparison.OrdinalIgnoreCase));
		var segB = rows.Count(r => string.Equals(r.SegmentBStatus, "ok", StringComparison.OrdinalIgnoreCase));
		var world = rows.Count(r => string.Equals(r.DominantLargeClassification, "world-like", StringComparison.OrdinalIgnoreCase));

		var minStart = largeStarts.Count > 0 ? largeStarts.Min().ToString(CultureInfo.InvariantCulture) : "-";
		var maxStart = largeStarts.Count > 0 ? largeStarts.Max().ToString(CultureInfo.InvariantCulture) : "-";
		var avgDec = largeDec.Count > 0 ? ((long)largeDec.Average()).ToString(CultureInfo.InvariantCulture) : "-";

		sb.AppendLine($"| {grp.Key} | {rows.Count} | {segAS} | {segB} | {world} | {minStart} | {maxStart} | {avgDec} |");
	}

	sb.AppendLine();
	sb.AppendLine("## Large stream start offsets (all maps)");
	sb.AppendLine();
	sb.AppendLine("| Offset | Count |");
	sb.AppendLine("|---:|---:|");

	foreach (var offGrp in analyses
		.Where(a => a.DominantLargeStart.HasValue)
		.GroupBy(a => a.DominantLargeStart!.Value)
		.OrderByDescending(g => g.Count())
		.ThenBy(g => g.Key))
	{
		sb.AppendLine($"| {offGrp.Key} | {offGrp.Count()} |");
	}

	sb.AppendLine();
	sb.AppendLine("## Top files by dominant large decompressed size");
	sb.AppendLine();
	sb.AppendLine("| File | MapType | LargeStart | LargeDecLen | LargeClass | TopTokens |");
	sb.AppendLine("|---|---|---:|---:|---|---|");

	foreach (var row in analyses
		.Where(a => a.DominantLargeDecompressedLength.HasValue)
		.OrderByDescending(a => a.DominantLargeDecompressedLength)
		.ThenBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
		.Take(20))
	{
		var tokens = (row.DominantLargeTopTokens ?? string.Empty).Replace("|", ", ");
		sb.AppendLine($"| {row.FileName} | {row.MapType} | {row.DominantLargeStart} | {row.DominantLargeDecompressedLength} | {row.DominantLargeClassification} | {tokens} |");
	}

	File.WriteAllText(summaryPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static void WriteCarvingCsv(string csvPath, List<MapAnalysis> analyses)
{
	using var sw = new StreamWriter(csvPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	sw.WriteLine("FileName,MapType,DominantLargeStart,DominantLargeDecompressedLength,CarveBoundaryCount,CarveBoundaryFirst,CarveAnchorHits,CarveLenPrefixedTop,CarveDimensionPairs");

	foreach (var a in analyses.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
	{
		sw.WriteLine(string.Join(',', new[]
		{
			Csv(a.FileName),
			Csv(a.MapType),
			Csv(a.DominantLargeStart?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.DominantLargeDecompressedLength?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.CarveBoundaryCount?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.CarveBoundaryFirst?.ToString(CultureInfo.InvariantCulture)),
			Csv(a.CarveAnchorHits),
			Csv(a.CarveLenPrefixedTop),
			Csv(a.CarveDimensionPairs)
		}));
	}

	static string Csv(string? s)
	{
		var text = s ?? string.Empty;
		if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
		{
			return text;
		}

		return '"' + text.Replace("\"", "\"\"") + '"';
	}
}

static void WriteCarvingSummary(string summaryPath, List<MapAnalysis> analyses, string rootPath)
{
	var sb = new StringBuilder();
	sb.AppendLine("# S2M Structure Carving Summary");
	sb.AppendLine();
	sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
	sb.AppendLine($"Root: {rootPath}");
	sb.AppendLine($"Maps analyzed: {analyses.Count}");
	sb.AppendLine();

	var withDominant = analyses.Where(a => a.DominantLargeDecompressedLength.HasValue).ToList();
	sb.AppendLine("## Overview");
	sb.AppendLine();
	sb.AppendLine($"- Maps with dominant large payload: {withDominant.Count}/{analyses.Count}");
	if (withDominant.Count > 0)
	{
		var avgBoundary = withDominant.Where(a => a.CarveBoundaryCount.HasValue).Select(a => a.CarveBoundaryCount!.Value).DefaultIfEmpty().Average();
		sb.AppendLine($"- Average boundary candidates per dominant payload: {(int)avgBoundary}");
	}
	sb.AppendLine();

	sb.AppendLine("## Frequent anchor tokens");
	sb.AppendLine();
	sb.AppendLine("| Anchor | Maps containing anchor |");
	sb.AppendLine("|---|---:|");

	var anchorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	foreach (var row in withDominant)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var hit in (row.CarveAnchorHits ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var token = hit.Split('@', 2)[0];
			if (!seen.Add(token))
			{
				continue;
			}

			if (!anchorCounts.TryGetValue(token, out var c))
			{
				anchorCounts[token] = 1;
			}
			else
			{
				anchorCounts[token] = c + 1;
			}
		}
	}

	foreach (var kv in anchorCounts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
	{
		sb.AppendLine($"| {kv.Key} | {kv.Value} |");
	}

	sb.AppendLine();
	sb.AppendLine("## Candidate boundary entries (top 20 by boundary count)");
	sb.AppendLine();
	sb.AppendLine("| File | MapType | LargeStart | LargeLen | BoundaryCount | FirstBoundary | Anchors | LengthPrefixedTop | DimensionPairs |\n|---|---|---:|---:|---:|---:|---|---|---|");

	foreach (var row in withDominant
		.OrderByDescending(a => a.CarveBoundaryCount ?? -1)
		.ThenBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
		.Take(20))
	{
		var anchors = (row.CarveAnchorHits ?? string.Empty).Replace("|", ", ");
		var lenpref = (row.CarveLenPrefixedTop ?? string.Empty).Replace("|", ", ");
		var dims = (row.CarveDimensionPairs ?? string.Empty).Replace("|", ", ");
		sb.AppendLine($"| {row.FileName} | {row.MapType} | {row.DominantLargeStart} | {row.DominantLargeDecompressedLength} | {row.CarveBoundaryCount} | {row.CarveBoundaryFirst} | {anchors} | {lenpref} | {dims} |");
	}

	File.WriteAllText(summaryPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static void WriteRegionSlicesCsv(string csvPath, List<MapAnalysis> analyses)
{
	using var sw = new StreamWriter(csvPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	sw.WriteLine("FileName,MapType,DominantLargeStart,RegionIndex,Start,EndExclusive,Length,Label,Confidence,AnchorTokens,TopTokens,DimensionPairs");

	foreach (var a in analyses.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
	{
		for (var i = 0; i < a.RegionSlices.Count; i++)
		{
			var r = a.RegionSlices[i];
			sw.WriteLine(string.Join(',', new[]
			{
				Csv(a.FileName),
				Csv(a.MapType),
				Csv(a.DominantLargeStart?.ToString(CultureInfo.InvariantCulture)),
				Csv(i.ToString(CultureInfo.InvariantCulture)),
				Csv(r.Start.ToString(CultureInfo.InvariantCulture)),
				Csv(r.EndExclusive.ToString(CultureInfo.InvariantCulture)),
				Csv(r.Length.ToString(CultureInfo.InvariantCulture)),
				Csv(r.Label),
				Csv(r.Confidence.ToString(CultureInfo.InvariantCulture)),
				Csv(r.AnchorTokens),
				Csv(r.TopTokens),
				Csv(r.DimensionPairs)
			}));
		}
	}

	static string Csv(string? s)
	{
		var text = s ?? string.Empty;
		if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
		{
			return text;
		}

		return '"' + text.Replace("\"", "\"\"") + '"';
	}
}

static void WriteRegionSlicesSummary(string summaryPath, List<MapAnalysis> analyses, string rootPath)
{
	var sb = new StringBuilder();
	sb.AppendLine("# S2M Region Slices Summary");
	sb.AppendLine();
	sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
	sb.AppendLine($"Root: {rootPath}");
	sb.AppendLine($"Maps analyzed: {analyses.Count}");
	sb.AppendLine();

	var mapsWithSlices = analyses.Where(a => a.RegionSlices.Count > 0).ToList();
	sb.AppendLine("## Coverage");
	sb.AppendLine();
	sb.AppendLine($"- Maps with region slices: {mapsWithSlices.Count}/{analyses.Count}");
	sb.AppendLine($"- Average slices per covered map: {(mapsWithSlices.Count == 0 ? 0 : mapsWithSlices.Average(a => a.RegionSlices.Count)):F1}");
	sb.AppendLine();

	sb.AppendLine("## Label distribution");
	sb.AppendLine();
	sb.AppendLine("| Label | Count |");
	sb.AppendLine("|---|---:|");
	foreach (var kv in mapsWithSlices
		.SelectMany(a => a.RegionSlices)
		.GroupBy(r => r.Label)
		.OrderByDescending(g => g.Count())
		.ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
	{
		sb.AppendLine($"| {kv.Key} | {kv.Count()} |");
	}

	sb.AppendLine();
	sb.AppendLine("## Top region anchors for reconstruction");
	sb.AppendLine();
	sb.AppendLine("| File | MapType | Regions | Label Mix | Dominant Start |");
	sb.AppendLine("|---|---|---:|---|---:|");
	foreach (var row in mapsWithSlices
		.OrderByDescending(a => a.RegionSlices.Count)
		.ThenBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
		.Take(20))
	{
		sb.AppendLine($"| {row.FileName} | {row.MapType} | {row.RegionSlices.Count} | {row.RegionTopLabels} | {row.DominantLargeStart} |");
	}

	sb.AppendLine();
	sb.AppendLine("## Suggested import order (generic)");
	sb.AppendLine();
	sb.AppendLine("1. Parse `core-simulation` region(s) first to initialize global systems.");
	sb.AppendLine("2. Parse `grid-or-terrain` regions to construct terrain/height/material grids.");
	sb.AppendLine("3. Parse `environment-fx` regions for ambient/environmental emitters and weather settings.");
	sb.AppendLine("4. Parse `entities-and-systems` regions for placed actors/buildings/path layers.");
	sb.AppendLine("5. Keep `mixed-unknown`/`binary-opaque` bytes archived for forward-compatible decoding.");

	File.WriteAllText(summaryPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static void WriteHeightProbeCsv(string csvPath, List<MapAnalysis> analyses)
{
	using var sw = new StreamWriter(csvPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	sw.WriteLine("FileName,MapType,DominantLargeStart,HeightLayerOffset,DimensionPairs,CandidateRank,Width,Height,PairOffset,DataStart,Format,BytesPerValue,MeanNeighborDiff,UniqueSampleValues,ContinuityScore");

	foreach (var a in analyses.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
	{
		var ranked = a.HeightCandidates
			.OrderByDescending(c => c.ContinuityScore)
			.ThenByDescending(c => c.UniqueSampleValues)
			.Take(8)
			.ToList();

		for (var i = 0; i < ranked.Count; i++)
		{
			var c = ranked[i];
			sw.WriteLine(string.Join(',', new[]
			{
				Csv(a.FileName),
				Csv(a.MapType),
				Csv(a.DominantLargeStart?.ToString(CultureInfo.InvariantCulture)),
				Csv(a.HeightLayerOffset?.ToString(CultureInfo.InvariantCulture)),
				Csv(a.HeightDimensionPairs),
				Csv((i + 1).ToString(CultureInfo.InvariantCulture)),
				Csv(c.Width.ToString(CultureInfo.InvariantCulture)),
				Csv(c.Height.ToString(CultureInfo.InvariantCulture)),
				Csv(c.PairOffset.ToString(CultureInfo.InvariantCulture)),
				Csv(c.DataStart.ToString(CultureInfo.InvariantCulture)),
				Csv(c.Format),
				Csv(c.BytesPerValue.ToString(CultureInfo.InvariantCulture)),
				Csv(c.MeanNeighborDiff.ToString("F6", CultureInfo.InvariantCulture)),
				Csv(c.UniqueSampleValues.ToString(CultureInfo.InvariantCulture)),
				Csv(c.ContinuityScore.ToString("F4", CultureInfo.InvariantCulture))
			}));
		}
	}

	static string Csv(string? s)
	{
		var text = s ?? string.Empty;
		if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
		{
			return text;
		}
		return '"' + text.Replace("\"", "\"\"") + '"';
	}
}

static void WriteHeightProbeSummary(string summaryPath, List<MapAnalysis> analyses, string rootPath)
{
	var sb = new StringBuilder();
	sb.AppendLine("# S2M Height Probe Summary");
	sb.AppendLine();
	sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
	sb.AppendLine($"Root: {rootPath}");
	sb.AppendLine($"Maps analyzed: {analyses.Count}");
	sb.AppendLine();

	var withDominant = analyses.Where(a => a.DominantLargeStart.HasValue).ToList();
	var withHeight = analyses.Where(a => a.HeightLayerOffset.HasValue).ToList();
	sb.AppendLine("## Coverage");
	sb.AppendLine();
	sb.AppendLine($"- Maps with dominant payload: {withDominant.Count}/{analyses.Count}");
	sb.AppendLine($"- Maps with HeightLayer anchor: {withHeight.Count}/{analyses.Count}");
	sb.AppendLine();

	sb.AppendLine("## Top candidates per map");
	sb.AppendLine();
	sb.AppendLine("| File | MapType | HeightLayerOffset | Pair hints | Best candidate | Score | Mean diff | Unique values |");
	sb.AppendLine("|---|---|---:|---|---|---:|---:|---:|");

	foreach (var a in withHeight.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
	{
		var best = a.HeightCandidates
			.OrderByDescending(c => c.ContinuityScore)
			.ThenByDescending(c => c.UniqueSampleValues)
			.FirstOrDefault();

		if (best == null)
		{
			sb.AppendLine($"| {a.FileName} | {a.MapType} | {a.HeightLayerOffset} | {a.HeightDimensionPairs} | - | - | - | - |");
			continue;
		}

		var bestText = $"{best.Width}x{best.Height} {best.Format} @ {best.DataStart}";
		sb.AppendLine($"| {a.FileName} | {a.MapType} | {a.HeightLayerOffset} | {a.HeightDimensionPairs} | {bestText} | {best.ContinuityScore:F2} | {best.MeanNeighborDiff:F3} | {best.UniqueSampleValues} |");
	}

	sb.AppendLine();
	sb.AppendLine("## Notes");
	sb.AppendLine();
	sb.AppendLine("- Scores are heuristic and intended for ranking candidate encodings, not final proof.");
	sb.AppendLine("- High score + sensible unique-value count + recurring dimensions across maps should be prioritized.");

	File.WriteAllText(summaryPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

internal sealed record HeaderParseResult(int HeaderEnd, Dictionary<string, string> StringOptions, Dictionary<string, int> IntOptions);

internal sealed class MapAnalysis
{
	public required string FileName { get; init; }
	public required string MapType { get; set; }
	public int FileSize { get; init; }
	public int? HeaderEnd { get; set; }

	public int? SegmentAStart { get; set; }
	public int? SegmentAEnd { get; set; }
	public int? SegmentACompressedLength { get; set; }
	public int? SegmentADecompressedLength { get; set; }
	public string SegmentAClassification { get; set; } = "";
	public string SegmentAStatus { get; set; } = "ok";
	public string SegmentATopTokens { get; set; } = "";

	public int? SegmentBStart { get; set; }
	public int? SegmentBEnd { get; set; }
	public int? SegmentBCompressedLength { get; set; }
	public int? SegmentBDecompressedLength { get; set; }
	public string SegmentBClassification { get; set; } = "";
	public string SegmentBStatus { get; set; } = "not-attempted";
	public string SegmentBTopTokens { get; set; } = "";

	public int? DominantLargeStart { get; set; }
	public int? DominantLargeCompressedLength { get; set; }
	public int? DominantLargeDecompressedLength { get; set; }
	public string DominantLargeClassification { get; set; } = "";
	public string DominantLargeTopTokens { get; set; } = "";

	public int? CarveBoundaryCount { get; set; }
	public int? CarveBoundaryFirst { get; set; }
	public string CarveAnchorHits { get; set; } = "";
	public string CarveLenPrefixedTop { get; set; } = "";
	public string CarveDimensionPairs { get; set; } = "";

	public int RegionSliceCount { get; set; }
	public string RegionTopLabels { get; set; } = "";
	public List<RegionSlice> RegionSlices { get; } = new();

	public int? HeightLayerOffset { get; set; }
	public string HeightDimensionPairs { get; set; } = "";
	public List<HeightCandidate> HeightCandidates { get; } = new();

	public string Error { get; set; } = "";
}

internal sealed class InflateResult
{
	public bool Success { get; init; }
	public int EndOffset { get; init; }
	public int DecompressedLength { get; init; }
	public byte[]? Payload { get; init; }
	public string Error { get; init; } = "";

	public static InflateResult Ok(int endOffset, int decompressedLength, byte[]? payload) =>
		new()
		{
			Success = true,
			EndOffset = endOffset,
			DecompressedLength = decompressedLength,
			Payload = payload
		};

	public static InflateResult Fail(string error) =>
		new()
		{
			Success = false,
			Error = error
		};
}

internal sealed record DominantStreamResult(int Start, int EndOffset, int DecompressedLength, byte[] Payload);

internal sealed record WindowMetrics(double PrintableRatio, double ZeroRatio, int UniqueByteCount);
internal sealed record AnchorHit(string Token, int Offset);
internal sealed record TokenCount(string Token, int Count);
internal sealed record PairCount(string Pair, int Count);
internal sealed record PayloadCarvingResult(
	List<int> BoundaryOffsets,
	List<AnchorHit> AnchorHits,
	List<TokenCount> LengthPrefixedTokenHits,
	List<PairCount> DimensionPairs);

internal sealed class RegionSlice
{
	public int Start { get; init; }
	public int EndExclusive { get; init; }
	public int Length { get; init; }
	public string Label { get; init; } = "";
	public int Confidence { get; init; }
	public string AnchorTokens { get; init; } = "";
	public string TopTokens { get; init; } = "";
	public string DimensionPairs { get; init; } = "";
}

internal sealed record HeightProbeResult(
	int? HeightLayerOffset,
	List<string> DimensionPairHints,
	List<HeightCandidate> Candidates);

internal sealed class HeightCandidate
{
	public int Width { get; init; }
	public int Height { get; init; }
	public int PairOffset { get; init; }
	public int DataStart { get; init; }
	public string Format { get; init; } = "";
	public int BytesPerValue { get; init; }
	public double MeanNeighborDiff { get; init; }
	public int UniqueSampleValues { get; init; }
	public double ContinuityScore { get; init; }
}
