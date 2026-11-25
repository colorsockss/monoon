# Rebuilds samples/SpriteLab/Assets/Showcase: the KeyRoom hero (four-way walk), coin and shadow; AnimatorLab's grass, well,
# bush and flowers; PlatformerLab's sign board (a 9-slice source) and InventoryLab's lantern icon; plus pieces drawn here: an oval
# pond, a ripple mask for the reflection, a lantern post and a firefly dot. Two clips use property keys only: the lantern's
# swing (Rotation) and the coins' pulse (Tint, Opacity). One art pixel = 2 world units.  Run:  python3 showcase-src/sprite/art.py
import math, pathlib, shutil, sys, uuid
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
OUT = SAMPLES / "SpriteLab/Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
KR = SAMPLES / "KeyRoom/Assets/Showcase"
for name in ["Coin", "ShadowM"] + [f"HeroWalk{d}{i}" for d in "NESW" for i in range(1, 5)]: shutil.copy(KR / f"{name}.png", OUT / f"{name}.png")
for name in ("Grass", "Well", "Bush", "Flowers"): shutil.copy(SAMPLES / f"AnimatorLab/Assets/Showcase/{name}.png", OUT / f"{name}.png")
shutil.copy(SAMPLES / "PlatformerLab/Assets/SignBoard.png", OUT / "SignBoard.png")
shutil.copy(SAMPLES / "InventoryLab/Assets/Icons/Lantern.png", OUT / "Lantern.png")
shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", OUT / "Panel.png")


def write(name, art, scale=2):
    rows = []
    for line in art:
        row = bytearray(b"".join(bytes(c) * scale for c in line))
        rows += [bytearray(row) for _ in range(scale)]
    save(OUT / name, len(art[0]) * scale, len(art) * scale, rows)


CLEAR = (0, 0, 0, 0)
# Pond: a rounded rectangle of water with a straight top bank, 165 × 50 art pixels, and a separate rim drawn above the
# reflection so the reflection starts at the waterline and never leaves the water.
W, H, R, RIM = 165, 50, 12, 3
def inside(x, y, inset=0):
    cx, cy = min(max(x + .5, R + inset), W - R - inset), min(max(y + .5, R + inset), H - R - inset)
    return math.hypot(x + .5 - cx, y + .5 - cy) <= R and inset <= x + .5 <= W - inset and inset <= y + .5 <= H - inset
BANK, WATER, GLINT = (58, 92, 70, 255), (70, 128, 178, 255), (96, 154, 204, 255)
pond = [[(WATER if (x * 3 + y * 7) % 23 else GLINT) if inside(x, y) else CLEAR for x in range(W)] for y in range(H)]
rim = [[BANK if inside(x, y) and not inside(x, y, RIM) else CLEAR for x in range(W)] for y in range(H)]
write("Pond.png", pond)
write("PondRim.png", rim)
# Ripple mask for the reflection (the hero's 21 × 30 art size): every fourth row is cut, like ripples across the water.
write("Ripple.png", [[(255, 255, 255, 0 if y % 4 == 3 else 255) for x in range(21)] for y in range(30)])
# Lantern post: a dark wooden post with an arm to the right, 30 × 48 art pixels.
WOOD, DARK = (122, 84, 50, 255), (82, 54, 34, 255)
post = [[CLEAR] * 30 for _ in range(48)]
for y in range(48):
    for x in range(2, 7): post[y][x] = DARK if x in (2, 6) else WOOD
for x in range(2, 26):
    for y in (2, 3, 4): post[y][x] = DARK if y in (2, 4) else WOOD
for y in range(5, 9): post[y][22] = DARK                                  # the hook
write("Post.png", post)
# Firefly: a soft 5 × 5 glow.
write("Firefly.png", [[(255, 236, 140, max(0, int(255 * (1 - math.hypot(x - 2, y - 2) / 2.9)))) for x in range(5)] for y in range(5)])


def meta(name):
    m = OUT / (name + ".meta")
    if not m.exists(): m.write_text(f"id: {uuid.uuid5(uuid.UUID('4394934e-c0de-4350-a585-27a682237b83'), 'showcase/' + name)}\n")
    return m.read_text().split()[1]


(OUT / "LanternSwing.clip").write_text("""frames:
- seconds: 2
  useRenderer: true
loop: true
tracks:
- property: Rotation
  keys:
  - time: 0
    value: "-14"
  - time: 1
    value: "14"
  - time: 2
    value: "-14"
""")
(OUT / "CoinPulse.clip").write_text(f"""frames:
- seconds: 1.2
  sprite: {meta('Coin.png')}
loop: true
tracks:
- property: Tint
  keys:
  - time: 0
    value: "#FFFFFF"
  - time: 0.6
    value: "#3FC39F"
  - time: 1.2
    value: "#FFFFFF"
- property: Opacity
  keys:
  - time: 0
    value: "1"
  - time: 0.6
    value: "0.25"
  - time: 1.2
    value: "1"
""")
print("wrote", len(list(OUT.glob("*.png"))), "pictures, 2 clips")
