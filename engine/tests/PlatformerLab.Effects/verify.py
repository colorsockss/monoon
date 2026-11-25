"""Run PlatformerLab's course showcase (ShowcaseCourse, no input: CourseRunner jumps between the pulley decks) and capture
the torch shader, point lights and the runner's landing dust.

prepare: copy the sample and make the course its start scene. play: Release / Native AOT headless replay. editor: Scene Play in the packaged Mac editor with the probe.
compare: every capture must be byte-identical across the given runs (also across builds of two engine commits).
"""
from pathlib import Path
import hashlib
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, SHOWCASE, COURSE = '3b309c28-75ce-562e-92ac-f94ee48b18c7', '40845ca7-0281-5a45-878a-c1da3b04f822', 'e4366c3e-3cfa-5c9b-b11e-7e2f25ffb84f'
FRAMES = 120
EVENTS = []
# Before the jump, in the air, landing burst, dust fading, settled. The torch cell and light flicker change every capture.
CAPTURES = [8, 30, 50, 54, 58, 62, 70, 80, 110]


def recording(path):
    lines = [f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{k}", down: {d}}}\n' for f, k, d in EVENTS]
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {COURSE}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents:' + (''.join(['\n'] + lines) if lines else ' []\n'), encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Jump.inputreplay')
    env = dict(os.environ, MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'Jump.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    check(out)


def editor(app, project, hook, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Jump.inputreplay')
    (out / 'captures.txt').write_text(','.join(map(str, CAPTURES)), encoding='utf-8')
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook.resolve()), MONOON_QA_PROJECT=str(project.resolve()),
               MONOON_EFFECTS_EDITOR=str(out), MONOON_USER_DATA=str(out / 'profile'))
    with (out / 'editor.log').open('wb') as log:
        subprocess.run([str(app.resolve()), str(project.resolve())], env=env, stdout=log, stderr=log, timeout=900, check=True)
    if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
    check(out)


def check(out):
    shots = [(out / 'captures' / f'{f:04}.png').read_bytes() for f in CAPTURES]
    assert all(len(s) > 1000 for s in shots) and len(set(shots)) == len(shots), 'missing or repeated captures'


def compare(*outs):
    outs = [Path(o) for o in outs]
    for frame in CAPTURES:
        hashes = [hashlib.sha256((o / 'captures' / f'{frame:04}.png').read_bytes()).hexdigest() for o in outs]
        assert len(set(hashes)) == 1, f'captures differ at frame {frame}: {list(zip(map(str, outs), hashes))}'
        print(f'{frame:04}.png\tidentical\tsha256 {hashes[0][:16]}', flush=True)


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    if command == 'prepare':
        shutil.copytree(paths[0], paths[1], ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
        settings = Path(paths[1]) / 'GameSettings'
        settings.write_text(settings.read_text(encoding='utf-8').replace(f'startScene: {SHOWCASE}', f'startScene: {COURSE}'), encoding='utf-8')
    elif command == 'compare': compare(*paths)
    else: {'play': play, 'editor': editor}[command](*(Path(p) for p in paths))
