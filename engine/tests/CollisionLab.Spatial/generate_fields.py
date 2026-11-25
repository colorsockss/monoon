"""Regenerate CollisionLab's two authored stress scenes from a deterministic layout."""
from pathlib import Path
from uuid import NAMESPACE_URL, uuid5

sample = Path(__file__).resolve().parents[3] / "samples" / "CollisionLab"
scenes = sample / "Scenes"
lab = (scenes / "ShowcaseCasts.scene.meta").read_text().split()[1]


def guid(key):
    return str(uuid5(NAMESPACE_URL, "MoNoon/CollisionLab/" + str(key)))


def selector():
    return ("{type: script, script: FieldSelector, values: {"
            f'Small: {{Id: "{guid(1000)}"}}, Large: {{Id: "{guid(10000)}"}}, Lab: {{Id: "{lab}"}}' + "}}")


def generate(count, columns):
    cam, canvas, status, detail = (guid(f"{count}/{name}") for name in ("cam", "canvas", "status", "detail"))
    lines = [f"schemaVersion: {22 if count == 10000 else 11}", "objects:",
             f"- id: {cam}", "  name: Query camera", "  x: 400", "  y: 225", "  components:",
             "  - {type: camera2D}", "  - " + selector(), "  - type: script", "    script: QueryField",
             f'    values: {{ColliderCount: {count}, Status: "{status}", Detail: "{detail}"}}',
             f"- {{id: {canvas}, name: Canvas, components: [{{type: uiCanvas}}]}}"]
    labels = [(guid(f"{count}/title"), "Title", 12, 26, f"Collision Lab  /  {count:,} colliders"),
              (status, "Status", 52, 19, "Comparing overlap, raycast and sweep..."),
              (detail, "Results", 82, 17, "Full scan and spatial grid"),
              (guid(f"{count}/keys"), "Keys", 125, 15, "1: 1,000     2: 10,000     R: measure again     0: probe scene")]
    for object_id, name, y, size, text in labels:
        lines.extend([f"- id: {object_id}", f"  name: {name}", f"  parentId: {canvas}", "  x: 24",
                      f"  y: {y}", "  width: 760", "  height: 38", "  components:", "  - {type: uiRect}",
                      f'  - {{type: uiText, size: {size}, text: "{text}"}}'])
    if count == 10000:
        lines.extend([f"- id: {guid('render/map')}", "  name: Field minimap", "  x: 640", "  y: 360",
                      "  components: [{type: camera2D, output: Minimap, zoom: 0.2}]"])
        for name, x, y, tint in [("Amber marker", 414, 300, "#EDAB44"), ("Blue marker", 426, 308, "#426FCC")]:
            lines.extend([f"- id: {guid(name)}", f"  name: {name}", f"  x: {x}", f"  y: {y}",
                          "  width: 48", "  height: 48", "  components:",
                          f'  - {{type: spriteRenderer, sortingLayer: 2, sortByY: true, squareColor: "{tint}"}}'])
            if name == "Amber marker":
                lines.append("  - {type: script, script: MoveMarker}")
        lines.extend([f"- id: {guid('render/hint')}", "  name: Marker controls", f"  parentId: {canvas}",
                      "  x: 24", "  y: 158", "  width: 620", "  height: 28", "  components:",
                      "  - {type: uiRect}", '  - {type: uiText, size: 15, text: "Arrows / WASD: move the amber marker in both views"}'])
    for i in range(count):
        row, col = divmod(i, columns)
        shape = ["boxCollider2D", "circleCollider2D", "polygonCollider2D"][i % 3]
        x, y = col * 32 - 160, row * 32 + 192
        layer = 1 + i % 3
        if i in (0, 1):
            x, shape, layer = -160, "boxCollider2D", 2
        trigger = i > 1 and i % 11 == 0
        tint = "#63CBBB" if trigger else ["#6386A7", "#8297AD", "#576F87"][i % 3]
        collider = f"{{type: {shape}, layer: {layer}, isTrigger: {str(trigger).lower()}}}"
        if shape == "polygonCollider2D":
            collider = collider[:-1] + ", points: [{x: 0, y: 0}, {x: 1, y: 0.5}, {x: 0, y: 1}]}"
        sprite = f'{{type: spriteRenderer, squareColor: "{tint}"}}'
        if shape != "boxCollider2D":
            asset = "Circle" if shape == "circleCollider2D" else "Triangle"
            asset_id = asset_ids[asset]
            sprite = f'{{type: spriteRenderer, assetId: {asset_id}, asset: Assets/{asset}.png, tint: "{tint}"}}'
        lines.append(f"- {{id: {guid('collider/' + str(i))}, name: Collider {i+1}, x: {x}, y: {y}, "
                     f"width: 16, height: 16, components: [{collider}, {sprite}]}}")
    lines.append(f"defaultCameraId: {cam}")
    path = scenes / f"Field{count}.scene"
    path.write_text("\n".join(lines) + "\n")
    path.with_suffix(".scene.meta").write_text(f"id: {guid(count)}\n")


asset_ids = {key: (sample / "Assets" / (key + ".png.meta")).read_text().split()[1] for key in ("Circle", "Triangle")}
if __name__ == "__main__":
    generate(1000, 40)
    generate(10000, 100)
