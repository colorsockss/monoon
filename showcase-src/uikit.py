# Writes the showcase UI 9-slice frames (1 PNG pixel = 1 game unit) into a folder:  uikit.py <Assets folder>
# Exact pixels matter at this size, so the frames are drawn here rather than generated.
import struct, sys, zlib, pathlib

CLEAR = (0, 0, 0, 0)
OUTLINE = (10, 18, 26, 255)
GOLD, GOLD_DARK = (214, 168, 86, 255), (128, 92, 44, 255)
LIGHT, LIGHT_SOFT = (255, 255, 255, 72), (255, 255, 255, 30)
SHADE, SHADE_SOFT = (0, 0, 0, 100), (0, 0, 0, 46)


def save(path, grid):
    h, w = len(grid), len(grid[0])
    raw = b"".join(b"\x00" + bytes(c for px in row for c in px) for row in grid)
    chunk = lambda kind, data: struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def frame(size, fill):
    return [[fill] * size for _ in range(size)]


def ring(grid, inset, top, left=None, bottom=None, right=None):
    """One-pixel ring `inset` pixels in; a side given None keeps what is there."""
    n = len(grid) - 1 - inset
    left, bottom, right = left or top, bottom or top, right or top
    for i in range(inset, n + 1):
        for (x, y), colour in (((i, inset), top), ((i, n), bottom), ((inset, i), left), ((n, i), right)):
            if colour != "keep": grid[y][x] = colour


def corners(grid, inset, colour):
    n = len(grid) - 1 - inset
    for x, y in ((inset, inset), (n, inset), (inset, n), (n, n)): grid[y][x] = colour


out = pathlib.Path(sys.argv[1])

# Button: clear centre so the theme's state colour shows through. Gold rim with cut corners, then a raised bevel.  borders 4
# The corner pixel takes the panel colour: the button's own fill is a square drawn underneath and would show there.
PANEL, STEEL, STEEL_DARK = (21, 35, 48, 255), (132, 146, 160, 255), (70, 82, 96, 255)
def plate(rim_light, rim_dark, raised, centre):
    g = frame(12, centre); ring(g, 0, OUTLINE); ring(g, 1, rim_light, rim_light, rim_dark, rim_dark); ring(g, 2, (10, 18, 26, 210))
    if raised: ring(g, 3, LIGHT, LIGHT_SOFT, SHADE, SHADE_SOFT)
    else: ring(g, 3, SHADE, SHADE, LIGHT_SOFT, LIGHT_SOFT)
    corners(g, 0, PANEL); corners(g, 1, OUTLINE)
    return g
save(out / "UiButton.png", plate(GOLD, GOLD_DARK, True, CLEAR))

# Slot: steel rim, pressed in and darkened.  borders 4
save(out / "UiSlot.png", plate(STEEL, STEEL_DARK, False, (0, 0, 0, 70)))

# Label: small opaque status plate.  borders 4
g = frame(12, (38, 58, 78, 255)); ring(g, 0, OUTLINE); ring(g, 1, GOLD_DARK); ring(g, 2, LIGHT_SOFT, "keep", SHADE_SOFT, "keep")
save(out / "UiLabel.png", g)

# Shade: translucent backdrop for text drawn over the world.  borders 4
g = frame(12, (12, 20, 34, 176)); ring(g, 0, (8, 12, 20, 230)); ring(g, 1, (214, 168, 86, 150)); corners(g, 1, GOLD)
save(out / "UiShade.png", g)

# Strips: full-width HUD bars with a gold rule on the inner edge.  borders left/right 1, the ruled side 3
for name, ruled in (("UiStripTop.png", 7), ("UiStripBottom.png", 0)):
    g = frame(8, (20, 30, 43, 255))
    inner = ruled - 1 if ruled else 1
    for x in range(8): g[ruled][x] = OUTLINE; g[inner][x] = GOLD; g[inner - 1 if ruled else inner + 1][x] = GOLD_DARK
    save(out / name, g)
print("wrote", *sorted(p.name for p in out.glob("Ui*.png")))
