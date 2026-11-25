# Rebuilds samples/SeedLab/Assets/Showcase from the other showcases' art (nothing generated):
# the dungeon maze tileset (a KeyRoom wall cap + three plain KeyRoom flagstones), the explorer (KeyRoom hero frames),
# the treasure chest, and for the crowd yard: grass, a flagstone tile, a wall tile, a plank tile and a tiny villager.
# Run:  python3 showcase-src/seed/art.py
import pathlib, shutil, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import load, save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
OUT = SAMPLES / "SeedLab/Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
KR = SAMPLES / "KeyRoom/Assets/Showcase"
for name in ["Chest.png", "ShadowM.png"] + [f"HeroIdle{d}.png" for d in "NESW"]: shutil.copy(KR / name, OUT / name)
for name in ["Grass.png", "Flowers.png", "Bush.png", "Well.png"]: shutil.copy(SAMPLES / "InventoryLab/Assets/Showcase" / name, OUT / name)
shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", OUT / "Panel.png")


def crop(path, x, y, w, h):
    _, _, rows = load(path)
    return [bytearray(r[x * 4:(x + w) * 4]) for r in rows[y:y + h]]


T = 32
wall = crop(KR / "WallCap.png", 0, 0, T, T)
special = {r * 8 + c for c, r in ((4, 0), (1, 1), (7, 2), (1, 4), (4, 5), (2, 6), (5, 1), (0, 3), (3, 4), (5, 6))}
plain = [i for i in range(64) if i not in special][:3]
# Floors from KeyRoom's flagstone sheet rebuilt brighter (FloorLight.png: keyroom/floor.png through pix tiles at PIX_GAIN 1.1,
# KeyRoom itself uses 0.72); walls are the wall cap darkened, so the passages read at a glance.
floors = [crop(pathlib.Path(__file__).parent / "FloorLight.png", i % 8 * T, i // 8 * T, T, T) for i in plain]
for r in wall:
    for x in range(0, len(r), 4): r[x:x + 3] = bytes(int(v * .55) for v in r[x:x + 3])
# Path marker: the first floor stone with a gold dot (3 × 3 art pixels with a dark rim), for the solved / saved route.
dot = [bytearray(r) for r in floors[0]]
for y in range(10, 22):
    for x in range(10, 22):
        rim = x in (10, 11, 20, 21) or y in (10, 11, 20, 21)
        if rim and (x in (10, 11) or x in (20, 21)) and (y in (10, 11) or y in (20, 21)): continue   # rounded corners
        dot[y][x * 4:x * 4 + 4] = bytes((92, 64, 20, 255) if rim else (255, 216, 74, 255))
# Maze tileset image, 5 tiles in a row: 1 wall, 2-4 floor stones, 5 path marker (DungeonMaze writes 1 for walls, 2..5 for passages).
save(OUT / "Maze.png", 5 * T, T, [wall[y] + floors[0][y] + floors[1][y] + floors[2][y] + dot[y] for y in range(T)])
# The KeyRoom chest is 20 × 17 art pixels; drop the two outer wood strips, a plank beside each strap and one body row
# so it fits one 32-unit maze cell (16 × 16 art pixels) instead of spilling into the walls.
_, _, chest = load(OUT / "Chest.png")
keep_x = [x for x in range(20) if x not in (1, 6, 13, 18)]
keep_y = [y for y in range(17) if y != 14]
save(OUT / "Chest.png", 32, 32, [bytearray(b"".join(bytes(chest[y * 2 + dy][x * 8:x * 8 + 4]) * 2 for x in keep_x))
                                 for y in keep_y for dy in (0, 1)])
save(OUT / "WallTile.png", T, T, wall)
save(OUT / "FloorTile.png", T, T, floors[0])
_, _, crate = load(KR / "Crate.png")
save(OUT / "Plank.png", 24, 24, [bytearray(r[8 * 4:32 * 4]) for r in crate[10:34]])

# Villager: 5 × 7 art pixels at 2 px each, light robe and hood (the crowd tints each band), darker outline-free shading.
L, M, S, C = (255, 255, 255, 255), (205, 205, 215, 255), (240, 214, 186, 255), (0, 0, 0, 0)
art = [".MMM.", "MLLLM", "MSSSM", ".LLL.", "LLLLL", "LMLML", ".M.M."]
pix = {"L": L, "M": M, "S": S, ".": C}
rows = [bytearray(b"".join(bytes(pix[c]) * 2 for c in line)) for line in art for _ in (0, 1)]
save(OUT / "Villager.png", 10, 14, rows)
print("wrote", len(list(OUT.glob("*.png"))), "pictures")
