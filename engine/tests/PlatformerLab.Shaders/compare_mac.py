"""Compare synchronized macOS window PNGs named 00.png through 32.png in each mode."""
import csv
import math
from pathlib import Path
import struct
import subprocess
import sys
import tempfile


def samples(path):
    # Decode the capture's display color profile identically for all three windows.
    with tempfile.NamedTemporaryFile(suffix=".bmp") as temp:
        subprocess.run(["sips", "-s", "format", "bmp", str(path), "--out", temp.name],
                       check=True, stdout=subprocess.DEVNULL)
        bmp = Path(temp.name).read_bytes()
    offset = struct.unpack_from("<I", bmp, 10)[0]
    width, height = struct.unpack_from("<ii", bmp, 18)
    step = struct.unpack_from("<H", bmp, 28)[0] // 8
    assert step in (3, 4)
    stride = ((width * step + 3) // 4) * 4
    rows = [bmp[offset + y * stride:offset + (y + 1) * stride]
            for y in (range(height - 1, -1, -1) if height > 0 else range(-height))]

    # Calibrate to the actual letterbox, including the Scene panel's noninteger zoom.
    clear = bytes([45, 33, 24] + ([255] if step == 4 else [])) * 4
    edges = [(row.find(clear) // step, row.rfind(clear) // step + 4, y + 1)
             for y, row in enumerate(rows) if clear in row]
    left = min(x for x, _, _ in edges)
    scale = (max(x for _, x, _ in edges) - left) / 800
    top = max(y for _, _, y in edges) - 450 * scale

    def pixel(x, y):
        start = math.floor(left + x * scale) * step
        return tuple(rows[math.floor(top + y * scale)][start:start + 3][::-1])

    flame = [pixel(422 + (x + .5) * 27 / 24, 175 + (y + .5) * 34 / 32)
             for y in range(32) for x in range(24)]
    # Horizontal differences retain the flame silhouette with little influence from the radial light.
    pattern = tuple(a - b for y in range(32) for x in range(23)
                    for a, b in zip(flame[y * 24 + x], flame[y * 24 + x + 1]))
    return pixel(432.5, 244.5), pixel(744.5, 244.5), pattern


def main():
    captures, result = map(Path, sys.argv[1:])
    modes = ["scene-play", "release", "aot"]
    data = {mode: [samples(captures / mode / f"{i:02}.png") for i in range(33)] for mode in modes}
    references = [point[2] for point in data["scene-play"][:32]]
    with result.open("w") as file:
        writer = csv.writer(file, lineterminator="\n")
        writer.writerow(["tick", "expected_frame", *[mode + "_frame" for mode in modes],
                         *[mode + "_lit_rgb" for mode in modes], "unlit_rgb"])
        for i in range(33):
            observed = []
            colors = []
            for mode in modes:
                lit, unlit, pattern = data[mode][i]
                errors = [sum(abs(a - b) for a, b in zip(pattern, ref)) for ref in references]
                assert errors.count(min(errors)) == 1
                observed.append(errors.index(min(errors)))
                assert observed[-1] == i % 32, (i, mode, observed[-1])
                assert unlit == (112, 182, 164), (mode, i, unlit)
                colors.append(lit)
            assert len(set(colors)) == 1, (i, colors)
            assert data["release"][i] == data["aot"][i]
            writer.writerow([0 if i == 0 else (5 * i + 2) // 2, i % 32, *observed,
                             *[" ".join(map(str, c)) for c in colors], "112 182 164"])
    assert max(c[0][0] for c in data["scene-play"]) - min(c[0][0] for c in data["scene-play"]) >= 25
    print("PASS: 32 flame frames and loop boundary, exact lit and unlit RGB at all 33 samples.")


if __name__ == "__main__":
    main()
