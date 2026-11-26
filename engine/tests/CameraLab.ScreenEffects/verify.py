"""Drive CameraLab's screen effects, comfort settings, rumble and cursor in Scene Play, Release and Native AOT on this Mac.

Usage: verify.py <project-copy> <release .app> <native-aot .app> <probe.dll> <out> [editor|release|aot ...]
Environment: MONOON_QA_WINDOW (window tool), MONOON_QA_RUMBLE_LIBRARY (rumble-pad.dylib).
Input goes through the fixture's SDL virtual gamepad (the same pad that records rumble), so no OS key events are needed.
"""
import ctypes, hashlib, os, re, shutil, signal, struct, subprocess as sp, sys, time, zlib
from pathlib import Path

root = Path(__file__).resolve().parents[3]
project, release, aot, probe, out = [Path(p).resolve() for p in sys.argv[1:6]]
modes = sys.argv[6:] or ['editor', 'release', 'aot']
out.mkdir(parents=True, exist_ok=True)
tool = os.environ['MONOON_QA_WINDOW']
# CameraLab actions on the pad: SDL gamepad button numbers, or ('a', axis) for the right trigger.
PAD = {'1': 2, '2': 1, '3': 9, '4': 10, '5': 4, '6': 3, '7': 7, '8': 8, 'F': 6, '.': ('a', 5)}
pid, proc, pad = 0, None, None


def invoke(*args):
    for attempt in range(8):
        try: return sp.check_output([tool, args[0], str(pid), *map(str, args[1:])], stderr=sp.PIPE)
        except sp.CalledProcessError as exc:
            if exc.returncode != 7 or attempt == 7: raise
            time.sleep(.25)


def info(): return list(map(int, invoke('info').split()))


def key(k, times=1):
    control = PAD[k]
    # Virtual trigger axes rest at -32768. The pad is read only while the game window has focus.
    kind, index, on, off = ('a', control[1], 32767, -32768) if isinstance(control, tuple) else ('b', control, 1, 0)
    for _ in range(times):
        activate()
        for value in (on, off):
            with pad.open('a') as f: f.write(f'{kind} {index} {value}\n')
            time.sleep(.1)


def activate():
    invoke('key', 6)  # The tool brings the window to the front first; Z is unbound in CameraLab.


class Point(ctypes.Structure): _fields_ = [('x', ctypes.c_double), ('y', ctypes.c_double)]


def hover(x, y):
    """Moves the pointer without an input event (no Accessibility permission needed)."""
    ctypes.CDLL('/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics').CGWarpMouseCursorPosition(Point(x, y))


def snap(name, cursor=False):
    w, x, y, ww, hh = info()
    raw = out / 'screen.png'
    args = ['screencapture', '-x'] + (['-C', '-R', f'{x},{y},{ww},{hh}'] if cursor else ['-o', '-l', str(w)])
    sp.run(args + [str(raw)], check=True)
    sp.run(['sips', '-z', str(hh), str(ww), str(raw), '--out', str(out / f'{name}.png')], stdout=sp.DEVNULL, check=True)
    raw.unlink()


def close():
    global pid, proc
    if not pid: return
    try: os.kill(pid, signal.SIGTERM)
    except ProcessLookupError: pass
    proc.wait(timeout=15)
    pid, proc = 0, None
    time.sleep(.5)


def launch(mode, profile, run):
    global pid, proc, pad
    close()
    pad = out / f'{mode}-{run}.pad'
    pad.write_text('')
    ticket = out / f'{mode}-{run}.ticket'
    ticket.unlink(missing_ok=True)
    env = dict(os.environ, MONOON_USER_DATA=str(profile), MONOON_FEEDBACK_LOG=str(out / f'{mode}.feedback'),
               MONOON_QA_RUMBLE=str(out / f'{mode}.rumble'), DYLD_INSERT_LIBRARIES=os.environ['MONOON_QA_RUMBLE_LIBRARY'],
               MONOON_QA_TICKET=str(ticket), MONOON_QA_PAD=str(pad))
    if mode == 'editor':
        env['DOTNET_STARTUP_HOOKS'] = str(probe)
        args = [root / 'editor/MoNoon.app/Contents/MacOS/MoNoon', project]
    else:
        args = [(release if mode == 'release' else aot) / 'Contents/MacOS/MoNoonPlayer']
    proc = sp.Popen(list(map(str, args)), env=env, stdout=open(out / f'{mode}-{run}.log', 'w'), stderr=sp.STDOUT)
    pid = proc.pid
    for _ in range(150):
        try: info(); break
        except sp.CalledProcessError: time.sleep(.2)
    if mode == 'editor':
        for _ in range(300):
            if ticket.exists(): break
            time.sleep(.2)
        if ticket.read_text() != 'playing': raise RuntimeError(ticket.read_text())
    activate()
    time.sleep(2)
    print(mode, run, pid, info(), flush=True)


def frozen(mode, trigger, steps, name, finish):
    """Trigger an effect while game time is frozen, advance exact ticks, capture, then let it finish."""
    key(trigger); key('.', steps); time.sleep(.5); snap(f'{mode}-{name}'); key('.', finish)


def run(mode):
    profile = out / f'{mode}-profile'
    shutil.rmtree(profile, ignore_errors=True)
    for suffix in ('feedback', 'rumble'): (out / f'{mode}.{suffix}').unlink(missing_ok=True)
    launch(mode, profile, 'first')
    snap(f'{mode}-off')
    key('F'); time.sleep(.5); snap(f'{mode}-still')
    frozen(mode, '1', 6, 'shake', 30)
    frozen(mode, '2', 6, 'flash', 30)
    frozen(mode, '4', 24, 'grade', 6)
    frozen(mode, '3', 15, 'fade', 30)
    key('3'); key('.', 36); key('4'); key('.', 24)
    key('5')
    frozen(mode, '1', 6, 'shake-reduced', 30)
    frozen(mode, '2', 6, 'flash-reduced', 30)
    key('F'); time.sleep(.3)
    # Rumble on fixed requests; then the player turns rumble off.
    key('6'); time.sleep(.8); key('1'); time.sleep(1); key('8'); key('6'); time.sleep(.5); key('1'); time.sleep(1)
    # Custom cursor over the game view, then the system cursor again.
    w, x, y, ww, hh = info()
    activate(); hover(x + ww // 4, y + hh // 3)
    key('7'); time.sleep(.3); hover(x + ww // 4 + 3, y + hh // 3 + 2); time.sleep(.6); snap(f'{mode}-cursor', cursor=True)
    key('7'); time.sleep(.3); activate(); hover(x + ww // 4, y + hh // 3); time.sleep(.6); snap(f'{mode}-system-cursor', cursor=True)
    close()
    folder = profile / ('Play' if mode == 'editor' else 'Games') / project_id()
    shutil.copyfile(folder / 'player.settings', out / f'{mode}-first.player.settings')
    shutil.copyfile(folder / 'slot-003.save', out / f'{mode}.record')
    # Restart on the same profile: reduced comfort and rumble off are restored; turning rumble on works again.
    launch(mode, profile, 'restart')
    snap(f'{mode}-restart')
    key('6'); time.sleep(.6); key('8'); key('6'); time.sleep(.8)
    close()
    shutil.copyfile(folder / 'player.settings', out / f'{mode}-restart.player.settings')


def project_id():
    for line in (project / 'GameSettings').read_text().splitlines():
        if line.startswith('projectId:'): return line.split(':', 1)[1].strip().replace('-', '')


def png(path):
    """Minimal PNG reader: 8-bit RGB/RGBA, no interlace. Returns width, height, rows of RGBA tuples."""
    data = path.read_bytes()
    pos, idat = 8, b''
    while pos < len(data):
        n, kind = struct.unpack('>I4s', data[pos:pos + 8]); body = data[pos + 8:pos + 8 + n]; pos += 12 + n
        if kind == b'IHDR': w, h, depth, color = struct.unpack('>IIBB', body[:10])
        elif kind == b'IDAT': idat += body
    assert depth == 8 and color in (2, 6), (depth, color)
    bpp = 4 if color == 6 else 3
    raw, stride, rows, prev = zlib.decompress(idat), w * bpp, [], bytearray(w * bpp)
    for y in range(h):
        f, line = raw[y * (stride + 1)], bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0; b = prev[i]; c = prev[i - bpp] if i >= bpp else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append([tuple(line[i:i + bpp]) + ((255,) if bpp == 3 else ()) for i in range(0, stride, bpp)])
        prev = line
    return w, h, rows


def mean(path, mask):
    """Mean RGB over the pixels where the frozen frames differ: the game world, without editor or window chrome."""
    _, _, rows = png(path)
    px = [rows[y][x] for y, x in mask]
    return tuple(round(sum(p[i] for p in px) / len(px), 1) for i in range(3))


def world(*paths):
    images = [png(p)[2] for p in paths]
    return [(y, x) for y in range(0, len(images[0]), 2) for x in range(0, len(images[0][0]), 2)
            if len({img[y][x][:3] for img in images}) == len(images)]


def expect(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message, flush=True)
    if not ok: raise SystemExit(1)


def check(mode):
    names = ['still', 'grade', 'fade', 'flash', 'flash-reduced']
    view = world(*(out / f'{mode}-{n}.png' for n in names))
    expect(len(view) > 2000, f'{mode}: effects change {len(view)} sampled pixels of the game view')
    off, grade, fade, flash, reduced = (mean(out / f'{mode}-{n}.png', view) for n in names)
    expect(grade[2] < off[2] * .8 and grade[0] < off[0] and grade[0] / max(grade[2], 1) > off[0] / max(off[2], 1),
           f'{mode}: colour adjustment darkens and warms the world {off} -> {grade}')
    expect(sum(fade) < sum(off) * .75, f'{mode}: half-way fade darkens {off} -> {fade}')
    expect(sum(flash) > sum(reduced) > sum(off), f'{mode}: flash brighter than reduced flash brighter than none {flash} / {reduced} / {off}')
    _, _, a = png(out / f'{mode}-still.png'); _, _, b = png(out / f'{mode}-shake.png'); _, _, c = png(out / f'{mode}-shake-reduced.png')
    moved = lambda img: sum(1 for y, x in view if img[y][x] != a[y][x])  # world pixels only; the HUD status text differs
    expect(moved(b) > 0 and moved(c) > 0, f'{mode}: shake moves the frozen frame, full and reduced ({moved(b)}, {moved(c)} pixels)')
    record = (out / f'{mode}.record').read_text()
    # The shake direction is seeded per trigger, so changed-pixel counts on the textured showcase floor do not rank the two
    # shakes; the zoom punch is not random: reduced comfort (0.25) scales its offset exactly.
    zoom = lambda key: float(re.search(rf'{key}\.6: "[^"]*zoom=([\d.]+)', record).group(1)) - 1
    expect(abs(zoom('impactReduced') - .25 * zoom('impact')) < 1e-3 and zoom('impact') > .01,
           f'{mode}: reduced comfort scales the drawn zoom punch to a quarter ({zoom("impact"):.4f} -> {zoom("impactReduced"):.4f})')
    expect('impact.6' in record and 'impactReduced.6' in record and 'grade.24' in record, f'{mode}: records drawn values on fixed ticks')
    first, restart = (out / f'{mode}-first.player.settings').read_text(), (out / f'{mode}-restart.player.settings').read_text()
    expect('accessibility.shakeScale: 0.25' in first and 'accessibility.flashScale: 0.25' in first and 'accessibility.rumble: false' in first,
           f'{mode}: player.settings stores reduced shake/flash and rumble off')
    expect('accessibility.rumble: true' in restart and 'accessibility.shakeScale: 0.25' in restart, f'{mode}: restart keeps reduced comfort, then rumble on')
    # One "attached" line per launch; times restart with each process.
    runs = [[tuple(map(int, l.split())) for l in part.strip().splitlines()] for part in (out / f'{mode}.rumble').read_text().split('attached')[1:]]
    starts = [(t, lo, hi) for calls in runs for t, lo, hi in calls if lo or hi]
    durations = [min(t2 for t2, a, b in calls if t2 > t and not a and not b) - t for calls in runs for t, lo, hi in calls if lo or hi]
    impact, buzz = (65535, 32768), (round(.3 * 65535), round(.9 * 65535))
    expected = [impact, impact, buzz, impact, buzz]  # two frozen impacts, buzz, impact, (rumble off skips two), buzz after restart
    expect(len(starts) == 5 and all(abs(s[1] - e[0]) <= 1 and abs(s[2] - e[1]) <= 1 for s, e in zip(starts, expected)),
           f'{mode}: SDL rumble calls reach the virtual pad {starts}')
    expect(all(abs(d - e) <= 60 for d, e in zip(durations, [400, 400, 200, 400, 200])), f'{mode}: SDL stops each rumble after its duration {durations} ms')
    feedback = (out / f'{mode}.feedback').read_text().splitlines()
    cursor = [l for l in feedback if l.startswith('cursor')]
    w, h, rows = png(project / 'Assets/Cursor.png')
    canonical = bytes(v for row in rows for p in row for v in (p if p[3] else (0, 0, 0, 0)))
    expect(cursor[0] == f'cursor file=Cursor.png size={w}x{h} hot=8,8 rgba-sha256={hashlib.sha256(canonical).hexdigest()}' and cursor[1] == 'cursor system',
           f'{mode}: cursor built from the project PNG pixels with hotspot 8,8, then the system cursor {cursor}')
    return [l for l in feedback if l.startswith('rumble')], record


try:
    for mode in modes: run(mode)
finally:
    close()
results = {mode: check(mode) for mode in modes}
first = results[modes[0]]
for mode in modes[1:]:
    expect(results[mode][1] == first[1], f'{mode}: effect record matches {modes[0]} byte for byte')
    expect([l.split(' device=')[1] for l in results[mode][0]] == [l.split(' device=')[1] for l in first[0]],
           f'{mode}: rumble record (device, low, high, ms) matches {modes[0]}')
