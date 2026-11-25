# Writes InventoryLab's second showcase scene (Scenes/ShowcaseStock.scene): the bag and the herbalist's stock ledger.
# Run after art.sh and scene.py:  python3 showcase-src/inventory/stock.py
#
# Over the herb yard (dimmed): a 4×5 bag of draggable slots (BagSlots flies each item to its new slot) and a ledger of
# 1,000 items in a data-bound list (LedgerList shows which rows are on screen and how few row objects exist; R sorts).
# UI units are game pixels (800 × 450). Both panels share one top edge and height; the grid has no layout padding so
# its cells are exactly CELL wide and centred by the panel's 16 px margins.
import pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/InventoryLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("6a7b735e-336d-5674-b2e7-a136db5a7771")
uid = lambda name: str(uuid.uuid5(NS, "showcase-stock/" + name))
oid = lambda name: uid("obj/" + name)
SLOT = "ff9ed6f0-ab54-46ec-ac7f-113d05db0e78"     # Assets/Ui/UiSlot.png
BUTTON = "a05499da-30c6-4b7a-913a-d7f5b8b8aeef"   # Assets/Ui/UiButton.png
ICONS = ("{herb: 5c1a7e2e-3b0d-4f53-9a51-6e0b7c11a503, crystal: 589328e1-9fc6-4acb-ac05-ac55da897e12, key: 8b767fbb-02ce-44a7-b503-e1c2cdcaafb1, "
         "rope: 76d6c4b8-9a70-4e63-bd75-6b35a78ba93f, lantern: 12aed755-8f41-4ce5-ac8c-8a074db75d38, map: 2ba01449-6f41-475c-86d7-23b8106e2b6a, "
         "flask: bf3e8702-0199-49a0-8336-6afdd8fba14d, bandage: 101c3dd5-450b-466a-b7ac-b3c3d3322d0a, arrow: 8dd9051c-717a-4396-8d92-fb9e3e304364, "
         "shield: 685066b2-faa0-44e0-af92-6526001f201c, compass: 82a2e9a8-988a-4f84-a9a7-ad355aecfac4, bread: 4a6cd46e-b3f6-4f76-a9b7-cacd817013ca}")


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


def asset(name):   # the yard scene (scene.py) already wrote these metas
    return (ART / (name + ".meta")).read_text().split()[1]


out = []
def obj(name, x, y, w, h, comps="", parent=None, extra=""):
    out.append(f"- id: {oid(name)}\n" + (f"  parentId: {oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def text(t, size=13, color="#F4F6F4", extra=""):
    return f'  - type: uiRect\n  - type: uiText\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n    richText: true\n    icons: {ICONS}\n{extra}'


def script(name, values="{}"):
    return f"  - type: script\n    script: {name}\n    values: {values}\n"


# ---- the yard behind the panels (no colliders or scripts) ------------------------------------------------------------
W, H = 533, 300
obj("Grass", 0, 0, W, H, f"  - type: spriteRenderer\n    assetId: {asset('Grass.png')}\n    asset: Assets/Showcase/Grass.png\n    drawMode: Tiled\n    order: -20\n")
def prop(name, image, cx, bottom):
    w, h = png_size(image)
    obj(name, cx - w / 2, bottom - h, w, h, sprite(image, order=bottom))
for i, x in enumerate((58, 126, 194, 358, 426, 494)): prop(f"Fence {i + 1}", "Fence.png", x, 52)
prop("Stall", "Stall.png", 292, 128); prop("Well", "Well.png", 74, 150); prop("Bush", "Bush.png", 478, 128)
prop("Herbalist", "Herbalist.png", 264, 160)
for i, (x, b) in enumerate(((160, 196), (382, 262), (40, 262), (496, 214))): prop(f"Flowers {i + 1}", "Flowers.png", x, b)
obj("Camera", W / 2, H / 2, 1, 1, "  - type: camera2D\n    zoom: 1.5\n")

# ---- UI -----------------------------------------------------------------------------------------------------------
canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
obj("Dim", 0, 0, 800, 450, "  - type: uiRect\n" + panel, parent="Canvas")   # UI colours have no alpha: the translucent panel PNG dims

TOP, PANEL_H, M = 20, 368, 16          # shared top edge, height and inner margin of both panels
CELL, GAP = 52, 8
grid_w, grid_h = 4 * CELL + 3 * GAP, 5 * CELL + 4 * GAP
bag_w = grid_w + 2 * M
obj("Bag panel", 24, TOP, bag_w, PANEL_H, "  - type: uiRect\n" + panel, parent="Canvas")
obj("Bag title", M, 12, grid_w, 20, text("<b>Bag</b>", 16), parent="Bag panel")
obj("Bag hint", M, 34, grid_w, 16, text("Drag an item onto another slot", 12, "#BFD0DC"), parent="Bag panel")
ghosts = [oid("Ghost"), oid("Ghost back")]
obj("Bag grid", M, 60, grid_w, grid_h, f"  - type: uiRect\n  - type: uiLayout\n    axis: Grid\n    columns: 4\n    gap: {GAP}\n    padding: 0\n"
    + script("Showcase/BagSlots", f'{{"Ghost": "{ghosts[0]}", "GhostBack": "{ghosts[1]}"}}'), parent="Bag panel")
slot_text = lambda item: text(f"<size=30><icon={item}></size>" if item else "", 13, "#FFFFFF", f"    insetX: {(CELL - 30) / 2:g}\n    insetY: 8\n")[len("  - type: uiRect\n"):]
contents = ["herb", "crystal", "key", "", "compass", "", "bread", "rope", "", "lantern", "", "map", "flask", "", "", "shield", "", "", "", ""]
for i, item in enumerate(contents):
    obj(f"Bag slot {i + 1}", 0, 0, CELL, CELL, f"  - type: uiRect\n  - type: uiImage\n    sprite: {SLOT}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
        "  - type: uiButton\n    draggable: true\n" + slot_text(item), parent="Bag grid")

lx = 24 + bag_w + 16
lw = 800 - 24 - lx
list_w = lw - 2 * M
obj("Ledger panel", lx, TOP, lw, PANEL_H, "  - type: uiRect\n" + panel, parent="Canvas")
obj("Ledger title", M, 12, list_w, 20, text("<b>Herbalist's stock ledger</b>", 16), parent="Ledger panel")
rng = obj("Ledger range", M, 34, list_w, 16, text("Rows 1–7 of 1,000", 12, "#BFD0DC"), parent="Ledger panel")
# Column headers double as the sort tabs; the row columns sit under them (row x = list x + 4 padding).
NAME_X, NUM_X, COUNT_X = 34, 262, 362        # inside a row
tabs = [obj("Tab number", M + 4 + NUM_X, 60, 80, 16, text("No.", 12, "#BFD0DC"), parent="Ledger panel"),
        obj("Tab name", M + 4 + NAME_X, 60, 120, 16, text("Name", 12, "#BFD0DC"), parent="Ledger panel"),
        obj("Tab count", M + 4 + COUNT_X, 60, 60, 16, text("Count", 12, "#BFD0DC"), parent="Ledger panel")]
list_h = PANEL_H - 82 - M
lst = obj("Ledger list", M, 82, list_w, list_h, "  - type: uiRect\n  - type: uiLayout\n    gap: 4\n    padding: 4\n  - type: uiScroll\n"
          + script("Showcase/LedgerList", f'{{"Range": "{rng}", "Tabs": ["{tabs[0]}", "{tabs[1]}", "{tabs[2]}"], "RowStep": 34}}'), parent="Ledger panel")
obj("Ledger row", 0, 0, list_w - 8, 30, f"  - type: uiRect\n  - type: uiImage\n    sprite: {BUTTON}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n  - type: uiButton\n"
    + text("<icon={icon}>", 14, "#F2F7F5", "    insetX: 10\n    insetY: 5\n")[len("  - type: uiRect\n"):], parent="Ledger list")
obj("Row name", NAME_X, 0, 200, 30, text("{name}", 14, "#F2F7F5", "    insetY: 6\n"), parent="Ledger row")
obj("Row number", NUM_X, 0, 80, 30, text("{number}", 13, "#95D8BE", "    insetY: 7\n"), parent="Ledger row")
obj("Row count", COUNT_X, 0, 60, 30, text("× {count}", 13, "#95D8BE", "    insetY: 7\n"), parent="Ledger row")

# Caption, then the two floating copies BagSlots moves (last, so they draw over both panels).
caption = "A bag and a 1,000-item ledger"
cw = 110 + 7.4 * len(caption) + 60
cap = obj("Caption", 12, -12, cw, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, parent="Canvas")
obj("Caption title", 12, 5, cw - 24, 20, text(f"<b>InventoryLab</b>   {caption}", 15), parent="Caption")
obj("Caption controls", 12, 25, cw - 24, 16, text("Drag  mouse      Scroll  wheel      Sort  R", 12, "#BFD0DC"), parent="Caption")
for g, name in zip(ghosts, ("Ghost", "Ghost back")):
    obj(name, 0, 0, CELL, CELL, "  - type: uiRect\n" + slot_text(""), parent="Canvas")   # the icon alone, lifted off its slot

scene = "schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {oid('Camera')}\n"
(ROOT / "Scenes/ShowcaseStock.scene").write_text(scene)
meta = ROOT / "Scenes/ShowcaseStock.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
print(f"wrote Scenes/ShowcaseStock.scene with {len(out)} objects; scene id {meta.read_text().split()[1]}")
