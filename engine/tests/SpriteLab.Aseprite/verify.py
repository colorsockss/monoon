"""The SpriteLab showcase firefly blinks with the clip imported from FireflyBlink.aseprite: check frame order and start ticks."""
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys

HERE = Path(__file__).resolve().parent
TICKS = 600  # 10 s at 60 Hz
FORBIDDEN = ('.aseprite', '.ase', 'Engine.Authoring', 'Engine.Editor', 'SkiaSharp', 'libSkiaSharp', 'Avalonia', 'MoNoon.dll')


def durations(aseprite):
    """Frame durations in ms, read straight from the file (independent of the engine's importer)."""
    data = Path(aseprite).read_bytes()
    count, pos, result = struct.unpack_from('<H', data, 6)[0], 128, []
    for _ in range(count):
        size, magic, _, ms = struct.unpack_from('<IHHH', data, pos)
        assert magic == 0xF1FA
        result.append(ms)
        pos += size
    return result


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.DS_Store', 'bin', 'obj'))
    (destination / 'Scripts').mkdir(exist_ok=True)
    shutil.copyfile(HERE / 'TraceFrames.source', destination / 'Scripts/TraceFrames.cs')
    scene = destination / 'Scenes/ShowcaseYard.scene'
    text = scene.read_text(encoding='utf-8')
    clip_id = (destination / 'Assets/Showcase/FireflyBlink.clip.meta').read_text(encoding='utf-8').split()[1]
    anchor = f'    clip: {clip_id}\n    playOnStart: true\n'
    assert text.count(anchor) == 1
    scene.write_text(text.replace(anchor, anchor + '  - type: script\n    script: TraceFrames\n'), encoding='utf-8')
    # Frame events only report which frame starts; order, timing and cells stay as generated.
    clip = destination / 'Assets/Showcase/FireflyBlink.clip'
    text = clip.read_text(encoding='utf-8')
    assert text.count('- sprite:') == 2
    parts = text.split('- sprite:')
    clip.write_text(parts[0] + ''.join(f'- event: F{i}\n  sprite:{p}' for i, p in enumerate(parts[1:])), encoding='utf-8')


def recording(path, settings):
    ids = dict(line.split(': ', 1) for line in settings.read_text(encoding='utf-8').splitlines() if line.startswith(('projectId', 'startScene')))
    path.write_text(f"version: 1\nproject: {ids['projectId']}\nstartScene: {ids['startScene']}\nframeTicks: [" +
                    ', '.join(['166667'] * TICKS) + ']\nevents: []\n', encoding='utf-8')


def expected(ms):
    """Start tick of every frame entered within TICKS, looping."""
    result, start, frame = [], 0, 0
    while start * 60 < TICKS * 1000:
        result.append((start * 60 / 1000, frame))
        start += ms[frame]
        frame = (frame + 1) % len(ms)
    return result


def check(out, aseprite):
    rows = [line.split() for line in (out / 'trace.txt').read_text(encoding='utf-8').splitlines()]
    seen = [(int(t), int(f)) for t, f, name in rows if name == f'F{f}']
    assert len(seen) == len(rows), rows
    want = expected(durations(aseprite))
    assert [f for _, f in seen] == [f for _, f in want], (seen, want)
    # One fixed tick is 16.7 ms; a frame may start on the tick at or right after its exact time.
    assert all(0 <= t - w <= 1 for (t, _), (w, _) in zip(seen, want)), (seen, want)
    result = {'durationsMs': durations(aseprite), 'frames': [{'tick': t, 'frame': f, 'exactTick': w} for (t, f), (w, _) in zip(seen, want)]}
    (out / 'result.json').write_text(json.dumps(result, indent=1) + '\n', encoding='utf-8')
    print(result, flush=True)


def forbidden(game_bundle):
    hits = [str(p) for p in Path(game_bundle).rglob('*') if p.is_file() and
            (p.suffix.lower() in ('.aseprite', '.ase') or any(name in p.name for name in FORBIDDEN[2:]))]
    hits += [str(p) for p in Path(game_bundle).rglob('*') if p.is_file() and p.stat().st_size < 400_000_000 and
             b'AsepriteImporter' in p.read_bytes()]
    assert not hits, hits
    print('no Aseprite source, importer, editor or SkiaSharp files in', game_bundle, flush=True)


def play(player, out, aseprite):
    out = out.resolve()
    out.mkdir(parents=True)
    game = player.resolve().parent / 'Game'
    recording(out / 'Frames.inputreplay', game / 'GameSettings')
    env = dict(os.environ, MONOON_FRAME_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
    subprocess.run([str(player.resolve()), '--replay-input', str(out / 'Frames.inputreplay'), str(out / 'slot')], env=env, check=True)
    check(out, aseprite)


if __name__ == '__main__':
    {'prepare': prepare, 'play': play, 'check': check, 'forbidden': forbidden}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
