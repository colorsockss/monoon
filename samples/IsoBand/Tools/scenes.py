"""Bake the four authored 64×32 isometric stages into editable MoNoon scenes."""
import json
import random
from PIL import Image, ImageFont

from art import ASSETS, BIOMES, ROOT, TILE_VARIANTS, ZONE_TINTS, asset, uid

# In tile coordinates the (1, 1) diagonal is straight down the screen. The path
# forks use the two isometric axes, while water and the outer void fence the islands.
MAPS = [
    [
        "~~....~~~~~~~",
        "~.......~~~~~",
        "..+...+..~~~~",
        "...+++++..~~~",
        "..+++++.+..~~",
        "~..++++++...~",
        "~...+.++.+..~",
        "~~...++++..~~",
        "~~~...++++.~~",
        "~~~~...+.+.~~",
        "~~~~~...+o..~",
        "~~~~~~.....~~",
        "~~~~~~~~~~~~~",
    ], [
        "~~....~~~~~~~",
        "~.....~~~~~~~",
        "..+....~~~~~~",
        "..+++...~~~~~",
        "~~.+oo...~~~~",
        "~~~..+++.~~~~",
        "~~~~..+++...~",
        "~~~~...++...~",
        "~~~~..~+++.~~",
        "~~~~...+o+..~",
        "~~~~~...+o+.~",
        "~~~~~~......~",
        "~~~~~~~~~~~~~",
    ], [
        "~~~...~~~~~~~",
        "~......~~~~~~",
        "..+......~~~~",
        "...+.....~~~~",
        "~...+....~~~~",
        "~~.o++.....~~",
        "~~..++++....~",
        "~~~..++++...~",
        "~~~~...+++.~~",
        "~~~~~..+o+.~~",
        "~~~~~~..+o..~",
        "~~~~~~~.....~",
        "~~~~~~~~~~~~~",
    ], [
        "~~....~~~~~~~",
        "~......~~~~~~",
        "..+.....~~~~~",
        "...+.....~~~~",
        "~~..++.~~~~~~",
        "~~..+o...~~~~",
        "~~~..++....~~",
        "~~~~..++++.~~",
        "~~~~...ooo..~",
        "~~~~~..ooo..~",
        "~~~~~~..oo..~",
        "~~~~~~~.....~",
        "~~~~~~~~~~~~~",
    ],
]
# Positions are given as tile indices and an optional offset in world pixels.
WAVES = [
    [],
    [("Marshling", 4, 4, -21, 5), ("Marshling", 5, 4, -10, 4), ("Marshling", 4, 5, 5, 9),
     ("Marshling", 7, 7, 1, -1), ("Brute", 8, 7, 7, -4), ("Marshling", 7, 8, 0, 5),
     ("Wisp", 9, 9, -3, 2), ("Brute", 10, 9, 8, 4), ("Marshling", 9, 10, -9, 0)],
    [("Wisp", 4, 4, -15, -3), ("Brute", 4, 5, 8, 4), ("Brute", 5, 5, 8, 2), ("Wisp", 3, 5, 2, 2),
     ("Brute", 7, 7, -12, 4), ("Wisp", 8, 7, 3, 0), ("Wisp", 7, 8, 8, 8), ("Brute", 8, 8, -13, 0),
     ("Wisp", 9, 9, 9, 3), ("Brute", 10, 9, -3, 6), ("Brute", 9, 10, 0, 4), ("Wisp", 10, 10, 8, 0)],
    [("Wisp", 4, 4, -16, 4), ("Wisp", 5, 4, -6, -2), ("Brute", 5, 5, -6, 3),
     ("Wisp", 7, 7, -14, 1), ("Brute", 8, 7, 1, 2), ("Brute", 7, 8, 1, 3),
     ("Stag", 9, 9, 1, 3)],
]
GROUPS = [[], [0, 0, 0, 1, 1, 1, 2, 2, 2], [0]*4+[1]*4+[2]*4, [0, 0, 0, 1, 1, 1, 2]]
FEATURES = [
    [("Inn", 2, 4, -7, 12), ("Forge", 4, 2, 11, 6), ("Store", 6, 3, 6, 15),
     ("Awning", 5, 5, 51, 1), ("Crates", 5, 4, 11, -1), ("Pine", 1, 4, -7, 3),
     ("Rail", 2, 5, -12, 3), ("Casks", 3, 5, -16, 6),
     ("Pine", 2, 5, -14, 0), ("Pine", 4, 1, 15, 2), ("Pine", 9, 7, 10, 8),
     ("Banner", 4, 5, 3, 4), ("Banner", 9, 10, 10, 2), ("Lantern", 5, 4, -7, 3), ("Lantern", 5, 6, 1, 3)],
    [("Pine", 1, 3, -1, 6), ("Pine", 3, 1, 14, 2), ("Pine", 5, 2, 7, 2),
     ("Pine", 4, 7, 9, 0), ("Pine", 8, 5, 10, 6), ("Pine", 10, 7, 3, 3),
     ("Pine", 7, 10, -8, 4), ("Pine", 10, 11, -20, 5),
     ("Lantern", 4, 3, -8, 4), ("Lantern", 7, 6, 0, 4), ("Lantern", 6, 8, 9, 2), ("Banner", 10, 10, -7, -2)],
    [("Arch", 4, 5, 3, -5), ("Arch", 7, 7, 5, -5), ("Pillar", 2, 4, 1, 2),
     ("Pillar", 5, 3, -2, 8), ("Pillar", 5, 6, 2, 1), ("Pillar", 8, 6, -4, 4),
     ("Pillar", 8, 9, -4, 3), ("Pillar", 10, 8, 10, 4), ("Banner", 5, 4, -8, 2),
     ("Lantern", 4, 3, -11, 5), ("Lantern", 6, 7, 14, 5), ("Lantern", 9, 8, -10, 7)],
    [("Branches", 2, 4, 8, 6), ("Branches", 4, 2, 6, 9), ("Branches", 7, 4, 9, 8),
     ("Branches", 5, 7, -8, 5), ("Branches", 10, 7, 14, 7), ("Branches", 8, 10, -12, 5),
     ("Grave", 4, 5, -10, 4), ("Grave", 5, 4, 6, 3), ("Grave", 6, 4, 8, 4),
     ("Grave", 7, 6, 12, 5), ("Grave", 6, 7, 3, 5), ("Grave", 9, 8, 1, 6),
     ("Lantern", 3, 4, 8, 4), ("Lantern", 5, 4, -4, 1), ("Lantern", 7, 8, 1, 4),
     ("Lantern", 8, 7, -3, 4), ("Lantern", 10, 9, 2, 1), ("Lantern", 9, 10, 2, 1)],
]
ROLE = {"Captain": (36, 40), "Shield": (42, 39), "Archer": (37, 39), "Healer": (38, 39),
        "Marshling": (34, 28), "Brute": (44, 31), "Wisp": (36, 33), "Stag": (61, 66)}
PARTY = ("Captain", "Shield", "Archer", "Healer", "Archer")
FORMATION = [(0, 0), (-26, 11), (26, 11), (-16, -16), (16, -16)]
NAMES = ("Haven", "Ashwater Marsh", "Fallen Gallery", "Stag Crypt")
SUBTITLES = ("The lanterns are still lit.", "Follow the lanterns.",
             "Find the old seal.", "Break the final ward.")
ORIGIN = (384, 29)
PORTALS = [(7, 7), (10, 10), (10, 10), (10, 10)]


def cell(q, r, dx=0, dy=0):
    return ORIGIN[0] + 32 + 32*(q-r) + dx, ORIGIN[1] + 16 + 16*(q+r) + dy


def yaml(value):
    return json.dumps(value, ensure_ascii=False)


def component(kind, **values):
    return "  - type: " + kind + "\n" + "".join(f"    {k}: {yaml(v)}\n" for k, v in values.items())


def sprite(name=None, **values):
    if name:
        values["assetId"] = asset(name + ".png")
    return component("spriteRenderer", **values)


sizes = {}
def size(name):
    if name not in sizes:
        with Image.open(ASSETS/(name+".png")) as im:
            sizes[name] = im.size
    return sizes[name]


def scene_id(name):
    return (ROOT/"Scenes"/(name+".scene.meta")).read_text().split()[1]


class Scene:
    def __init__(self, zone):
        self.zone = zone
        self.name = BIOMES[zone]
        self.items = {}
        self.references = {}

    def key(self, name):
        return uid(self.name + "/" + name)

    def add(self, name, x, y, w, h, components, parent=None, active=True, centered=False, groups=()):
        assert name not in self.items, name
        identity = self.key(name)
        self.items[name] = (f"- id: {identity}\n  name: {yaml(name)}\n  x: {x}\n  y: {y}\n  width: {w}\n  height: {h}\n"
                            + (f"  parentId: {parent}\n" if parent else "")
                            + ("  active: false\n" if not active else "")
                            + ("  pivotX: 0.5\n  pivotY: 0.5\n" if centered else "")
                            + ("  groups: "+yaml(groups)+"\n" if groups else "")
                            + ("  components:\n" + components if components else "  components: []\n"))
        return identity

    def prop(self, name, art, x, y, order=0, *, static=False, scale=1):
        w, h = [round(v * scale) for v in size(art)]
        return self.add(name, x-w//2, y-h+1, w, h, sprite(art, sortByY=not static, order=order, pixelSnap=True, tint=ZONE_TINTS[self.zone]))

    def terrain(self):
        self.add("Deep water", -80, -50, 480, 270,
                 sprite("Terrain/"+self.name+"Water", order=-22, tint=ZONE_TINTS[self.zone]) +
                 component("parallax2D", scrollX=.14, scrollY=.19, repeatX=True, repeatY=True))
        rows = MAPS[self.zone]
        r = random.Random(67 + self.zone)
        lines = []
        cliffs = []
        for j, row in enumerate(rows):
            for i, kind in enumerate(row):
                if kind == "~":
                    shallow = any(
                        0 <= j+dy < len(rows) and 0 <= i+dx < len(row) and rows[j+dy][i+dx] != "~"
                        for dx, dy in ((1,0), (0,1), (-1,0), (0,-1))
                    )
                    variant = r.randrange(7, 10) if shallow else 11
                else:
                    variant = 10 if kind == "o" else r.randrange(3, 7) if kind == "+" else r.randrange(3)
                lines.append(f"      - {{index: {j*32+i}, tile: {self.zone*TILE_VARIANTS+variant+1}}}\n")
                if kind == "~":
                    continue
                sides = int(j+1 == len(rows) or rows[j+1][i] == "~") + 2*int(i+1 == len(row) or row[i+1] == "~")
                if sides:
                    x, y = cell(i, j)
                    cliffs.append((x, y, sides))
        self.references["Floor"] = self.add("Diamond terrain", *ORIGIN, 64, 32, component(
            "tilemap2D", layout="Isometric", cellSize={"width":64, "height":32}, order=-5,
            tileset=asset("Field.tileset")) + "    chunks:\n    - x: 0\n      y: 0\n      cells:\n" + "".join(lines))
        for n, (x, y, sides) in enumerate(sorted(cliffs, key=lambda t: t[1])):
            self.add(f"Shore face {n:02}", x-32, y-16, 64, 43, sprite(f"Terrain/{self.name}{sides}", order=-4, tint=ZONE_TINTS[self.zone]))
        if self.zone == 3:
            x, y = cell(9, 9)
            self.add("Guardian seal", x-80, y-40, 160, 81, sprite("Props/GuardianSeal", order=-3, tint=ZONE_TINTS[self.zone]))
        habitats = [
            ("Flowers", "Crates", "Cairn", "Flowers"),
            ("Reeds", "Mushrooms", "Cairn", "Reeds"),
            ("Cairn", "Flowers", "Cairn", "Flowers"),
            ("Mushrooms", "Grave", "Cairn", "Mushrooms"),
        ][self.zone]
        for j, row in enumerate(rows):
            for i, kind in enumerate(row):
                if kind != "." or r.randrange(4) == 0:
                    continue
                for n in range(r.randrange(1, 3)):
                    art = r.choice(habitats)
                    x, y = cell(i, j, r.randrange(-16, 16), r.randrange(-4, 5))
                    self.prop(f"Ground detail {i}-{j}-{n}", "Props/"+art, x, y, -3, static=True)
        for n, (art, q, r, dx, dy) in enumerate(FEATURES[self.zone]):
            x, y = cell(q, r, dx, dy)
            self.prop(f"{art} {n+1}", art if art in ("Pine", "Pillar") else "Props/"+art, x, y)
            if art == "Lantern":
                self.light(f"Lantern light {n}", x, y-19, 42 if self.zone < 3 else 61, .34 if self.zone < 3 else .84)
        if self.zone == 1:
            self.emitter("River fog", *cell(6, 6), "Mist", manual=False)
        x, y = cell(*( (3, 3) if self.zone != 0 else (4, 4) ))
        if self.zone:
            self.reference_prop("Campfire", "Camp", "Campfire", x-26, y+3)
            self.light("Camp light", x-26, y-3, 71, .68)
            self.emitter("Camp embers", x-26, y-9, "Embers", manual=False)
            # A second safe rest point before the last encounter keeps regrouping short.
            x2, y2 = cell(7, 7, 27, -11)
            self.reference_prop("FieldRest", "Field camp", "Campfire", x2, y2)
            self.light("Field camp light", x2, y2-6, 50, .49)
        else:
            self.reference_prop("Campfire", "Camp", "Campfire", x, y+11)
            self.light("Market light", x, y+1, 74, .67)
            self.emitter("Market embers", x, y-3, "Embers", manual=False)
            self.prop("Quartermaster Ysra", "Healer", x+26, y+11)
            self.label("Quartermaster plate", "Ysra", x-10, y-36, 40, 8, "#FFDEA2")
        x, y = cell(*PORTALS[self.zone])
        self.reference_prop("Portal", "Gate", "Portal", x, y)
        self.light("Gate light", x, y-15, 55, .48, "#75dfc9")

    def reference_prop(self, field, name, art, x, y):
        self.prop(name, art, x, y)
        self.references[field] = self.add(name+" footprint", x, y, 1, 1, "")

    def light(self, name, x, y, radius, intensity, color="#FFC98A"):
        art = "FX/LanternGlow" if color == "#FFC98A" else "FX/GateGlow"
        spread = round(radius * (1 + intensity))
        self.add(name, x-spread//2, y-spread*3//10+8, spread, spread*3//5,
            sprite(art, order=-2, tint=ZONE_TINTS[self.zone]))

    def emitter(self, name, x, y, art, manual=True):
        w, h = (235, 105) if art == "Mist" else (44, 44)
        return self.add(name, x-w//2, y-h//2, w, h, sprite(
            "FX/Blank", sortingLayer=4 if art == "Mist" else 3) +
            component("spriteAnimator", clip=asset("FX/"+art+".clip"), playOnStart=not manual))

    def label(self, name, text, x, y, w, px, color="#FFEAD3", active=True):
        return self.add(name, x, y, w, px+7, component("text2D", text=text, size=px, color=color,
            richText=True, sortingLayer=5, order=1), active=active)

    def actor(self, name, art, x, y, foe=False, boss=False):
        root = self.add(name, x, y, 1, 1, "")
        self.add(name+" shadow", -15, -5, 30, 12, sprite("UI/Shadow", order=-1, pixelSnap=True), root)
        w, h = ROLE[art]
        self.add(name+" motion", -w//2, -h+3, w, h, sprite(sortByY=True, pixelSnap=True) +
            component("spriteAnimator", controller=asset("Motion/"+art+".animator"), playOnStart=True),
                 root, centered=True)
        if not boss:
            self.add(name+" bar", -12, -h-3, 24, 4, sprite(squareColor="#151925", sortingLayer=4), root)
            self.add(name+" HP", -11, -h-2, 22, 2, sprite(squareColor="#ff766e" if foe else "#58e3cf", sortingLayer=4, order=1), root)
        return root

    def actors(self):
        x, y = cell(2, 2)
        self.references["Companions"] = []
        for i, (role, (dx, dy)) in enumerate(zip(PARTY, FORMATION)):
            root = self.actor(f"Companion {i}" if i else "Captain", role, x+dx, y+dy)
            if i:
                self.references["Companions"].append(root)
        self.references["Enemies"] = []
        self.references["EnemyGroups"] = GROUPS[self.zone]
        self.references["EnemyKinds"] = [("Marshling","Wisp","Brute","Stag").index(art) for art, *_ in WAVES[self.zone]]
        for i, (art, q, r, dx, dy) in enumerate(WAVES[self.zone]):
            root = self.actor(f"Foe {i+1}", art, *cell(q, r, dx, dy), True, art == "Stag")
            self.references["Enemies"].append(root)
        self.add("Camera anchor", 0, -16, 1, 1, "", self.key("Captain"))
        self.references["EncounterFocus"] = self.add("Encounter focus", x, y, 1, 1, "",
                                                     active=False, groups=("Expedition framing",))
        self.references["Camera"] = self.add("Expedition camera", x, y-16, 1, 1, component(
            "camera2D", zoom=1, follow=self.key("Camera anchor"), halfLife=.10,
            frameGroup="Expedition framing", framePadding=69, deadZoneWidth=5, deadZoneHeight=5))

    def effects(self):
        self.references["Sounds"] = []
        for name, volume in [("Strike", .35), ("Hurt", .42), ("Bow", .36), ("Pulse", .54),
                             ("Crown", .34), ("Clear", .43), ("Heal", .40), ("Windup", .53), ("Step", .20)]:
            self.references["Sounds"].append(self.add(name+" sound", 0, 0, 1, 1, component(
                "audioSource", sound=asset("FX/"+name+".wav"), bus="Effects", volume=volume)))
        self.references["CombatEmitters"] = []
        banner = None
        for art in ("Steel","Brass","Feathers","Critical","Jade","Mud","WispSmoke","Stone",
                    "Soul","Bannerglow","Emberfall","Mud","WispSmoke","Stone","Soul","Soul",
                    "MudPuff","WispPuff","StonePuff"):
            name = f"Particles {len(self.references['CombatEmitters']) + 1} {art}"
            parent = banner if art == "Emberfall" else None
            emitter = self.add(name, 0, 0, 1, 1, component("particleEmitter2D",
                particles=asset(f"FX/{art}.particles"), trigger="Manual", sortingLayer=4, order=-3), parent)
            self.references["CombatEmitters"].append(emitter)
            if art == "Bannerglow":
                banner = emitter
        for field, art, w, h in (
            ("Ring","PulseRune",204,104), ("ShieldRing","ShieldRune",52,28),
            ("HealGlyph","HealRune",44,26), ("Warning","GuardianRune",96,96),
            ("Cracks","GuardianCrack",104,104),
        ):
            self.references[field] = self.add(art, 0, 0, w, h,
                sprite("FX/"+art, sortingLayer=1, order=1), centered=True)
        for field, art, w, h in (("Saber","Saber",52,52),("WarFlag","WarFlag",44,50)):
            self.references[field] = self.add(art, 0, 0, w, h,
                sprite("FX/Blank", sortingLayer=4, order=-4) +
                component("spriteAnimator", clip=asset(f"FX/{art}.clip"), playOnStart=False),
                active=field == "Saber", centered=True)
        self.references["Numbers"], self.references["NumberInk"], self.references["NumberOutline"] = [], [], []
        for i in range(12):
            name = f"Damage {i+1}"
            root = self.add(name, 0, 0, 1, 1, "")
            self.references["Numbers"].append(root)
            self.references["NumberInk"].append(self.add(name+" ink", -38, -9, 76, 18, component(
                "text2D", text="", size=12, color="#FFF1C6", richText=True, sortingLayer=5, order=2), root))
            for j, (dx, dy) in enumerate(((-.65,0),(.65,0),(0,-.65),(0,.65))):
                self.references["NumberOutline"].append(self.add(name+f" outline {j+1}", -38+dx, -9+dy, 76, 18, component(
                    "text2D", text="", size=12, color="#24151B", richText=True, sortingLayer=5, order=1), root))
        self.references["Arrows"] = [self.add(f"Arrow {i+1}", 0, 0, 16, 16, sprite(
            "UI/Arrow", sortingLayer=4) + component("trail2D", time=.12, width=2.5,
            startColor="#EAC68A", endColor="#B78E66", maxPoints=10,
            minStep=2, sortingLayer=4, order=-3), active=False, centered=True) for i in range(8)]
        self.references["Loot"] = [self.add(f"Crown {i+1}", 0, 0, 13, 13, sprite(
            "UI/Coin", sortingLayer=4)) for i in range(12)]
        self.references["Waypoint"] = self.add("Rally pennant", 0, 0, 20, 20,
            sprite("UI/Compass", sortingLayer=4), active=False)

    def widget(self, name, x, y, w, h, *, parent=None, text=None, px=9, color="#fff1d7",
               skin=None, anchor=None, image_color=None, rich=False, wrap=False, vertical=None, scale_width=False):
        attrs = {"anchor": anchor} if anchor else {}
        if vertical is not None:
            attrs.update(top=vertical[0], bottom=vertical[1])
        if scale_width:
            attrs["scaleHorizontalWithText"] = True
        border = {"Panel": 7, "Scroll": 7, "Cloth": 7, "Ledger": 9, "Button": 5,
                  "ButtonFocus": 5, "ButtonDown": 5, "Keycap": 5, "StateBadge": 5}.get(skin)
        props = component("uiRect", **attrs)
        if skin:
            props += component("uiImage", sprite=asset("UI/"+skin+".png"), **(
                {"borders": dict.fromkeys(("left","top","right","bottom"), border)} if border else {}))
        elif image_color:
            props += component("uiImage", color=image_color)
        if text is not None:
            props += component("uiText", text=text, size=px, color=color, richText=rich, wrap=wrap, insetX=0, insetY=0)
        return self.add(name, x, y, w, h, props, parent or self.ui)

    def text(self, name, parent, x, mid, width, text="", px=9.5, color="#FFF1D7", rich=False, scale_width=False, align_right=False):
        # Row-local groups give text an anchor and preserve the common baseline as font metrics change.
        row = self.widget(name+" row", x, mid-1, width, 2, parent=parent,
                          anchor="TopRight" if align_right else None)
        return self.widget(name, 0, 1.6, width, 1, parent=row, text=text, px=px, color=color,
                           anchor="MiddleLeft", rich=rich, scale_width=scale_width)

    def centered_label(self, name, parent, x, y, w, h, text, px=8.5, color="#FFF1D7"):
        row = self.widget(name+" center", x, y, w, h, parent=parent)
        advance = round(self.hud_font.getlength(text) * px / 272, 2)
        return self.widget(name, 0, 1.6, advance, 1, parent=row, anchor="Center", scale_width=True,
                           text=text, px=px, color=color)

    def keycap(self, name, parent, x, y, width, height, key):
        chip = self.widget(name, x, y, width, height, parent=parent, skin="Keycap")
        self.centered_label(name+" glyph", chip, 0, 0, width, height, key, color="#FFDFAC")
        return chip

    def action(self, name, parent, x, y, w, key, label):
        rect = self.widget(name, x, y, w, 52, parent=parent, skin="Button")
        focus = self.widget(name+" selected", 0, 0, w, 52, parent=rect, skin="ButtonFocus")
        pressed = self.widget(name+" pressed", 0, 0, w, 52, parent=rect, skin="ButtonDown")
        key_width = {"Enter":46, "Tab":36, "N":24}[key]
        self.keycap(name+" key", rect, (w-key_width)//2, 5, key_width, 21, key)
        self.centered_label(name+" text", rect, 0, 25, w, 24, label, px=9.5)
        return rect, focus, pressed

    def hud(self):
        self.hud_font = ImageFont.truetype(str(ASSETS/"Fonts/GowunDodum-Regular.ttf"), 272)
        self.ui = self.add("HUD", 0, 0, 1, 1, component("uiCanvas"))
        r = self.references
        party = self.widget("Party panel", 8, 8, 124, 34, skin="Panel")
        r["PartyPanel"] = party
        r["PartySlots"], r["PartyFill"], r["PartyDown"] = [], [], []
        for i, role in enumerate(PARTY):
            col = self.widget(f"Party slot {i+1}", 8+22*i, 4, 20, 26, parent=party)
            r["PartySlots"].append(col)
            self.widget(f"Portrait {i+1}", 0, 0, 20, 20, parent=col, skin=role)
            r["PartyDown"].append(self.widget(f"Down {i+1}", 0, 0, 20, 20, parent=col, skin="Down"))
            self.widget(f"Portrait track {i+1}", 0, 21, 20, 7, parent=col, image_color="#231413")
            self.widget(f"Portrait wax {i+1}", 1, 22, 18, 5, parent=col, image_color="#A54635")
            r["PartyFill"].append(self.widget(f"Portrait HP {i+1}", 1, 22, 18, 5, parent=col, skin="GaugeGold"))

        progress = self.widget("Progress panel", 356, 8, 116, 34, skin="Panel")
        r["ProgressPanel"] = progress
        self.widget("Crowns illustration", 8, 10, 12, 12, parent=progress, skin="Crown")
        r["Crowns"] = self.text("Crowns value", progress, 22, 16, 43, "6", 10)
        self.text("Level label", progress, -31, 16, 18, "Lv", 8.5, "#FFDFAC", align_right=True)
        badge = self.widget("Level medal", -6, 4, 24, 24, parent=progress, skin="LevelBadge", anchor="TopRight")
        r["Level"] = self.centered_label("Level value", badge, 0, 0, 24, 24, "0", px=9.5)
        r["ExperienceTrack"] = self.widget("XP track", 8, 27, 100, 5, parent=progress, image_color="#221510")
        r["Experience"] = self.widget("XP fill", 9, 28, 98, 3, parent=progress, skin="GaugeGold")

        pulse = self.widget("Pulse panel", 180, 226, 108, 36, skin="Panel")
        r["PulsePanel"] = pulse
        self.widget("Pulse medallion", 8, 5, 26, 26, parent=pulse, skin="Pulse")
        r["PulseSegments"] = [self.widget(f"Charge {i}", 8, 5, 26, 26, parent=pulse, skin=f"Charge{i:02}") for i in range(1, 13)]
        r["PulseShine"] = self.widget("Ready glint", 8, 5, 26, 26, parent=pulse, skin="MedalShine")
        r["PulseCaption"] = self.text("Pulse caption", pulse, 40, 18, 98, "Pulse Ready")

        wave = self.widget("Wave panel", 8, 226, 164, 36, skin="Panel")
        r["WavePanel"] = wave
        self.text("Wave label", wave, 8, 18, 48, "Wave", 9.5)
        r["WaveMarks"] = []
        for i in range(3):
            self.widget(f"Ward {i+1} socket", 58+i*10, 14, 8, 8, parent=wave, skin="WaveOff")
            r["WaveMarks"].append(self.widget(f"Ward {i+1} lit", 58+i*10, 14, 8, 8, parent=wave, skin="WaveOn"))
        self.widget("Wave divider", 91, 8, 1, 20, parent=wave, image_color="#A9754C")
        self.widget("Foe icon", 96, 12, 12, 12, parent=wave, skin="FoeMark")
        r["FoeCount"] = self.text("Foe count", wave, 112, 18, 47, "0/9", 9.5)

        prompt = self.widget("Status panel", 8, 226, 164, 36, skin="Scroll")
        r["PromptPanel"] = prompt
        r["Prompt"] = self.text("Status", prompt, 9, 18, 147, color="#45291B")
        r["PromptKeyText"] = self.text("Keyed status", prompt, 60, 18, 94, color="#45291B")
        r["GateChip"] = self.keycap("Gate key", prompt, 9, 7, 46, 22, "Enter")
        r["RestChip"] = self.keycap("Rest key", prompt, 9, 7, 46, 22, "R")

        boss = self.widget("Guardian panel", 142, 8, 204, 36, skin="Panel")
        r["BossPanel"] = boss
        self.text("Guardian label", boss, 8, 14, 120, "Stag Guardian", 9.5)
        tell = self.widget("Guardian badge", 128, 4, 68, 20, parent=boss, skin="StateBadge")
        r["BossTell"] = self.text("Guardian tell", tell, 5, 10, 60, px=8.5)
        self.widget("Guardian track", 8, 26, 188, 6, parent=boss, image_color="#261418")
        r["BossFill"] = self.widget("Guardian HP", 9, 27, 186, 4, parent=boss, image_color="#ED8268")

        card = self.widget("Chapter card", 150, 8, 180, 41)
        r["ChapterPanel"] = card
        r["BannerSkin"] = self.widget("Cloth banner", 0, 0, 180, 41, parent=card, skin="Cloth")
        r["ScrollSkin"] = self.widget("Zone scroll", 0, 0, 180, 41, parent=card, skin="Scroll")
        r["ChapterTitle"] = self.text("Chapter title", card, 8, 13, 164, px=10, rich=True)
        r["ChapterLine"] = self.text("Chapter subtitle", card, 8, 28, 164, px=8.5, color="#FFDFAC", rich=True)
        keys = self.widget("Gate notice actions", 8, 20, 164, 18, parent=card)
        r["ChapterActions"] = keys
        self.keycap("Chapter gate key", keys, 0, 0, 46, 18, "Enter")
        r["ChapterGate"] = self.text("Chapter gate action", keys, 50, 9, 110, px=8.5)
        retreat = self.widget("Return shortcut", 88, 0, 76, 20, parent=keys)
        r["ChapterRetreat"] = retreat
        self.keycap("Return key", retreat, 0, 0, 20, 18, "V")
        self.text("Return action", retreat, 24, 9, 52, "Haven", px=8.5)

        menu = self.widget("Ledger", 36, 29, 408, 212, skin="Ledger")
        r["MenuPanel"] = menu
        r["MenuTag"] = self.text("Ledger heading", menu, 16, 24, 180, px=8.5, color="#563722")
        r["MenuTitle"] = self.text("Ledger title", menu, 16, 49, 180, px=14, color="#382116")
        self.widget("Ledger left rule", 16, 66, 176, 1, parent=menu, image_color="#B28356")
        r["MenuRecordLabels"], r["MenuRecord"] = [], []
        for i, label in enumerate(("Ashwater", "Gallery", "Crypt", "Level", "Band", "Field time")):
            mid = 80+i*21
            r["MenuRecordLabels"].append(self.text(f"Record label {i+1}", menu, 16, mid, 114, label, 9.5, "#45291B"))
            r["MenuRecord"].append(self.text(f"Expedition record {i+1}", menu, 133, mid, 58, px=9.5, color="#45291B"))
        self.widget("Record column rule", 124, 70, 1, 127, parent=menu, image_color="#C09362")
        r["MenuRule"] = self.key("Record column rule")
        r["MenuLine"] = self.text("Ledger description", menu, 216, 44, 176, px=9.5, color="#45291B")
        r["MenuLine2"] = self.text("Ledger detail", menu, 216, 69, 176, px=9.5, color="#45291B")
        actions = [
            self.action("Muster action", menu, 216, 96, 88, "Enter", "Muster"),
            self.action("Resume action", menu, 216, 96, 88, "Enter", "Resume"),
            self.action("Haven action", menu, 216, 96, 84, "Enter", "Haven"),
            self.action("New run action", menu, 308, 96, 84, "N", "New run"),
            self.action("Close notes action", menu, 216, 144, 88, "Tab", "Close"),
        ]
        r["MenuButtons"], r["ButtonFocus"], r["ButtonDown"] = map(list, zip(*actions))
        seal = self.widget("Ledger signature", 216, 163, 176, 33, parent=menu)
        r["MenuSeal"] = seal
        self.widget("Signature rule", 0, 30, 176, 1, parent=seal, image_color="#B28356")
        self.widget("Expedition seal", 0, 4, 24, 24, parent=seal, skin="Wax")
        self.text("Ledger signature text", seal, 32, 17, 144, "Filed by Ysra", 8.5, "#563722")

        r["MenuHelpRows"] = []
        for i, (key, label) in enumerate((("WASD","Move / click"),("H","Hire"),("U","Upgrade"),
                                          ("Enter","Gate"),("Space","Pulse"),("R","Rest at fire"))):
            pos = (0,1,2,3,1,2)[i]
            line = self.widget(f"Field note {i+1}", 216, 24+28*pos, 176, 24, parent=menu)
            r["MenuHelpRows"].append(line)
            self.keycap(f"Field note key {i+1}", line, 0, 1, 52, 22, key)
            self.text(f"Field note label {i+1}", line, 60, 12, 116, label, 8.5, "#45291B")

        shop = self.widget("Quartermaster shop", 8, 174, 164, 88, skin="Panel")
        r["ShopPanel"] = shop
        self.text("Quartermaster heading", shop, 8, 15, 148, "Quartermaster", 10)
        r["ShopPrices"] = []
        for i,(key, label, field, price) in enumerate((("H","Hire","HireLine","6"),("U","Gear","GearLine","8"))):
            row = self.widget(label+" action row", 8, 28+27*i, 148, 24, parent=shop, skin="Button")
            self.keycap(label+" key", row, 4, 2, 23, 20, key)
            r[field] = self.text(label+" price", row, 32, 12, 78, px=9.5)
            price_box = self.widget(label+" crown price", 112, 0, 30, 24, parent=row)
            r["ShopPrices"].append(price_box)
            self.widget(label+" coin", 0, 6, 12, 12, parent=price_box, skin="Crown")
            self.text(label+" amount", price_box, 16, 12, 13, price, 9.5)

    def build(self):
        self.terrain()
        self.actors()
        self.effects()
        self.hud()
        self.references.update({"Zone":self.zone, "ZoneName":NAMES[self.zone], "ZoneSubtitle":SUBTITLES[self.zone],
                                "Haven":{"Id":scene_id("Haven")}, "Next":{"Id":scene_id(BIOMES[self.zone+1])}
                                if 0 < self.zone < 3 else {"Id": "00000000-0000-0000-0000-000000000000"}})
        self.references["RegionTotals"] = [len(wave) for wave in WAVES[1:]]
        self.references["Routes"] = [scene_id(v) for v in BIOMES[1:]]
        for field, roots in [("Party", [self.key("Captain"), *self.references["Companions"]]),
                             ("Foe", self.references["Enemies"])]:
            stems = ["Captain", *[f"Companion {i}" for i in range(1, 5)]] if field == "Party" else [
                f"Foe {i+1}" for i in range(len(roots))]
            self.references[field+"Motion"] = [self.key(s+" motion") for s in stems]
            self.references[field+"Bars"] = [self.key(s+" bar") if s+" bar" in self.items else self.key(s) for s in stems]
            self.references[field+"Life"] = [self.key(s+" HP") if s+" HP" in self.items else self.key(s) for s in stems]
        self.items["Captain"] = self.items["Captain"].replace("  components: []", "  components:") + component("script", script="Expedition", values=self.references)
        out = "schemaVersion: 42\nobjects:\n" + "".join(self.items.values())
        return out + "defaultCameraId: " + self.references["Camera"] + "\n"


def main():
    for zone, name in enumerate(BIOMES):
        (ROOT/"Scenes"/(name+".scene")).write_text(Scene(zone).build(), encoding="utf-8")


if __name__ == "__main__":
    main()
