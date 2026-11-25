# Rebuilds samples/CameraLab/Assets/Showcase from the other showcases' art (same style, nothing generated):
# KeyRoom's hero, floor and crates, the herb yard's grass and stalls, the night camp's tent and pines.
# The partner is the hero with other hair and tunic colours (exact palette swaps).
# Run:  python3 showcase-src/camera/art.py
import pathlib, shutil, struct, zlib

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples"
OUT = ROOT / "CameraLab/Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
copies = {
    "KeyRoom": ["Floor.png", "Barrel.png", "Crate.png", "Chest.png", "ShadowM.png", "ShadowL.png"]
               + [f"HeroIdle{d}.png" for d in "NESW"] + [f"HeroWalk{d}{i}.png" for d in "NESW" for i in (1, 2, 3, 4)],
    "InventoryLab": ["Grass.png", "Stall.png", "Well.png", "Fence.png", "Bush.png", "Flowers.png", "Herb1.png", "Herb2.png", "Herb3.png", "Herbalist.png"],
    "ParticleLab": ["Tent.png", "Pine.png", "Lantern.png", "Bench.png", "Campfire.png"],
}
for sample, names in copies.items():
    for name in names: shutil.copy(ROOT / sample / "Assets/Showcase" / name, OUT / name)
shutil.copy(ROOT / "PlatformerLab/Assets/Ui/UiShade.png", OUT / "Panel.png")   # the HUD panel frame (9-slice, borders 4)


def load(path):
    data = path.read_bytes(); i, idat = 8, b""
    while i < len(data):
        n = struct.unpack(">I", data[i:i + 4])[0]; kind = data[i + 4:i + 8]; body = data[i + 8:i + 8 + n]
        if kind == b"IHDR": w, h = struct.unpack(">II", body[:8]); assert body[8:10] == b"\x08\x06", "expects 8-bit RGBA"
        if kind == b"IDAT": idat += body
        i += 12 + n
    raw, stride, rows, prev, p = zlib.decompress(idat), w * 4, [], bytearray(w * 4), 0
    for _ in range(h):
        f = raw[p]; line = bytearray(raw[p + 1:p + 1 + stride]); p += 1 + stride
        for x in range(stride):
            a = line[x - 4] if x >= 4 else 0; up = prev[x]; c = prev[x - 4] if x >= 4 else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + up) & 255
            elif f == 3: line[x] = (line[x] + (a + up) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(up - c), abs(a - c), abs(a + up - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else up if pb <= pc else c)) & 255
        rows.append(line); prev = line
    return w, h, rows


def save(path, w, h, rows):
    chunk = lambda k, d: struct.pack(">I", len(d)) + k + d + struct.pack(">I", zlib.crc32(k + d))
    raw = b"".join(b"\x00" + bytes(r) for r in rows)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


# Red hair -> straw blond, green tunic -> blue.
SWAP = {(0xB0, 0x4E, 0x4B): (0xD8, 0xB0, 0x4A), (0x7D, 0x2F, 0x29): (0x9A, 0x74, 0x2A), (0x45, 0x68, 0x2F): (0x3E, 0x5C, 0x9A)}
for src in sorted(OUT.glob("Hero*.png")):
    w, h, rows = load(src)
    for r in rows:
        for x in range(w):
            if (k := tuple(r[x * 4:x * 4 + 3])) in SWAP: r[x * 4:x * 4 + 3] = bytes(SWAP[k])
    save(OUT / src.name.replace("Hero", "Partner"), w, h, rows)
print("wrote", len(list(OUT.glob("*.png"))), "pictures to", OUT.relative_to(ROOT.parent))
