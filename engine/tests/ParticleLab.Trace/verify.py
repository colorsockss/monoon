"""Replay a ParticleLab scene and compare per-tick particle traces (and captures) of Scene Play, Release and Native AOT.

prepare <sample> <dest> <scene>: copy the sample, start it at <scene> and attach ParticleTrace to each scene's camera.
play <player> <out> <scene>: headless Release / Native AOT replay. editor <app> <project> <probe.dll> <out> <scene>: Scene Play.
compare <out>...: traces and captures must be byte-identical.
all <sample> <out> [--editor <app> <probe.dll>] [--scenes A,B]: every scene (or the listed ones) through prepare, Release and
Native AOT builds and replays, Scene Play (each editor run inside the GUI lock, MONOON_GUI_LOCK overrides the wrapper),
compare, the forbidden-file check, and on macOS arm64 the committed trace in results/particles/osx-arm64.
"""
from pathlib import Path
import hashlib
import json
import os
import platform
import re
import shlex
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
BASELINE = REPO / 'engine/tests/results/particles/osx-arm64'
# Editor and tool files a game build must not contain (AGENTS.md runtime boundary).
FORBIDDEN = ('Avalonia', 'SkiaSharp', 'Engine.Editor', 'Engine.Authoring', 'MoNoon.dll', 'libSkiaSharp')
PROJECT = '78fa20ac-f557-51e2-b1d4-4fa8d6384536'
# id, an object to wait for in the editor, frames, key events, captured frames. The camp and the workshop are the sample's two
# showcase scenes; Stress is a test-only scene (not in the sample's builds), so prepare adds it to the copy's included scenes.
SCENES = {
    'Showcase': ('b8f2ab84-5869-5a85-a9cc-f8245e12d7c1', 'Rockets', 600,
                 [(60, 'Space', 'true'), (64, 'Space', 'false'), (240, 'Space', 'true'), (244, 'Space', 'false'),
                  (420, 'Space', 'true'), (424, 'Space', 'false')], [100, 500]),
    'Workshop': ('20bac3a9-6b46-52fc-8c71-ee549360abb8', 'Point sparks', 600,
                 # No input in the first 300 ticks: ParticleChecks compares them with the editor preview.
                 [(320, 'Space', 'true'), (324, 'Space', 'false'), (480, '1', 'true'), (484, '1', 'false')], [90, 300, 590]),
    'Stress': ('f94d7d8d-773d-5014-b991-2f1ef4d32f9d', 'Storm', 300, [], [200, 290]),
    # The workshop again, tracing only its still Curves emitters (Bubbles, Flame, Pinwheels): the editor preview of the same three
    # must match this trace tick by tick (ParticleChecks and ParticleLab.Editor).
    'Curves': ('20bac3a9-6b46-52fc-8c71-ee549360abb8', 'Pinwheels', 300, [], [150, 290]),
}
GROUP = {'Curves': 'Curves'}
# Emitters (objects tagged Particles) in scene order: the camp's rain cloud, its splash, the rockets and their burst.
CAMP_RAIN, CAMP_SPLASH, CAMP_ROCKETS, CAMP_BURST = 3, 4, 8, 9


def recording(path, scene):
    sid, _, frames, events, _ = SCENES[scene]
    lines = [f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{k}", down: {d}}}\n' for f, k, d in events]
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {sid}\nframeTicks: [' + ', '.join(['166667'] * frames) +
                    ']\nevents:' + ('\n' + ''.join(lines) if lines else ' []\n'), encoding='utf-8')


def prepare(sample, dest, scene):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'ParticleTrace.source', dest / 'Scripts/ParticleTrace.cs')
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    first, sid = SCENES['Showcase'][0], SCENES[scene][0]
    assert f'startScene: {first}' in text and 'includedScenes: [' in text
    text = text.replace(f'startScene: {first}', f'startScene: {sid}', 1)
    if sid not in text.split('includedScenes: [', 1)[1].split(']', 1)[0]: text = text.replace('includedScenes: [', f'includedScenes: [{sid}, ', 1)
    settings.write_text(text, encoding='utf-8')
    for file in (dest / 'Scenes').glob('*.scene'):
        text = file.read_text(encoding='utf-8')
        assert text.count('  - type: camera2D\n') == 1, file
        # After the camera component and its fields (editor-saved scenes list them; hand-written ones do not).
        file.write_text(re.sub(r'(  - type: camera2D\n(?:    .*\n)*)', r'\1  - type: script\n    script: "ParticleTrace"\n', text, count=1), encoding='utf-8')


def captures(scene): return SCENES[scene][4]


def play(exe, out, scene):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / f'{scene}.inputreplay', scene)
    env = dict(os.environ, MONOON_PARTICLE_TRACE_GROUP=GROUP.get(scene, ''), MONOON_PARTICLE_TRACE=str(out / 'trace.tsv'), MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, captures(scene)))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / f'{scene}.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=600)
    check(out, scene)


def editor(app, project, hook, out, scene):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / f'{scene}.inputreplay', scene)
    (out / 'captures.txt').write_text(','.join(map(str, captures(scene))), encoding='utf-8')
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook.resolve()), MONOON_QA_PROJECT=str(project.resolve()),
               MONOON_PARTICLES_EDITOR=str(out), MONOON_PARTICLES_WAIT=SCENES[scene][1],
               MONOON_PARTICLE_TRACE_GROUP=GROUP.get(scene, ''), MONOON_PARTICLE_TRACE=str(out / 'trace.tsv'), MONOON_USER_DATA=str(out / 'profile'))
    with (out / 'editor.log').open('wb') as log:
        subprocess.run([str(app.resolve()), str(project.resolve())], env=env, stdout=log, stderr=log, timeout=900, check=True)
    if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
    check(out, scene)


def check(out, scene):
    rows = [line.split(' ') for line in (out / 'trace.tsv').read_text(encoding='utf-8').splitlines()]
    frames = SCENES[scene][2]
    assert [int(r[0]) for r in rows] == list(range(len(rows))) and len(rows) >= frames - 2, len(rows)
    emitters = {int(r[1]) for r in rows}
    counts = [int(r[2]) for r in rows]
    result = {'scene': scene, 'rows': len(rows), 'emitters': sorted(emitters), 'peak': max(counts), 'last': counts[-1],
              'distinctHashes': len({r[3] for r in rows})}
    assert len(emitters) == 1 and result['distinctHashes'] > len(rows) * .9, result
    if scene == 'Stress': assert max(counts) == 10240 and counts[-1] >= 10200, result
    # Per-emitter born/died/collided/dropped per tick, in tag (scene) order.
    events = [[list(map(int, e.split('/'))) for e in r[4].split(',')] for r in rows]
    def total(emitter, field): return sum(row[emitter][field] for row in events)
    if scene == 'Workshop':  # Space pops the manual emitter (60) and bursts Dust and Hit (32); 1 bursts them again
        assert counts[322] - counts[319] > 40 and counts[482] - counts[479] >= 25, result
    if scene == 'Showcase':
        # Rain on the ground tiles: every drop that hits throws 2 splashes (or drops them when Splash is full).
        hits, splashes, dropped = total(CAMP_RAIN, 2), total(CAMP_SPLASH, 0), events[-1][CAMP_SPLASH][3]
        # Three presses of Space fire three rockets each; each rocket death bursts into exactly 48 sparks.
        deaths, sparks = total(CAMP_ROCKETS, 1), total(CAMP_BURST, 0)
        result.update(rainHits=hits, splashes=splashes, splashDropped=dropped, rockets=total(CAMP_ROCKETS, 0), rocketDeaths=deaths, sparks=sparks)
        assert hits > 100 and splashes + dropped == 2 * hits, result
        assert deaths == 9 and sparks == 48 * deaths and events[-1][CAMP_BURST][3] == 0, result
    for frame in captures(scene):
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame
    (out / 'result.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result), flush=True)


def compare(*outs):
    outs = [Path(o) for o in outs]
    traces = [(o / 'trace.tsv').read_bytes() for o in outs]
    for o, t in zip(outs[1:], traces[1:]):
        assert t == traces[0], f'{o} trace differs from {outs[0]}'
    rows = traces[0].count(b"\n")
    lines = [f"trace\t{rows} rows\tsha256 {hashlib.sha256(traces[0]).hexdigest()[:16]}"]
    for shot in sorted(p.name for p in (outs[0] / 'captures').glob('*.png')):
        hashes = [hashlib.sha256((o / 'captures' / shot).read_bytes()).hexdigest() for o in outs]
        assert len(set(hashes)) == 1, f'captures differ at {shot}: {hashes}'
        lines.append(f'{shot}\tidentical\tsha256 {hashes[0][:16]}')
    print('\n'.join(lines), flush=True)


def same_as(trace, earlier):
    """The first four columns (tick, emitters, live count, hash) equal an earlier trace without the events column."""
    new = [' '.join(line.split(' ')[:4]) for line in Path(trace).read_text(encoding='utf-8').splitlines()]
    old = Path(earlier).read_text(encoding='utf-8').splitlines()
    assert new == old, f'{trace} differs from {earlier} at row {next(i for i, (a, b) in enumerate(zip(new + [None], old + [None])) if a != b)}'
    print(f'{trace}: {len(new)} rows identical to {earlier}', flush=True)


def forbidden(app):
    found = [str(f.relative_to(app)) for f in app.rglob('*') if any(name in f.name for name in FORBIDDEN)]
    assert not found, f'{app}: editor or tool files in a game build: {found[:5]}'


def same_as_baseline(trace, scene):
    """Equal to the committed macOS arm64 trace; traces recorded before the events column compare their first four columns."""
    committed = BASELINE / f'{scene.lower()}-trace.tsv'
    if not committed.exists(): return 'no committed trace'
    old = committed.read_text(encoding='utf-8').splitlines()
    new = trace.read_text(encoding='utf-8').splitlines()
    if old and len(old[0].split(' ')) == 4: new = [' '.join(line.split(' ')[:4]) for line in new]
    assert new == old, f'{scene}: trace differs from {committed} at row {next(i for i, (a, b) in enumerate(zip(new + [None], old + [None])) if a != b)}'
    return f'= {committed.name}'


def run_all(sample, out, editor_app=None, probe=None, scenes=None):
    out = out.resolve()
    lock = shlex.split(os.environ.get('MONOON_GUI_LOCK') or f'sh {REPO / "engine/scripts/gui-lock.sh"}')
    arm_mac = platform.system() == 'Darwin' and platform.machine() == 'arm64'
    summary = []
    for scene in scenes or SCENES:
        work = out / scene
        prepare(sample.resolve(), work / 'ParticleLab', scene)
        for kind in ('release', 'native-aot'):
            subprocess.run([str(REPO / 'monoon'), 'build', str(work / 'ParticleLab'), kind, '--profile', 'distribution',
                            '--output', str(work / 'builds')], check=True, stdout=subprocess.DEVNULL, timeout=1800)
        release, aot = work / 'builds/ParticleLab.app', work / 'builds/NativeAOT/ParticleLab.app'
        forbidden(release)
        forbidden(aot)
        play(release / 'Contents/MacOS/MoNoonPlayer', work / 'release', scene)
        play(aot / 'Contents/MacOS/MoNoonPlayer', work / 'aot', scene)
        runs = [work / 'release', work / 'aot']
        if editor_app:
            prepare(sample.resolve(), work / 'edit/ParticleLab', scene)
            subprocess.run(lock + [sys.executable, str(Path(__file__).resolve()), 'editor', str(editor_app.resolve()), str(work / 'edit/ParticleLab'),
                                   str(probe.resolve()), str(work / 'scene-play'), scene], check=True, timeout=1800)
            runs.insert(0, work / 'scene-play')
        compare(*runs)
        trace = runs[0] / 'trace.tsv'
        baseline = same_as_baseline(trace, scene) if arm_mac else 'not compared (other OS or architecture)'
        line = f"{scene}\t{' · '.join(r.name for r in runs)} identical\tsha256 {hashlib.sha256(trace.read_bytes()).hexdigest()[:16]}\t{baseline}"
        print(line, flush=True)
        summary.append(line)
    (out / 'summary.txt').write_text('\n'.join(summary) + '\n', encoding='utf-8')


if __name__ == '__main__':
    command, *args = sys.argv[1:]
    if command == 'compare': compare(*args)
    elif command == 'same-as': same_as(*args)
    elif command == 'prepare': prepare(Path(args[0]), Path(args[1]), args[2])
    elif command == 'play': play(Path(args[0]), Path(args[1]), args[2])
    elif command == 'editor': editor(Path(args[0]), Path(args[1]), Path(args[2]), Path(args[3]), args[4])
    elif command == 'check': check(Path(args[0]), args[1])
    elif command == 'all':
        app = probe = None
        if '--editor' in args:
            i = args.index('--editor')
            app, probe = Path(args[i + 1]), Path(args[i + 2])
        names = args[args.index('--scenes') + 1].split(',') if '--scenes' in args else None
        run_all(Path(args[0]), Path(args[1]), app, probe, names)
