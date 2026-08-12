The .s2m file format begins with a file header, then zero-to-many zlib compressed sections.

First, read the uncompressed [FileHeader](./FileHeader.md). Directly after parsing the file header you will encounter three zlib compressed segments. Deflate all three zlib segments and store them in byte buffers or files to read from. Follow the direction for each segment below.

- MapHeader Segment - Will always be present (Mission and Scenario data). This will always be the first decompressed segment
- RadarMap Segment - ?
- S2Game Segment - Will always be present (static terrain data)

## For MapHeaderSegment
After zlib deflating MapHeaderSegment, begin parsing the file by parsing an [Object](./Object.md). Keep track of the last object enum parsed. **Note**: For the very first object you parse, a [MapHeader](./MapHeaderSegment/MapHeader.md), you will parse an object Id and enum twice. Read the file for notes on this.

## Parsing Markers
- The byte sequence "AF 1E FF FF" represents the end of an object
- The byte sequence "AD DE FF FF" represents the end of the segment entirely