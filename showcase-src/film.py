#!/usr/bin/env python3
"""Films a showcase video from a shot file, without a window or screen recording.

    python3 showcase-src/film.py showcase-src/shots/KeyRoom.json [--out ~/Movies/MoNoon-showcase] [--keep]

A shot file names the sample and one or more parts. Each part copies the sample, makes the part's scene the start scene,
builds the Release player, writes an .inputreplay from the part's inputs and replays it headless while the player saves
every frame as PNG (MONOON_REPLAY_SNAPSHOTS, SDL software renderer). The frames of all parts are joined into one
1600×900 60 fps mp4 by encode.swift, and frames.swift writes a contact sheet next to it for checking.

    {"sample": "KeyRoom", "parts": [{
        "scene": "Scenes/Showcase.scene",      # optional, default: the sample's start scene
        "seconds": 24,                         # replay length
        "keep": [1.0, 23.5],                   # optional: the seconds that go into the video
        "hold": [[0.5, 2.0, "Right"]],         # key held from..to (seconds)
        "tap":  [[3.0, "Space"]],              # key pressed for 0.1 s
        "type": [[4.0, "모눈"]],               # committed text
        "click": [[5.0, 400, 225]],            # left click at game pixel x, y
        "move": [[5.5, 420, 230]]}]}           # pointer move
"""
import argparse, hashlib, json, os, re, shutil, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCRATCH = Path(os.environ.get('FILM_WORK', ROOT / 'engine/.artifacts/film'))
TICK = 166667  # 1/60 s in 100 ns ticks
TAP = 6


def tool(name):
    exe = SCRATCH / 'tools' / name
    src = ROOT / f'showcase-src/{name}.swift'
    if not exe.exists() or exe.stat().st_mtime < src.stat().st_mtime:
        exe.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(['swiftc', '-O', str(src), '-o', str(exe)], check=True, stderr=subprocess.DEVNULL)
    return exe


def field(text, key):
    return re.search(rf'^{key}: *(\S+)', text, re.M).group(1)


def prepare(sample, scene):
    """Copy of the sample that starts in `scene`; reused while the sample and scene are unchanged."""
    src = ROOT / 'samples' / sample
    digest = hashlib.sha256(scene.encode())
    for p in sorted(src.rglob('*')):
        if p.is_file() and not {'.monoon', 'bin', 'obj', 'Tests'} & set(p.relative_to(src).parts):
            digest.update(str(p.relative_to(src)).encode()); digest.update(p.read_bytes())
    work = SCRATCH / 'builds' / f'{sample}-{digest.hexdigest()[:12]}'
    app = work / 'out' / f'{sample}.app'
    if app.exists(): return work, app
    shutil.rmtree(work, ignore_errors=True)
    shutil.copytree(src, work / sample, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    settings = work / sample / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    if scene:
        sid = field((work / sample / (scene + '.meta')).read_text(encoding='utf-8'), 'id')
        text = re.sub(r'^startScene: *\S+', f'startScene: {sid}', text, flags=re.M)
        if f'- {sid}' not in text:
            text = text.replace('includedScenes:\n', f'includedScenes:\n- {sid}\n')
        settings.write_text(text, encoding='utf-8')
    subprocess.run([str(ROOT / 'monoon'), 'build', str(work / sample), 'release', '--profile', 'distribution', '--output', str(work / 'out')],
                   check=True, stdout=subprocess.DEVNULL)
    return work, app


def recording(part, settings):
    frames = round(part['seconds'] * 60)
    f = lambda s: min(frames - 1, round(s * 60))
    events = []
    for a, b, key in part.get('hold', []):
        events += [(f(a), f'{{kind: key, control: "{key}", down: true}}'), (f(b), f'{{kind: key, control: "{key}", down: false}}')]
    for a, key in part.get('tap', []):
        events += [(f(a), f'{{kind: key, control: "{key}", down: true}}'), (f(a) + TAP, f'{{kind: key, control: "{key}", down: false}}')]
    for a, text in part.get('type', []):
        events.append((f(a), '{kind: commit, text: ' + json.dumps(text, ensure_ascii=False) + '}'))
    for a, x, y in part.get('move', []):
        events.append((f(a), f'{{kind: pointer, x: {x}, y: {y}}}'))
    for a, x, y in part.get('click', []):
        events += [(f(a), f'{{kind: pointer, x: {x}, y: {y}}}'),
                   (f(a), f'{{kind: pointer, x: {x}, y: {y}, control: Mouse.Left, down: true}}'),
                   (f(a) + TAP, f'{{kind: pointer, x: {x}, y: {y}, control: Mouse.Left, down: false}}')]
    events.sort(key=lambda e: e[0])
    body = ''.join(f'- frame: {n}\n  timeTicks: {n * TICK}\n  input: {e}\n' for n, e in events)
    return (f'version: 1\nproject: {field(settings, "projectId")}\nstartScene: {field(settings, "startScene")}\n'
            f'frameTicks: [{", ".join([str(TICK)] * frames)}]\nevents:\n{body}'), frames


def film(shot_path, out_dir, keep_frames):
    shot = json.loads(Path(shot_path).read_text(encoding='utf-8'))
    name = Path(shot_path).stem
    run = SCRATCH / 'runs' / name
    shutil.rmtree(run, ignore_errors=True)
    joined = run / 'joined'; joined.mkdir(parents=True)
    count = 0
    for i, part in enumerate(shot['parts']):
        work, app = prepare(shot['sample'], part.get('scene', ''))
        settings = (app / 'Contents/Resources/Game/GameSettings')
        settings = settings if settings.exists() else next(app.rglob('Game/GameSettings'))
        text, frames = recording(part, settings.read_text(encoding='utf-8'))
        replay, profile, frame_dir = run / f'part{i}.inputreplay', run / f'profile{i}', run / f'part{i}'
        replay.write_text(text, encoding='utf-8'); frame_dir.mkdir()
        env = dict(os.environ, MONOON_USER_DATA=str(profile), MONOON_FEEDBACK_LOG=str(run / f'feedback{i}.log'),
                   MONOON_REPLAY_SNAPSHOTS=f'{frame_dir};{",".join(map(str, range(frames)))}',
                   SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
        with (run / f'part{i}.log').open('wb') as log:
            subprocess.run(['sh', str(ROOT / 'engine/scripts/gui-lock.sh'), str(app / 'Contents/MacOS/MoNoonPlayer'),
                            '--replay-input', str(replay), str(profile)], env=env, stdout=log, stderr=log, check=True, timeout=1800)
        a, b = part.get('keep', [0, part['seconds']])
        for n in range(round(a * 60), min(frames, round(b * 60))):
            os.replace(frame_dir / f'{n:04}.png', joined / f'{count:05}.png'); count += 1
        if not keep_frames: shutil.rmtree(frame_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    mp4 = out_dir / f'{name}.mp4'
    subprocess.run([str(tool('encode')), str(mp4), str(joined), '60'], check=True)
    seconds = count / 60
    marks = [round(seconds * k / 9, 2) for k in range(9)]
    subprocess.run([str(tool('frames')), str(mp4), str(run / 'sheet.png')] + [str(m) for m in marks], check=True, stdout=subprocess.DEVNULL)
    if not keep_frames: shutil.rmtree(joined)
    print(f'{mp4} {seconds:.1f}s  sheet {run / "sheet.png"} (at {", ".join(map(str, marks))} s)')


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('shots', nargs='+')
    p.add_argument('--out', type=Path, default=Path.home() / 'Movies/MoNoon-showcase')
    p.add_argument('--keep', action='store_true', help='keep the PNG frames under engine/.artifacts/film/runs')
    args = p.parse_args()
    for s in args.shots: film(s, args.out.expanduser(), args.keep)
