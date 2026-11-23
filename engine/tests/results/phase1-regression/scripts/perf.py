"""phase1 headline timings, base (git archive b524375, own templates) vs cand (wip/phase1), 5 alternating rounds.
Usage: python3 perf.py <out dir>. QEMU guest must be stopped (recorded in qemu.txt)."""
import csv, json, os, statistics, subprocess as sp, sys
from pathlib import Path

R = Path(__file__).resolve().parent
W = Path('/Users/paare/codex_project/brutal_engine/.worktrees/phase1')
TREES = {'base': R / 'base', 'cand': W}
OUT = Path(sys.argv[1]); OUT.mkdir(parents=True, exist_ok=False)
RUNS = OUT / 'runs'; RUNS.mkdir()
HEADLESS = dict(SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')


def qemu(): return sp.run(['pgrep', '-f', 'qemu-system'], capture_output=True).returncode == 0


def contacts(csv_path):
    frames = {}
    with open(csv_path, newline='') as f:
        for r in csv.DictReader(f):
            n = int(r['frame'])
            if 11 <= n <= 150 and r['name'] == 'Physics.Contacts':
                frames[n] = frames.get(n, 0.0) + float(r['inclusive_us'])
    return statistics.median(frames.get(n, 0.0) for n in range(11, 151))


def collision(tree, out):
    o = R / 'perf' / tree / 'collision'
    sp.run([sys.executable, str(W / 'engine/tests/CollisionLab.FrameProfiler/replay.py'),
            str(o / 'Development/CollisionLab.app/Contents/MacOS/MoNoonPlayer'),
            str(o / 'CollisionLab.app/Contents/MacOS/MoNoonPlayer'), str(out)], check=True, capture_output=True)
    load = json.loads((out / 'summary.json').read_text())['loading']
    return {'contacts_us': contacts(out / 'development.csv'), 'loading_frame_ms': load['frame_ms'],
            'scene_loading_ms': load['loading_ms'], 'loading_alloc_b': load['alloc_bytes']}


def barrage(tree, out):
    res = {}
    for mode, sub in (('release', ''), ('aot', 'NativeAOT/')):
        exe = R / 'perf' / tree / f'BarrageLab/{sub}BarrageLab.app/Contents/MacOS/MoNoonPlayer'
        o = out / mode
        sp.run([sys.executable, str(W / 'engine/tests/BarrageLab.PrefabPools/replay.py'), str(exe), str(o)], check=True, capture_output=True)
        s = json.loads((o / 'summary.json').read_text())
        res |= {f'inst_{mode}_mean_us': s['instantiate']['mean_us'], f'inst_{mode}_p95_us': s['instantiate']['p95_us'],
                f'inst_{mode}_bytes': s['instantiate']['bytes_per_frame'], f'hits_{mode}': s['instantiate']['hits']}
    return res


def bench(tree, out):
    dll = TREES[tree] / 'engine/.artifacts/bin/Physics2D/release/Physics2D.dll'
    res = {}
    for mode, args in (('static', []), ('tiles', ['tiles', str(TREES[tree])])):
        text = sp.run(['dotnet', str(dll), *args], check=True, capture_output=True, text=True).stdout
        (out.parent / f'{out.name}-bench-{mode}.txt').write_text(text)
        v = {}
        for line in text.splitlines():
            if ': median ' in line:
                name, rest = line.split(': median ')
                v[name] = float(rest.split()[0])
        res |= {f'{mode}_contacts_ms': v['Physics.Contacts'], f'{mode}_alloc_b': v['Allocated']}
        if mode == 'static': res['static_rigidbodies_ms'] = v['Physics.Rigidbodies']
    return res


def text(tree, out):
    exe = R / 'textbench' / ('before' if tree == 'base' else 'after') / 'out' / ('before' if tree == 'base' else 'after')
    t = sp.run([str(exe)], check=True, capture_output=True, text=True).stdout
    out.with_suffix('.text.txt').write_text(t)
    line = next(l for l in t.splitlines() if l.startswith('cached frame'))
    return {'text_cached_us': float(line.split('median ')[1].split()[0]), 'text_bytes': float(line.split(', ')[-1].split()[0])}


rows = {'base': [], 'cand': []}
meta = {'qemu_running': [], 'order': []}
for i in range(1, 6):
    order = ('base', 'cand') if i % 2 else ('cand', 'base')
    meta['order'].append(order)
    for tree in order:
        meta['qemu_running'].append(qemu())
        r = {'round': i}
        r |= collision(tree, RUNS / f'collision-{tree}-{i}')
        r |= barrage(tree, RUNS / f'barrage-{tree}-{i}')
        r |= bench(tree, RUNS / f'bench-{tree}-{i}')
        r |= text(tree, RUNS / f'text-{tree}-{i}')
        rows[tree].append(r)
        print(tree, json.dumps(r), flush=True)
summary = {}
for tree in rows:
    for key in rows[tree][0]:
        if key == 'round': continue
        v = sorted(x[key] for x in rows[tree])
        summary.setdefault(key, {})[tree] = {'median': v[2], 'min': v[0], 'max': v[-1]}
for key, s in summary.items():
    b, c = s['base'], s['cand']
    win = abs(b['median'] - c['median']) > (b['max'] - b['min']) + (c['max'] - c['min'])
    print(f"{key}: base {b['median']} [{b['min']}-{b['max']}] cand {c['median']} [{c['min']}-{c['max']}] {'DIFF' if win else 'overlap'}")
(OUT / 'timing.json').write_text(json.dumps({'meta': meta, 'rows': rows, 'summary': summary}, indent=1))
