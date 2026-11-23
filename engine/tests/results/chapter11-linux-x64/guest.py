"""Runs inside the Debian x64 guest: roadmap 11 harnesses (Release + Native AOT built here with the packaged CLI, editor Scene
Play in the packaged editor under Xvfb), the 11.12 packages built on the Mac, and the 6.8 / InventoryLab regressions.
Resumable: finished steps are skipped; steps.txt (space-separated names) re-runs only those steps.
MoNoon no longer ships a Linux editor (2026-11-23): on linux-x64 the editor steps are skipped and the comparisons use Release and
Native AOT only. The 2026-11-23 run in README.md still ran them with the last Linux editor package."""
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

EDITOR = rid != 'linux-x64'
SCENE = ['scene-play'] if EDITOR else []
EDITOR_ONLY = {'copy-editor', 'copy-editor-noclip', 'copy-release', 'copy-native-aot', 'reload-editor', 'extensions-editor', 'editor68'}
if EDITOR and not (base / 'editor').exists():
    with tarfile.open('editor.tar.gz') as t: t.extractall(base)
editor = base / 'editor/MoNoon'
for f in ([editor] if EDITOR else []) + [base / 'templates/linux-x64/MoNoonPlayer']: f.chmod(0o755)

summary_path = R / 'summary.json'
summary = json.loads(summary_path.read_text()) if summary_path.exists() else {}
summary['host'] = {'display': 'Xvfb 2560x1440x24', 'rid': rid, 'platform': platform.platform(), 'machine': platform.machine(),
                   'python': sys.version.split()[0], 'slow': os.environ['MONOON_QA_SLOW'], 'timeout_scale': TCG,
                   'dotnet': _run(['dotnet', '--version'], capture_output=True, text=True).stdout.strip()}
only = (base / 'steps.txt').read_text().split() if (base / 'steps.txt').exists() else None

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def persist(): summary_path.write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')

def step(name, clean=None):
    def wrap(fn):
        if only and name not in only: return fn
        if not only and summary.get(name, {}).get('status'): return fn
        if not EDITOR and (name.endswith('scene-play') or name in EDITOR_ONLY): return fn
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
    e = sorted(p.name for p in (base / 'editor').iterdir() if 'box2d' in p.name.lower()) if EDITOR else []
    assert 'libbox2d.so' in t and 'Brutal.Box2D.dll' in t and ('libbox2d.so' in e or not EDITOR), (t, e)
    ldd = _run(['ldd', str(base / 'templates/linux-x64/libbox2d.so')], capture_output=True, text=True).stdout
    assert 'not found' not in ldd, ldd
    return {'template': t, 'editor': e, 'ldd': ldd.strip().splitlines()}

TRACES = {'joints': ('PlatformerLab.Joints', 'joints/PlatformerLab', ['trace.tsv', 'result.json']),
          'tiles': ('PlatformerLab.TileCollision', 'tiles/PlatformerLab', ['trace.tsv', 'result.json'])}
for feature, (harness, project, names) in TRACES.items():
    for mode in ['release', 'native-aot']:
        @step(f'{feature}-{mode}', R / feature / mode)
        def _(harness=harness, project=project, names=names, feature=feature, mode=mode):
            V = module(harness, 'verify')
            out = R / feature / mode
            player = build(P / project, mode, base / 'products' / feature)
            V.play(player, out)
            return files(out, names) | {'result': load(out / 'result.json'), 'build': built[(str(P / project), mode)]}
    @step(f'{feature}-scene-play', R / feature / 'scene-play')
    def _(harness=harness, project=project, names=names, feature=feature):
        out = R / feature / 'scene-play'
        script(harness, 'editor.py', editor, P / project, probe(harness), out)
        return files(out, names) | {'result': load(out / 'result.json')}
@step('joints-compare')
def _():
    V = module('PlatformerLab.Joints', 'verify')
    V.compare(*(R / 'joints' / m for m in [*SCENE, 'release', 'native-aot']))
    return {'trace.tsv': sha(R / 'joints/release/trace.tsv')}

for mode in ['release', 'native-aot']:
    @step(f'spatial-{mode}', R / 'spatial' / mode)
    def _(mode=mode):
        out = R / 'spatial' / mode
        out.mkdir(parents=True)
        player = build(P / 'raw/CollisionLab', mode, base / 'products/spatial')
        script('CollisionLab.Spatial', 'verify.py', '--player', player, '--output', out / 'spatial.log')
        rows = [l for l in (out / 'spatial.log').read_text().splitlines() if l.startswith('SPATIAL ')]
        return {'rows': rows, 'build': built[(str(P / 'raw/CollisionLab'), mode)]}

# ---- 11.1 script waits and 11.9 project tests
waits = P / 'waits/KeyRoom'
for mode in ['release', 'native-aot']:
    @step(f'waits-{mode}', R / 'waits' / mode)
    def _(mode=mode):
        V = module('KeyRoom.ScriptWaits', 'verify')
        V.play(build(waits, mode, base / 'products/waits'), R / 'waits' / mode)
        return files(R / 'waits' / mode, [f'{mode}.trace'])
@step('waits-scene-play', R / 'waits/scene-play')
def _():
    script('KeyRoom.ScriptWaits', 'editor.py', editor, waits, probe('KeyRoom.ScriptWaits'), R / 'waits/scene-play')
    return files(R / 'waits/scene-play', ['scene-play.trace'])
@step('waits-compare')
def _():
    V = module('KeyRoom.ScriptWaits', 'verify')
    V.compare(*(R / 'waits' / m / f'{m}.trace' for m in [*SCENE, 'release', 'native-aot']))

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
@step('rich-scene-play', R / 'rich/scene-play')
def _():
    script('InventoryLab.RichText', 'editor.py', editor, rich, probe('InventoryLab.RichText'), R / 'rich/scene-play')
    return files(R / 'rich/scene-play', ['reveal.tsv'])
@step('rich-compare')
def _():
    V = module('InventoryLab.RichText', 'verify')
    V.MODES = [m for m in V.MODES if m != 'scene-play' or EDITOR]
    V.compare(R / 'rich')
    return {'comparison': (R / 'rich/comparison.tsv').read_text()}

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

# ---- 11.7 paths and lines
for feature, harness, project in [('paths', 'PlatformerLab.Paths', 'paths/PlatformerLab'), ('lines', 'BarrageLab.Lines', 'lines/BarrageLab')]:
    for mode in ['release', 'native-aot']:
        @step(f'{feature}-{mode}', R / feature / mode)
        def _(feature=feature, harness=harness, project=project, mode=mode):
            V = module(harness, 'verify')
            V.play(build(P / project, mode, base / 'products' / feature), R / feature / mode)
            return files(R / feature / mode, ['trace.tsv'])
    @step(f'{feature}-scene-play', R / feature / 'scene-play')
    def _(feature=feature, harness=harness, project=project):
        script(harness, 'editor.py', editor, P / project, probe('PlatformerLab.Paths'), R / feature / 'scene-play')
        return files(R / feature / 'scene-play', ['trace.tsv'])
    @step(f'{feature}-compare')
    def _(feature=feature, harness=harness):
        V = module(harness, 'verify')
        if not hasattr(V, 'compare'): V.paths.CAPTURES[:] = V.CAPTURES; V = V.paths  # BarrageLab.Lines' __main__ does the same
        V.compare(*(R / feature / m for m in [*SCENE, 'release', 'native-aot']))

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

summary['done'] = time.strftime('%Y-%m-%d %H:%M:%S')
persist()
print('SUMMARY', {k: v.get('status') for k, v in summary.items() if isinstance(v, dict) and 'status' in v}, flush=True)
