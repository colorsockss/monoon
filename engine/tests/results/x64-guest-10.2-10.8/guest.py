"""Runs inside the Windows x64 / Debian x64 guest: roadmap 10.2-10.8 harnesses, Release + Native AOT built here, editor Scene Play."""
from pathlib import Path
import hashlib, importlib.util, json, os, platform, runpy, shutil, subprocess as sp, sys, tarfile, time, traceback, zipfile

base = Path.cwd()
win = os.name == 'nt'
rid = 'win-x64' if win else 'linux-x64'
EXE = 'MoNoonPlayer.exe' if win else 'MoNoonPlayer'
R = base / 'results'
R.mkdir(exist_ok=True)
os.environ['MONOON_PLAYERS_DIR'] = str(base / 'templates')
os.environ['DOTNET_PROCESSOR_COUNT'] = '4' if win else '2'
QA, P = base / 'qa', base / 'projects'
# TCG emulation (x86 on an ARM Mac) is roughly 10-30x slower than the Mac: scale harness subprocess timeouts only.
TCG = 15
_run = sp.run
def scaled_run(*a, timeout=None, **k): return _run(*a, timeout=None if timeout is None else timeout * TCG, **k)
sp.run = scaled_run

if not (base / 'editor').exists():
    if win:
        with zipfile.ZipFile('editor.zip') as z: z.extractall(base)
    else:
        with tarfile.open('editor.tar.gz') as t: t.extractall(base)
editor = base / 'editor' / ('MoNoon.exe' if win else 'MoNoon')
if not win:
    for f in [editor, base / 'templates/linux-x64/MoNoonPlayer']: f.chmod(0o755)

summary_path = R / 'summary.json'
summary = json.loads(summary_path.read_text()) if summary_path.exists() else {}
summary['host'] = {'rid': rid, 'platform': platform.platform(), 'machine': platform.machine(), 'python': sys.version.split()[0]}
only = (base / 'steps.txt').read_text().split() if (base / 'steps.txt').exists() else None

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def step(name):
    def wrap(fn):
        if only and name not in only: return fn
        # Resume after the expired Windows evaluation's hourly forced shutdown: finished steps are kept.
        if not only and summary.get(name, {}).get('status'): return fn
        feature, target = name.split('-', 1)
        shutil.rmtree(R / feature / target, ignore_errors=True)  # an interrupted attempt
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
        summary_path.write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')
        if os.environ.get('MONOON_USB'):  # persist each step on the FAT image before any forced shutdown
            shutil.copytree(R, Path(os.environ['MONOON_USB']) / 'results', dirs_exist_ok=True)
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

players = {}
def build(project, mode, profile, out):
    key = (str(project), mode, profile)
    if key in players: return players[key]
    cmd = ['dotnet', base / 'cli/MoNoon.Cli.dll', 'build', project, mode, '--target', rid, '--profile', profile, '--output', out]
    folder = out / ('Development' if profile == 'development' else '') / ('NativeAOT' if mode == 'native-aot' else '')
    found = sorted(folder.glob('*-' + rid))
    if found and (found[0] / EXE).exists():  # resumed run: the builder only moves a finished product into place
        print('Reusing', found[0], flush=True)
    else:
        print('Running', *map(str, cmd), flush=True)
        _run(list(map(str, cmd)), check=True)
        found = sorted(folder.glob('*-' + rid))
    assert len(found) == 1, found
    player = found[0] / EXE
    if not win: player.chmod(0o755)
    assert (found[0] / 'MoNoonPlayer.dll').exists() == (mode == 'release'), found[0]
    skia = [str(p.relative_to(found[0])) for p in found[0].rglob('*') if 'skia' in p.name.lower()]
    assert not skia, skia
    players[key] = player
    return player

def files(out, names): return {n: sha(out / n) for n in names if (out / n).exists()}

# 10.2 player rebinding
reb = P / 'rebinding/KeyRoom'
for mode in ['release', 'native-aot']:
    @step(f'rebinding-{mode}')
    def _():
        V = module('KeyRoom.Rebinding', 'verify')
        out = R / 'rebinding' / mode
        V.play(build(reb, mode, 'distribution', base / 'products/rebinding'), out)
        return files(out, ['rebind.trace', 'restart.trace', 'restored.trace', 'rebind.player.settings', 'restart.player.settings', 'restored.player.settings'])
@step('rebinding-scene-play')
def _():
    out = R / 'rebinding/scene-play'
    script('KeyRoom.Rebinding', 'editor.py', editor, reb, base / 'probes/EditorProbe.dll', out)
    return files(out, ['rebind.trace', 'restart.trace', 'restored.trace', 'rebind.player.settings', 'restart.player.settings', 'restored.player.settings'])

if (base / 'wait200').exists() or (only and 'rebinding-scene-play-wait200' in only):
    # Diagnostic only (not the repo harness): the trace script gives a pending additive scene 20 ms of real time per
    # replay frame. Under TCG the controls screen can still be loading when Enter arrives, so retry with 200 ms.
    @step('rebinding-scene-play-wait200')
    def _():
        copy = P / 'rebinding-wait200/KeyRoom'
        if not copy.exists(): shutil.copytree(reb, copy, ignore=shutil.ignore_patterns('.monoon'))
        trace = copy / 'Scripts/TraceBindings.cs'
        text = trace.read_text(encoding='utf-8')
        assert text.count('Thread.Sleep(20)') == 1 or 'Thread.Sleep(200)' in text
        trace.write_text(text.replace('Thread.Sleep(20)', 'Thread.Sleep(200)'), encoding='utf-8')
        out = R / 'rebinding/scene-play-wait200'
        script('KeyRoom.Rebinding', 'editor.py', editor, copy, base / 'probes/EditorProbe.dll', out)
        return files(out, ['rebind.trace', 'restart.trace', 'restored.trace', 'rebind.player.settings', 'restart.player.settings', 'restored.player.settings'])

# 10.4 prefab structure: the editor applies the override first, then both players are built from the saved copy
pre = P / 'prefab/KeyRoom'
@step('prefab-editor')
def _():
    out = R / 'prefab/editor'
    script('KeyRoom.PrefabStructure', 'editor.py', editor, pre, base / 'probes/PrefabStructureProbe.dll', out)
    return {'editor.txt': (out / 'editor.txt').read_text(encoding='utf-8')}
for mode in ['release', 'native-aot']:
    @step(f'prefab-{mode}')
    def _():
        V = module('KeyRoom.PrefabStructure', 'verify')
        out = R / 'prefab' / mode
        V.play(build(pre, mode, 'distribution', base / 'products/prefab'), out)
        return {'result': json.loads((out / 'result.json').read_text()), 'result.json': sha(out / 'result.json')}

# 10.5 data list + the existing Herbalist replay against the Mac save
inv = P / 'raw/InventoryLab'
for mode in ['release', 'native-aot']:
    @step(f'datalist-{mode}')
    def _():
        V = module('InventoryLab.DataList', 'verify')
        out = R / 'datalist' / mode
        V.play(build(inv, mode, 'distribution', base / 'products/inventory'), out)
        return {'result': json.loads((out / 'result.json').read_text(encoding='utf-8')), 'slot-002.save': sha(out / 'profile/slot-002.save')}
    @step(f'herbalist-{mode}')
    def _():
        out = R / 'herbalist' / mode
        out.mkdir(parents=True)
        profile = out / 'profile'
        env = dict(os.environ, SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio', MONOON_USER_DATA=str(profile))
        env.pop('DISPLAY', None); env.pop('WAYLAND_DISPLAY', None)
        player = build(inv, mode, 'distribution', base / 'products/inventory')
        _run([str(player), '--replay-input', str(base / 'fixtures/Herbalist.inputreplay'), str(profile)], env=env, check=True, timeout=3600)
        shutil.copy2(profile / 'slot-001.save', out / f'{mode}.save')
        shutil.copy2(player.parent / 'MoNoonBuild', out / f'{mode}.receipt')
        actual, expected = (out / f'{mode}.save').read_bytes(), (base / 'fixtures/Herbalist.save').read_bytes()
        info = {'save': hashlib.sha256(actual).hexdigest(), 'bytes': len(actual), 'expected': hashlib.sha256(expected).hexdigest()}
        assert actual == expected, info
        return info

# 10.6 tile terrain + existing grid navigation
ter = P / 'terrain/SeedLab'
for mode in ['release', 'native-aot']:
    @step(f'terrain-{mode}')
    def _():
        V = module('SeedLab.TileTerrain', 'verify')
        out = R / 'terrain' / mode
        V.play(build(ter, mode, 'distribution', base / 'products/terrain'), out, ter / 'Assets/Route.tileset')
        return files(out, ['trace.txt', 'result.json']) | {'result': json.loads((out / 'result.json').read_text())}
@step('terrain-scene-play')
def _():
    out = R / 'terrain/scene-play'
    script('SeedLab.TileTerrain', 'editor.py', editor, ter, base / 'probes/TerrainProbe.dll', out)
    return files(out, ['trace.txt', 'result.json'])
grid = P / 'grid/SeedLab'
for mode in ['release', 'native-aot']:
    @step(f'grid-{mode}')
    def _():
        V = module('SeedLab.GridNavigation', 'verify')
        out = R / 'grid' / mode
        V.play(build(grid, mode, 'distribution', base / 'products/grid'), out)
        return files(out, ['trace.txt', 'result.json']) | {'result': json.loads((out / 'result.json').read_text())}

# 10.8 prefab pools
bar = P / 'raw/BarrageLab'
for mode in ['release', 'native-aot']:
    @step(f'barrage-{mode}')
    def _():
        out = R / 'barrage' / mode
        script('BarrageLab.PrefabPools', 'replay.py', build(bar, mode, 'distribution', base / 'products/barrage'), out)
        return json.loads((out / 'summary.json').read_text())

# 10.3 frame profiler
col = P / 'raw/CollisionLab'
for mode in ['release', 'native-aot']:
    @step(f'profiler-{mode}')
    def _():
        out = R / 'profiler' / mode
        dev = build(col, mode, 'development', base / 'products/profiler')
        dist = build(col, mode, 'distribution', base / 'products/profiler')
        script('CollisionLab.FrameProfiler', 'replay.py', dev, dist, out)
        s = json.loads((out / 'summary.json').read_text())
        return {'query_frames': s['query_frames'], 'slowest_after_r': {k: v for k, v in s['slowest_after_r'].items() if k != 'tree'}}
@step('profiler-scene-play')
def _():
    out = R / 'profiler/scene-play'
    script('CollisionLab.FrameProfiler', 'editor.py', editor, col, base / 'probes/ProfilerProbe.dll', out)
    shutil.rmtree(out / 'CollisionLab', ignore_errors=True)
    r = json.loads((out / 'result.json').read_text())
    return {k: r[k] for k in ('slowest', 'query_frames') if k in r}

# 10.7 split loading
spl = P / 'split/CollisionLab'
for mode in ['release', 'native-aot']:
    @step(f'split-{mode}')
    def _():
        out = R / 'split' / mode
        script('CollisionLab.SplitLoading', 'replay.py', 'run', build(spl, mode, 'development', base / 'products/split'), out, '--runs', '3')
        return json.loads((out / 'summary.json').read_text())['medians']
if (base / 'splitlong').exists() or (only and any(s.startswith('split-long') for s in only)):
    # Diagnostic only (not the repo harness): under TCG the 4 ms split load does not commit within the harness's
    # 300 recorded frames. Same replay code with 1,200 frames and one run, so commit and cancel can be observed.
    for mode in ['release', 'native-aot']:
        @step(f'split-long-{mode}')
        def _():
            # The harness's own replay()/window(); only the frame count and the cancel retry spacing differ
            # (TCG run-to-run variance of the background read exceeds the harness's 3 x 2-frame retries).
            m = module('CollisionLab.SplitLoading', 'replay')
            m.FRAMES = 1200
            out = R / 'split' / f'long-{mode}'
            out.mkdir(parents=True)
            player = build(spl, mode, 'development', base / 'products/split')
            res = {}
            for path, budget in m.BUDGETS.items():
                for scenario, key in (('add', '4'), ('replace', '3')):
                    r = res[f'{scenario}/{path}'] = m.replay(player, out, f'{scenario}-{path}', key, budget)
                    print(scenario, path, {k: r[k] for k in ('frames', 'total_ms', 'max_ms', 'p95_ms', 'committed', 'resources')}, flush=True)
                uploads = res[f'add/{path}']['upload_frames']
                cancel = (uploads[0] + uploads[-1]) // 2 if uploads else res[f'add/{path}']['end'] - 2
                for attempt in range(8):
                    r = m.replay(player, out, f'cancel-{path}', '4', budget, cancel)
                    print('cancel', path, 'frame', cancel, 'committed', r['committed'], r['resources'], flush=True)
                    if not r['committed']: break
                    cancel -= 8
                r['cancel_frame'], r['attempts'] = cancel, attempt + 1
                res[f'cancel/{path}'] = r
            (out / 'summary.json').write_text(json.dumps(res, indent=1) + '\n', encoding='utf-8')
            assert all(res[f'{s}/{p}']['committed'] for s in ('add', 'replace') for p in m.BUDGETS), 'a load did not commit in 1,200 frames'
            for p in m.BUDGETS:
                c = res[f'cancel/{p}']
                assert not c['committed'] and c['resources'].get('end') == c['resources'].get('start'), ('cancel', p, c['resources'])
            return {k: {m2: v[m2] for m2 in ('frames', 'total_ms', 'max_ms', 'p95_ms', 'committed', 'resources')} | ({'cancel_frame': v['cancel_frame'], 'attempts': v['attempts']} if 'attempts' in v else {}) for k, v in res.items()}
if not win:
    @step('split-scene-play')
    def _():
        out = R / 'split/scene-play'
        script('CollisionLab.SplitLoading', 'editor.py', editor, spl, base / 'probes/SplitLoadingProbe.dll', out, 3)
        return json.loads((out / 'summary.json').read_text())
else:
    for i in (1, 2, 3):
        @step(f'split-scene-play{i}')
        def _():
            out = R / f'split/scene-play{i}'
            script('CollisionLab.SplitLoading', 'editor.py', editor, spl, base / 'probes/SplitLoadingProbe.dll', out, 1)
            return json.loads((out / 'summary.json').read_text())

summary['done'] = time.strftime('%Y-%m-%d %H:%M:%S')
summary_path.write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')
print('SUMMARY', {k: v.get('status') for k, v in summary.items() if isinstance(v, dict) and 'status' in v}, flush=True)
