# Writes CameraLab's framing showcase Scenes/ShowcaseFraming.scene from the fair in Scenes/Showcase.scene (same map, props,
# player and partner; split screen, minimap, zones and HUD left out).  Run after scene.py:  python3 showcase-src/camera/framing.py
#
# The hero gathers herbs for the herbalist. Two cameras. "Follow" tracks him with a 120 × 80 deadzone (a thin UI frame marks
# it) and 0.25 s look-ahead: he picks three herbs by the campfire without the camera moving, then runs east along a trail of
# herbs and the camera leads. "Frame" then frames the group "Party" (hero and partner) with Padding 48: the partner walks
# in from far off, the view widens to hold both and closes in as they meet, and they walk to the herbalist together.
# FramingTour (Scripts/Showcase) walks both, picks the herbs and switches the active camera.
import pathlib, re, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/CameraLab"
NS = uuid.UUID("cf624488-489b-5951-a279-fffa22d99f7a")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
oid = lambda name: uid("obj/framing/" + name)
src = (ROOT / "Scenes/Showcase.scene").read_text()
head, body = src.split("objects:\n", 1)
body = body.split("defaultCameraId:")[0]
blocks = ["- id: " + b for b in body.split("- id: ")[1:]]
name_of = lambda b: re.search(r"\n  name: (.*)", b).group(1)
id_of = lambda b: re.match(r"- id: (\S+)", b).group(1)
drop = {"Patrol 1", "Patrol 2", "Patrol 3", "Patrol 4", "Powder zone", "Evening zone", "Player Camera", "Partner Camera", "Map Camera", "Canvas", "Split HUD"}
gone = set()   # dropped objects and everything under them (blocks list parents first)
for b in blocks:
    parent = re.search(r"\n  parentId: (\S+)", b)
    if name_of(b) in drop or (parent and parent.group(1) in gone): gone.add(id_of(b))
kept = []
for b in blocks:
    if id_of(b) in gone: continue
    if name_of(b) == "Player":
        b = b.replace("  - type: script\n    script: Showcase/FieldWalker\n    values: {}\n", "").replace("  components:\n", "  groups:\n  - Party\n  components:\n", 1)
    if name_of(b) == "Partner":
        b = re.sub(r"  - type: script\n    script: Showcase/PatrolWalker\n    values: \{[^\n]*\}\n", "", b).replace("  components:\n", "  groups:\n  - Party\n  components:\n", 1)
        b = re.sub(r"\n  x: \S+\n  y: \S+", "\n  x: 1400\n  y: 760", b, 1)   # waits by the herbalist's field
    if name_of(b) == "Player picture":
        b = b.replace("  - type: script\n    script: Showcase/HeroAnimation\n    values: {}\n", "")
    kept.append(b)
ids = {name_of(b): id_of(b) for b in kept}
assert all(k in ids for k in ("Player", "Partner", "Player picture", "Partner picture"))
panel_id = re.search(r"sprite: (\S+)\n    borders", src).group(1)
panel = f"  - type: uiImage\n    sprite: {panel_id}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"


def obj(name, x, y, w, h, comps, parent=None, extra=""):
    kept.append(f"- id: {oid(name)}\n" + (f"  parentId: {oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
                f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n  components:\n{comps}")
    return oid(name)


ZOOM, DW, DH = 1.5, 120, 80
bounds = "{x: 0, y: 0, width: 1600, height: 896}"
follow = obj("Follow Camera", 420, 520, 1, 1, f"  - type: camera2D\n    zoom: {ZOOM}\n    bounds: {bounds}\n    follow: {ids['Player']}\n    halfLife: 0.18\n"
             f"    deadZoneWidth: {DW}\n    deadZoneHeight: {DH}\n    lookAhead: 0.25\n")
frame = obj("Frame Camera", 420, 520, 1, 1, f"  - type: camera2D\n    zoom: {ZOOM}\n    bounds: {bounds}\n    frameGroup: Party\n    framePadding: 48\n    halfLife: 0.4\n",
            extra="  active: false\n")
obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
# The deadzone in screen pixels: its world size times the zoom, centred.
bw, bh, edge = DW * ZOOM, DH * ZOOM, 2
obj("Deadzone", 0, 0, bw, bh, "  - type: uiRect\n    anchor: Center\n", parent="Canvas")
for i, (x, y, w, h) in enumerate(((0, 0, bw, edge), (0, bh - edge, bw, edge), (0, 0, edge, bh), (bw - edge, 0, edge, bh))):
    obj(f"Deadzone edge {i + 1}", x, y, w, h, '  - type: uiRect\n  - type: uiImage\n    color: "#D8B848"\n', parent="Deadzone")
# Herbs to pick: three by the campfire (inside the deadzone), four on a trail east. A shared emitter sparkles where one goes.
art = ROOT / "Assets/Showcase"
meta_id = lambda name: (art / (name + ".meta")).read_text().split()[1]
(art / "HerbPick.particles").write_text("""duration: 0.5
looping: false
rate: 0
bursts: [{time: 0, count: 22}]
spread: 360
speed: {min: 30, max: 100}
lifetime: {min: 0.35, max: 0.7}
size: {min: 2, max: 4}
color: {min: "#B6F07A", max: "#FFE08C"}
endSize: 0.4
gravity: -20
blend: Additive
""")
if not (art / "HerbPick.particles.meta").exists(): (art / "HerbPick.particles.meta").write_text(f"id: {uid('HerbPick.particles')}\n")
HERBS = [(455, 505), (392, 535), (448, 548), (600, 520), (760, 515), (920, 525), (1080, 520)]
picks = []
for i, (x, y) in enumerate(HERBS):
    image = f"Herb{i % 3 + 1}.png"
    picks.append(obj(f"Pick {i + 1}", x - 6, y - 26, 34, 34, f"  - type: spriteRenderer\n    assetId: {meta_id(image)}\n    asset: Assets/Showcase/{image}\n    sortByY: true\n"))
spark = obj("Pick spark", 0, 0, 1, 1, f"  - type: particleEmitter2D\n    particles: {meta_id('HerbPick.particles')}\n    trigger: Manual\n    sortingLayer: 5\n")
obj("Herb panel", -12, 12, 150, 56, "  - type: uiRect\n    anchor: TopRight\n" + panel, parent="Canvas")
obj("Herb label", 12, 8, 126, 14, '  - type: uiRect\n  - type: uiText\n    text: "HERBS"\n    size: 11\n    color: "#BFD0DC"\n', parent="Herb panel")
counter = obj("Herb count", 12, 22, 126, 30, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "0 / 7"\n    size: 24\n    color: "#FFD84A"\n', parent="Herb panel")
tags = [("deadzone", "Deadzone", "the camera waits inside the box"), ("lead", "Look-ahead", "it runs ahead of the hero"),
        ("frame", "Frame group", "zoom out to keep the party in view")]
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tw, 24, f"  - type: uiRect\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
obj("Caption", 12, -12, 420, 30, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, parent="Canvas")
obj("Caption text", 12, 5, 400, 20, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>CameraLab</b>   Deadzone, look-ahead, group framing"\n    size: 15\n', parent="Caption")
obj("Tour", 0, 0, 1, 1, "  - type: script\n    script: Showcase/FramingTour\n    values: {"
    + f'"Player": "{ids["Player"]}", "Partner": "{ids["Partner"]}", "PlayerPicture": "{ids["Player picture"]}", "PartnerPicture": "{ids["Partner picture"]}", '
    f'"Follow": "{follow}", "Frame": "{frame}", "Deadzone": "{oid("Deadzone")}", "Spark": "{spark}", "Counter": "{counter}", '
    '"Picks": [' + ", ".join(f'"{x}"' for x in picks) + "]}\n")
schema = re.match(r"schemaVersion: (\d+)", head).group(1)
(ROOT / "Scenes/ShowcaseFraming.scene").write_text(f"schemaVersion: {max(int(schema), 42)}\nobjects:\n" + "".join(kept) + f"defaultCameraId: {follow}\n")
meta = ROOT / "Scenes/ShowcaseFraming.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseFraming.scene')}\n")
print("scene", meta.read_text().split()[1], len(kept), "objects")
