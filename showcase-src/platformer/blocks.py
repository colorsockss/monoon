# Ground pieces for PlatformerLab's showcase scenes, cut from the sample's own art so they match the Showcase scene.
# Ground.png is a 4-bit autotile (tile index = up + 2*right + 4*down + 8*left neighbours; a tile with nothing above carries
# grass, every open side has the dark outline). block() lays those tiles out for any size and drops a middle band so the
# outlined edges survive; hill() lays grass along a curve over the same rock; squeeze() shrinks a strip such as Deck.png.
# All pictures are 1 texel per world unit (the scenes use camera zoom 1.5, the Showcase scene's 1.5 screen pixels per texel).
import pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import load, save

ART = pathlib.Path(__file__).resolve().parents[2] / "samples/PlatformerLab/Assets/Showcase"
T = 32
_, _, GROUND = load(ART / "Ground.png")


def _px(rows, x, y): return rows[y][x * 4:x * 4 + 4]


def _tile(up, right, down, left):
    i = up + 2 * right + 4 * down + 8 * left
    return (i % 4) * T, (i // 4) * T


def _cut(rows, w, h):
    """Keep the outer halves of a bigger picture: w columns and h rows, edges intact."""
    W, H = len(rows[0]) // 4, len(rows)
    xs = list(range(w // 2)) + list(range(W - (w - w // 2), W))
    ys = list(range(h // 2)) + list(range(H - (h - h // 2), H))
    return [bytearray(b"".join(_px(rows, x, y) for x in xs)) for y in ys]


def squeeze(name, out, w, h):
    """A smaller copy of a strip picture (Deck.png) with its outline on all sides."""
    _, _, rows = load(ART / name)
    save(ART / out, w, h, _cut(rows, w, h))
    return out


def block(w, h, left=True, right=True):
    """Ground block of w x h units, grass on top; left/right False when another piece of ground continues on that side."""
    cols, rws = -(-w // T), -(-h // T)
    W, H = cols * T, rws * T
    rows = [bytearray(W * 4) for _ in range(H)]
    for r in range(rws):
        for c in range(cols):
            tx, ty = _tile(r > 0, c < cols - 1 or not right, r < rws - 1, c > 0 or not left)
            for y in range(T):
                rows[r * T + y][c * T * 4:(c + 1) * T * 4] = GROUND[ty + y][tx * 4:(tx + T) * 4]
    name = f"Block{w}x{h}{'' if left else 'L'}{'' if right else 'R'}.png"
    save(ART / name, w, h, _cut(rows, w, h) if (W, H) != (w, h) else rows)
    return name


def hill(name, w, h, surface):
    """Ground under a curve: surface(x) is the grass line in rows from the top; rock stays fixed while the grass follows."""
    gx, gy = _tile(0, 1, 1, 1)      # grass tile with ground on three sides
    rx, ry = _tile(1, 1, 1, 1)      # plain rock
    grass = {bytes(_px(GROUND, gx + x, gy + y)) for x in range(T) for y in range(3)}
    rows = [bytearray(w * 4) for _ in range(h)]
    for x in range(w):
        top = round(surface(x))
        for y in range(max(0, top), h):
            rel = y - top
            g = bytes(_px(GROUND, gx + x % T, gy + rel)) if rel < 12 else None
            rows[y][x * 4:x * 4 + 4] = g if g in grass else _px(GROUND, rx + x % T, ry + y % T)
    save(ART / name, w, h, rows)
    return name
