"""Compare native .s2m/.sh2 startup transforms; reads files without changing them.

Uses the existing generic object dumper. Exported data is evidence for placement
research, not a map-specific runtime placement override.
"""
import argparse
import json
import struct
from pathlib import Path
from probe_s2m_walls import objects, segments


def placements(path):
    result = []
    for obj in objects(segments(path)[2]):
        if obj['type'] not in ('VillageFlagPole', 'StockPile', 'Granary'):
            continue
        payload = obj['payload']
        marker = payload.find(b'\xa5\xc1\xff\xff')
        if marker < 0:
            continue
        header = marker + 8
        if payload[header:header+12] == struct.pack('<i', 8) + b'Building':
            header += 12
        if struct.unpack_from('<2i', payload, header) != (1, 52):
            raise ValueError(f"Unrecognized Building body: {obj['type']} {obj['id']}")
        body = header + 8
        x, y, z, yaw = struct.unpack_from('<4f', payload, body + 24)
        width, depth = struct.unpack_from('<2i', payload, body + 40)
        slot, serial = struct.unpack_from('<2H', payload, body + 19)
        result.append(dict(type=obj['type'], object_id=obj['id'],
                           building_slot=slot, reference_serial=serial,
                           x=x/1024, z=z/1024, raw_height=y, yaw=yaw,
                           width=width, depth=depth))
    return result


def markers(path):
    table = next(obj['payload'] for obj in objects(segments(path)[0])
                 if obj['type'] == 'EstateMarkers')
    length, count = struct.unpack_from('<2i', table)
    if length != 4 + 12*count or not 0 <= count <= 32:
        raise ValueError('Invalid marker table')
    result = []
    for estate in range(count):
        packed, x, z, kind = struct.unpack_from('<ihhi', table, 8 + estate*12)
        if kind:
            result.append(dict(estate=estate, x=x, z=z, kind=kind))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--map', required=True, type=Path)
    parser.add_argument('--save', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    initial, saved = placements(args.map), placements(args.save)
    def transform(p):
        return tuple(p[k] for k in ('type', 'x', 'z', 'yaw', 'width', 'depth'))
    existing = {transform(p) for p in initial}
    report = dict(map=args.map.name, save=args.save.name,
                  markers=markers(args.map),
                  initial_buildings=initial,
                  added_buildings=[p for p in saved if transform(p) not in existing])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(f"Exported {len(report['added_buildings'])} added buildings to {args.output}")


if __name__ == '__main__':
    main()
