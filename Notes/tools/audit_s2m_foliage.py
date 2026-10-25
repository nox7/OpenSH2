"""Inventory foliage selectors without modifying maps (Python 3 standard library).

Asset tables were recovered from the installed Stronghold2.exe; see
Notes/FileFormats/S2M/S2GameSegment/FoliageRenderingAudit.md for evidence and limits.
Coverage columns retain the pre-implementation (Forest families 0-2) baseline
for comparison with that audit, rather than describing the updated renderer.
The object framing helper scans trailers and is a research probe, not a replacement
for the runtime S2M reader.
"""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
from probe_s2m_walls import objects, segments


TREE_ASSETS = {
    0: [f'forest_tree_{i}' for i in range(1, 5)],
    1: [f'winter_tree_{i}' for i in range(1, 5)],
    2: ['tree_1', 'tree_1b', 'tree_1c', 'tree_1d'],
    3: ['tree_2', 'tree_2b'],
    4: ['tree_3', 'tree_3b', 'tree_3c'],
    5: ['tree_4', 'tree_4b'],
    6: ['tree_5'],
    7: ['tree_6', 'tree_6b', 'tree_6c'],
    8: ['tree_7', 'tree_7b', 'tree_7c', 'tree_7d'],
    9: ['tree_8'],
    10: [f'marsh_tree_{i}' for i in range(1, 8)],
    11: ['tree_10'],
    12: ['tree_11'],
    13: ['tree_12', 'tree_12b'],
}
ROCK_ASSETS = {
    0: ['rock_4', 'rock_5', 'rock_6', 'rock_9', 'rock_12'],
    1: ['rock_2', 'rock_8', 'rock_14'],
    2: ['rock_1', 'rock_3'],
    3: ['rock_7', 'rock_10', 'rock_13'],
    **{i + 4: [f'rock_flat_{size}_{j}' for j in range(1, 7)]
       for i, size in enumerate(('s', 'm', 'l', 'xl'))},
    8: [f'rock_iron_{i}' for i in range(1, 13)],
}
SIZES = {'Forest': 65, 'Rocks': 31, 'Grass': 27, 'Shrubs': 27, 'NaturalStone': 23}


def asset_for(kind, family, variant):
    if kind == 'Grass':
        return 'terrain/grass_bb.dds' if 0 <= family < 16 else None
    if kind == 'NaturalStone':
        names = [f'rock_natural_stone_{letter}{i}' for letter in 'ABCDEF' for i in (1, 2)]
    else:
        table = TREE_ASSETS if kind == 'Forest' else ROCK_ASSETS if kind == 'Rocks' else {
            i: [f'shrub_{i+1}'] for i in range(9)}
        names = table.get(family, [])
    index = family if kind == 'NaturalStone' else variant
    return f'meshes/landscape/{names[index]}.gr2' if 0 <= index < len(names) else None


def audit(path, game_path=None):
    obs = list(objects(segments(path)[2]))
    rows, layers = [], {}
    for obj in obs:
        kind = obj['type']
        if kind not in SIZES:
            continue
        if kind in layers:
            raise ValueError(f'Duplicate {kind} object')
        data = obj['payload']
        length, count = struct.unpack_from('<II', data)
        size = SIZES[kind]
        end = 8 + count * size
        if length != len(data) - 4 or end > len(data):
            raise ValueError(f'Invalid {kind} array framing')
        if kind != 'Forest' and end != len(data):
            raise ValueError(f'Unexpected {kind} trailing bytes')
        layers[kind] = dict(count=count, record_bytes=size, trailing_bytes=len(data)-end,
                            payload_sha256=hashlib.sha256(data).hexdigest())
        for i in range(count):
            r = data[8+i*size:8+(i+1)*size]
            if kind == 'Forest':
                family, variant = struct.unpack_from('<HH', r, 48)
            elif kind == 'NaturalStone':
                family, variant = struct.unpack_from('<H', r, 12)[0], 0
            else:
                offset = 28 if kind == 'Rocks' else 24
                family, variant = r[offset:offset+2]
            asset = asset_for(kind, family, variant)
            # This reflects current catalog coverage, not successful Unity drawing.
            supported = kind == 'Forest' and family <= 2 and variant < 4
            row = dict(layer=kind, index=i, family=family, variant=variant,
                       asset=asset, catalog_supported=supported)
            if game_path and asset:
                row['asset_exists'] = (game_path / asset).is_file()
            rows.append(row)
    groups = collections.Counter((r['layer'], r['family'], r['variant'], r['asset'],
                                  r['catalog_supported']) for r in rows)
    summary = dict(source=str(path.resolve()), source_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                   layers=layers, landscape_records=len(rows),
                   catalog_supported=sum(r['catalog_supported'] for r in rows),
                   catalog_unsupported=sum(not r['catalog_supported'] for r in rows),
                   unresolved_asset_records=sum(r['asset'] is None for r in rows),
                   crop_actors={k: sum(o['type'] == k for o in obs) for k in ('AppleTree', 'Wheat', 'Hops')},
                   groups=[dict(layer=k[0], family=k[1], variant=k[2], asset=k[3],
                                catalog_supported=k[4], count=v) for k, v in sorted(groups.items())])
    return summary, rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('map', type=Path)
    parser.add_argument('--game-path', type=Path, help='Verify resolved assets exist')
    parser.add_argument('--output', type=Path, help='Output stem for summary JSON and per-record CSV')
    args = parser.parse_args()
    summary, rows = audit(args.map, args.game_path)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.with_suffix('.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
        with args.output.with_suffix('.csv').open('w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=list(rows[0]) if rows else ['layer'])
            writer.writeheader()
            writer.writerows(rows)
    print(json.dumps(summary, indent=2))
    if any(r.get('asset_exists') is False for r in rows):
        raise SystemExit('One or more resolved assets are missing from the supplied installation')


if __name__ == '__main__':
    main()
