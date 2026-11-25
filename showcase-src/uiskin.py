# Gives the showcase samples' screen UI the 9-slice frames from uikit.py.  Run from samples/:  uiskin.py <kit folder>
#   every UI Button without an image gets the button frame (slot frame where the name matches `slots`)
#   named colour panels become frames; `backdrops` adds translucent plates behind text drawn over the world
import pathlib, re, shutil, sys, uuid

kit = pathlib.Path(sys.argv[1])
B4 = "{left: 4, top: 4, right: 4, bottom: 4}"
B8 = "{left: 8, top: 8, right: 8, bottom: 8}"
SAMPLES = {
    "InventoryLab": dict(slots=r"^Slot \d+$", images={"Surface": ("UiPanel", B8), "Inventory panel": ("UiPanel", B8)}),
    "KeyRoom": dict(images={"Door hint": ("UiLabel", B4), "Pause indicator": ("UiLabel", B4), "Compass bearing": ("UiLabel", B4),
                            "Inventory": ("GuideFrame", B8), "Compass confirmation": ("GuideFrame", B8)}),
    "BarrageLab": dict(images={"Top strip": ("UiStripTop", "{left: 1, top: 1, right: 1, bottom: 3}"),
                               "Bottom strip": ("UiStripBottom", "{left: 1, top: 3, right: 1, bottom: 1}")}),
    "ParticleLab": dict(images={"Top strip": ("UiStripTop", "{left: 1, top: 1, right: 1, bottom: 3}")}),
    "PlatformerLab": dict(backdrops={"Platform.scene": [(20, 12, 620, 142), (20, 358, 480, 84)], "Parallax.scene": [(20, 12, 620, 92)]}),
}


def sprite_id(sample, name):
    """Asset id of Assets/<name>.png in the sample, copying it from the kit (with a new identity) when it is not there yet."""
    found = [p for p in pathlib.Path(sample, "Assets").rglob(name + ".png") if "Ui" in p.parts or name == "GuideFrame"]
    png = found[0] if found else pathlib.Path(sample, "Assets", "Ui", name + ".png")
    if not png.exists():
        png.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(kit / (name + ".png"), png)
    meta = pathlib.Path(str(png) + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uuid.uuid4()}\n")
    return meta.read_text().split()[1]


for sample, rules in SAMPLES.items():
    counts = {}
    for scene in sorted(pathlib.Path(sample, "Scenes").glob("*.scene")):
        blocks = re.split(r"\n(?=- id: )", scene.read_text())
        tail = ""
        m = re.search(r"\n(?=[A-Za-z])", blocks[-1])
        if m: blocks[-1], tail = blocks[-1][: m.start()], blocks[-1][m.start():]
        out = []
        for block in blocks:
            name = (re.search(r'^  "?name"?: "?([^"\n]+)', block, re.M) or [0, ""])[1]
            has = lambda kind: re.search(rf'^  - type: "?{kind}"?$', block, re.M)
            def image(sprite, borders):
                return f"  - type: uiImage\n    sprite: {sprite_id(sample, sprite)}\n    borders: {borders}\n"
            if has("uiButton") and not has("uiImage"):
                kind = "UiSlot" if re.search(rules.get("slots", r"^\b$"), name) else "UiButton"
                block = re.sub(r'^  - type: "?uiButton"?$', lambda m: image(kind, B4) + m.group(0), block, count=1, flags=re.M)
                counts[kind] = counts.get(kind, 0) + 1
            elif name in rules.get("images", {}) and has("uiImage") and "    sprite:" not in block:
                sprite, borders = rules["images"][name]
                block, n = re.subn(r'  - type: "?uiImage"?\n    "?color"?: [^\n]+\n?', lambda m: image(sprite, borders), block, count=1)
                assert n == 1, (scene, name)
                block = block.rstrip("\n")
                counts[sprite] = counts.get(sprite, 0) + 1
            out.append(block)
            if has("uiCanvas") and scene.name in rules.get("backdrops", {}) and "name: HUD backdrop" not in scene.read_text():
                canvas = re.search(r"^- id: (\S+)", block, re.M).group(1)
                for x, y, w, h in rules["backdrops"][scene.name]:
                    out.append(f"- id: {uuid.uuid4()}\n  name: HUD backdrop\n  parentId: {canvas}\n  x: {x}\n  y: {y}\n  width: {w}\n  height: {h}\n"
                               f"  components:\n  - type: uiRect\n" + image("UiShade", B4).rstrip("\n"))
                    counts["UiShade"] = counts.get("UiShade", 0) + 1
        scene.write_text("\n".join(out) + tail)
    print(sample, counts)
