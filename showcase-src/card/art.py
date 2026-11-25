# Rebuilds samples/CardLab/Assets/Showcase (nothing generated): six card icons from InventoryLab's item icons, the hero and
# yard props from AnimatorLab, the chest from KeyRoom, plus drawn pieces: a card face (9-slice), a card back, a stepping stone,
# and the table felt. One art pixel = 2 world units.  Run:  python3 showcase-src/card/art.py
import pathlib, random, shutil, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import load, save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
OUT = SAMPLES / "CardLab/Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
for name in ("Herb", "Bread", "Lantern", "Map", "Compass", "Rope"): shutil.copy(SAMPLES / f"InventoryLab/Assets/Icons/{name}.png", OUT / f"{name}.png")
for name in ["Grass", "Bush", "Flowers", "Fence", "Barrel", "ShadowM", "HeroIdleE"] + [f"HeroWalkE{i}" for i in range(1, 5)]:
    shutil.copy(SAMPLES / f"AnimatorLab/Assets/Showcase/{name}.png", OUT / f"{name}.png")
shutil.copy(SAMPLES / "KeyRoom/Assets/Showcase/Chest.png", OUT / "Chest.png")
shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", OUT / "Panel.png")


def canvas(w, h, fill=(0, 0, 0, 0)):
    return [[fill] * w for _ in range(h)]


def write(name, art, scale=2):
    rows = []
    for line in art:
        row = bytearray(b"".join(bytes(c) * scale for c in line))
        rows += [bytearray(row) for _ in range(scale)]
    save(OUT / name, len(art[0]) * scale, len(art) * scale, rows)


# Card face, 12 × 12 art pixels drawn as a 9-slice (borders 6 units): dark rim, parchment, a thin gold inner line.
RIM, PAPER, LINE = (43, 33, 24, 255), (236, 222, 184, 255), (196, 168, 112, 255)
face = canvas(12, 12, PAPER)
for y in range(12):
    for x in range(12):
        if x in (0, 11) or y in (0, 11): face[y][x] = RIM
        elif x in (2, 9) and 2 <= y <= 9 or y in (2, 9) and 2 <= x <= 9: face[y][x] = LINE
for x, y in ((0, 0), (11, 0), (0, 11), (11, 11)): face[y][x] = (0, 0, 0, 0)
write("Card.png", face)

# Card back, 42 × 58 art pixels (84 × 116 units): navy with a gold rim and a diamond lattice.
W, H = 42, 58
back = canvas(W, H, (31, 53, 80, 255))
for y in range(H):
    for x in range(W):
        if x in (0, W - 1) or y in (0, H - 1): back[y][x] = RIM
        elif x in (2, W - 3) and 2 <= y <= H - 3 or y in (2, H - 3) and 2 <= x <= W - 3: back[y][x] = (214, 178, 92, 255)
        elif 4 <= x <= W - 5 and 4 <= y <= H - 5 and ((x + y) % 6 == 0 or (x - y) % 6 == 0): back[y][x] = (54, 84, 118, 255)
for x, y in ((0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)): back[y][x] = (0, 0, 0, 0)
for y in range(H // 2 - 3, H // 2 + 4):          # a small gold diamond in the middle
    for x in range(W // 2 - 3, W // 2 + 4):
        if abs(x - W // 2 + .5) + abs(y - H // 2 + .5) <= 3.5: back[y][x] = (232, 196, 102, 255)
write("CardBack.png", back)

# Stepping stone, 12 × 7 art pixels.
ST = {"L": (190, 184, 168, 255), "M": (150, 144, 132, 255), "D": (104, 98, 92, 255)}
stone = [".LLLLLLLL...", "LLMMMMMMLL..", "LMMMMMMMMMD.", "MMMMMMMMMMDD", ".DMMMMMMMDD.", "..DDDDDDDD..", "............"]
write("Stone.png", [[ST.get(c, (0, 0, 0, 0)) for c in line] for line in stone])

# Table felt, 32 × 32 art pixels tiled: dark green with a soft speckle.
rng = random.Random(7)
felt = [[(22 + (d := rng.randint(-3, 3)), 52 + d, 46 + d, 255) for _ in range(32)] for _ in range(32)]
write("Felt.png", felt)
write("Rail.png", [[(92, 62, 38, 255)] * 4, [(122, 84, 50, 255)] * 4, [(70, 46, 30, 255)] * 4])
print("wrote", len(list(OUT.glob("*.png"))), "pictures")
