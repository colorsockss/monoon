"""Write small .aseprite files from the documented format (Aseprite itself is not needed).

  python3 aseprite_fixture.py fixtures <directory>   Rgba / Indexed / Multiply regression fixtures
  python3 aseprite_fixture.py sample <SpriteLab>     Assets/Showcase/FireflyBlink.aseprite from Firefly.png
  python3 aseprite_fixture.py variant <file>         the sample with 1.0 s / 0.3 s frames and a red dim frame
"""
from pathlib import Path
import struct
import sys
import zlib

# Layer flags: 1 visible, 2 editable.
VISIBLE, HIDDEN = 3, 2


def text(value):
    data = value.encode('utf-8')
    return struct.pack('<H', len(data)) + data


def chunk(kind, body):
    return struct.pack('<IH', len(body) + 6, kind) + body


def layer(name, flags=VISIBLE, opacity=255, blend=0):
    return chunk(0x2004, struct.pack('<HHHHHHB3x', flags, 0, 0, 0, 0, blend, opacity) + text(name))


def cel(index, x, y, w, h, pixels, opacity=255, compressed=True):
    head = struct.pack('<HhhBHh5x', index, x, y, opacity, 2 if compressed else 0, 0) + struct.pack('<HH', w, h)
    return chunk(0x2005, head + (zlib.compress(pixels) if compressed else pixels))


def linked(index, frame):
    return chunk(0x2005, struct.pack('<HhhBHh5xH', index, 0, 0, 255, 1, 0, frame))


def tags(entries):
    body = struct.pack('<H8x', len(entries))
    for name, start, end, direction, repeat in entries:
        body += struct.pack('<HHBH6x3xx', start, end, direction, repeat) + text(name)
    return chunk(0x2018, body)


def palette(colors):
    body = struct.pack('<III8x', len(colors), 0, len(colors) - 1)
    for rgba in colors: body += struct.pack('<H4B', 0, *rgba)
    return chunk(0x2019, body)


def aseprite(width, height, depth, frames, transparent=0, colors=256):
    """frames: [(milliseconds, [chunk bytes])]"""
    body = b''
    for duration, chunks in frames:
        data = b''.join(chunks)
        body += struct.pack('<IHHH2xI', 16 + len(data), 0xF1FA, len(chunks), duration, len(chunks)) + data
    header = struct.pack('<IHHHHHIH8xB3xHBBhhHH84x', 128 + len(body), 0xA5E0, len(frames), width, height, depth,
                         1, 100, transparent, colors, 1, 1, 0, 0, 16, 16)
    assert len(header) == 128
    return header + body


def fill(w, h, pixel):
    return pixel * (w * h)


def fixture(indexed, multiply=False):
    """8x6, four frames (100/150/200/250 ms). Body is red, Hidden is a green full-canvas layer that must not show,
    Glow is blue at layer opacity 128. Frame 4 links frame 1's Body cel."""
    if indexed:
        red, green, blue, white = b'\x01', b'\x02', b'\x03', b'\x04'
        colors = palette([(0, 0, 0, 0), (255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (255, 255, 255, 255)])
    else:
        red, green, blue, white = b'\xff\x00\x00\xff', b'\x00\xff\x00\xff', b'\x00\x00\xff\xff', b'\xff\xff\xff\xff'
        colors = palette([(0, 0, 0, 255), (255, 255, 255, 255)])
    first = [colors, layer('Body'), layer('Hidden', HIDDEN), layer('Glow', opacity=128, blend=1 if multiply else 0),
             tags([('Walk', 0, 2, 0, 0), ('Back', 0, 2, 1, 0), ('Bounce', 0, 3, 2, 0), ('Twice', 1, 2, 0, 2),
                   ('Swing', 0, 2, 2, 3), ('Mirror', 0, 2, 3, 0)]),
             cel(0, 0, 1, 2, 2, fill(2, 2, red), compressed=False), cel(1, 0, 0, 8, 6, fill(8, 6, green)),
             cel(2, 6, 4, 2, 2, fill(2, 2, blue))]
    frames = [(100, first),
              (150, [cel(0, 1, 1, 2, 2, fill(2, 2, red)), cel(2, 1, 1, 2, 2, fill(2, 2, blue))]),
              (200, [cel(0, 2, 1, 2, 2, fill(2, 2, red)), cel(2, 4, 0, 1, 1, white, opacity=128)]),
              (250, [linked(0, 0)])]
    return aseprite(8, 6, 8 if indexed else 32, frames, colors=5 if indexed else 2)


def read_png(path):
    """8-bit RGBA, non-interlaced PNG -> (width, height, rows of bytes)."""
    data = Path(path).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    pos, idat = 8, b''
    while pos < len(data):
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b'IHDR':
            width, height, bits, color, _, _, interlace = struct.unpack('>IIBBBBB', body)
            assert (bits, color, interlace) == (8, 6, 0), path
        elif kind == b'IDAT': idat += body
        pos += 12 + length
    raw, stride, rows, prev = zlib.decompress(idat), width * 4, [], bytes(width * 4)
    for y in range(height):
        kind, line = raw[y * (stride + 1)], bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - 4] if i >= 4 else 0
            b, c = prev[i], prev[i - 4] if i >= 4 else 0
            if kind == 1: line[i] = (line[i] + a) & 255
            elif kind == 2: line[i] = (line[i] + b) & 255
            elif kind == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif kind == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(bytes(line))
        prev = line
    return width, height, b''.join(rows)


def sample(spritelab, durations=(900, 300), dim_color=None):
    """The SpriteLab showcase firefly's blink: the bright Firefly.png, then the same glow at a third of its alpha (or, with
    dim_color, recoloured). A hidden Sketch layer covers the canvas and must not reach the sheet."""
    w, h, bright = read_png(Path(spritelab) / 'Assets' / 'Showcase' / 'Firefly.png')
    dim = b''.join((bytes(dim_color) + bytes([bright[i + 3]])) if dim_color and bright[i + 3] else
                   bright[i:i + 3] + bytes([bright[i + 3] // 3]) for i in range(0, len(bright), 4))
    sketch = fill(w, h, b'\xff\x00\xff\xff')
    frames = [(durations[0], [palette([(0, 0, 0, 255), (255, 255, 255, 255)]), layer('Art'), layer('Sketch', HIDDEN),
                              cel(0, 0, 0, w, h, bright), cel(1, 0, 0, w, h, sketch)]),
              (durations[1], [cel(0, 0, 0, w, h, dim), linked(1, 0)])]
    return aseprite(w, h, 32, frames, colors=2)


if __name__ == '__main__':
    command, target = sys.argv[1], Path(sys.argv[2])
    if command == 'fixtures':
        target.mkdir(parents=True, exist_ok=True)
        (target / 'Rgba.aseprite').write_bytes(fixture(False))
        (target / 'Indexed.aseprite').write_bytes(fixture(True))
        (target / 'Multiply.aseprite').write_bytes(fixture(False, multiply=True))
    elif command == 'sample':
        (target / 'Assets' / 'Showcase' / 'FireflyBlink.aseprite').write_bytes(sample(target))
    elif command == 'variant':
        # Reads Firefly.png next to the given .aseprite file (Assets/Showcase).
        target.write_bytes(sample(target.parent.parent.parent, (1000, 300), (255, 64, 64)))
    else:
        raise SystemExit(__doc__)
