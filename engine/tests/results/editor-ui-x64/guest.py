"""Runs inside the Windows x64 / Debian x64 guest: roadmap 12.8 packaged-editor regression (6.8 operation check,
12.x docking/theme/panel harnesses, prefab and rebinding Scene Play). Resumable: finished steps are skipped."""
from pathlib import Path
import hashlib, importlib.util, json, os, platform, runpy, shutil, subprocess as sp, sys, tarfile, time, traceback, zipfile

base = Path.cwd()
win = os.name == 'nt'
rid = 'win-x64' if win else 'linux-x64'
R = base / 'results'
R.mkdir(exist_ok=True)
os.environ['MONOON_PLAYERS_DIR'] = str(base / 'templates')
os.environ['MONOON_CLI'] = str(base / 'cli/MoNoon.Cli.dll')
os.environ['DOTNET_PROCESSOR_COUNT'] = '4' if win else '2'
os.environ.setdefault('SDL_AUDIO_DRIVER', 'dummy')
QA, P = base / 'qa', base / 'projects'
# TCG emulation (x86 on an ARM Mac) is roughly 10-30x slower than the Mac: scale harness subprocess timeouts and,
# through MONOON_QA_SLOW, the startup hooks' waits. Neither changes what is checked.
TCG = 15
os.environ['MONOON_QA_SLOW'] = os.environ.get('MONOON_QA_SLOW', '10')
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
if win:
    # The guest desktop is 1280x800 (a 1920x1080 QEMU VGA framebuffer, pillarboxed). 1600/1920px editor windows need a wider
    # desktop: switch the mode for this session only (flag 0: not written to the registry, gone after logoff/reboot).
    import ctypes
    from ctypes import wintypes as wt
    class DEVMODE(ctypes.Structure):
        _fields_ = [('name', wt.WCHAR * 32), ('spec', wt.WORD), ('driver', wt.WORD), ('size', wt.WORD), ('extra', wt.WORD), ('fields', wt.DWORD),
                    ('pos_x', wt.LONG), ('pos_y', wt.LONG), ('orientation', wt.DWORD), ('fixed', wt.DWORD), ('color', ctypes.c_short), ('duplex', ctypes.c_short),
                    ('yres', ctypes.c_short), ('tt', ctypes.c_short), ('collate', ctypes.c_short), ('form', wt.WCHAR * 32), ('logpixels', wt.WORD),
                    ('bpp', wt.DWORD), ('width', wt.DWORD), ('height', wt.DWORD), ('flags', wt.DWORD), ('freq', wt.DWORD), ('rest', wt.DWORD * 8)]
    u32 = ctypes.windll.user32
    dm = DEVMODE(); dm.size = ctypes.sizeof(DEVMODE)
    modes, i = set(), 0
    while u32.EnumDisplaySettingsW(None, i, ctypes.byref(dm)): modes.add((dm.width, dm.height)); i += 1
    u32.EnumDisplaySettingsW(None, -1, ctypes.byref(dm))
    before = (dm.width, dm.height)
    code = None
    if before[0] < 1920 and (1920, 1080) in modes:
        dm.width, dm.height, dm.fields = 1920, 1080, 0x80000 | 0x100000
        code = u32.ChangeDisplaySettingsW(ctypes.byref(dm), 0)
        time.sleep(3)
    u32.EnumDisplaySettingsW(None, -1, ctypes.byref(dm))
    display = {'before': before, 'after': (dm.width, dm.height), 'change_code': code, 'modes': sorted(modes)[-6:]}
    print('Display', display, flush=True)
summary['host'] = {'display': display if win else 'Xvfb 2560x1440x24', 'rid': rid, 'platform': platform.platform(), 'machine': platform.machine(), 'python': sys.version.split()[0],
                   'slow': os.environ['MONOON_QA_SLOW'], 'timeout_scale': TCG}
only = (base / 'steps.txt').read_text().split() if (base / 'steps.txt').exists() else None

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def persist():
    summary_path.write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')
    if os.environ.get('MONOON_USB'):  # persist each step on the FAT image before the expired evaluation's forced shutdown
        shutil.copytree(R, Path(os.environ['MONOON_USB']) / 'results', dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns('KeyRoom', 'build-before', 'build-after', '*.bak', 'Play'))

def step(name, clean=None):
    def wrap(fn):
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

def module(path, name):
    sys.modules.pop(name, None)
    sys.path.insert(0, str(path.parent))
    try:
        spec = importlib.util.spec_from_file_location(name, path)
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

def files(out, names): return {n: sha(out / n) for n in names if (out / n).exists()}
def load(path): return json.loads(Path(path).read_text(encoding='utf-8'))

# 6.8 packaged-editor operation regression: real mouse/keyboard input, Player X 274 -> 190, Ctrl+S, Play, F5, reopen.
@step('editor68', R / 'editor68')
def _():
    V = module(QA / 'KeyRoom.Desktop/verify_editor.py', 'verify_editor')
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

# 12.1-12.5 docking: five editor launches sharing one results folder (restore/fallback need first, panels-restore needs panels).
dock = R / 'docking'
for phase in ['first', 'restore', 'fallback', 'panels', 'panels-restore']:
    @step(f'docking-{phase}')
    def _(phase=phase):
        # Resume after a forced shutdown: start the phase again from the folder as it was before the interrupted attempt.
        bak = R / f'docking.{phase}.bak'
        if phase == 'first':
            shutil.rmtree(dock, ignore_errors=True)
        elif bak.exists():
            shutil.rmtree(dock, ignore_errors=True); shutil.copytree(bak, dock)
        else:
            shutil.copytree(dock, bak)
        script('KeyRoom.EditorDocking', 'editor.py', editor, P / 'KeyRoom', base / 'probes/DockingProbe.dll', dock, phase)
        r = load(dock / f'{phase}.json')
        keep = {'first': ['baselines', 'splitter', 'play', 'custom', 'current', 'current_inspector', 'window', 'default_slots', 'captures', 'screen'],
                'restore': ['restored', 'restored_inspector', 'restored_window', 'reset', 'reopened', 'baseline'],
                'fallback': ['layout', 'window', 'slots'],
                'panels': ['opened', 'replaced', 'floats', 'game', 'tools', 'console_floating', 'layout_before_exit', 'captures'],
                'panels-restore': ['console_restored', 'animator_fallback', 'captures']}[phase]
        return {k: r.get(k) for k in keep}

# 12.6 theme tokens: Inspector rows at 1280/1920, long name, former windows as panels.
@step('theme', R / 'theme')
def _():
    out = R / 'theme'
    script('KeyRoom.EditorTheme', 'editor.py', editor, P / 'KeyRoom', base / 'probes/EditorThemeProbe.dll', out)
    r = load(out / 'result.json')
    return {'inspections': [{k: v for k, v in i.items() if k != 'rows'} | {'label_x': sorted({x['label_x'] for x in i['rows']}), 'field_x': sorted({x['field_x'] for x in i['rows']}),
                             'rows': len(i['rows'])} for i in r['inspections']], 'long_name': r['long_name'], 'windows': [w['name'] for w in r['windows']], 'tokens': r['tokens']}

# 12.7 panel details incl. the Release build before/after the eye/lock session (built here with the packaged CLI).
@step('panels', R / 'panels')
def _():
    out = R / 'panels'
    script('KeyRoom.EditorPanelsDetail', 'editor.py', editor, P / 'KeyRoom', base / 'probes/PanelsDetailProbe.dll', out)
    first = load(out / 'first.json')
    return {'scene': load(out / 'save.json')['scene']['sha'], 'hierarchy_search': first['hierarchy_search'], 'alignment': first['alignment'],
            'console': {k: first['console'][k] for k in ('counts', 'all', 'without_info', 'collapsed', 'collapsed_all', 'searched')},
            'project': first['project'], 'restart_sha': load(out / 'restart.json')['restart']['sha'], 'captures': first.get('captures')}

# 10.4 prefab structure in the editor (prepared on the Mac with the harness's own prepare step).
@step('prefab-editor', R / 'prefab-editor')
def _():
    out = R / 'prefab-editor'
    script('KeyRoom.PrefabStructure', 'editor.py', editor, P / 'prefab/KeyRoom', base / 'probes/PrefabStructureProbe.dll', out)
    return {'editor.txt': (out / 'editor.txt').read_text(encoding='utf-8')}

REB = ['rebind.trace', 'restart.trace', 'restored.trace', 'rebind.player.settings', 'restart.player.settings', 'restored.player.settings']
@step('rebinding-scene-play', R / 'rebinding-scene-play')
def _():
    out = R / 'rebinding-scene-play'
    script('KeyRoom.Rebinding', 'editor.py', editor, P / 'rebinding/KeyRoom', base / 'probes/EditorProbe.dll', out)
    return files(out, REB)

# Diagnostic only (not the repo harness), as in the 10.2-10.8 guest run: the trace script gives a pending additive scene
# 20 ms of real time per replay frame. Under TCG the controls screen can still be loading when Enter arrives: 200 ms.
@step('rebinding-scene-play-wait200', R / 'rebinding-scene-play-wait200')
def _():
    copy = P / 'rebinding-wait200/KeyRoom'
    if not copy.exists(): shutil.copytree(P / 'rebinding/KeyRoom', copy, ignore=shutil.ignore_patterns('.monoon'))
    trace = copy / 'Scripts/TraceBindings.cs'
    text = trace.read_text(encoding='utf-8')
    assert text.count('Thread.Sleep(20)') == 1 or 'Thread.Sleep(200)' in text
    trace.write_text(text.replace('Thread.Sleep(20)', 'Thread.Sleep(200)'), encoding='utf-8')
    out = R / 'rebinding-scene-play-wait200'
    script('KeyRoom.Rebinding', 'editor.py', editor, copy, base / 'probes/EditorProbe.dll', out)
    return files(out, REB)

summary['done'] = time.strftime('%Y-%m-%d %H:%M:%S')
persist()
print('SUMMARY', {k: v.get('status') for k, v in summary.items() if isinstance(v, dict) and 'status' in v}, flush=True)
