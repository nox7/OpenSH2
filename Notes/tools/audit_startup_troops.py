"""Test placement hypotheses against original map/save fixtures, without editing them.

Example: python -B Notes/tools/audit_startup_troops.py --map <s2m> --saves <sh2> ...
The fixture soldiers are deliberately treated as construction enemies. This tool
does not claim to decode the original game's complete diplomacy state.
"""
import argparse
import hashlib
import json
import math
import struct
from pathlib import Path
from probe_s2m_walls import segments, objects, map_blocks
from probe_startup_layout import placements, markers


def troops(objs):
    result = []
    # Concrete combat types in the supplied experiments; Lord is deliberately
    # excluded. Expand only when another fixture establishes its classification.
    for obj in objs:
        p = obj['payload']
        if obj['type'] not in ('Archer', 'Spearman') or p[:8] != struct.pack('<2i', 0, 59):
            continue
        x, y, z = struct.unpack_from('<3f', p, 29)
        result.append(dict(type=obj['type'], x=x/1024, z=z/1024,
                           raw_height=y, navigation_layer=p[21]))
    return result


def building_rectangles(objs):
    for obj in objs:
        p = obj['payload']
        off = p.find(b'\xa5\xc1\xff\xff')
        if off < 0:
            continue
        h = off + 8
        if p[h:h+12] == struct.pack('<i', 8) + b'Building':
            h += 12
        if h+60 > len(p) or struct.unpack_from('<2i', p, h) != (1, 52):
            continue
        x, y, z, yaw, w, d = struct.unpack_from('<4f2i', p, h+32)
        x, z, turn = round(x/1024), round(z/1024), round(yaw/90) % 4
        if turn in (2, 3):
            x -= w
        if turn in (1, 2):
            z -= d
        yield x, z, w, d


def area(x, z, width, depth, margin_x=0, margin_z=0):
    return {(a, b) for a in range(x-margin_x, x+width+margin_x)
            for b in range(z-margin_z, z+depth+margin_z)}


def offsets(radius):
    yield 0, 0
    for r in range(1, radius+1):
        for x in range(-r, r+1):
            yield x, -r
        for z in range(-r+1, r+1):
            yield r, z
        for x in range(r-1, -r-1, -1):
            yield x, r
        for z in range(r-1, -r, -1):
            yield -r, z


def run(map_path, saves, estate):
    chunks = segments(map_path)
    source = list(objects(chunks[2]))
    wall_bytes = map_blocks(next(o['payload'] for o in source if o['type'] == 'Map'))[14][1]
    side = math.isqrt(len(wall_bytes)//24)
    ownership = next(o['payload'] for o in objects(chunks[1]) if o['type'] == 'EstateLayer')[-side*side:]
    blocked = {(i % side, i//side) for i in range(side*side) if 1 <= wall_bytes[i*24+16] <= 5}
    for rect in building_rectangles(source):
        blocked.update(area(*rect))
    marker = next(m for m in markers(map_path) if m['estate'] == estate)
    def legal(cells, held):
        return not (cells & blocked or cells & held) and all(
            1 <= x < side-1 and 1 <= z < side-1 and ownership[z*side+x] == estate
            for x, z in cells)
    rows = []
    previous = None
    previous_troops = set()
    for save in saves:
        objs = list(objects(segments(save)[2]))
        soldiers = troops(objs)
        def rejected(x, z):
            return [dict(**t, distance_squared=(x+2-t['x'])**2+(z+2-t['z'])**2)
                    for t in soldiers if (x+2-t['x'])**2+(z+2-t['z'])**2 < 36]
        pole = next(((marker['x']+dx, marker['z']+dz) for dx, dz in offsets(10)
                     if legal(area(marker['x']+dx, marker['z']+dz, 1, 1, 1, 1), set())
                     and not rejected(marker['x']+dx, marker['z']+dz)), None)
        no_enemy_filter = next(((marker['x']+dx, marker['z']+dz) for dx, dz in offsets(10)
                                if legal(area(marker['x']+dx, marker['z']+dz, 1, 1, 1, 1), set())), None)
        with_height = next(((marker['x']+dx, marker['z']+dz) for dx, dz in offsets(10)
                            if legal(area(marker['x']+dx, marker['z']+dz, 1, 1, 1, 1), set())
                            and not any((marker['x']+dx+2-t['x'])**2+(marker['z']+dz+2-t['z'])**2
                                        +(t['raw_height']/1024)**2 < 36 for t in soldiers)), None)
        held = area(*pole, 1, 1, 1, 1) if pole else set()
        # A nearest-feasible stockpile model matches these fixtures. Exact native
        # stockpile tie-breaking is not established by these five examples alone.
        candidates = sorted(offsets(40), key=lambda p: (p[0]**2+p[1]**2, p[1], p[0]))
        stock = next(((marker['x']-4+dx, marker['z']+dz) for dx, dz in candidates
                      if legal(area(marker['x']-4+dx, marker['z']+dz, 7, 5, 0, 1), held)), None)
        native = [p for p in placements(save) if ownership[min(side-1, int(p['z']))*side+min(side-1, int(p['x']))] == estate]
        actual_pole = next((p['x'], p['z']) for p in native if p['type'] == 'VillageFlagPole')
        actual_stock = next((p['x'], p['z']) for p in native if p['type'] == 'StockPile')
        identities = {(t['type'], t['x'], t['z']) for t in soldiers}
        native_wall = map_blocks(next(o['payload'] for o in objs if o['type'] == 'Map'))[14][1]
        rows.append(dict(save=save.name, troops=soldiers,
                         added_troops=sorted(identities-previous_troops),
                         actual_pole=actual_pole, predicted_pole=pole,
                         pole_without_troop_check=no_enemy_filter,
                         pole_with_3d_distance=with_height,
                         actual_stockpile=actual_stock, predicted_stockpile=stock,
                         match=pole == actual_pole and stock == actual_stock,
                         previous_pole_rejected_by=rejected(*previous) if previous else [],
                         walls_sha256=hashlib.sha256(native_wall).hexdigest()))
        previous, previous_troops = actual_pole, identities
    return dict(map=map_path.name, estate=estate, marker=marker,
                model='Pole: 3x3 clear area, clockwise square rings, 2D centre (x+2,z+2), radius 6; stockpile: nearest feasible marker(-4,0), 7x5 plus front/back border, pole area reserved.',
                limitations='Combat types Archer/Spearman treated as enemies; stockpile tie-breaking and full diplomacy are not recovered.',
                results=rows)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--map', type=Path, required=True)
    p.add_argument('--saves', type=Path, nargs='+', required=True)
    p.add_argument('--estate', type=int, default=8)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    report = run(a.map, a.saves, a.estate)
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    for row in report['results']:
        print(row['save'], 'pole', row['predicted_pole'], 'stockpile', row['predicted_stockpile'], 'MATCH' if row['match'] else 'MISMATCH')
    if not all(row['match'] for row in report['results']):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
