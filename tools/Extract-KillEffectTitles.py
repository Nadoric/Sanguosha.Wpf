#!/usr/bin/env python3
"""Reproduce cropped official title pixels. Requires Pillow; no network / generated artwork."""
from pathlib import Path
import hashlib
import json
from PIL import Image

root = Path(__file__).resolve().parent.parent
assets = root / "src/Sanguosha.Desktop/Assets/Official/KillEffects"
regions = json.loads((assets / "title-regions.json").read_text(encoding="utf-8"))
manifest_path = assets / "sources.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
derived = []
for tier, (name, region) in enumerate(regions.items(), 1):
    x, y, w, h = map(int, region["bounds"].split(","))
    rotated = region.get("rotate") == "90"
    with Image.open(assets / region["page"]) as texture:
        image = texture.convert("RGBA").crop((x, y, x + (h if rotated else w), y + (w if rotated else h)))
    if rotated:
        image = image.transpose(Image.Transpose.ROTATE_270)
    path = assets / "Titles" / f"{tier}.png"
    path.parent.mkdir(exist_ok=True)
    image.save(path, optimize=True)
    data = path.read_bytes()
    derived.append({"tier": tier, "path": path.relative_to(root).as_posix(), "source": region["page"],
                    "atlas": name + ".atlas", "region": region["name"],
                    "operation": "crop atlas bounds; undo packed rotation; no repainting",
                    "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
manifest["derived"] = derived
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {len(derived)} official title crops regenerated ({sum(x['bytes'] for x in derived):,} bytes).")
