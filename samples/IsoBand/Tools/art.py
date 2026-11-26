"""Build IsoBand's pixel art, clips, particles and sounds from its checked-in source sprites."""
import hashlib
import math
import random
import uuid
import wave
from pathlib import Path

from PIL import Image, ImageChops, ImageColor, ImageDraw

import combat_art
import visuals

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
NAMESPACE = uuid.UUID("56a3a1e6-45d4-5055-90b4-d22196ec482f")
ACTORS = ("Captain", "Shield", "Archer", "Healer", "Marshling", "Brute", "Wisp", "Stag")
BIOMES = ("Haven", "Marsh", "Ruins", "Crypt")
TILE_VARIANTS = 12
ZONE_TINTS = ("#EBE4D0", "#CFDED0", "#F0CBAF", "#9B91BB")
PALETTES = [
    ("#526e54", "#618062", "#748a62", "#384f49", "#a78b66", "#c3a77c", "#3c615e", "#283e3d"),
    ("#3a6460", "#47746a", "#628477", "#2a484d", "#87916e", "#a7a782", "#315a5f", "#233942"),
    ("#89735d", "#917d65", "#a58b6c", "#6f5d51", "#917a63", "#a68e72", "#553f4a", "#3c2e3c"),
    ("#51445e", "#65566f", "#7e6c85", "#3b334b", "#7a687f", "#a18da1", "#453b60", "#2e2944"),
]


def uid(name):
    return str(uuid.uuid5(NAMESPACE, name))


def asset(name):
    meta = ASSETS / (name + ".meta")
    return meta.read_text().splitlines()[0].split()[-1] if meta.exists() else uid("asset/" + name)


def put(name, data, extra=""):
    path = ASSETS / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(data, encoding="utf-8")
    Path(str(path) + ".meta").write_text(f"id: {asset(name)}\n" + extra, encoding="utf-8")


def png(name, im, slices=()):
    path = ASSETS / (name + ".png")
    path.parent.mkdir(parents=True, exist_ok=True)
    im = im.convert("RGBA")
    # Exact palette indices for small sprites, preserving independent alpha values.
    if len(im.getcolors(256) or []) > 0:
        swatch = list(dict.fromkeys(im.getdata()))
        ids = {color: i for i, color in enumerate(swatch)}
        pal = Image.new("P", im.size)
        pal.putdata([ids[c] for c in im.getdata()])
        pal.putpalette([v for color in swatch for v in color[:3]])
        pal.info["transparency"] = bytes(c[3] for c in swatch)
        im = pal
    im.save(path, optimize=True)
    meta = f"id: {asset(name + '.png')}\n"
    if slices:
        meta += "slices:\n" + "".join(
            f"- source:\n    x: {x}\n    y: {y}\n    width: {w}\n    height: {h}\n"
            for x, y, w, h in slices
        )
        meta += f"importHash: {hashlib.sha256(path.read_bytes()).hexdigest().upper()}\n"
    Path(str(path) + ".meta").write_text(meta, encoding="utf-8")


def canvas(w, h):
    im = Image.new("RGBA", (w, h))
    return im, ImageDraw.Draw(im)


class SpriteAtlas:
    def __init__(self, name, width=384):
        self.path = name
        self.width = width
        self.x = self.y = self.row_height = 0
        self.frames = {}

    def add(self, frame):
        w, h = frame.size
        # Reuse identical frames and pack cells without the unused slots of a rectangular pose grid.
        key = (w, h, frame.tobytes())
        if key in self.frames:
            return self.frames[key][0]
        if self.x + w > self.width:
            self.y += self.row_height
            self.x = self.row_height = 0
        rect = (self.x, self.y, w, h)
        self.frames[key] = rect, frame
        self.x += w
        self.row_height = max(self.row_height, h)
        return rect

    def save(self):
        out, _ = canvas(self.width, self.y + self.row_height)
        rects = []
        for rect, frame in self.frames.values():
            out.alpha_composite(frame, rect[:2])
            rects.append(rect)
        png(self.path, out.quantize(colors=224, method=Image.Quantize.FASTOCTREE), rects)


def actor_art(name, atlas):
    base = Image.open(ASSETS / (name + ".png")).convert("RGBA")
    w, h = base.size
    fw, fh = w + 12, h + 5
    bottom = h - 5
    states = {"Idle": (.23, .23, .23, .23), "Walk": (.10, .10, .10, .10),
              "Attack": (.09, .06, .11), "Hurt": (.055, .08), "Fall": (.07, .08, .33)}
    rects = {}
    for state, durations in states.items():
        for n, _ in enumerate(durations):
            frame, draw = canvas(fw, fh)
            lean, bob = (0, 0)
            if state == "Idle":
                lean, bob = [(0, 0), (0, -1), (1, -1), (1, 0)][n]
            if state == "Walk":
                lean, bob = [(-1, -1), (0, -2), (1, -1), (0, 0)][n]
            if state == "Attack":
                lean, bob = [(-3, -1), (4, -2), (2, 0)][n]
            if state == "Hurt":
                lean = -n * 2
            if state == "Fall" and n:
                body = base.rotate(-20 * n, Image.Resampling.NEAREST, expand=True)
                body.thumbnail((fw - 2, h - n * 7), Image.Resampling.NEAREST)
                if n == 2:
                    body.putalpha(body.getchannel("A").point(lambda a: a * 2 // 3))
                frame.alpha_composite(body, ((fw - body.width) // 2, fh - body.height - 1))
            else:
                if state == "Hurt":
                    tint = {"Marshling": "#B78655", "Wisp": "#A785C8",
                            "Brute": "#AE92A3", "Stag": "#B99D8C"}.get(name, "#C18269")
                    glaze = Image.new("RGBA", base.size, tint)
                    glaze.putalpha(base.getchannel("A"))
                    body = Image.blend(base, glaze, .32 if n == 0 else .13)
                else:
                    body = base
                frame.alpha_composite(body.crop((0, 0, w, bottom)), (6 + lean, 3 + bob))
                for side in range(2):
                    a, b = (0, w // 2) if side == 0 else (w // 2, w)
                    stride = ((-2, 2), (-1, 0), (2, -2), (0, -1))[n] if state == "Walk" else (0, 0)
                    frame.alpha_composite(body.crop((a, bottom, b, h)),
                                          (6 + a + stride[side], 3 + bottom - abs(stride[side]) // 2))
                if bob < 0:
                    frame.alpha_composite(body.crop((0, bottom + bob, w, bottom)), (6, 3 + bottom + bob))
                if state == "Attack" and n == 1:
                    color = {"Captain": "#ffe1a2", "Shield": "#ffb87c", "Archer": "#e7ffc4",
                             "Healer": "#90f2bd"}.get(name, "#ffa5a8")
                    if name in ("Archer", "Healer", "Wisp"):
                        draw.line((fw - 10, 15, fw - 3, 15), fill=color)
                        draw.line((fw - 6, 12, fw - 6, 18), fill=color)
                    else:
                        draw.arc((fw - 23, 8, fw - 1, fh - 6), 290, 74, fill=color, width=2)
                        draw.arc((fw - 20, 10, fw - 3, fh - 8), 320, 63, fill="#fff6d9")
            rects[state, n] = atlas.add(frame)
    stem = "Motion/" + name
    states_yaml = []
    for state, durations in states.items():
        clip = stem + state + ".clip"
        clip_asset(clip, atlas.path, [rects[state, n] for n in range(len(durations))],
                   durations, state in ("Idle", "Walk"))
        states_yaml.append(f"- name: {state}\n  clip: {asset(clip)}\n")
    transitions = [
        ("Any State", "Fall", "Pose", "Equal", 4),
        ("Any State", "Hurt", "Pose", "Equal", 3),
        ("Any State", "Attack", "Pose", "Equal", 2),
        ("Any State", "Walk", "Pose", "Equal", 1),
        ("Any State", "Idle", "Pose", "Equal", 0),
    ]
    put(stem + ".animator", "entry: Idle\nstates:\n" + "".join(states_yaml) +
        'parameters:\n- {name: Pose, kind: Int, default: 0}\ntransitions:\n' +
        "".join(f"- {{from: {source}, to: {dest}, parameter: {param}, test: {test}, value: {value}}}\n"
                for source, dest, param, test, value in transitions))
    if name not in ACTORS[:4]:
        return
    png("UI/" + name, visuals.portrait(base))


def terrain():
    # A 2:1 diamond shares its edge colors with neighboring tiles, given the same biome.
    sheet, _ = canvas(64 * TILE_VARIANTS, 32 * len(BIOMES))
    tile_groups = []
    for zone, (a, b, c, dark, road, light, water, deep) in enumerate(PALETTES):
        for variant in range(TILE_VARIANTS):
            tile, d = canvas(64, 32)
            r = random.Random(34 + 125 * zone + variant)
            wet = variant in (7, 8, 9, 11)
            path = variant in range(3, 7)
            main = water if wet else road if path else a
            d.rectangle((0, 0, 63, 31), fill=main)
            if wet:
                # Ease the shallows into the world backdrop without drawing a water-tile grid.
                ocean = ImageColor.getrgb(deep)
                shallow = ImageColor.getrgb(water)
                d.rectangle((0, 0, 63, 31), fill=tuple((x*3+y*2)//5 for x, y in zip(ocean, shallow)))
                for i in range(19):
                    x, y = r.randrange(64), r.randrange(32)
                    span = r.randrange(2, 17)
                    d.line((x, y, x + span, y), fill=r.choice([deep, deep, water] if variant < 9 else [deep, water, a]))
                    if i % 8 == 0:
                        d.line((x + 1, y - 1, x + span - 2, y - 1), fill=a)
                if variant == 9:
                    for x, y in [(32, 15), (45, 19), (23, 20)]:
                        d.ellipse((x - 3, y - 1, x + 4, y + 1), fill=b)
                        d.point((x + 1, y - 1), fill=c)
            else:
                for i in range(40):
                    x, y = r.randrange(64), r.randrange(32)
                    span = r.randrange(2, 6 if zone == 2 else 9)
                    color = r.choice([b, c, dark]) if not path else r.choice([light, light, b, dark])
                    if path and i < 18:
                        d.polygon([(x, y), (x + span//2, y-2), (x + span, y), (x + span//2, y+2)], fill=color)
                    else:
                        d.line((x, y, x + span//2, y), fill=color)
                        if not path and i % 7 == 0:
                            d.line((x, y, x - 2, y - 3), fill=b if variant == 0 else c)
                            d.line((x, y, x + 1, y - 2), fill=dark)
                if variant == 10: # standing stones / engraved approach
                    d.line((9, 15, 31, 4, 54, 15, 31, 27, 9, 15), fill=dark)
                    d.line((22, 15, 31, 10, 40, 15, 31, 20, 22, 15), fill=light, width=2)
            # The cross sections meet without an authored black grid.
            mask = Image.new("L", (64, 32))
            pixels = mask.load()
            for y in range(32):
                limit = min(y, 31 - y) * 2
                for x in range(max(0, 30 - limit), min(64, 34 + limit)):
                    pixels[x, y] = 255
            if variant != 11:  # Open sea renders through the parallax backdrop but remains solid.
                tile = ImageChops.multiply(tile, Image.new("RGBA", tile.size, ZONE_TINTS[zone]))
                tile.putalpha(mask)
                sheet.alpha_composite(tile, (variant * 64, zone * 32))
            index = zone * TILE_VARIANTS + variant + 1
            name = "water" if wet else "path" if path else "land"
            tile_groups.append(f"- id: {index}\n  source: {{x: {variant*64}, y: {zone*32}, width: 64, height: 32}}\n"
                               f"  terrain: {name}\n" + ("  moveCost: 0\n" if wet else ""))
        for sides in range(1, 4):
            cliff, d = canvas(64, 43)
            if sides & 1:
                d.polygon([(0, 16), (32, 31), (32, 42), (0, 27)], dark)
                d.line((0, 16, 32, 31), fill=b)
                for x in range(1, 31):
                    y = 16 + x // 2
                    if x % 6 in (1, 2):
                        d.line((x, y + 2, x, y + 8), fill=a)
            if sides & 2:
                d.polygon([(32, 31), (63, 16), (63, 27), (32, 42)], deep)
                d.line((32, 31, 63, 16), fill=a)
                for x in range(33, 63):
                    y = 31 - (x - 32) // 2
                    if x % 7 in (0, 1, 2):
                        d.line((x, y + 3, x, y + 8), fill=dark)
            png(f"Terrain/{BIOMES[zone]}{sides}", cliff)
        background, d = canvas(240, 135)
        r = random.Random(zone+11)
        ocean = ImageColor.getrgb(deep)
        for y in range(135):
            d.line((0, y, 239, y), fill=tuple(max(0, channel - 5 + int(2 * math.cos(y * math.tau / 135))) for channel in ocean))
        for _ in range(145):
            x, y = r.randrange(240), r.randrange(135)
            d.line((x, y, x + r.randrange(3, 29), y), fill=tuple(channel + r.choice((-7, -2, 3)) for channel in ocean))
        for x, y in [(37, 111), (170, 38), (193, 91), (51, 31), (104, 55)]:
            d.ellipse((x-15, y-2, x+14, y+2), outline=ocean)
            d.ellipse((x-24, y-4, x+23, y+4), outline=tuple(max(0, q-3) for q in ocean))
        png("Terrain/" + BIOMES[zone] + "Water", background)
    png("FieldTiles", sheet)
    put("Field.tileset", f"image: {asset('FieldTiles.png')}\ntileSize: {{width: 64, height: 32}}\n"
        f"margin: 0\nspacing: 0\nnextId: {len(tile_groups)+1}\ntiles:\n" + "".join(tile_groups))


def props():
    for name in visuals.HOUSES:
        png("Props/" + name, visuals.cottage(name))
    png("Props/Awning", visuals.stall())
    png("Props/Rail", visuals.rail())
    png("Props/Casks", visuals.casks())
    for name in ("Reeds", "Flowers", "Mushrooms", "Cairn", "Grave", "Lantern", "Banner", "Arch", "Crates", "Branches"):
        w, h = {"Banner": (16, 37), "Arch": (68, 57), "Lantern": (15, 30), "Branches": (43, 48), "Grave": (22, 25), "Crates": (26, 20)}.get(name, (23, 15))
        im, d = canvas(w, h)
        d.ellipse((1, h-7, w-2, h-1), fill="#141f2960")
        if name in ("Reeds", "Flowers", "Mushrooms"):
            r = random.Random(7)
            for x in range(2, w-1, 3):
                y = r.randint(2, 9)
                d.line((x+2, h-4, x, y), fill="#63856d")
                d.line((x+2, h-5, x+4, y+2), fill="#84a477")
                if name == "Flowers":
                    d.line((x-1, y, x+1, y), fill=r.choice(["#e9bb7b", "#e59d91", "#e7dac3"]))
                elif name == "Mushrooms" and x % 2 == 0:
                    d.polygon([(x-2,y+2), (x-1,y), (x+2,y-1), (x+4,y+2), (x+2,y+3)], "#b99bd7")
                    d.point((x+1,y), "#ffdab7")
                else:
                    d.line((x,y-2,x,y+1), fill="#bf9c7c")
        elif name in ("Cairn", "Grave"):
            d.polygon([(3,h-4),(4,h-10),(9,h-15),(17,h-14),(20,h-7),(15,h-3)], "#656779")
            d.polygon([(4,h-10),(9,h-15),(17,h-14),(14,h-10)], "#9c9193")
            d.polygon([(9,h-5),(8,5),(11,2),(16,3),(18,6),(17,h-7)], "#777282")
            d.line((9,7,9,h-6), fill="#a3a0ab")
            if name == "Grave":
                d.line((13,7,13,15), fill="#4d4758")
                d.line((11,10,15,10), fill="#4d4758")
        elif name in ("Lantern", "Banner"):
            d.line((7,4,7,h-4), fill="#503536", width=3)
            d.line((7,4,7,h-4), fill="#ad8265")
            if name == "Banner":
                d.line((2,6,14,6), fill="#e1b080")
                d.polygon([(4,7),(13,7),(13,25),(8,21),(4,24)], "#9c5261")
                d.polygon([(6,12),(9,9),(12,12),(9,16)], "#f3c788")
            else:
                d.rectangle((3,5,11,15), fill="#98644e", outline="#29252b")
                d.rectangle((5,6,9,13), fill="#ffc576")
                d.line((7,5,7,12), fill="#ffecaf")
                d.line((4,4,10,4), fill="#be8260")
        elif name == "Arch":
            d.polygon([(1,52),(4,22),(17,11),(34,2),(50,9),(63,22),(66,53),(56,55),(52,25),
                       (44,18),(34,14),(24,19),(15,26),(13,55)], "#574950")
            d.line((2,52,6,22,18,11,34,2,51,10,63,23), fill="#b29578", width=3)
            d.line((9,52,11,25,22,15,34,9,47,14,58,23,63,51), fill="#877269", width=3)
            for x,y in [(5,28),(4,40),(16,15),(25,10),(41,9),(51,16),(59,28),(60,42)]:
                d.line((x,y,x+7,y+2), fill="#3e383f",width=2)
            d.polygon([(29,5),(39,5),(38,14),(34,18),(30,14)], "#b68a6c")
            d.line((34,7,34,13), fill="#f6c58e")
        elif name == "Crates":
            for x, y in [(2,5),(10,1),(13,9)]:
                d.polygon([(x,y+3),(x+6,y),(x+12,y+3),(x+12,y+7),(x+6,y+10),(x,y+7)], "#93654c")
                d.polygon([(x,y+3),(x+6,y),(x+12,y+3),(x+6,y+6)], "#c29a70")
                d.line((x+6,y+6,x+6,y+10), fill="#62483e")
        else:
            d.line((21,h-4,22,14,18,2), fill="#443c53", width=4)
            d.line((21,36,22,15,18,2), fill="#867087", width=2)
            for line in [(21,33,8,26,4,14),(20,25,13,16,12,8),(22,26,34,14,36,1),(22,36,35,29,40,20)]:
                d.line(line, fill="#786681", width=2)
        png("Props/" + name, im)


def seal():
    im, d = canvas(160, 81)
    center = (80, 40)
    d.ellipse((6, 4, 154, 76), fill="#342c4b52", outline="#8c758f")
    d.ellipse((12, 9, 148, 70), outline="#6e5d79")
    d.ellipse((26, 15, 134, 65), outline="#9d829b", width=2)
    d.ellipse((32, 19, 128, 61), outline="#534560")
    for i in range(12):
        a = i * math.tau / 12
        x = round(center[0] + 63*math.cos(a))
        y = round(center[1] + 29*math.sin(a))
        d.polygon([(x,y-3),(x+4,y),(x,y+3),(x-4,y)], "#8f7b94", outline="#332e47")
    d.line([(63, 47), (57, 32), (51, 25), (52, 32), (47, 29), (57, 38), (69, 44),
            (80, 51), (91, 44), (103, 38), (112, 29), (107, 32), (108, 25), (102, 32), (97, 47)],
           fill="#c1a5aa", width=2)
    d.polygon([(79, 49), (86, 53), (80, 62), (74, 53)], "#332f48", outline="#917a98")
    d.line([(2, 54), (16, 49), (20, 42), (34, 42), (41, 39)], fill="#4d405e")
    d.line([(122, 60), (133, 57), (133, 50), (146, 45), (154, 46)], fill="#4d405e")
    png("Props/GuardianSeal", im)


def ui():
    visuals.kit(png)
    for name in ("Crown", "Compass", "Arrow", "Coin", "Shadow"):
        im, d = canvas(16, 16)
        if name in ("Compass",):
            d.polygon([(8,0),(15,8),(8,15),(0,8)], "#1f3b44", outline="#80d9c3")
            d.polygon([(8,3),(10,7),(13,8),(10,10),(8,13),(6,10),(3,8),(6,6)], "#c5f5d8")
            d.point((8,8), "#ffffff")
        elif name in ("Crown", "Coin"):
            d.ellipse((2,2,13,13), fill="#9f694c", outline="#f4ca82")
            d.ellipse((4,3,11,11), fill="#d89b5d")
            d.line((5,6,7,4,9,6,7,8,5,6), fill="#ffeeaf")
        elif name == "Arrow":
            d.line((1,8,13,8), fill="#ffe6a9", width=2)
            d.polygon([(10,5),(15,8),(10,10)], "#fff8d5")
            d.line((1,6,3,8,1,10), fill="#a7e6b6")
        else:
            d.ellipse((1,3,14,12), fill="#151e285b")
            d.ellipse((3,4,12,10), fill="#0e17285c")
        png("UI/" + name, im)


def glow(name, rgb, strength, size, soft=True):
    im, d = canvas(size, size)
    center = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            radius = math.hypot(x-center, y-center) / (center + .5)
            if radius >= 1:
                continue
            falloff = (1 - radius)**2 if soft else 1 - radius**2
            alpha = round(strength * falloff / 5) * 5
            if alpha:
                d.point((x, y), (*rgb, alpha))
    png(name, im)


def clip_asset(name, sheet, rects, durations, loop):
    frames = []
    for (x, y, w, h), seconds in zip(rects, durations):
        frames.append(f"- sprite: {asset(sheet + '.png')}\n  seconds: {seconds}\n"
                      f"  source: {{x: {x}, y: {y}, width: {w}, height: {h}}}\n")
    put(name, "frames:\n" + "".join(frames) + f"loop: {str(loop).lower()}\n")


def effect_frames():
    atlas = SpriteAtlas("FX/Effects")
    for name, count, duration in (("Embers", 8, .11), ("Mist", 12, .14)):
        rng = random.Random(name)
        particles = [(rng.uniform(.45, 1), rng.random()) for _ in range(10 if name == "Embers" else 6)]
        w, h = (96, 43) if name == "Mist" else (44, 44)
        rects = []
        for i in range(count):
            im, d = canvas(w, h)
            for j, (speed, lag) in enumerate(particles):
                t = (i/count + lag) % 1
                if name == "Embers":
                    x = round(22 + math.cos(j*2.4)*t*10)
                    y = round(29 - t*(13+speed*9))
                    if .10 < t < .95:
                        d.point((x, y), "#ffd891" if t < .6 else "#e493566e")
                else:
                    x = round(12+j*14 + math.sin(t*math.tau)*4)
                    y = 14+(j%3)*5
                    alpha = 5 + round(9*math.sin(t*math.pi))
                    for radius, opacity in ((12, alpha), (7, alpha+7)):
                        d.ellipse((x-radius, y-3-(radius//5), x+radius, y+3+(radius//5)),
                                  fill=(154, 200, 183, opacity))
            rects.append(atlas.add(im))
        clip_asset("FX/"+name+".clip", atlas.path, rects, [duration]*count, True)
    atlas.save()


def feedback():
    glow("FX/LanternGlow", (255, 183, 110), 205, 32)
    glow("FX/GateGlow", (113, 217, 188), 190, 32)
    effect_frames()
    combat_art.build(png, put, asset, SpriteAtlas, clip_asset)
    sound = ASSETS/"FX"
    for name, length, start, end, noise in (
        ("Strike", .115, 310, 120, .55), ("Hurt", .18, 220, 72, .28), ("Bow", .13, 510, 250, .35),
        ("Pulse", .50, 145, 49, .13), ("Crown", .22, 750, 1030, .0), ("Clear", .60, 440, 660, .0),
        ("Heal", .30, 610, 840, .0), ("Windup", .78, 80, 185, .08), ("Step", .10, 120, 45, .5),
    ):
        path = sound/(name+".wav")
        r = random.Random(name)
        phase = 0
        with wave.open(str(path), "wb") as wav:
            sample_rate = 12000
            wav.setparams((1, 1, sample_rate, 0, "NONE", "not compressed"))
            pcm = bytearray()
            for i in range(round(length*sample_rate)):
                t = i/(length*sample_rate)
                phase += 2*math.pi*(start + t*(end-start))/sample_rate
                envelope = min(1, t*45)*(1-t)**1.8
                note = math.sin(phase)*.64 + math.sin(phase*1.5)*.22 + math.sin(phase*2)*.14
                pcm.append(128 + round(45*envelope*(note*(1-noise) + r.uniform(-noise, noise))))
            wav.writeframes(pcm)
        Path(str(path)+".meta").write_text("id: "+asset("FX/"+name+".wav")+"\n", encoding="utf-8")


def main():
    terrain()
    for name, actors in (("PartyPoses", ACTORS[:4]), ("FoePoses", ACTORS[4:])):
        atlas = SpriteAtlas("Motion/"+name)
        for actor in actors:
            actor_art(actor, atlas)
        atlas.save()
    props()
    seal()
    ui()
    feedback()


if __name__ == "__main__":
    main()
