"""Input-based release, AOT and Scene Play runs for the sample games."""
from dataclasses import dataclass
import hashlib
import os
from pathlib import Path
import re
import subprocess
import sys


@dataclass(frozen=True)
class Route:
    project: str
    start: str
    scene_object: str
    prefix: str
    line_pattern: str
    frames: int
    captures: tuple
    count: int


def keys(frame, control, down):
    return frame, f'{{kind: key, control: "{control}", down: {str(down).lower()}}}'


def click(frame, x, y):
    return [
        (frame, f'{{kind: pointer, x: {x}, y: {y}}}'),
        (frame, f'{{kind: pointer, control: "Mouse.Left", down: true, x: {x}, y: {y}}}'),
        (frame+1, f'{{kind: pointer, control: "Mouse.Left", down: false, x: {x}, y: {y}}}'),
    ]


def prepare(spec, events, output):
    output.mkdir(parents=True)
    start = [f'version: 1\nproject: {spec.project}\nstartScene: {spec.start}\n',
             'frameTicks: [' + ', '.join(['166667'] * spec.frames) + ']\nevents:\n']
    for frame, action in sorted(events, key=lambda event: event[0]):
        start.append(f'- frame: {frame}\n  timeTicks: {frame * 166667}\n  input: {action}\n')
    (output / 'Trail.inputreplay').write_text(''.join(start), encoding='utf-8')
    (output / 'frames.txt').write_text(','.join(map(str, spec.captures)), encoding='utf-8')


def check(spec, out, captures):
    text = (out / 'route.txt').read_text(encoding='utf-8')
    actual = tuple(int(match.group(1)) for match in re.finditer(spec.line_pattern, text))
    expected = tuple(range(1, spec.count + 1))
    if actual != expected:
        raise RuntimeError(f'{out}: expected {expected}, got {actual}; see the run log')
    if captures:
        for frame in spec.captures:
            image = out / 'captures' / f'{frame:04}.png'
            if not image.is_file() or image.stat().st_size < 1000:
                raise RuntimeError(f'{out}: missing capture for frame {frame}')
    print(text.strip())


def main(spec, events):
    mode, *args = sys.argv[1:]
    if mode == 'compare':
        paths = [Path(p) for p in args]
        names = ['route.txt']
        if all((out / 'captures').is_dir() for out in paths):
            names.extend(f'captures/{frame:04}.png' for frame in spec.captures)
        for name in names:
            first = (paths[0] / name).read_bytes()
            for out in paths[1:]:
                if (out / name).read_bytes() != first:
                    raise RuntimeError(f'{name}: {paths[0]} differs from {out}')
            print(f'{name}\t{hashlib.sha256(first).hexdigest()}')
        return

    out = Path(args[-1]).resolve()
    prepare(spec, events, out)
    if mode == 'prepare':
        return
    if mode == 'play':
        player = Path(args[0]).resolve()
        with_captures = sys.platform == 'darwin'
        env = dict(os.environ, SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
        if with_captures:
            env['MONOON_REPLAY_SNAPSHOTS'] = f'{out / "captures"};{(out / "frames.txt").read_text()}'
        log = out / 'player.log'
        with log.open('wb') as stream:
            subprocess.run([player, '--replay-input', out / 'Trail.inputreplay', out / 'profile'],
                           env=env, stdout=stream, stderr=stream, check=True, timeout=180)
        lines = [match.group() for match in re.finditer(spec.line_pattern, log.read_text(encoding='utf-8', errors='replace'))]
        (out / 'route.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
        check(spec, out, with_captures)
    elif mode == 'editor':
        app, project, hook = (Path(p).resolve() for p in args[:3])
        env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_SAMPLE_ROUTE_EDITOR=str(out),
                   MONOON_SAMPLE_ROUTE_START=spec.scene_object, MONOON_SAMPLE_ROUTE_LOG=spec.prefix,
                   MONOON_USER_DATA=str(out / 'storage'))
        with (out / 'editor.log').open('wb') as log:
            subprocess.run([app, project], env=env, stdout=log, stderr=log, check=True,
                           timeout=240 * float(os.environ.get('MONOON_QA_SLOW', '1')))
        if (out / 'error.txt').exists():
            raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
        check(spec, out, True)
    else:
        raise ValueError(f'Unknown route mode {mode}')
