"""CameraLab script debug drawing (roadmap 16.14) in exported players: G turns the gizmos on, a headless replay saves a frame.

Usage: verify.py play <MoNoonPlayer> <out>                     one build (any profile): replays without and with G
       verify.py check <development-out> <distribution-out>     development draws the gizmos, distribution draws nothing
"""
import os, struct, subprocess, sys, zlib
from pathlib import Path

PROJECT = 'cf624488-489b-5951-a279-fffa22d99f7a'
FRAMES, SNAP = 40, 30
GOLD, CYAN = (255, 215, 0), (0, 255, 255)


def recording(path, gizmos):
    events = [(5, True), (6, False)] if gizmos else []
    lines = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "G", down: {str(d).lower()}}}\n'
                    for f, d in events)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {PROJECT}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents:' + ('\n' + lines if lines else ' []\n'), encoding='utf-8')


def pixels(png):
    """RGB tuples of an 8-bit RGB/RGBA PNG (what SdlFrameCapture writes), row by row."""
    data, pos, idat = png.read_bytes(), 8, b''
    while pos < len(data):
        n, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        if kind == b'IHDR':
            width, height, depth, color = struct.unpack('>IIBB', body[:10])
            assert depth == 8 and color in (2, 6), (depth, color)
            bpp = 4 if color == 6 else 3
        elif kind == b'IDAT':
            idat += body
        pos += 12 + n
    raw, stride, prev, out = zlib.decompress(idat), width * bpp, bytearray(width * bpp), []
    for y in range(height):
        start = y * (stride + 1)
        f, line = raw[start], bytearray(raw[start + 1:start + 1 + stride])
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        out.append([tuple(line[x * bpp:x * bpp + 3]) for x in range(width)])
        prev = line
    return out


def count(png, color):
    return sum(1 for row in pixels(png) for p in row if all(abs(p[i] - color[i]) <= 2 for i in range(3)))


def play(player, out):
    out = out.resolve(); out.mkdir(parents=True)
    for run in ('plain', 'gizmos'):
        recording(out / f'{run}.inputreplay', run == 'gizmos')
        profile = out / f'{run}-profile'
        env = dict(os.environ, MONOON_USER_DATA=str(profile), MONOON_REPLAY_SNAPSHOTS=f'{out / run};{SNAP}',
                   SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
        with (out / f'{run}.log').open('wb') as log:
            subprocess.run([str(player.resolve()), '--replay-input', str(out / f'{run}.inputreplay'), str(profile)],
                           env=env, stdout=log, stderr=log, timeout=300, check=True)
        png = out / run / f'{SNAP:04}.png'
        print(f'{out.name} {run}: gold={count(png, GOLD)} cyan={count(png, CYAN)} bytes={png.stat().st_size}', flush=True)


def expect(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message, flush=True)
    if not ok: raise SystemExit(1)


def check(development, distribution):
    shot = lambda o, run: o / run / f'{SNAP:04}.png'
    gold, cyan = count(shot(development, 'gizmos'), GOLD), count(shot(development, 'gizmos'), CYAN)
    expect(gold > 100 and cyan > 50, f'development: G draws the gold circle and the cyan line ({gold}, {cyan} pixels)')
    expect(count(shot(development, 'plain'), GOLD) == 0, 'development: no gizmos without G')
    expect(shot(distribution, 'gizmos').read_bytes() == shot(distribution, 'plain').read_bytes(),
           'distribution: the frame after G is byte-identical to the frame without G')
    expect(shot(distribution, 'plain').read_bytes() == shot(development, 'plain').read_bytes(),
           'development and distribution draw the same frame without G')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'play': play, 'check': check}[command](*(Path(p) for p in paths))
