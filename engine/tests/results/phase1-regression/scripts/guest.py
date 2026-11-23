"""Runs inside the Debian x64 guest (phase1 final regression): the chapter-11 guest set of .test-env/work/ch11/guest.py (Release +
Native AOT built here with the packaged CLI, Scene Play in the packaged editor under Xvfb, 11.12 packages built on the Mac, 6.8 and
InventoryLab regressions) plus Joints/Paths/Lines/TileCollision three-path equality and DOTNET_EnableAVX2=0 (x64-determinism),
AudioLab Release/AOT (AudioLab.Vorbis/ci.py capture+compare), Samples.GameText and InventoryLab build_guest.py from a source
snapshot. Same harness calls and pass criteria; timeouts scaled for TCG. Game builds live on tmpfs (products -> /tmp/products) and
are removed after each feature. Resumable: finished steps are skipped; steps.txt (space-separated names) re-runs only those."""
from pathlib import Path
import hashlib, importlib.util, json, os, platform, runpy, shutil, stat, subprocess as sp, sys, tarfile, time, traceback

base = Path.cwd()
rid, EXE = 'linux-x64', 'MoNoonPlayer'
R = base / 'results'
R.mkdir(exist_ok=True)
os.environ['MONOON_PLAYERS_DIR'] = str(base / 'templates')
os.environ['MONOON_CLI'] = str(base / 'cli/MoNoon.Cli.dll')
os.environ['DOTNET_PROCESSOR_COUNT'] = '2'
os.environ.setdefault('SDL_AUDIO_DRIVER', 'dummy')
# Box2D packages (11.3) copied from the Mac's NuGet cache: a second offline source next to the existing guest feed.
os.environ['RestoreSources'] = os.environ['RestoreSources'] + ';' + str(base / 'feed')
QA, P = base / 'qa', base / 'projects'
# TCG emulation (x86 on an ARM Mac) is roughly 10-30x slower than the Mac: scale harness subprocess timeouts and,
# through MONOON_QA_SLOW, the startup hooks' waits. Neither changes what is checked.
TCG = 15
os.environ['MONOON_QA_SLOW'] = os.environ.get('MONOON_QA_SLOW', '10')
_run = sp.run
def scaled_run(*a, timeout=None, **k): return _run(*a, timeout=None if timeout is None else timeout * TCG, **k)
sp.run = scaled_run

if not (base / 'editor').exists():
    with tarfile.open('editor.tar.gz') as t: t.extractall(base)
    (base / 'editor.tar.gz').unlink()  # the run folder is on a RAM tmpfs
editor = base / 'editor/MoNoon'
for f in [editor, base / 'templates/linux-x64/MoNoonPlayer']: f.chmod(0o755)

summary_path = R / 'summary.json'
summary = json.loads(summary_path.read_text()) if summary_path.exists() else {}
summary['host'] = {'display': 'Xvfb 2560x1440x24', 'rid': rid, 'platform': platform.platform(), 'machine': platform.machine(),
                   'python': sys.version.split()[0], 'slow': os.environ['MONOON_QA_SLOW'], 'timeout_scale': TCG,
                   'dotnet': _run(['dotnet', '--version'], capture_output=True, text=True).stdout.strip()}
only = (base / 'steps.txt').read_text().split() if (base / 'steps.txt').exists() else None

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def persist(): summary_path.write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')

# User decision during this run: Linux x64 is CI only. Editor steps (Scene Play, editor.py/StartupHook probes, packaged editor)
# are excluded from here on; compares become Release = Native AOT. Steps already finished keep their recorded status.
EDITOR_STEPS = ('-scene-play', 'copy-editor', 'reload-editor', 'extensions-editor', 'editor68', 'copy-release', 'copy-native-aot')
def step(name, clean=None):
    def wrap(fn):
        if name.endswith(EDITOR_STEPS) or name.startswith(('copy-editor', 'copy-release', 'copy-native-aot')):
            if not summary.get(name, {}).get('status') == 'pass': summary[name] = {'status': 'excluded', 'reason': 'user decision: Linux is CI only (no editor)'}; persist()
            return fn
        if only and name not in only: return fn
        if not only and summary.get(name, {}).get('status'): return fn
        if clean: shutil.rmtree(clean, ignore_errors=True)  # an interrupted attempt
        print(f'=== STEP {name} {time.strftime("%H:%M:%S")}', flush=True)
        t0 = time.time()
        try:
            detail = fn() or {}
            summary[name] = {'status': 'pass', **detail}
            print(f'=== PASS {name}', flush=True)
        except BaseException as e:  # harnesses report FAIL via SystemExit
            if isinstance(e, KeyboardInterrupt): raise
            tb = traceback.format_exc()
            print(tb, flush=True)
            summary[name] = {'status': 'fail', 'error': tb[-4000:]}
            print(f'=== FAIL {name}', flush=True)
        summary[name]['seconds'] = round(time.time() - t0)
        persist()
        return fn
    return wrap

def module(harness, name):
    sys.modules.pop(name, None)
    sys.path.insert(0, str(QA / harness))
    try:
        spec = importlib.util.spec_from_file_location(name, QA / harness / f'{name}.py')
        m = importlib.util.module_from_spec(spec); sys.modules[name] = m; spec.loader.exec_module(m)
        return m
    finally:
        sys.path.pop(0)

def script(harness, name, *args):
    """Run a harness CLI script in this process so the scaled timeouts apply."""
    sys.modules.pop('verify', None)
    old = sys.argv
    sys.argv = [str(QA / harness / name), *map(str, args)]
    print('Running', *sys.argv, flush=True)
    try:
        runpy.run_path(sys.argv[0], run_name='__main__')
    except SystemExit as e:
        if e.code not in (None, 0): raise
    finally:
        sys.argv = old

def probe(harness): return next((base / 'probes' / harness).glob('*.dll'))

FORBIDDEN = ('SkiaSharp', 'Avalonia', 'HarfBuzz', 'Engine.Editor', 'Engine.Authoring', 'Microsoft.CodeAnalysis', 'MoNoon.dll')
players, built = {}, {}
def build(project, mode, out):
    key = (str(project), mode)
    if key in players: return players[key]
    cmd = ['dotnet', base / 'cli/MoNoon.Cli.dll', 'build', project, mode, '--target', rid, '--profile', 'distribution', '--output', out]
    folder = out / ('NativeAOT' if mode == 'native-aot' else '')
    found = sorted(folder.glob('*-' + rid))
    if found and (found[0] / EXE).exists():  # resumed run: the builder only moves a finished product into place
        print('Reusing', found[0], flush=True)
    else:
        print('Running', *map(str, cmd), flush=True)
        t0 = time.time()
        _run(list(map(str, cmd)), check=True)
        print(f'Built in {time.time() - t0:.0f} s', flush=True)
        found = sorted(folder.glob('*-' + rid))
    assert len(found) == 1, found
    game = found[0]
    (game / EXE).chmod(0o755)
    assert (game / 'MoNoonPlayer.dll').exists() == (mode == 'release'), game
    files = [p for p in game.rglob('*') if p.is_file()]
    bad = [str(p.relative_to(game)) for p in files if any(n in p.name for n in FORBIDDEN)]
    assert not bad, bad
    built[key] = {'files': len(files), 'box2d': sorted(str(p.relative_to(game)) for p in files if 'box2d' in p.name.lower()), 'forbidden': bad}
    players[key] = game / EXE
    return players[key]

def files(out, names): return {n: sha(out / n) for n in names if (out / n).exists()}
def load(p): return json.loads(Path(p).read_text(encoding='utf-8'))

# ---- 11.3 Box2D: package files in the template and the editor, then Joints, tile collision, spatial queries
@step('box2d-files')
def _():
    t = sorted(p.name for p in (base / 'templates/linux-x64').iterdir() if 'box2d' in p.name.lower())
    e = sorted(p.name for p in (base / 'editor').iterdir() if 'box2d' in p.name.lower())
    assert 'libbox2d.so' in t and 'Brutal.Box2D.dll' in t and 'libbox2d.so' in e, (t, e)
    ldd = _run(['ldd', str(base / 'templates/linux-x64/libbox2d.so')], capture_output=True, text=True).stdout
    assert 'not found' not in ldd, ldd
    return {'template': t, 'editor': e, 'ldd': ldd.strip().splitlines()}

def clean(feature): shutil.rmtree(base / 'products' / feature, ignore_errors=True); players.clear()  # tmpfs budget

# Joints/TileCollision (11.3) and Paths/Lines (11.7): three paths, byte comparison, and the Release player with the JIT's AVX2
# group (FMA included in .NET 10) disabled, which must not change the trace (x64-determinism).
TRACES = [('joints', 'PlatformerLab.Joints', 'joints/PlatformerLab', 'PlatformerLab.Joints', ['trace.tsv', 'result.json']),
          ('tiles', 'PlatformerLab.TileCollision', 'tiles/PlatformerLab', 'PlatformerLab.TileCollision', ['trace.tsv', 'result.json']),
          ('paths', 'PlatformerLab.Paths', 'paths/PlatformerLab', 'PlatformerLab.Paths', ['trace.tsv']),
          ('lines', 'BarrageLab.Lines', 'lines/BarrageLab', 'PlatformerLab.Paths', ['trace.tsv'])]
for feature, harness, project, probe_of, names in TRACES:
    for mode in ['release', 'native-aot']:
        @step(f'{feature}-{mode}', R / feature / mode)
        def _(harness=harness, project=project, names=names, feature=feature, mode=mode):
            V = module(harness, 'verify')
            out = R / feature / mode
            player = build(P / project, mode, base / 'products' / feature)
            V.play(player, out)
            extra = {'result.json': sha(out / 'result.json')} if (out / 'result.json').exists() else {}  # BarrageLab.Lines writes a Python repr
            return files(out, names) | extra | {'build': built[(str(P / project), mode)]}
    @step(f'{feature}-release-noavx2', R / feature / 'release-noavx2')
    def _(harness=harness, project=project, feature=feature):
        V = module(harness, 'verify')
        out = R / feature / 'release-noavx2'
        os.environ['DOTNET_EnableAVX2'] = '0'
        try: V.play(build(P / project, 'release', base / 'products' / feature), out)
        finally: os.environ.pop('DOTNET_EnableAVX2')
        assert (out / 'trace.tsv').read_bytes() == (R / feature / 'release/trace.tsv').read_bytes(), 'DOTNET_EnableAVX2=0 changed the Release trace'
        return {'trace.tsv': sha(out / 'trace.tsv')}
    clean(feature)
    @step(f'{feature}-scene-play', R / feature / 'scene-play')
    def _(harness=harness, project=project, names=names, feature=feature, probe_of=probe_of):
        out = R / feature / 'scene-play'
        script(harness, 'editor.py', editor, P / project, probe(probe_of), out)
        return files(out, names)
    @step(f'{feature}-compare')
    def _(feature=feature, harness=harness):
        V = module(harness, 'verify')
        outs = [R / feature / m for m in ['release', 'native-aot']]
        for n in ['trace.tsv', 'result.json']:
            if (outs[0] / n).exists(): assert (outs[0] / n).read_bytes() == (outs[1] / n).read_bytes(), f'{n}: Release and Native AOT differ'
        return {'trace.tsv': sha(R / feature / 'release/trace.tsv')}

for mode in ['release', 'native-aot']:
    @step(f'spatial-{mode}', R / 'spatial' / mode)
    def _(mode=mode):
        out = R / 'spatial' / mode
        out.mkdir(parents=True)
        player = build(P / 'raw/CollisionLab', mode, base / 'products/spatial')
        script('CollisionLab.Spatial', 'verify.py', '--player', player, '--output', out / 'spatial.log')
        rows = [l for l in (out / 'spatial.log').read_text().splitlines() if l.startswith('SPATIAL ')]
        return {'rows': rows, 'build': built[(str(P / 'raw/CollisionLab'), mode)]}

clean('spatial')
# ---- 11.1 script waits and 11.9 project tests
waits = P / 'waits/KeyRoom'
for mode in ['release', 'native-aot']:
    @step(f'waits-{mode}', R / 'waits' / mode)
    def _(mode=mode):
        V = module('KeyRoom.ScriptWaits', 'verify')
        V.play(build(waits, mode, base / 'products/waits'), R / 'waits' / mode)
        return files(R / 'waits' / mode, [f'{mode}.trace'])
clean('waits')
@step('waits-scene-play', R / 'waits/scene-play')
def _():
    script('KeyRoom.ScriptWaits', 'editor.py', editor, waits, probe('KeyRoom.ScriptWaits'), R / 'waits/scene-play')
    return files(R / 'waits/scene-play', ['scene-play.trace'])
@step('waits-compare')
def _():
    V = module('KeyRoom.ScriptWaits', 'verify')
    assert (R / 'waits/release/release.trace').read_bytes() == (R / 'waits/native-aot/native-aot.trace').read_bytes(), 'traces differ'
    return {'trace': sha(R / 'waits/release/release.trace')}

@step('project-tests', R / 'project-tests')
def _():
    out = R / 'project-tests'; out.mkdir(parents=True)
    copy = out / 'KeyRoom'
    shutil.copytree(P / 'raw/KeyRoom', copy)
    r = _run(['dotnet', str(base / 'cli/MoNoon.Cli.dll'), 'test', 'project', str(copy)], capture_output=True, text=True, timeout=3600)
    (out / 'keyroom.txt').write_text(r.stdout + r.stderr, encoding='utf-8')
    print(r.stdout + r.stderr, flush=True)
    assert r.returncode == 0, r.returncode
    return {'tail': (r.stdout + r.stderr).strip().splitlines()[-3:]}

# ---- 11.4/11.6 CameraLab: headless replay variant (no window tool or rumble fixture on Linux)
for mode in ['release', 'native-aot']:
    @step(f'camlab-{mode}', R / 'camlab' / mode)
    def _(mode=mode):
        M = module('CameraLab.ScreenEffects', 'replay')
        M.play(build(P / 'raw/CameraLab', mode, base / 'products/camlab'), R / 'camlab' / mode)
        return files(R / 'camlab' / mode, ['record', 'first.player.settings', 'restart.player.settings'])
clean('camlab')
@step('camlab-compare')
def _():
    module('CameraLab.ScreenEffects', 'replay').compare(R / 'camlab/release', R / 'camlab/native-aot')

# ---- 11.5 rich text
rich = P / 'rich/InventoryLab'
for mode in ['release', 'native-aot']:
    @step(f'rich-{mode}', R / 'rich' / mode)
    def _(mode=mode):
        V = module('InventoryLab.RichText', 'verify')
        V.play(build(rich, mode, base / 'products/rich'), R / 'rich' / mode)
        return files(R / 'rich' / mode, ['reveal.tsv'])
clean('rich')
@step('rich-scene-play', R / 'rich/scene-play')
def _():
    script('InventoryLab.RichText', 'editor.py', editor, rich, probe('InventoryLab.RichText'), R / 'rich/scene-play')
    return files(R / 'rich/scene-play', ['reveal.tsv'])
@step('rich-compare')
def _():
    a, b = R / 'rich/release', R / 'rich/native-aot'
    assert (a / 'reveal.tsv').read_bytes() == (b / 'reveal.tsv').read_bytes(), 'reveal.tsv differs'
    caps = {p.relative_to(a).as_posix(): (sha(p), sha(b / p.relative_to(a))) for p in sorted(a.rglob('*.png'))}
    return {'reveal.tsv': sha(a / 'reveal.tsv'), 'captures': caps, 'captures_equal': all(x == y for x, y in caps.values())}

# ---- 11.8 Aseprite (players first: the editor check overwrites ArrowCycle.aseprite in the copy)
ase = P / 'ase/SpriteLab'
ase_source = base / 'ase-ArrowCycle.aseprite'
if not ase_source.exists(): shutil.copy2(ase / 'Assets/ArrowCycle.aseprite', ase_source)
for mode in ['release', 'native-aot']:
    @step(f'ase-{mode}', R / 'ase' / mode)
    def _(mode=mode):
        V = module('SpriteLab.Aseprite', 'verify')
        player = build(ase, mode, base / 'products/ase')
        V.play(player, R / 'ase' / mode, ase_source)
        V.forbidden(player.parent)
        return {'result': load(R / 'ase' / mode / 'result.json')} if (R / 'ase' / mode / 'result.json').exists() else {}
clean('ase')
@step('ase-scene-play', R / 'ase/scene-play')
def _():
    script('SpriteLab.Aseprite', 'editor.py', editor, ase, probe('SpriteLab.Aseprite'), R / 'ase/scene-play')
    return {'reimport': load(R / 'ase/scene-play/reimport.json')}

# ---- 11.11 copy/paste: the editor saves the copy, then both players are built from it
copy = P / 'copy/KeyRoom'
@step('copy-editor', R / 'copy/editor')
def _():
    script('KeyRoom.CopyPaste', 'editor.py', editor, copy, probe('KeyRoom.CopyPaste'), R / 'copy/editor')
    return {'editor.txt': (R / 'copy/editor/editor.txt').read_text(encoding='utf-8')}
# Diagnostic (opt-in in the hook): this Xvfb session's OS clipboard hands back nothing, so the hook records its three OS
# clipboard checks as skipped and every other check (paste, Undo/Redo, Cut, missing reference, save, Scene Play) runs.
# Scene Play only ticks while the editor window is active, and Xvfb has no window manager to activate it: like the 6.8
# check (xdotool windowfocus), keep X input focus on the editor window while this step runs.
import threading
def focus_editor(stop):
    while not stop.wait(2):
        for w in _run(['xdotool', 'search', '--onlyvisible', '--name', 'MoNoon'], capture_output=True, text=True).stdout.split():
            _run(['xdotool', 'windowfocus', w], capture_output=True)
@step('copy-editor-noclip', R / 'copy/editor-noclip')
def _():
    os.environ['MONOON_QA_NO_OS_CLIPBOARD'] = '1'
    stop = threading.Event(); threading.Thread(target=focus_editor, args=(stop,), daemon=True).start()
    try: script('KeyRoom.CopyPaste', 'editor.py', editor, copy, probe('KeyRoom.CopyPaste'), R / 'copy/editor-noclip')
    finally: del os.environ['MONOON_QA_NO_OS_CLIPBOARD']; stop.set()
    return {'editor.txt': (R / 'copy/editor-noclip/editor.txt').read_text(encoding='utf-8')}
for mode in ['release', 'native-aot']:
    @step(f'copy-{mode}', R / 'copy' / mode)
    def _(mode=mode):
        assert 'pass' in (summary.get('copy-editor', {}).get('status'), summary.get('copy-editor-noclip', {}).get('status')), 'needs the copy saved by the editor check'
        V = module('KeyRoom.CopyPaste', 'verify')
        player = build(copy, mode, base / 'products/copy')
        V.play(player, R / 'copy' / mode)
        V.forbidden(player.parent)
        return {'result': load(R / 'copy' / mode / 'result.json')}

clean('copy')
# ---- 11.2 script reload and 11.10 editor extensions (the extensions check builds Release + AOT with MONOON_CLI)
@step('reload-editor', R / 'reload')
def _():
    script('KeyRoom.ScriptReload', 'editor.py', editor, P / 'raw/KeyRoom', probe('KeyRoom.ScriptReload'), R / 'reload')
    return {'reload': load(R / 'reload/reload.json')}
@step('extensions-editor', R / 'extensions')
def _():
    script('KeyRoom.EditorExtensions', 'editor.py', editor, P / 'raw/KeyRoom', probe('KeyRoom.EditorExtensions'), R / 'extensions')
    shutil.rmtree(R / 'extensions/release', ignore_errors=True); shutil.rmtree(R / 'extensions/native-aot', ignore_errors=True)  # file lists kept
    return {k: v for k, v in load(R / 'extensions/extensions.json').items() if not k.startswith(('inspector_', 'panel_'))}

# ---- 11.12 packages made on the Mac with --package on: modes, desktop entry, smoke test and replay from the extracted folder
def extract(game):
    out = R / 'package' / game
    out.mkdir(parents=True)
    _run(['tar', '-xzf', str(base / f'packages/{game}-linux-x64.tar.gz'), '-C', str(out)], check=True)
    return out / f'{game}-linux-x64'
def mode_of(p): return stat.filemode(p.stat().st_mode)
@step('package-keyroom', R / 'package/KeyRoom')
def _():
    root = extract('KeyRoom')
    modes = {n: mode_of(root / n) for n in ['MoNoonPlayer', 'install-desktop-entry.sh', 'createdump', 'KeyRoom.desktop', 'KeyRoom.png', 'Game/GameSettings', 'libSDL3.so.0', 'libbox2d.so'] if (root / n).exists()}
    assert modes['MoNoonPlayer'][3] == 'x' and modes['install-desktop-entry.sh'][3] == 'x' and modes['Game/GameSettings'] == '-rw-r--r--', modes
    home = R / 'package/KeyRoom-home'; home.mkdir()
    env = {k: v for k, v in os.environ.items() if k != 'XDG_DATA_HOME'} | {'HOME': str(home)}
    out = _run([str(root / 'install-desktop-entry.sh')], env=env, capture_output=True, text=True, check=True).stdout
    entries = list((home / '.local/share/applications').glob('*.desktop'))
    assert len(entries) == 1, entries
    text = entries[0].read_text()
    fields = dict(l.split('=', 1) for l in text.splitlines() if '=' in l)
    exe, icon = Path(fields['Exec'].strip('"')), Path(fields['Icon'])
    assert text.startswith('[Desktop Entry]\n') and fields['Type'] == 'Application' and fields['Name'] == 'KeyRoom', text
    assert exe == root.resolve() / 'MoNoonPlayer' and os.access(exe, os.X_OK) and icon == root.resolve() / 'KeyRoom.png' and icon.exists(), text
    validate = shutil.which('desktop-file-validate')
    checked = _run([validate, str(entries[0])], capture_output=True, text=True).stdout if validate else 'desktop-file-validate not installed'
    profile = R / 'package/KeyRoom-smoke'
    env = dict(os.environ, MONOON_USER_DATA=str(profile), SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    smoke = _run([str(exe), '--smoke-test', str(profile)], env=env, capture_output=True, text=True, timeout=300)
    (R / 'package/KeyRoom-smoke.log').write_text(smoke.stdout + smoke.stderr)
    assert smoke.returncode == 0, smoke.stdout + smoke.stderr
    return {'modes': modes, 'installer': out.strip(), 'desktop': text, 'validate': checked, 'smoke': (smoke.stdout + smoke.stderr).strip().splitlines()[-3:]}
@step('package-inventorylab', R / 'package/InventoryLab')
def _():
    root = extract('InventoryLab')
    out = R / 'package/InventoryLab-replay'; out.mkdir()
    profile = out / 'profile'
    env = dict(os.environ, SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio', MONOON_USER_DATA=str(profile))
    env.pop('DISPLAY', None)
    _run([str(root / EXE), '--replay-input', str(base / 'fixtures/Herbalist.inputreplay'), str(profile)], env=env, check=True, timeout=3600)
    actual, expected = (profile / 'slot-001.save').read_bytes(), (base / 'fixtures/Herbalist.save').read_bytes()
    (out / 'package.save').write_bytes(actual)
    info = {'mode': mode_of(root / EXE), 'save': hashlib.sha256(actual).hexdigest(), 'bytes': len(actual), 'expected': hashlib.sha256(expected).hexdigest()}
    assert actual == expected, info
    return info

# ---- regressions: 6.8 packaged editor and the InventoryLab Herbalist replay (Release + AOT built here)
@step('editor68', R / 'editor68')
def _():
    sys.path.insert(0, str(QA / 'KeyRoom.Desktop'))
    sys.modules.pop('verify', None)  # verify_editor imports KeyRoom.Desktop/verify, not a harness module loaded earlier
    spec = importlib.util.spec_from_file_location('verify_editor', QA / 'KeyRoom.Desktop/verify_editor.py')
    V = importlib.util.module_from_spec(spec); sys.modules['verify_editor'] = V; spec.loader.exec_module(V)
    sys.path.pop(0)
    wait = V.wait
    V.wait = lambda check, description, timeout=90: wait(check, description, timeout * TCG)
    out = R / 'editor68'
    old = sys.argv
    sys.argv = ['verify_editor.py', str(editor), str(base / 'KeyRoom-sample.zip'), str(out), '--expected-save', str(base / 'fixtures/collected.save')]
    try:
        V.main()
    finally:
        sys.argv = old
    return files(out, ['KeyRoom.scene', 'edit/baseline.save', 'edit/edited.save', 'reopen/edited.save'])

inv = P / 'raw/InventoryLab'
for mode in ['release', 'native-aot']:
    @step(f'herbalist-{mode}', R / 'herbalist' / mode)
    def _(mode=mode):
        out = R / 'herbalist' / mode
        out.mkdir(parents=True)
        profile = out / 'profile'
        env = dict(os.environ, SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio', MONOON_USER_DATA=str(profile))
        env.pop('DISPLAY', None); env.pop('WAYLAND_DISPLAY', None)
        player = build(inv, mode, base / 'products/inventory')
        _run([str(player), '--replay-input', str(base / 'fixtures/Herbalist.inputreplay'), str(profile)], env=env, check=True, timeout=3600)
        shutil.copy2(profile / 'slot-001.save', out / f'{mode}.save')
        shutil.copy2(player.parent / 'MoNoonBuild', out / f'{mode}.receipt')
        actual, expected = (out / f'{mode}.save').read_bytes(), (base / 'fixtures/Herbalist.save').read_bytes()
        info = {'save': hashlib.sha256(actual).hexdigest(), 'bytes': len(actual), 'expected': hashlib.sha256(expected).hexdigest(), 'build': built[(str(inv), mode)]}
        assert actual == expected, info
        return info

clean('inventory')

# ---- AudioLab (SDL_mixer): Release + AOT built here, captured through SDL's disk audio driver, F stop fade sent with xdotool.
# Uses AudioLab.Vorbis/ci.py's own capture() and compare() against the Mac record (results/audio-mixer/playback.csv).
import csv, types
for mode, reference in [('release', 'release'), ('native-aot', 'aot')]:
    @step(f'audio-{mode}', R / 'audio' / mode)
    def _(mode=mode, reference=reference):
        C = module('AudioLab.Vorbis', 'ci')
        C.time = types.SimpleNamespace(monotonic=lambda: time.monotonic() / TCG, sleep=time.sleep)  # capture()'s waits, scaled
        with C.baseline_csv(rid).open() as f:  # per-architecture PCM baseline (user decision): playback-linux-x64.csv
            mac = {row['mode']: row for row in csv.DictReader(f) if row['codec'] == 'ogg'}
        folder = R / 'audio' / mode; folder.mkdir(parents=True)
        product = build(P / 'raw/AudioLab', mode, base / 'products/audio').parent
        assert (product / 'Game/Assets/Tide.ogg').read_bytes() == (P / 'raw/AudioLab/Assets/Tide.ogg').read_bytes()
        environment = dict(SDL_AUDIO_DRIVER='disk', SDL_AUDIO_FORMAT='F32LE', SDL_AUDIO_CHANNELS='2', SDL_AUDIO_FREQUENCY=str(C.RATE),
                           SDL_AUDIO_DISK_OUTPUT_FILE=str(folder / 'device.f32'), MONOON_AUDIO_CAPTURE=str(folder / 'mix.f32'),
                           MONOON_USER_DATA=str(folder / 'profile'))
        for m in ['verify', 'verify_editor']: sys.modules.pop(m, None)  # capture() imports KeyRoom.Desktop's verify_editor/verify
        C.capture(product / EXE, environment, folder, None)
        metrics = C.compare(folder, mac[reference])
        for n in ['mix.f32', 'device.f32']: (folder / n).unlink()
        return {'metrics': metrics, 'build': built[(str(P / 'raw/AudioLab'), mode)]}
clean('audio')

# ---- Samples.GameText (SDL_ttf): KeyRoom bag (project fonts) and PlatformerLab cutscene (OS fonts), three paths, byte comparison.
for game in ['KeyRoom', 'PlatformerLab']:
    for mode in ['release', 'native-aot']:
        @step(f'text-{game}-{mode}', R / 'text' / game / mode)
        def _(game=game, mode=mode):
            V = module('Samples.GameText', 'verify')
            V.play(game, build(P / 'text' / game, mode, base / 'products/text' / game), R / 'text' / game / mode)
            return {'captures': {p.name: sha(p) for p in sorted((R / 'text' / game / mode / 'captures').glob('*.png'))}}
    clean(f'text/{game}')
    @step(f'text-{game}-scene-play', R / 'text' / game / 'scene-play')
    def _(game=game):
        script('Samples.GameText', 'editor.py', game, editor, P / 'text' / game, probe('Samples.GameText'), R / 'text' / game / 'scene-play')
        return {'captures': {p.name: sha(p) for p in sorted((R / 'text' / game / 'scene-play/captures').glob('*.png'))}}
    @step(f'text-{game}-compare')
    def _(game=game):
        a, b = R / 'text' / game / 'release/captures', R / 'text' / game / 'native-aot/captures'
        caps = {p.name: (sha(p), sha(b / p.name)) for p in sorted(a.glob('*.png'))}
        assert caps and all(x == y for x, y in caps.values()), caps
        return {'captures': caps}

# ---- InventoryLab.Replay/build_guest.py from the source snapshot (template, kit and CLI compiled here), on tmpfs.
@step('inventory-build-guest', R / 'build-guest')
def _():
    src, work = Path('/tmp/src-phase1'), Path('/tmp/build-guest')
    shutil.rmtree(src, ignore_errors=True); shutil.rmtree(work, ignore_errors=True); src.mkdir()
    _run(['python3', '-m', 'zipfile', '-e', str(base / 'src.zip'), str(src)], check=True)
    r = _run(['python3', '-u', 'engine/tests/InventoryLab.Replay/build_guest.py', str(work / 'builds'), str(work / 'results'),
              '--expected-rid', rid], cwd=src, capture_output=True, text=True)
    (R / 'build-guest').mkdir(parents=True)
    (R / 'build-guest/output.txt').write_text(r.stdout + r.stderr)
    if (work / 'results').exists(): shutil.copytree(work / 'results', R / 'build-guest/results')
    shutil.rmtree(src, ignore_errors=True); shutil.rmtree(work, ignore_errors=True)
    assert r.returncode == 0, (r.stdout + r.stderr)[-3000:]
    return {'tail': (r.stdout + r.stderr).strip().splitlines()[-4:]}

summary['done'] = time.strftime('%Y-%m-%d %H:%M:%S')
persist()
print('SUMMARY', {k: v.get('status') for k, v in summary.items() if isinstance(v, dict) and 'status' in v}, flush=True)
