"""CameraLab's showcase split screen (merging) and per-camera screen effects in a disposable copy.

prepare <sample> <dest>   copy CameraLab and add TraceSplit to the showcase (Scenes/Showcase.scene, the start scene) as it is:
                          Player Camera splits with Partner Camera (merge 260, split 340, blend 0.6 s). TraceSplit starts
                          per-camera effects on fixed ticks and traces both views
play <MoNoonPlayer> <out> headless replay with SDL software-renderer snapshots; the trace has one line per game tick
check <out>...            the traces are byte-identical, the merge/split behaviour holds, and the snapshots of every out
                          that has them keep each camera's effects inside its own viewport
"""
import os, re, shutil, struct, subprocess, sys, zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = 'cf624488-489b-5951-a279-fffa22d99f7a'
SHOWCASE = '2c9a883e-aeee-5d4a-a6a3-66257b497a77'
FRAMES = 480
# The player walks right towards the partner (patrolling east of the plaza) until the views merge, then back west until
# they split again. FieldWalker moves 150 units a second.
KEYS = [(30, 230, 'Right'), (240, 440, 'Left')]
# Frame 22: main view black (its own Adjust), partner view red (its own Flash). Frame 300: merged (ticks 257–356), effects over.
SNAPSHOTS = (22, 300)
LEFT, RIGHT, MERGED = (0, 0, 400, 450), (400, 0, 400, 450), (0, 0, 800, 450)
# HUD drawn over the views (game pixels): feature tags, caption, seam; partner label, minimap and its label, effects panel.
HUD_LEFT = [(12, 12, 360, 52), (12, 394, 400, 44), (396, 0, 8, 450)]
HUD_RIGHT = [(396, 0, 8, 450), (412, 12, 120, 22), (604, 12, 184, 130), (488, 334, 300, 104)]
CAPTION = (24, 400, 300, 30)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceSplit.source', destination / 'Scripts/TraceSplit.cs')
    scene = destination / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    ids = {m.group(2): m.group(1) for m in re.finditer(r'(?m)^- id: (\S+)\n(?:  parentId: \S+\n)?  name: (.*)$', text)}
    assert 'splitWith: ' + ids['Partner Camera'] in text
    trace = ('- id: 6b2f0d7e-5a41-4c8b-a3e9-2d7c1f04b8a6\n  name: Split trace\n  width: 1\n  height: 1\n  components:\n'
             f'  - type: script\n    script: TraceSplit\n    values:\n      Main: "{ids["Player Camera"]}"\n      Second: "{ids["Partner Camera"]}"\n')
    assert text.count('defaultCameraId:') == 1
    scene.write_text(text.replace('defaultCameraId:', trace + 'defaultCameraId:'), encoding='utf-8')


def recording(path):
    lines = []
    for down, up, key in KEYS:
        for frame, state in ((down, 'true'), (up, 'false')):
            lines.append((frame, f'- frame: {frame}\n  timeTicks: {frame * 166667}\n  input: {{kind: key, control: "{key}", down: {state}}}\n'))
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SHOWCASE}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents:\n' + ''.join(l for _, l in sorted(lines, key=lambda x: x[0])), encoding='utf-8')


def play(player, out):
    out = out.resolve(); out.mkdir(parents=True)
    recording(out / 'split.inputreplay')
    env = dict(os.environ, MONOON_SPLIT_TRACE=str(out / 'split.trace'), MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, SNAPSHOTS))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'play.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'split.inputreplay'), str(out / 'profile')],
                       env=env, stdout=log, stderr=log, timeout=300, check=True)
    check(out)


def pixels(png):
    """RGB rows of an 8-bit RGB/RGBA PNG (what SdlFrameCapture writes)."""
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


def area(rows, rect, skip=()):
    x0, y0, w, h = rect
    def inside(x, y, r): return r[0] <= x < r[0] + r[2] and r[1] <= y < r[1] + r[3]
    return [rows[y][x] for y in range(y0, y0 + h) for x in range(x0, x0 + w) if not any(inside(x, y, r) for r in skip)]


def red(p): return p[0] > p[1] + 80 and p[0] > p[2] + 80


def rows(trace):
    for line in trace.read_text(encoding='utf-8').splitlines():
        tick, a, b, ea, eb, es = line.split('|')
        nums = lambda s: [float(v) for v in s.split(',')]
        a, b = nums(a), nums(b)
        yield int(tick), tuple(int(v) for v in a[:4]), a[4:], tuple(int(v) for v in b[:4]), b[4:], nums(ea), nums(eb), nums(es)


def expect(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message, flush=True)
    if not ok: raise SystemExit(1)


def check(*outs):
    # Scene Play keeps running for a moment after the recording ends; compare the ticks every run has.
    traces = [(o / 'split.trace').read_text(encoding='utf-8').splitlines(keepends=True) for o in outs]
    common = min(map(len, traces))
    for o, trace in zip(outs[1:], traces[1:]):
        expect(trace[:common] == traces[0][:common], f'{o.name}: first {common} ticks of the split trace match {outs[0].name} byte for byte')
    t = {r[0]: r[1:] for r in rows(outs[0] / 'split.trace')}
    ticks = sorted(t)
    expect(len(t) >= FRAMES - 30, f'{len(t)} ticks traced')
    expect(t[ticks[0]][0] == LEFT and t[ticks[0]][2] == RIGHT, f'players far apart start split: Player left {t[ticks[0]][0]}, Partner right {t[ticks[0]][2]}')
    widths = [t[k][0][2] for k in ticks]
    merged = [k for k in ticks if t[k][0] == MERGED]
    expect(len(merged) >= 10, f'merged for {len(merged)} ticks ({merged[0] if merged else "-"}–{merged[-1] if merged else "-"}) while the players are close')
    expect(all(t[k][2:4] == t[k][0:2] for k in merged), 'a merged Partner Camera reports the shared view')
    steps = [abs(widths[i] - widths[i - 1]) for i in range(1, len(widths))]
    expect(max(steps) <= 18, f'viewport width changes at most {max(steps)} px per tick (0.6 s smoothstep: 400 x 1.5 / 36 + 1)')
    phases = []
    for w in widths:
        state = 'merged' if w == 800 else 'split' if w == 400 else 'blend'
        if not phases or phases[-1] != state: phases.append(state)
    expect(phases == ['split', 'blend', 'merged', 'blend', 'split'], f'one merge and one split, no flicker: {" > ".join(phases)}')
    last = t[ticks[-1]]
    expect(last[0] == LEFT and last[2] == RIGHT, f'walked back west, the views split again with the Player on the left: Player {last[0]}, Partner {last[2]}')
    flash = [k for k in ticks if t[k][5][3] > 0]
    expect(flash and flash[0] in (20, 21) and all(t[k][4][3] == 0 and t[k][6][3] == 0 for k in ticks),
           f'partner flash from tick {flash[0] if flash else "-"}; main and screen-wide flash stay 0')
    expect(all(t[k][6][0] == 0 and t[k][6][1] == 0 and t[k][6][2] == 1 for k in ticks), 'per-camera shake and zoom punch leave the screen-wide frame alone')
    expect(any(t[k][4][0] != 0 for k in ticks) and any(t[k][5][2] > 1 for k in ticks), 'main shake and partner zoom punch run on tick 90')
    for o in outs:
        shots = o / 'captures'
        if not shots.exists(): continue
        effect, clean = pixels(shots / f'{SNAPSHOTS[0]:04}.png'), pixels(shots / f'{SNAPSHOTS[1]:04}.png')
        left, right, hud = area(effect, LEFT, HUD_LEFT), area(effect, RIGHT, HUD_RIGHT), area(effect, CAPTION)
        reds = sum(map(red, right)) / len(right)
        # The HUD panels and the seam are drawn over the views; they are left out, the rest is compared as fractions.
        black = sum(p == (0, 0, 0) for p in left) / len(left)
        expect(black > .9, f'{o.name} frame {SNAPSHOTS[0]}: the main camera\'s own Adjust(-1) blacks out its viewport ({black:.1%} black)')
        expect(reds > .9 and not any(map(red, left)), f'{o.name} frame {SNAPSHOTS[0]}: the partner flash fills its viewport ({reds:.1%} red) and nothing else')
        expect(not any(map(red, hud)) and any(p != (0, 0, 0) for p in hud), f'{o.name} frame {SNAPSHOTS[0]}: the HUD is outside both effects')
        union = area(clean, MERGED, HUD_LEFT + HUD_RIGHT)
        # The art has a few red pixels of its own (hair, barrels): a leftover flash would tint the whole view.
        expect(sum(map(red, union)) < len(union) // 100 and sum(p == (0, 0, 0) for p in union) < len(union) // 10,
               f'{o.name} frame {SNAPSHOTS[1]}: merged view without leftover effects')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in paths))
