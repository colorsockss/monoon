# Rebuilds samples/CollisionLab/Assets/Showcase (nothing generated): night grass from InventoryLab's grass, three 5 × 5 stone
# kinds (pebble = circle collider, block = box, shard = triangle polygon) each dim and lantern-lit, the lantern ring, the panel
# and the loading bar fill. Stones are drawn one art pixel per world unit so 10,000 of them fit one 800 × 450 view.
# Run:  python3 showcase-src/collision/art.py
import math, pathlib, shutil, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import load, save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
OUT = SAMPLES / "CollisionLab/Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", OUT / "Panel.png")

# Night grass: the day grass darkened and pushed toward blue.
w, h, rows = load(SAMPLES / "InventoryLab/Assets/Showcase/Grass.png")
for r in rows:
    for x in range(0, w * 4, 4):
        R, G, B = r[x:x + 3]
        r[x:x + 3] = bytes((int(R * .20 + 4), int(G * .27 + 6), int(B * .34 + 14)))
save(OUT / "NightGrass.png", w, h, rows)

SHAPES = {
    "Pebble": [".BBB.", "BLLBB", "BLBBD", "BBBDD", ".DDD."],
    "Block": ["LLLLB", "LBBBD", "LBBBD", "LBBBD", "BDDDD"],
    "Shard": ["L....", "LLB..", "LBBBD", "BBD..", "D...."],
}
DIM = {"Pebble": ((126, 140, 156), (88, 100, 114), (58, 66, 78)),
       "Block": ((134, 120, 106), (96, 86, 78), (64, 56, 52)),
       "Shard": ((110, 150, 150), (72, 104, 108), (46, 70, 74))}
LIT = {"Pebble": ((255, 244, 190), (255, 214, 92), (210, 148, 44)),
       "Block": ((255, 222, 150), (255, 176, 66), (196, 112, 36)),
       "Shard": ((236, 255, 214), (170, 240, 150), (90, 170, 90))}
# Dim stones sit close to the night grass so the lantern's lit circle carries the picture.
DIM = {k: tuple(tuple(int(c * .62) for c in col) for col in v) for k, v in DIM.items()}
for name, art in SHAPES.items():
    for suffix, pal in (("", DIM[name]), ("Lit", LIT[name])):
        px = {"L": pal[0], "B": pal[1], "D": pal[2]}
        save(OUT / f"{name}{suffix}.png", 5, 5, [bytearray(b"".join(bytes(px[c]) + b"\xff" if c in px else b"\0\0\0\0" for c in line)) for line in art])

# Lantern: a warm ring 40 art pixels across (2 units each, 80 × 80) with a faint glow inside and a small flame in the middle.
N = 40
ring = []
for y in range(N):
    line = bytearray()
    for x in range(N):
        d = math.hypot(x + .5 - N / 2, y + .5 - N / 2)
        if 18.6 <= d < 19.8: c = (255, 216, 74, 255)
        elif d < 18.6: c = (255, 200, 90, int(16 + 26 * (1 - d / 18.6)))
        else: c = (0, 0, 0, 0)
        if abs(x + .5 - N / 2) < 1.1 and abs(y + .5 - N / 2) < 1.6: c = (255, 244, 200, 255)
        line += bytes(c) * 2
    ring += [line, bytearray(line)]
save(OUT / "Lantern.png", 2 * N, 2 * N, ring)
save(OUT / "BarFill.png", 4, 4, [bytearray(bytes((255, 216, 74, 255)) * 4) for _ in range(4)])
print("wrote", len(list(OUT.glob("*.png"))), "pictures")
