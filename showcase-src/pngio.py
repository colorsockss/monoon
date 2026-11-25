# Minimal RGBA PNG read / write for the showcase art scripts (no PIL on this Mac).
import struct, zlib


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


