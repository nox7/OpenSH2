"""Inspect Walls payloads without modifying installed map files (Python 3 stdlib)."""
import argparse
import collections
import csv
import json
from pathlib import Path
import struct
import zlib


def u32(data, offset):
    return struct.unpack_from('<I', data, offset)[0]


def segments(path):
    data = Path(path).read_bytes()
    p = 4
    for _ in range(u32(data, 0)):
        p += 4 + u32(data, p)
        p += 4 + 2 * u32(data, p)
    count = u32(data, p)
    p += 4
    for _ in range(count):
        p += 4 + u32(data, p)
        p += 4
    result = []
    while p + 2 <= len(data) and data[p] == 0x78 and int.from_bytes(data[p:p+2], 'big') % 31 == 0:
        decoder = zlib.decompressobj()
        result.append(decoder.decompress(data[p:]) + decoder.flush())
        if not decoder.eof:
            raise ValueError('Incomplete zlib stream')
        p = len(data) - len(decoder.unused_data)
    return result


def objects(data):
    p, types = 8, {}
    def name():
        nonlocal p
        n = u32(data, p)
        p += 4
        value = data[p:p+n].decode('ascii')
        p += n
        return value
    while True:
        start = p
        oid = u32(data, p)
        p += 4
        if oid == 0xffffdead:
            assert p == len(data)
            return
        tid = u32(data, p)
        p += 4
        if tid not in types:
            types[tid] = name()
        parent = u32(data, p)
        p += 4
        if parent == tid:
            if u32(data, p) != 0xffff1eaf:
                p += 4
        elif parent and parent not in types:
            types[parent] = name()
        end = data.index(b'\xaf\x1e\xff\xff', p)
        yield dict(type=types[tid], id=oid, start=start, offset=p, payload=data[p:end])
        p = end + 4


def map_blocks(payload):
    # First length-prefixed block contains count + serialized layer references.
    p = 4 + u32(payload, 0)
    result = {}
    while p < len(payload):
        tag, size = struct.unpack_from('<II', payload, p)
        start = p + 8
        if start + size > len(payload) or tag in result:
            raise ValueError('Invalid or repeated Map block')
        result[tag] = (start, payload[start:start+size])
        p = start + size
    assert p == len(payload)
    return result


def wall_candidates(payload):
    start, data = map_blocks(payload)[14]
    if len(data) != 256 * 256 * 24:
        raise ValueError('Expected 256x256 Map tag 14 records of 24 bytes')
    for i in range(65536):
        record = data[i*24:(i+1)*24]
        if not any(record):
            continue
        yield dict(x=i % 256, z=i // 256, payload_offset=start+i*24,
                   **dict(zip(('corner0', 'corner1', 'corner2', 'corner3'),
                              struct.unpack_from('<4f', record))),
                   **{f'byte{k}': record[k] for k in range(16, 24)})


def export_candidates(obj, path, output):
    output.mkdir(parents=True, exist_ok=True)
    rows = list(wall_candidates(obj['payload']))
    stem = output / path.stem
    if rows:
        with stem.with_suffix('.csv').open('w', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)
    summary = dict(source=str(path.resolve()), map_payload_offset=obj['offset'],
                   map_payload_bytes=len(obj['payload']), nonzero_records=len(rows),
                   blocks=[dict(tag=tag, payload_offset=start, bytes=len(data))
                           for tag, (start, data) in map_blocks(obj['payload']).items()],
                   byte16=dict(collections.Counter(r['byte16'] for r in rows)),
                   byte17=dict(collections.Counter(r['byte17'] for r in rows)),
                   byte23=dict(collections.Counter(r['byte23'] for r in rows)))
    stem.with_suffix('.json').write_text(json.dumps(summary, indent=2))
    # Colors are hypotheses, not a decoded material enum. Include all nonzero
    # records: shape-zero cells can hold elevated surfaces or auxiliary flags.
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">',
           '<rect width="256" height="256" fill="#101820"/>']
    for r in rows:
        color = '#ffb454' if r['byte17'] & 2 else '#76c9ef' if r['byte16'] else '#526070'
        svg.append(f'<rect x="{r["x"]}" y="{r["z"]}" width="1" height="1" fill="{color}"/>')
    svg.append('</svg>')
    stem.with_suffix('.svg').write_text('\n'.join(svg))
    print(json.dumps(summary))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('maps', nargs='+', type=Path)
    parser.add_argument('--dump', type=Path, help='Directory for raw Walls payloads')
    parser.add_argument('--output', type=Path, help='Export Map tag 14 candidates as CSV, JSON and SVG')
    args = parser.parse_args()
    for path in args.maps:
        obs = list(objects(segments(path)[2]))
        for obj in obs:
            if obj['type'] == 'Walls':
                payload = obj['payload']
                report = {k: v for k, v in obj.items() if k != 'payload'}
                report.update(map=path.name, bytes=len(payload), head=payload[:128].hex(' '))
                print(json.dumps(report))
                if args.dump:
                    args.dump.mkdir(parents=True, exist_ok=True)
                    (args.dump / (path.stem + '-walls.bin')).write_bytes(payload)
        print('Types:', dict(collections.Counter(o['type'] for o in obs if any(t in o['type'].lower() for t in ('wall', 'gate', 'tower', 'layer')))))
        if args.output:
            export_candidates(next(o for o in obs if o['type'] == 'Map'), path, args.output)


if __name__ == '__main__':
    main()
