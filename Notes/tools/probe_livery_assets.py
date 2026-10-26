"""Read native livery evidence without modifying original game assets (stdlib only)."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


KNOWN_EXE = "f3ccdf9bb41158202dffc000e5f2036a82f4c1ce8c5c89462f6140000f32d34b"


def definition_settings(path):
    data = path.read_bytes()
    version, family = struct.unpack_from("<ii", data)
    if version not in (4, 5, 6):
        raise ValueError(f"Unsupported human definition version: {version}")
    model = data[8:40].split(b"\0", 1)[0].decode("ascii")
    attachments = struct.unpack_from("<i", data, 40)[0]
    if not 0 <= attachments <= 128:
        raise ValueError("Invalid attachment count")
    p = 44 + 32 * attachments
    slots = struct.unpack_from("<i", data, p)[0]
    if not 0 <= slots <= 10000:
        raise ValueError("Invalid slot count")
    p += 4 + slots * (24 + (attachments if version < 6 else 0))
    settings = struct.unpack_from("<ii", data, p)
    return dict(file=path.name, version=version, family=family, model=model,
                settings=settings, remaining_bytes=len(data) - p - 8)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_root", type=Path)
    args = parser.parse_args()
    data = (args.game_root / "Stronghold2.exe").read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != KNOWN_EXE:
        raise SystemExit(f"Unknown executable SHA-256 {digest}; do not reuse fixed offsets.")
    palette = []
    for slot in range(32):
        b, g, r, a = struct.unpack_from("4B", data, 0x68D190 + slot * 4)
        palette.append(dict(slot=slot, rgb=f"#{r:02X}{g:02X}{b:02X}", alpha=a))
    crests = []
    for choice in range(11):
        number = {6: 8, 8: 9, 9: 6}.get(choice, choice)
        path = args.game_root / "crests" / f"crest{number}.PNG"
        png = path.read_bytes()
        if png[:8] != b"\x89PNG\r\n\x1a\n":
            raise ValueError(f"Not a PNG: {path}")
        width, height = struct.unpack_from(">II", png, 16)
        crests.append(dict(choice=choice, file=path.name, width=width, height=height))
    samples = {"archer", "lord", "william", "knight", "olaf", "queen", "hawk", "bull"}
    definitions = [definition_settings(p) for p in sorted(args.game_root.rglob("*.uni"))
                   if p.stem.lower() in samples]
    print(json.dumps(dict(executable_sha256=digest, palette=palette,
                         crests=crests, definitions=definitions), indent=2))


if __name__ == "__main__":
    main()
