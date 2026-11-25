# Points scene objects at project PNGs.  Run from the sample root.
#   attach.py <scene> "<object name>=<Assets/X.png>" ...           give the object's Sprite Renderer that PNG
#   attach.py <scene> "+<name>=<Assets/X.png>@x,y,w,h,layer[,Tiled]"  add a new sprite object (backgrounds)
import pathlib, re, sys, uuid


def asset_id(asset):
    meta = pathlib.Path(asset + ".meta")
    if not meta.exists():
        meta.write_text(f"id: {uuid.uuid4()}\n")
    return meta.read_text().split()[1]


scene = pathlib.Path(sys.argv[1])
blocks = re.split(r"\n(?=- id: )", scene.read_text())
tail = ""
if "\n" in blocks[-1]:  # top-level keys after the last object (defaultCameraId, ...)
    m = re.search(r"\n(?=[A-Za-z])", blocks[-1])
    if m:
        blocks[-1], tail = blocks[-1][: m.start()], blocks[-1][m.start():]
for spec in sys.argv[2:]:
    name, asset = spec.split("=", 1)
    if name.startswith("+"):
        asset, place = asset.split("@")
        x, y, w, h, layer, *mode = place.split(",")
        lines = [f"- id: {uuid.uuid4()}", f"  name: {name[1:]}", f"  x: {x}", f"  y: {y}", f"  width: {w}", f"  height: {h}", "  components:",
                 "  - type: spriteRenderer", f"    sortingLayer: {layer}", f"    assetId: {asset_id(asset)}", f"    asset: {asset}"]
        if mode:
            lines.append(f"    drawMode: {mode[0]}")
        blocks.append("\n".join(lines))
        continue
    hits = [i for i, b in enumerate(blocks) if re.search(rf"^  name: \"?{re.escape(name)}\"?$", b, re.M)]
    assert len(hits) == 1, f"{name}: {len(hits)} objects"
    b = blocks[hits[0]]
    assert "    asset: " not in b.split("- type: spriteRenderer")[1].split("\n  - type:")[0], f"{name} already has a sprite"
    assert b.count("  - type: spriteRenderer\n") == 1, name
    blocks[hits[0]] = b.replace("  - type: spriteRenderer\n", f"  - type: spriteRenderer\n    assetId: {asset_id(asset)}\n    asset: {asset}\n")
scene.write_text("\n".join(blocks) + tail)
