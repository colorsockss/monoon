"""Guest (run folder): swap Engine.Core.dll variants into a copy of the Release (JIT) game and replay each scene with and
without DOTNET_EnableAVX2=0 (which, in .NET 10, also disables FMA). Identical traces = that variant is ISA-independent.
v0 = before the fix (helpers present, call sites unchanged), v1 = v0 + only the motor carry block fused,
v2 = the fix minus the motor carry block, v3 = v0 + only Lines2D.cs (paths, lines) fused, fix = shipped Engine.Core.
Args (optional): features variants, e.g. `paths v3`."""
from pathlib import Path
import hashlib, importlib.util, json, os, shutil, subprocess as sp, sys
base = Path.cwd(); out = base / 'results/bisect'; out.mkdir(parents=True, exist_ok=True)
_run = sp.run
sp.run = lambda *a, timeout=None, **k: _run(*a, timeout=None if timeout is None else timeout * 15, **k)
def module(h):
    sys.path.insert(0, str(base / 'qa' / h)); sys.modules.pop('verify', None)
    spec = importlib.util.spec_from_file_location('verify', base / 'qa' / h / 'verify.py')
    m = importlib.util.module_from_spec(spec); sys.modules['verify'] = m; spec.loader.exec_module(m); sys.path.pop(0); return m
report = json.loads((out / 'bisect.json').read_text()) if (out / 'bisect.json').exists() else {}
only_f = sys.argv[1].split(',') if len(sys.argv) > 1 else None
only_v = sys.argv[2].split(',') if len(sys.argv) > 2 else ['v0', 'v1', 'v2', 'fix']
for feature, harness in [('joints', 'PlatformerLab.Joints'), ('paths', 'PlatformerLab.Paths')]:
    if only_f and feature not in only_f: continue
    game = base / 'bisect' / feature
    shutil.rmtree(game, ignore_errors=True)
    shutil.copytree(base / 'products' / feature / 'PlatformerLab-linux-x64', game, symlinks=True)
    shipped = (game / 'Engine.Core.dll').read_bytes()
    V = module(harness)
    for variant in only_v:
        (game / 'Engine.Core.dll').write_bytes(shipped if variant == 'fix' else (base / f'variants/Engine.Core.{variant}.dll').read_bytes())
        row = {}
        for isa in ['default', 'noavx2']:
            o = out / f'{feature}-{variant}-{isa}'; shutil.rmtree(o, ignore_errors=True)
            if isa == 'noavx2': os.environ['DOTNET_EnableAVX2'] = '0'
            try: V.play(game / 'MoNoonPlayer', o)
            finally: os.environ.pop('DOTNET_EnableAVX2', None)
            row[isa] = hashlib.sha256((o / 'trace.tsv').read_bytes()).hexdigest()[:16]
        row['same'] = row['default'] == row['noavx2']
        report[f'{feature} {variant}'] = row
        print(feature, variant, row, flush=True)
        (out / 'bisect.json').write_text(json.dumps(report, indent=1) + '\n')
    shutil.rmtree(game)
