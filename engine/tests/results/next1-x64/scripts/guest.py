"""Runs inside the Debian x64 guest (wip/next1 Linux x64 regression, games only: no editor). cwd = run folder on a RAM tmpfs.

- Harness steps come from repo/engine/tests/harnesses.json, as engine/scripts/run-harness.py writes them, limited to the
  Release and Native AOT modes and rewritten for this host: ./monoon build -> the Mac-built CLI with --target linux-x64
  (repo/monoon is monoon-guest), <Name>.app/Contents/MacOS/MoNoonPlayer -> <Name>-linux-x64/MoNoonPlayer, osx-arm64 ->
  linux-x64, no GUI lock. Steps that need Scene Play output are left out (their modes include scene-play). Both Release and
  Native AOT are built here, two jobs at a time.
- After each harness: --smoke-test and file/size facts for each built player, a byte comparison of every Release output
  folder with its Native AOT twin, and the outputs (no builds or project copies) copied to results/h/<id>.
- Combined builds for the last jobs (user instruction mid-run): KeyRoom.WorldCheckpoints on the KeyRoom.InspectorComponents
  build, AudioLab.Vorbis on AudioLab.Buses's default build. InventoryLab.ScriptFields replays the project the Mac editor saved.
- Extra steps: packages built on the Mac for linux-x64 (tar.gz layout, libSDL3.so link, deps.json, smoke, replays), a SpriteLab
  package built here, the Herbalist replay, the AOT size variants (15.9) and ./monoon test --tier fast.
Resumable: passed steps are skipped; steps.txt (space-separated names) runs only those. Timeouts of harness subprocesses are
scaled by site/sitecustomize.py (MONOON_TIMEOUT_SCALE); MONOON_QA_SLOW scales probe-style waits."""
from pathlib import Path
import hashlib, json, os, platform, re, shutil, stat, subprocess as sp, sys, tarfile, time, traceback

base = Path.cwd()
repo = base / 'repo'
rid = 'linux-x64'
R = base / 'results'
R.mkdir(exist_ok=True)
P = Path('/tmp/products')  # tmpfs, removed after each step
os.environ.update(MONOON_PLAYERS_DIR=str(repo / 'player-templates'), MONOON_QA_SLOW=os.environ.get('MONOON_QA_SLOW', '10'),
                  PYTHONPATH=str(base / 'site'), MONOON_TIMEOUT_SCALE='15', DOTNET_PROCESSOR_COUNT='2')
os.environ['RestoreSources'] = os.environ['RestoreSources'] + ';' + str(base / 'feed')
CLI = repo / 'engine/.artifacts/bin/Engine.Cli/release/MoNoon.Cli.dll'
sys.path.insert(0, str(base / 'site')); import sitecustomize  # noqa: E402,F401 (scaled subprocess timeouts here as well)
NOVIDEO = dict(SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')

summary_path = R / 'summary.json'
summary = json.loads(summary_path.read_text()) if summary_path.exists() else {}
summary['host'] = {'rid': rid, 'platform': platform.platform(), 'machine': platform.machine(), 'python': sys.version.split()[0],
                   'dotnet': sp.run(['dotnet', '--version'], capture_output=True, text=True).stdout.strip(),
                   'slow': os.environ['MONOON_QA_SLOW'], 'timeout_scale': os.environ['MONOON_TIMEOUT_SCALE']}
only = (base / 'steps.txt').read_text().split() if (base / 'steps.txt').exists() else None


def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def persist(): summary_path.write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')


import threading
LOCK = threading.Lock()


def run_step(name, fn):
    if only and name not in only: return
    if not only and summary.get(name, {}).get('status') == 'pass': return  # failed steps run again
    print(f'=== STEP {name} {time.strftime("%H:%M:%S")}', flush=True)
    t0 = time.time()
    try:
        result = {'status': 'pass', **(fn() or {})}
        print(f'=== PASS {name}', flush=True)
    except BaseException as e:
        if isinstance(e, KeyboardInterrupt): raise
        tb = traceback.format_exc()
        print(tb, flush=True)
        result = {'status': 'fail', 'error': tb[-4000:], **getattr(e, 'detail', {})}
        print(f'=== FAIL {name}', flush=True)
    result['seconds'] = round(time.time() - t0)
    with LOCK:
        summary[name] = result
        persist()


def step(name):
    def wrap(fn):
        run_step(name, fn)
        return fn
    return wrap


class Failed(Exception):
    def __init__(self, message, detail): super().__init__(message); self.detail = detail


# ---------------------------------------------------------------- built players: smoke test, files, sizes
FORBIDDEN = ('SkiaSharp', 'Avalonia', 'HarfBuzz', 'Engine.Editor', 'Engine.Authoring', 'Microsoft.CodeAnalysis', 'MoNoon.dll')
NATIVE = ('libSDL3.so', 'libSDL3.so.0', 'libSDL3_ttf.so', 'libSDL3_mixer.so', 'libbox2d.so')


def facts(game):
    """Files, bytes (symlinks not counted), native libraries, the libSDL3.so link and deps.json consistency."""
    files = [p for p in game.rglob('*') if p.is_file() and not p.is_symlink()]
    info = {'files': len(files), 'bytes': sum(p.stat().st_size for p in files), 'exe_bytes': (game / 'MoNoonPlayer').stat().st_size,
            'exe_mode': stat.filemode((game / 'MoNoonPlayer').stat().st_mode),
            'native': {n: ('-> ' + os.readlink(game / n)) if (game / n).is_symlink() else (game / n).stat().st_size
                       for n in NATIVE if (game / n).exists() or (game / n).is_symlink()},
            'forbidden': [str(p.relative_to(game)) for p in files if any(f in p.name for f in FORBIDDEN)]}
    deps = game / 'MoNoonPlayer.deps.json'
    if deps.exists():
        listed = {'runtime': set(), 'native': set()}
        for lib in json.loads(deps.read_text())['targets'].values():
            for entry in lib.values():
                for kind in listed: listed[kind] |= {Path(f).name for f in entry.get(kind, {})}
        # Native files the build leaves out (15.1, 15.11) stay listed in deps.json (size-wave1: check they do no harm on x64).
        # A missing managed assembly would stop the host, so only that one fails the check.
        info['deps_missing'] = sorted(n for n in listed['runtime'] if not (game / n).exists())
        info['deps_native_not_shipped'] = sorted(n for n in listed['native'] if not (game / n).exists())
    return info


def smoke(exe, where):
    profile = where / 'smoke-profile'
    shutil.rmtree(profile, ignore_errors=True)
    env = dict(os.environ, MONOON_USER_DATA=str(profile), **NOVIDEO)
    r = sp.run([str(exe), '--smoke-test', str(profile)], env=env, capture_output=True, text=True, timeout=1800, cwd=exe.parent)
    shutil.rmtree(profile, ignore_errors=True)
    return {'code': r.returncode, 'tail': (r.stdout + r.stderr).strip().splitlines()[-2:]}


def products(work):
    found = []
    for exe in sorted(work.rglob('MoNoonPlayer')):
        if exe.is_file() and exe.parent.name.endswith('-' + rid) and 'smoke-profile' not in exe.parts:
            found.append((exe, 'native-aot' if 'NativeAOT' in exe.parts or not (exe.parent / 'MoNoonPlayer.dll').exists() else 'release'))
    return found


# ---------------------------------------------------------------- harness steps from the manifest
MANIFEST = {h['id']: h for h in json.loads((repo / 'engine/tests/harnesses.json').read_text())['harnesses']}
SELECTED = {'release', 'native-aot'}
# ./monoon test replay compiles Engine.Checks; the Herbalist replay below plays the same recording in both players.
DROP = {'InventoryLab.Replay': ['./monoon test replay']}
# The Release and Native AOT plays replay the project the editor phase saved (Inspector edits); Linux has no editor, so the
# copy the Mac editor saved (InventoryLab.ScriptFields edit + reopen on this commit) replaces the prepare step.
REPLACE = {'InventoryLab.ScriptFields': ('python3 $qa/verify.py prepare samples/InventoryLab "$work/InventoryLab"',
                                         f'cp -R "{base}/mac-edited/InventoryLab" "$work/InventoryLab"')}


def rewrite(cmd):
    for hid, (old, new) in REPLACE.items(): cmd = cmd.replace(old, new) if (base / 'mac-edited').exists() else cmd
    cmd = cmd.replace('$lock ', '').replace('osx-arm64', rid)
    cmd = re.sub(r'(\w+)\.app/Contents/MacOS/MoNoonPlayer', r'\1-' + rid + '/MoNoonPlayer', cmd)
    return re.sub(r'(\w+)\.app\b', r'\1-' + rid, cmd)


def pairs(work):
    """Each Release output folder next to its Native AOT twin (release/aot, release/native-aot, release-x/aot-x, runs/...)."""
    out = {}
    for d in sorted(p for p in work.rglob('release*') if p.is_dir() and not p.name.endswith('-' + rid)):
        if any(x.startswith('builds') or x == 'NativeAOT' for x in d.relative_to(work).parts[:-1]): continue
        twin = next((d.with_name(d.name.replace('release', t, 1)) for t in ('native-aot', 'aot') if d.with_name(d.name.replace('release', t, 1)).is_dir()), None)
        if not twin: continue
        a = {p.relative_to(d).as_posix(): p for p in d.rglob('*') if p.is_file() and not p.is_symlink()}
        b = {p.relative_to(twin).as_posix(): p for p in twin.rglob('*') if p.is_file() and not p.is_symlink()}
        same = sorted(k for k in a.keys() & b.keys() if a[k].read_bytes() == b[k].read_bytes())
        out[d.relative_to(work).as_posix() + ' = ' + twin.name] = {
            'same': len(same), 'differ': sorted(k for k in a.keys() & b.keys() if k not in same),
            'only_release': sorted(a.keys() - b.keys()), 'only_aot': sorted(b.keys() - a.keys())}
    return out


def keep_outputs(work, dest):
    """Copy outputs (traces, captures, json, logs) but not builds, products, project copies or raw PCM."""
    def skip(d):
        return (d.name.startswith(('builds', 'barrage-release', 'barrage-aot')) and not d.name.endswith('replay')) or d.name in ('NativeAOT', 'exports') \
            or d.name.endswith('-' + rid) or (d / 'GameSettings').exists() or d.name == 'smoke-profile'
    for src in work.rglob('*'):
        if not src.is_file() or src.is_symlink() or src.suffix == '.f32' or src.stat().st_size > 8_000_000: continue
        rel = src.relative_to(work)
        if any(skip(work.joinpath(*rel.parts[:i + 1])) for i in range(len(rel.parts) - 1)): continue
        (dest / rel).parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dest / rel)


def harness(hid, extra=None):
    h = MANIFEST[hid]
    work, out = P / hid, R / 'h' / hid
    shutil.rmtree(work, ignore_errors=True); shutil.rmtree(out, ignore_errors=True)
    work.mkdir(parents=True); out.mkdir(parents=True)
    steps = [s for s in h['steps'] if s['kind'] in ('prepare', 'build', 'play', 'compare') and set(s.get('modes', [])) <= SELECTED
             and not any(d in s['run'] for d in DROP.get(hid, []))]
    lines = ['set -e', f'cd "{repo}"', f'qa=engine/tests/{hid}', f'work="{work}"', 'bin=engine/.artifacts/bin']
    if hid == 'AudioLab.Vorbis': lines.append('export MONOON_SCALE_MONOTONIC=1')
    for i, s in enumerate(steps):
        lines += [f'echo "@@step {i} {s["kind"]} {",".join(s.get("modes", [])) or "-"}"; t=$(date +%s)', rewrite(s['run']),
                  f'echo "@@done {i} $(( $(date +%s) - t ))"']
    (out / 'run.sh').write_text('\n'.join(lines) + '\n')
    with (out / 'run.log').open('wb') as log:
        code = sp.run(['bash', str(out / 'run.sh')], stdout=log, stderr=sp.STDOUT).returncode
    text = (out / 'run.log').read_text(errors='replace')
    done = {int(m) for m in re.findall(r'^@@done (\d+)', text, re.M)}
    detail = {'code': code, 'steps': [{'kind': s['kind'], 'modes': s.get('modes'), 'seconds': next((int(x) for i2, x in re.findall(r'^@@done (\d+) (\d+)', text, re.M) if int(i2) == i), None),
                                       'ok': i in done} for i, s in enumerate(steps)]}
    built = {}
    for exe, mode in products(work):
        key = f'{exe.parent.name}/{mode}'
        if key in built: continue
        built[key] = facts(exe.parent) | {'smoke': smoke(exe, work)}
    detail['products'] = built
    if extra and code == 0:
        try: detail['extra'] = extra(work, out)
        except Exception: detail['extra_error'] = traceback.format_exc()[-3000:]; code = code or 99
    detail['pairs'] = pairs(work)
    keep_outputs(work, out / 'out')
    shutil.rmtree(work, ignore_errors=True)
    bad = [k for k, v in built.items() if v['smoke']['code'] != 0 or v['forbidden'] or v.get('deps_missing')]
    if code or bad:
        tail = [l for l in text.splitlines() if re.search(r'(Error|Exception|FAIL)\b', l)][-3:]
        raise Failed(f'{hid}: exit {code}, bad products {bad}, {tail}', detail)
    return detail


def herbalist(exe, folder):
    folder.mkdir(parents=True, exist_ok=True)
    profile = folder / 'profile'
    env = dict(os.environ, MONOON_USER_DATA=str(profile), **NOVIDEO)
    env.pop('DISPLAY', None)
    sp.run([str(exe), '--replay-input', str(repo / 'engine/tests/fixtures/InventoryLab/Herbalist.inputreplay'), str(profile)],
           env=env, check=True, timeout=3600, cwd=exe.parent)
    actual, expected = (profile / 'slot-001.save').read_bytes(), (repo / 'engine/tests/fixtures/InventoryLab/Herbalist.save').read_bytes()
    (folder / 'herbalist.save').write_bytes(actual)
    shutil.rmtree(profile)
    info = {'bytes': len(actual), 'sha256': hashlib.sha256(actual).hexdigest(), 'equal_fixture': actual == expected}
    assert actual == expected, info
    return info


def replay_extra(work, out):
    return {mode: herbalist(exe, out / f'herbalist-{mode}') for exe, mode in products(work)}


# ---------------------------------------------------------------- packages built on the Mac (and SpriteLab here)
def unpack(tgz, dest):
    dest.mkdir(parents=True)
    with tarfile.open(tgz) as t:
        members = {m.name: m for m in t.getmembers()}
        t.extractall(dest)
    root = next(dest.iterdir())
    top = root.name + '/'
    listing = {n[len(top):]: ('link -> ' + m.linkname if m.issym() else stat.filemode(m.mode | (stat.S_IFDIR if m.isdir() else stat.S_IFREG)))
               for n, m in members.items() if n.startswith(top) and n != root.name}
    return root, listing


def layout(root, listing, game):
    info = facts(root)
    sdl = listing.get('libSDL3.so', '')
    info['tar_libSDL3.so'] = sdl
    info['tar_modes'] = {n: listing.get(n) for n in ['MoNoonPlayer', 'install-desktop-entry.sh', 'Game/GameSettings', 'libSDL3.so.0',
                                                     'libSDL3_ttf.so', 'libSDL3_mixer.so', 'libbox2d.so', 'MoNoonPlayer.deps.json'] if n in listing}
    exec_ok = [n for n, m in listing.items() if not m.startswith('link') and m[3] == 'x' and not m.startswith('d')]
    info['executables'] = sorted(exec_ok)
    assert sdl == 'link -> libSDL3.so.0', sdl
    assert listing['MoNoonPlayer'][3] == 'x' and listing['Game/GameSettings'] == '-rw-r--r--', info['tar_modes']
    assert not info['forbidden'] and not info.get('deps_missing'), info
    return info


def load(name, path):
    import importlib.util
    if name in sys.modules: return sys.modules[name]
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec); sys.modules[name] = m; spec.loader.exec_module(m)
    return m


def keyroom_checkpoints(product, folder):
    ci = load('ci_world', repo / 'engine/tests/KeyRoom.WorldCheckpoints/ci.py')
    folder.mkdir(parents=True)
    ci.check_runtime_package(product, 'release' if (product / 'MoNoonPlayer.dll').exists() else 'native-aot')
    ci.check_player(product, rid, folder)
    return sorted(p.name for p in folder.rglob('*.save'))


def gametext(name, exe, folder):
    r = sp.run([sys.executable, str(repo / 'engine/tests/Samples.GameText/verify.py'), 'play', name, str(exe), str(folder)],
               capture_output=True, text=True)
    (folder.parent / (folder.name + '.log')).write_text(r.stdout + r.stderr)
    assert r.returncode == 0, (r.stdout + r.stderr)[-2000:]
    return {p.name: sha(p) for p in sorted((folder / 'captures').glob('*.png'))}


def package_checks(game, root, listing, out):
    info = {'layout': layout(root, listing, game), 'smoke': smoke(root / 'MoNoonPlayer', out)}
    assert info['smoke']['code'] == 0, info['smoke']
    if game == 'KeyRoom' and (root / 'install-desktop-entry.sh').exists():
        home = out / 'home'; home.mkdir()
        env = {k: v for k, v in os.environ.items() if k != 'XDG_DATA_HOME'} | {'HOME': str(home)}
        sp.run([str(root / 'install-desktop-entry.sh')], env=env, capture_output=True, text=True, check=True)
        entries = list((home / '.local/share/applications').glob('*.desktop'))
        assert len(entries) == 1, entries
        info['desktop'] = entries[0].read_text()
        info['checkpoints'] = keyroom_checkpoints(root, out / 'checkpoints')
    if game == 'InventoryLab':
        info['herbalist'] = herbalist(root / 'MoNoonPlayer', out / 'herbalist')
        r = sp.run([sys.executable, str(repo / 'engine/tests/InventoryLab.Replay/check_recovery.py'), str(root / 'MoNoonPlayer'),
                    str(repo / 'engine/tests/fixtures/InventoryLab'), str(out / 'recovery')], capture_output=True, text=True)
        info['recovery'] = (r.stdout + r.stderr).strip().splitlines()[-3:]
        assert r.returncode == 0, info['recovery']
    if game == 'SpriteLab':
        assert not {'libSDL3_ttf.so', 'libSDL3_mixer.so', 'libbox2d.so'} & set(info['layout']['native']), info['layout']['native']
        info['captures'] = gametext('SpriteLab', root / 'MoNoonPlayer', out / 'gametext')
    if game == 'PlatformerLab':  # the Joints-prepared copy
        r = sp.run([sys.executable, str(repo / 'engine/tests/PlatformerLab.Joints/verify.py'), 'play', str(root / 'MoNoonPlayer'), str(out / 'joints')],
                   capture_output=True, text=True)
        (out / 'joints.log').write_text(r.stdout + r.stderr)
        assert r.returncode == 0, (r.stdout + r.stderr)[-2000:]
        info['joints'] = {n: sha(out / 'joints' / n) for n in ('trace.tsv', 'result.json') if (out / 'joints' / n).exists()}
    return info


for game in ['KeyRoom', 'InventoryLab', 'SpriteLab', 'PlatformerLab']:
    @step(f'mac-package-{game}')
    def _(game=game):
        out = R / 'mac-package' / game
        shutil.rmtree(out, ignore_errors=True); out.mkdir(parents=True)
        root, listing = unpack(base / f'mac/{game}-linux-x64.tar.gz', P / 'mac' / game)
        (out / 'listing.json').write_text(json.dumps(listing, indent=1))
        (out / 'manifest.json').write_text(json.dumps({k: sha(root / k) for k, m in listing.items() if not m.startswith(('link', 'd'))}, indent=1))
        try: return package_checks(game, root, listing, out)
        finally: shutil.rmtree(P / 'mac' / game, ignore_errors=True)

# ---------------------------------------------------------------- determinism gates and engine features (Release + Native AOT)
GATES = ['ParticleLab.Trace', 'PlatformerLab.Joints', 'PlatformerLab.TileCollision', 'PlatformerLab.Paths', 'BarrageLab.Lines', 'PlatformerLab.Colliders',
         'CollisionLab.Spatial', 'BarrageLab.PrefabPools', 'InventoryLab.Replay', 'KeyRoom.InputInteractions',
         'CameraLab.Framing', 'SeedLab.Autotile', 'AnimatorLab.Conditions', 'AnimatorLab.Directions', 'PlatformerLab.Lighting',
         'InventoryLab.WorldText', 'InventoryLab.BagGrid', 'SeedLab.NavigationFree', 'KeyRoom.Tweens', 'PlatformerLab.Parallax']


# Combined builds (user instruction mid-run: one Release + one Native AOT guest build per sample where the prepares agree;
# proof on the Mac in README). KeyRoom.WorldCheckpoints replays its checkpoint recordings on the KeyRoom.InspectorComponents
# build (the Mac osx-arm64 Release of that project passed every ci.py fixture match); AudioLab.Vorbis's ci.py capture runs on
# AudioLab.Buses's "default" project (a byte-identical copy of samples/AudioLab).
def keyroom_extra(work, out):
    return {mode: keyroom_checkpoints(exe.parent, out / 'worldcheckpoints' / mode) for exe, mode in products(work)}


def silent_blocks(folder):
    device, mix, block = (folder / 'device.f32').read_bytes(), (folder / 'mix.f32').read_bytes(), 512 * 8
    blocks = [device[i:i + block] for i in range(0, len(device), block)]
    kept = b''.join(b for b in blocks if any(b))
    return f'{sum(not any(b) for b in blocks)}, device without them equals the capture: {kept.strip(bytes(1)) == mix.strip(bytes(1))}'


def vorbis_extra(work, out):
    import csv, types
    sys.path.insert(0, str(repo / 'engine/tests/AudioLab.Vorbis'))  # ci.py imports measure.py next to it
    C = load('ci_vorbis', repo / 'engine/tests/AudioLab.Vorbis/ci.py')
    C.time = types.SimpleNamespace(monotonic=lambda: time.monotonic() / 15, sleep=time.sleep)  # capture()'s waits, scaled for TCG
    with C.baseline_csv(rid).open() as f:  # per-architecture PCM baseline: results/audio-mixer/playback-linux-x64.csv
        rows = {row['mode']: row for row in csv.DictReader(f) if row['codec'] == 'ogg'}
    result = {}
    for mode, reference in [('release', 'release'), ('native-aot', 'aot')]:
        game = next(exe.parent for exe, m in products(work / 'builds-default') if m == mode)
        # Under TCG a slow JIT start can make SDL's disk device insert silent 512-frame blocks (phase1 saw the same); the
        # baseline checks run first, so a device-only failure is recorded and the capture repeated once.
        for attempt in (1, 2):
            folder = out / 'vorbis' / (mode if attempt == 1 else f'{mode}-retry'); folder.mkdir(parents=True)
            environment = dict(SDL_AUDIO_DRIVER='disk', SDL_AUDIO_FORMAT='F32LE', SDL_AUDIO_CHANNELS='2', SDL_AUDIO_FREQUENCY=str(C.RATE),
                               SDL_AUDIO_DISK_OUTPUT_FILE=str(folder / 'device.f32'), MONOON_AUDIO_CAPTURE=str(folder / 'mix.f32'),
                               MONOON_USER_DATA=str(folder / 'profile'))
            C.capture(game / 'MoNoonPlayer', environment, folder, None)
            try:
                result[mode] = C.compare(folder, rows[reference]); break
            except ValueError as e:
                if attempt == 2 or 'SDL disk output' not in str(e): raise
                result[mode + '-first-attempt'] = f'{e} (silent 512-frame blocks: {silent_blocks(folder)})'
            finally:
                for n in ['mix.f32', 'device.f32']: (folder / n).unlink(missing_ok=True)
    return result


# Two at a time (4 vCPUs, DOTNET_PROCESSOR_COUNT=2 each): a guest Release build takes ~8 min and a Native AOT build ~8-10 min
# under TCG. Each job has its own work folder under /tmp/products.
def vorbis_job():
    """AudioLab.Vorbis alone (its first run inside g-AudioLab stopped at a loader error after the Buses checks passed):
    the plain sample (byte-identical to Buses's default project), Release + Native AOT built here, ci.py capture/compare."""
    work, out = P / 'AudioLab.Vorbis', R / 'h' / 'AudioLab.Vorbis'
    shutil.rmtree(work, ignore_errors=True); shutil.rmtree(out, ignore_errors=True); out.mkdir(parents=True)
    shutil.copytree(repo / 'samples/AudioLab', work / 'AudioLab', ignore=shutil.ignore_patterns('.monoon*', 'Builds'))
    try:
        for mode in ['release', 'native-aot']:
            sp.run(['dotnet', str(CLI), 'build', str(work / 'AudioLab'), mode, '--target', rid, '--profile', 'distribution',
                    '--output', str(work / 'builds-default')], check=True, stdout=sp.DEVNULL)
        return {'products': {f'{e.parent.name}/{m}': facts(e.parent) for e, m in products(work)}, 'vorbis': vorbis_extra(work, out)}
    finally: shutil.rmtree(work, ignore_errors=True)


from concurrent.futures import ThreadPoolExecutor
JOBS = [('h-' + hid, lambda hid=hid: harness(hid, replay_extra if hid == 'InventoryLab.Replay' else None)) for hid in GATES]
JOBS += [('g-KeyRoom', lambda: harness('KeyRoom.InspectorComponents', keyroom_extra)),
         ('g-AudioLab', lambda: harness('AudioLab.Buses', vorbis_extra)), ('h-AudioLab.Vorbis', vorbis_job)]
if (base / 'mac-edited').exists(): JOBS.append(('h-InventoryLab.ScriptFields', lambda: harness('InventoryLab.ScriptFields')))
def guest_package_spritelab():
    out = R / 'guest-package/SpriteLab'
    shutil.rmtree(out, ignore_errors=True); out.mkdir(parents=True)
    src = P / 'src/SpriteLab'
    shutil.rmtree(src, ignore_errors=True)
    shutil.copytree(repo / 'samples/SpriteLab', src, ignore=shutil.ignore_patterns('.monoon'))
    info = {}
    for mode in ['release', 'native-aot']:
        o = P / f'out-{mode}'
        shutil.rmtree(o, ignore_errors=True); shutil.rmtree(P / f'x-{mode}', ignore_errors=True)
        sp.run(['dotnet', str(CLI), 'build', str(src), mode, '--target', rid, '--profile', 'distribution', '--package', 'on', '--output', str(o)], check=True)
        tgz = next(o.rglob('*-linux-x64.tar.gz'))
        root, listing = unpack(tgz, P / f'x-{mode}')
        (out / f'{mode}-listing.json').write_text(json.dumps(listing, indent=1))
        if mode == 'release':
            (out / 'manifest.json').write_text(json.dumps({k: sha(root / k) for k, m in listing.items() if not m.startswith(('link', 'd'))}, indent=1))
        info[mode] = package_checks('SpriteLab', root, listing, out / mode)
    for d in ['src', 'out-release', 'out-native-aot', 'x-release', 'x-native-aot']: shutil.rmtree(P / d, ignore_errors=True)
    assert info['release']['captures'] == info['native-aot']['captures'], 'Release and Native AOT captures differ'
    return info

# ---------------------------------------------------------------- 15.9: InventoryLab Linux AOT size (Size vs Speed, and the base commit)
def aot_size_variants():
    # head-size is the h-InventoryLab.Replay Native AOT build (same CLI, plain samples/InventoryLab); not built again.
    replay = summary.get('h-InventoryLab.Replay', {}).get('products', {})
    info = {'head-size': replay.get('InventoryLab-linux-x64/native-aot', 'missing')}
    # The base commit builds its own samples/InventoryLab (HEAD's sample uses 13.x/16.x APIs the base engine lacks).
    for name, cli, templates, sample in [('head-speed', base / 'cli-speed/MoNoon.Cli.dll', repo / 'player-templates', repo / 'samples/InventoryLab'),
                                         ('base-d523ec7', base / 'base/cli/MoNoon.Cli.dll', base / 'base/player-templates', base / 'base/samples/InventoryLab')]:
        done = R / 'aot-size' / name / 'facts.json'
        if done.exists(): info[name] = json.loads(done.read_text()); continue  # a finished variant from an earlier attempt
        if not cli.exists() or not sample.exists(): info[name] = 'not shipped'; continue
        shutil.rmtree(P / name, ignore_errors=True)
        src = P / name / 'InventoryLab'
        shutil.copytree(sample, src, ignore=shutil.ignore_patterns('.monoon'))
        env = dict(os.environ, MONOON_PLAYERS_DIR=str(templates))
        t0 = time.time()
        sp.run(['dotnet', str(cli), 'build', str(src), 'native-aot', '--target', rid, '--profile', 'distribution', '--output', str(P / name / 'out')], check=True, env=env)
        game = P / name / 'out/NativeAOT/InventoryLab-linux-x64'
        info[name] = facts(game) | {'build_seconds': round(time.time() - t0), 'smoke': smoke(game / 'MoNoonPlayer', P / name)}
        try: info[name]['herbalist'] = herbalist(game / 'MoNoonPlayer', R / 'aot-size' / name)
        except Exception as e: info[name]['herbalist'] = f'FAIL {e}'[:500]
        done.parent.mkdir(parents=True, exist_ok=True); done.write_text(json.dumps(info[name], indent=1))
        shutil.rmtree(P / name)
    return info

JOBS += [('guest-package-SpriteLab', guest_package_spritelab), ('aot-size-variants', aot_size_variants)]
with ThreadPoolExecutor(int(os.environ.get('MONOON_GUEST_WORKERS', '2'))) as pool:
    list(pool.map(lambda job: run_step(*job), JOBS))


# ---------------------------------------------------------------- ./monoon test --tier fast (Engine.Checks compiled here from the snapshot)
@step('monoon-test-fast')
def _():
    r = sp.run(['sh', str(repo / 'monoon'), 'test', '--tier', 'fast'], cwd=repo, capture_output=True, text=True, timeout=6 * 3600)
    (R / 'monoon-test-fast.log').write_text(r.stdout + r.stderr)
    lines = (r.stdout + r.stderr).splitlines()
    assert r.returncode == 0, [l for l in lines if 'FAIL' in l or 'Exception' in l][:5] or lines[-5:]
    return {'pass_lines': sum(l.startswith('PASS') for l in lines), 'skip_lines': sum(l.startswith('SKIP') for l in lines), 'tail': lines[-2:]}


summary['done'] = time.strftime('%Y-%m-%d %H:%M:%S')
persist()
print('SUMMARY', {k: v.get('status') for k, v in summary.items() if isinstance(v, dict) and 'status' in v}, flush=True)
