"""Runs inside the Debian x64 guest (unfused Affine2D): Joints, Paths, Lines and TileCollision in Release + Native AOT built here with
the packaged CLI, then byte comparison of the traces. Trimmed copy of .test-env/work/ch11/guest.py (same harness calls, timeouts
scaled for TCG). steps.txt (space-separated) limits the steps. The 2026-11-23 run also compared editor Scene Play under Xvfb;
MoNoon no longer ships a Linux editor (2026-11-23), so reruns compare Release and Native AOT only."""
from pathlib import Path
import hashlib, importlib.util, json, os, platform, runpy, shutil, subprocess as sp, sys, time, traceback

base = Path.cwd()
rid, EXE = 'linux-x64', 'MoNoonPlayer'
R = base / 'results'
R.mkdir(exist_ok=True)
os.environ['MONOON_PLAYERS_DIR'] = str(base / 'templates')
os.environ['MONOON_CLI'] = str(base / 'cli/MoNoon.Cli.dll')
os.environ['DOTNET_PROCESSOR_COUNT'] = '2'
os.environ.setdefault('SDL_AUDIO_DRIVER', 'dummy')
os.environ['RestoreSources'] = os.environ['RestoreSources'] + ';' + str(base / 'feed')
QA, P = base / 'qa', base / 'projects'
TCG = 15
os.environ['MONOON_QA_SLOW'] = os.environ.get('MONOON_QA_SLOW', '10')
_run = sp.run
def scaled_run(*a, timeout=None, **k): return _run(*a, timeout=None if timeout is None else timeout * TCG, **k)
sp.run = scaled_run

(base / 'templates/linux-x64/MoNoonPlayer').chmod(0o755)

summary_path = R / 'summary.json'
summary = json.loads(summary_path.read_text()) if summary_path.exists() else {}
summary['host'] = {'rid': rid, 'platform': platform.platform(), 'machine': platform.machine(),
                   'cpu_flags': sorted(set(open('/proc/cpuinfo').read().split()) & {'fma', 'avx', 'avx2', 'avx512f', 'sse4_2'}),
                   'dotnet': _run(['dotnet', '--version'], capture_output=True, text=True).stdout.strip()}
only = (base / 'steps.txt').read_text().split() if (base / 'steps.txt').exists() else None
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def persist(): summary_path.write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')

def step(name, clean=None):
    def wrap(fn):
        if only and name not in only: return fn
        if not only and summary.get(name, {}).get('status'): return fn
        if clean: shutil.rmtree(clean, ignore_errors=True)
        print(f'=== STEP {name} {time.strftime("%H:%M:%S")}', flush=True)
        t0 = time.time()
        try:
            summary[name] = {'status': 'pass', **(fn() or {})}
            print(f'=== PASS {name}', flush=True)
        except BaseException as e:
            if isinstance(e, KeyboardInterrupt): raise
            tb = traceback.format_exc(); print(tb, flush=True)
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

def probe(name): return base / 'probes' / name
FORBIDDEN = ('SkiaSharp', 'Avalonia', 'HarfBuzz', 'Engine.Editor', 'Engine.Authoring', 'Microsoft.CodeAnalysis', 'MoNoon.dll')
players = {}
def build(project, mode, out):
    key = (str(project), mode)
    if key in players: return players[key]
    cmd = ['dotnet', base / 'cli/MoNoon.Cli.dll', 'build', project, mode, '--target', rid, '--profile', 'distribution', '--output', out]
    print('Running', *map(str, cmd), flush=True)
    t0 = time.time(); _run(list(map(str, cmd)), check=True); print(f'Built in {time.time() - t0:.0f} s', flush=True)
    found = sorted((out / ('NativeAOT' if mode == 'native-aot' else '')).glob('*-' + rid))
    assert len(found) == 1, found
    game = found[0]
    (game / EXE).chmod(0o755)
    assert (game / 'MoNoonPlayer.dll').exists() == (mode == 'release'), game
    bad = [str(p.relative_to(game)) for p in game.rglob('*') if p.is_file() and any(n in p.name for n in FORBIDDEN)]
    assert not bad, bad
    players[key] = game / EXE
    return players[key]

FEATURES = [('joints', 'PlatformerLab.Joints', 'joints/PlatformerLab', 'JointsProbe.dll', ['trace.tsv', 'result.json']),
            ('paths', 'PlatformerLab.Paths', 'paths/PlatformerLab', 'PathsProbe.dll', ['trace.tsv']),
            ('lines', 'BarrageLab.Lines', 'lines/BarrageLab', 'PathsProbe.dll', ['trace.tsv']),
            ('tiles', 'PlatformerLab.TileCollision', 'tiles/PlatformerLab', 'TileContactProbe.dll', ['trace.tsv'])]
for feature, harness, project, probe_dll, names in FEATURES:
    for mode in ['release', 'native-aot']:
        @step(f'{feature}-{mode}', R / feature / mode)
        def _(feature=feature, harness=harness, project=project, names=names, mode=mode):
            V = module(harness, 'verify')
            out = R / feature / mode
            V.play(build(P / project, mode, base / 'products' / feature), out)
            return {n: sha(out / n) for n in names if (out / n).exists()}
    @step(f'{feature}-compare')
    def _(feature=feature, harness=harness):
        V = module(harness, 'verify')
        outs = [R / feature / m for m in ['release', 'native-aot']]
        if hasattr(V, 'paths'): V.paths.CAPTURES[:] = V.CAPTURES; V = V.paths  # BarrageLab.Lines' __main__ does the same
        if hasattr(V, 'compare'): V.compare(*outs)
        else: assert len({(o / 'trace.tsv').read_bytes() for o in outs}) == 1, 'traces differ'  # TileCollision has no compare
        return {'trace.tsv': sha(R / feature / 'release/trace.tsv')}
    # Same Release player with the JIT's AVX2 group (FMA included in .NET 10) disabled: must not change the trace now.
    @step(f'{feature}-release-noavx2', R / feature / 'release-noavx2')
    def _(feature=feature, harness=harness, project=project, names=names):
        V = module(harness, 'verify')
        out = R / feature / 'release-noavx2'
        os.environ['DOTNET_EnableAVX2'] = '0'
        try: V.play(build(P / project, 'release', base / 'products' / feature), out)
        finally: os.environ.pop('DOTNET_EnableAVX2')
        same = (out / 'trace.tsv').read_bytes() == (R / feature / 'release/trace.tsv').read_bytes()
        assert same, 'DOTNET_EnableAVX2=0 changed the Release trace'
        return {'trace.tsv': sha(out / 'trace.tsv')}

summary['done'] = time.strftime('%Y-%m-%d %H:%M:%S')
persist()
print('SUMMARY', {k: v.get('status') for k, v in summary.items() if isinstance(v, dict) and 'status' in v}, flush=True)
