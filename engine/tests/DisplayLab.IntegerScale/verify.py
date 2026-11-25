"""Roadmap 16.8: DisplayLab with integer scale in a real Mac window at three sizes, a relaunch, and Release vs Native AOT."""
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import time
import zlib

LOGICAL = (800, 450)
BADGE = (750, 26, 24, 24)  # logical rectangle of the 12 x 12 PixelBadge.png on ShowcaseDisplay's settings panel
ART = ["............", ".##########.", ".#oooooooo#.", ".#o##oo##o#.", ".#o##oo##o#.", ".#oooooooo#.",
       ".#o######o#.", ".#oo####oo#.", ".#oooooooo#.", ".##########.", "....####....", "..########.."]


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    # The showcase sample already turns integer scale on; the copy keeps the harness's builds out of the repository.
    assert 'integerScale: true' in (destination / 'GameSettings').read_text(encoding='utf-8')


def read_png(path):
    data = Path(path).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', path
    pos, idat, header = 8, b'', None
    while pos < len(data):
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b'IHDR':
            header = struct.unpack('>IIBBBBB', body)
        elif kind == b'IDAT':
            idat += body
        pos += 12 + length
    w, h, depth, color, _, _, interlace = header
    assert depth == 8 and color in (2, 6) and interlace == 0, header
    channels = 4 if color == 6 else 3
    raw, stride = zlib.decompress(idat), w * channels
    rows, previous = [], bytearray(stride)
    for y in range(h):
        kind, line = raw[y * (stride + 1)], bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - channels] if i >= channels else 0
            b, c = previous[i], previous[i - channels] if i >= channels else 0
            if kind == 1: line[i] = (line[i] + a) & 255
            elif kind == 2: line[i] = (line[i] + b) & 255
            elif kind == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif kind == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(bytes(line))
        previous = line
    return w, h, channels, rows


def client_area(path, client):
    w, h, ch, rows = read_png(path)
    chrome = h - round(client[1] * w / client[0])  # screencapture includes the title bar
    return w, h - chrome, ch, rows[chrome:]


def longest_run(values):
    best, start = (0, 0), None
    for i, on in enumerate(values + [False]):
        if on and start is None: start = i
        elif not on and start is not None:
            best, start = max(best, (i - start, start)), None
    return best[1], best[0]


def analyse(path, client):
    w, h, ch, rows = client_area(path, client)
    px = lambda x, y: tuple(rows[y][x * ch:x * ch + 3])
    # Bars are black; the game clears its logical area to the blue-grey background. macOS draws a one-pixel title bar
    # separator over the client's first row, so take the longest coloured run.
    left, width = longest_run([px(x, h // 2) != (0, 0, 0) for x in range(w)])
    top, height = longest_run([px(w // 2, y) != (0, 0, 0) for y in range(h)])
    k = width // LOGICAL[0]
    if top == 1 and height == k * LOGICAL[1] - 1: top, height = 0, height + 1
    expect(k >= 1 and (width, height) == (k * LOGICAL[0], k * LOGICAL[1]) and abs(left - (w - width) / 2) <= 1 and
           abs(top - (h - height) / 2) <= 1, f'{path.name}: {w}x{h} client shows the game at a whole {k}x ({width}x{height} at {left},{top})')
    # The capture is colour-managed, so compare texels with each other rather than with the PNG's exact values.
    cell, seen, wrong = BADGE[2] // len(ART) * k, {}, []
    for j, row in enumerate(ART):
        for i, texel in enumerate(row):
            x0, y0 = left + BADGE[0] * k + i * cell, top + BADGE[1] * k + j * cell
            block = {px(x, y) for y in range(y0, y0 + cell) for x in range(x0, x0 + cell)}
            if len(block) != 1 or seen.setdefault(texel, next(iter(block))) not in block: wrong.append((i, j, sorted(block)[:3]))
    background = px(left + (BADGE[0] - 4) * k, top + (BADGE[1] + 12) * k)  # the panel just left of the badge
    expect(not wrong and len(set(seen.values())) == 3 and seen['.'] == background,
           f'{path.name}: every badge texel is one uniform {cell}x{cell} block in one of three colours (no filtering) {wrong[:3]}')
    return dict(client=list(client), density=w / client[0], viewport=[left, top, width, height], scale=k)


def play(player, out, helper):
    out = out.resolve()
    out.mkdir(parents=True)
    profile = out / 'profile'
    profile.mkdir()
    report = {}
    # The fourth launch reopens the previous size from the same profile and must draw the same pixels.
    for name, size in [('default', (800, 450)), ('small', (720, 540)), ('large', (1300, 760)), ('reopened', None)]:
        if size:
            (profile / 'display.settings').write_text(
                f'version: 1\nvalues:\n  width: {size[0]}\n  height: {size[1]}\n  fullscreen: false\n  vsync: true\n', encoding='utf-8')
        client = size or report['large']['client']
        with (out / f'{name}.log').open('wb') as log:
            proc = subprocess.Popen([str(player.resolve()), '--record-input', str(out / f'{name}.inputreplay'), str(profile)],
                                    cwd=player.parent, env=dict(os.environ, MONOON_USER_DATA=str(profile)), stdout=log, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic() + 60
            while True:
                try:
                    window = subprocess.check_output([str(helper), 'geometry', str(proc.pid)], text=True, stderr=subprocess.DEVNULL).split()
                    break
                except subprocess.CalledProcessError:
                    if time.monotonic() > deadline or proc.poll() is not None: raise
                    time.sleep(.2)
            time.sleep(2)  # first frames and texture uploads
            subprocess.run(['screencapture', '-x', '-o', '-l', window[0], str(out / f'{name}.png')], check=True)
        finally:
            proc.terminate()
            proc.wait(timeout=20)
        report[name] = analyse(out / f'{name}.png', tuple(client))
    scales = [report[n]['scale'] for n in ['default', 'small', 'large']]
    expect(len(set(scales)) == 3, f'three window sizes use three whole scales {scales}')
    expect(report['reopened']['viewport'] == report['large']['viewport'] and
           badge_pixels(out / 'reopened.png', report['reopened']) == badge_pixels(out / 'large.png', report['large']),
           'the relaunched game reopens at the saved size with the same viewport and badge pixels')
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')


def badge_pixels(path, shot):
    # The showcase room animates (walking hero, flames, frame meter), so whole frames differ between runs; the badge on the
    # settings panel is still.
    w, h, ch, rows = client_area(path, tuple(shot['client']))
    (left, top, _, _), k = shot['viewport'], shot['scale']
    x0, y0 = left + BADGE[0] * k, top + BADGE[1] * k
    return [rows[y][x0 * ch:(x0 + BADGE[2] * k) * ch] for y in range(y0, y0 + BADGE[3] * k)]


def compare(release, aot):
    report = json.loads((Path(release) / 'report.json').read_text())
    for shot in sorted(Path(release).glob('*.png')):
        expect(badge_pixels(shot, report[shot.stem]) == badge_pixels(Path(aot) / shot.name, report[shot.stem]),
               f'Release and Native AOT {shot.name} badge pixels are identical')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'compare': compare}[command](*(Path(p) for p in paths))
