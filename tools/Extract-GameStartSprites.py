#!/usr/bin/env python3
"""Extract original Laya 1.6 public texture metadata. Pillow required, no network."""
from pathlib import Path
import hashlib, json, struct
from PIL import Image
root = Path(__file__).resolve().parent.parent
assets = root / "src/Sanguosha.Desktop/Assets/Official/GameStart"
name = "SF_kaizhan_eff_putong"
data = (assets / (name + ".sk")).read_bytes()
pos = 0
for _ in range(3):
    length = struct.unpack_from("<H", data, pos)[0]
    pos += 2 + length
_, _, pos = struct.unpack_from("<BII", data, pos)
count = struct.unpack_from("<I", data, pos)[0]; pos += 4
length = struct.unpack_from("<H", data, pos)[0]; pos += 2
names = data[pos:pos+length].decode("utf-8").split("\n"); pos += length
regions = {}
for i in range(count):
    values = struct.unpack_from("<8f", data, pos); pos += 32
    regions[names[i*2+1]] = dict(page=names[i*2], x=values[0], y=values[1], width=values[2], height=values[3])
manifest_path = assets / "sources.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
derived = []
for output, region, rotation in [("title", "moren/kaizhan_1", 270), ("blade-left", "moren/kaizhan_9", 0),
                                  ("blade-right", "moren/kaizhan_10", 0), ("glow", "glow_tx1", 0)]:
    r = regions[region]
    x, y, w, h = (int(r[k]) for k in ("x", "y", "width", "height"))
    with Image.open(assets / r["page"]) as texture:
        image = texture.convert("RGBA").crop((x, y, x+w, y+h))
    if rotation: image = image.transpose(Image.Transpose.ROTATE_270)
    path = assets / "Sprites" / (output + ".png"); path.parent.mkdir(exist_ok=True)
    image.save(path, optimize=True)
    content = path.read_bytes()
    derived.append(dict(path=path.relative_to(root).as_posix(), source=r["page"], skeleton=name + ".sk",
                        region=region, bounds=[x,y,w,h], rotation=rotation, bytes=len(content),
                        sha256=hashlib.sha256(content).hexdigest(), operation="crop original pixels; undo packed rotation"))
manifest["derived"] = derived
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {len(derived)} official start sprites extracted ({sum(x['bytes'] for x in derived):,} bytes).")
