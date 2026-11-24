#!/usr/bin/env python3
"""Run MoNoon test harnesses from engine/tests/harnesses.json (docs/manual/agent-workflow.md#하네스-실행기).

  run-harness.py KeyRoom.Rebinding [more ids] [--mode release,native-aot]   run named harnesses
  run-harness.py --changed <base or range> [--broad]                        harnesses the diff touches
  run-harness.py --tier fast [--changed <base>]                             ./monoon test + headless modes of touched harnesses
  run-harness.py --tier full                                                ./monoon test + every harness, every mode
  run-harness.py --list | --check | ... --dry-run

Each harness runs its manifest steps in one bash process under a fresh work folder (default: a new temp folder, never the
repository). Steps that open a window carry $lock (engine/scripts/gui-lock.sh, or MONOON_GUI_LOCK). summary.json and one log
per harness go to --out.
"""
import argparse
import fnmatch
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
TESTS = REPO / 'engine/tests'
MANIFEST = TESTS / 'harnesses.json'
MODES = ('scene-play', 'release', 'native-aot', 'headless')
NOT_HARNESSES = {'Engine.Checks', 'Shared', 'fixtures', 'results', '__pycache__'}  # __pycache__: bytecode next to imported harness modules
APP = REPO / 'editor/MoNoon.app/Contents/MacOS/MoNoon'
TEMPLATE = REPO / 'player-templates/osx-arm64/MoNoonPlayer'


def load():
    return json.loads(MANIFEST.read_text(encoding='utf-8'))


def git(*args):
    return subprocess.run(['git', *args], cwd=REPO, check=True, capture_output=True, text=True).stdout.split('\n')


def changed_files(spec):
    """A range (a..b, a...b, x^!) as git diff takes it; otherwise a base: its merge-base to the working tree plus untracked files."""
    if '..' in spec or spec.endswith('^!'):
        files = git('diff', '--name-only', spec)
    else:
        base = git('merge-base', spec, 'HEAD')[0]
        files = git('diff', '--name-only', base) + git('ls-files', '--others', '--exclude-standard')
    return sorted({f for f in files if f})


def file_modes(path):
    """Modes a changed file can affect: editor and probe sources only Scene Play, player sources only the built games."""
    name = path.rsplit('/', 1)[-1]
    if path.startswith(('engine/src/Engine.Editor/', 'engine/tests/Shared/')): return {'scene-play'}
    if path.startswith('engine/tests/') and (name in ('editor.py', 'StartupHook.cs', 'gizmo.py') or name.endswith('.csproj')):
        return {'scene-play'}
    if path.startswith('engine/src/Engine.Player/'): return {'release', 'native-aot'}
    return set(MODES)


def matches(path, globs):
    return any(fnmatch.fnmatchcase(path, g) for g in globs)


def select(manifest, files, broad):
    """id -> (modes, 'direct' | 'broad'). Direct: the harness folder, its samples or its watch globs. Broad: manifest 'broad' globs."""
    chosen = {}
    for h in manifest['harnesses']:
        own = set(h['modes'])
        for f in files:
            direct = (f.startswith(f"engine/tests/{h['id']}/") or any(f.startswith(f'samples/{s}/') for s in h['samples'])
                      or matches(f, h['watch']))
            wide = not direct and broad and matches(f, manifest['broad'])
            if not (direct or wide): continue
            modes, kind = chosen.get(h['id'], (set(), 'broad'))
            chosen[h['id']] = (modes | (file_modes(f) & own or own), 'direct' if direct or kind == 'direct' else 'broad')
    return chosen


def gui_modes(h):
    return {m for s in h['steps'] if s.get('gui') for m in s.get('modes', h['modes'])}


def selected_steps(h, modes):
    """Steps without modes always run; a step with modes runs when all of them are selected (compare steps need every run)."""
    return [s for s in h['steps'] if set(s.get('modes', [])) <= modes]


def script(h, steps, work):
    probe = h.get('probe', '')
    lines = ['set -e', f'cd "{REPO}"', f"qa=engine/tests/{h['id']}", f'work="{work}"',
             'app=editor/MoNoon.app/Contents/MacOS/MoNoon', 'bin=engine/.artifacts/bin',
             f'probe_dll=$bin/{probe}/release/{probe}.dll', 'lock="${MONOON_GUI_LOCK:-sh engine/scripts/gui-lock.sh}"']
    for i, s in enumerate(steps):
        tag = f"{i} {s['kind']} {','.join(s.get('modes', [])) or '-'}"
        lines += [f'echo "@@step {tag}"; t=$(date +%s)', s['run'], f'echo "@@done {i} $(( $(date +%s) - t ))"']
    return '\n'.join(lines) + '\n'


def first_error(log, work):
    errors = sorted(work.rglob('error.txt'), key=lambda p: p.stat().st_mtime)
    if errors:
        text = errors[-1].read_text(encoding='utf-8', errors='replace').strip().splitlines()
        if text: return f'{errors[-1].relative_to(work)}: {text[0]}'
    tail = log.split('@@step')[-1].splitlines()[1:]
    hits = [l for l in tail if re.search(r'(Error|Exception|FAIL)\b', l) and not re.match(r'\s*(at |---)', l)]
    # The .NET 'Unhandled exception.' line or Python's final 'XError: message' line beats traceback source lines.
    best = [l for l in hits if re.match(r'(Unhandled exception|[\w.]*(Error|Exception)\b:)', l.strip())]
    return ((best or hits)[0] if hits else next((l for l in reversed(tail) if l.strip()), '')).strip()[:300]


def preflight(h, modes, build_editor):
    """Missing prerequisites as a message, or ''. Builds the packaged editor once when asked."""
    needs = set(h['needs'])
    if 'templates' in needs and modes & {'release', 'native-aot', 'headless'} and not TEMPLATE.exists():
        return f'player templates missing ({TEMPLATE.relative_to(REPO)}): run engine/scripts/bootstrap-worktree.sh'
    if 'editor' in needs and 'scene-play' in modes or any('$app' in s['run'] for s in selected_steps(h, modes)):
        if build_editor and not getattr(preflight, 'built', False):
            subprocess.run(['engine/scripts/build-editor-mac.sh'], cwd=REPO, check=True)
            preflight.built = True
        if not APP.exists(): return 'editor/MoNoon.app missing: run engine/scripts/build-editor-mac.sh (or pass --build-editor)'
    if any('clip-qa/window' in s['run'] for s in selected_steps(h, modes)) and not (REPO / 'engine/.artifacts/clip-qa/window').exists():
        return 'engine/.artifacts/clip-qa/window missing (window capture tool, built by the clip QA setup)'
    return ''


def run_harness(h, modes, out, args):
    steps = selected_steps(h, modes)
    work = out / h['id']
    shutil.rmtree(work, ignore_errors=True)
    work.mkdir(parents=True)
    text = script(h, steps, work)
    (work / 'run.sh').write_text(text, encoding='utf-8')
    row = {'id': h['id'], 'modes': sorted(modes, key=MODES.index), 'exit': 0, 'seconds': 0, 'failed_step': '', 'first_error': '',
           'log': str(work / 'run.log')}
    if args.dry_run:
        print(text)
        return row
    problem = preflight(h, modes, args.build_editor)
    if problem:
        row.update(exit=2, first_error=problem)
        return row
    start = time.time()
    with (work / 'run.log').open('wb') as log:
        try:
            code = subprocess.run(['bash', str(work / 'run.sh')], cwd=REPO, stdout=log, stderr=subprocess.STDOUT,
                                  timeout=h.get('timeout', 7200)).returncode
        except subprocess.TimeoutExpired:
            code = 124
    row['seconds'] = round(time.time() - start)
    row['exit'] = code
    if code:
        log = (work / 'run.log').read_text(encoding='utf-8', errors='replace')
        last = re.findall(r'^@@step (\d+) (\S+) (\S+)$', log, re.M)
        if last:
            i = int(last[-1][0])
            row['failed_step'] = f"{last[-1][1]} {last[-1][2]}: {steps[i]['run'][:160]}"
        row['first_error'] = first_error(log, work)
    return row


def check(manifest):
    problems = []
    ids = [h['id'] for h in manifest['harnesses']]
    folders = sorted(p.name for p in TESTS.iterdir() if p.is_dir() and p.name not in NOT_HARNESSES and not p.name.startswith('.'))
    if sorted(ids) != folders:
        problems.append(f'folders without an entry: {sorted(set(folders) - set(ids))}; entries without a folder: {sorted(set(ids) - set(folders))}')
    files = [f for f in git('ls-files') if f]
    for h in manifest['harnesses']:
        for s in h['steps']:
            if set(s.get('modes', [])) - set(h['modes']): problems.append(f"{h['id']}: step modes {s['modes']} not in {h['modes']}")
            if bool(s.get('gui')) != ('$lock' in s['run']): problems.append(f"{h['id']}: gui flag and $lock disagree: {s['run'][:80]}")
            if '$app' in s['run'] and not s.get('gui'): problems.append(f"{h['id']}: editor step without the GUI lock: {s['run'][:80]}")
        for g in h['watch']:
            if not any(fnmatch.fnmatchcase(f, g) for f in files): problems.append(f"{h['id']}: watch glob matches nothing: {g}")
        for b in h['baselines']:
            path = b.split(' (')[0]
            if not any(fnmatch.fnmatchcase(f, path) or f.startswith(path) for f in files): problems.append(f"{h['id']}: baseline not found: {b}")
        if h['steps'] and subprocess.run(['bash', '-n'], input=script(h, h['steps'], Path('/nonexistent')), text=True).returncode:
            problems.append(f"{h['id']}: steps are not valid bash")
    for p in problems: print('FAIL', p)
    print(f"{len(ids)} harnesses, {len(problems)} problems")
    return 1 if problems else 0


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('ids', nargs='*')
    p.add_argument('--changed', metavar='BASE_OR_RANGE')
    p.add_argument('--tier', choices=('fast', 'full'))
    p.add_argument('--mode', help='comma-separated: ' + ','.join(MODES))
    p.add_argument('--broad', action='store_true', help='with --changed: also harnesses reached only through shared engine code')
    p.add_argument('--out', type=Path)
    p.add_argument('--plan', action='store_true', help='print the selection and stop')
    p.add_argument('--dry-run', action='store_true', help='also write and print each harness script without running it')
    p.add_argument('--build-editor', action='store_true', help='run engine/scripts/build-editor-mac.sh once before Scene Play')
    p.add_argument('--skip-checks', action='store_true', help='with --tier: do not run ./monoon test')
    p.add_argument('--list', action='store_true')
    p.add_argument('--check', action='store_true')
    args = p.parse_args()
    manifest = load()
    by_id = {h['id']: h for h in manifest['harnesses']}
    if args.check: return check(manifest)
    if args.list:
        for h in manifest['harnesses']:
            gui = gui_modes(h)
            modes = ' '.join(m + ('*' if m in gui else '') for m in h['modes'])
            print(f"{h['id']:30} {modes:36} {','.join(h['needs']):40} {'manual' if 'manual' in h else ''}")
        print('* = opens a window (GUI lock); fast tier leaves these modes out')
        return 0
    unknown = [i for i in args.ids if i not in by_id]
    if unknown: p.error(f'unknown harness {unknown}; see --list')
    wanted = set(args.mode.split(',')) if args.mode else set(MODES)
    if wanted - set(MODES): p.error(f'unknown mode {sorted(wanted - set(MODES))}')

    plan = {}  # id -> (modes, reason)
    for i in args.ids: plan[i] = (set(by_id[i]['modes']), 'named')
    if args.changed:
        files = changed_files(args.changed)
        found = select(manifest, files, broad=args.broad or args.tier == 'full')
        print(f'{len(files)} changed files; {sum(k == "direct" for _, k in found.values())} harnesses direct, '
              f'{sum(k == "broad" for _, k in found.values())} broad')
        plan.update({i: v for i, v in found.items() if i not in plan})
        if not found: print('no harness matched; the fast tier (./monoon test) covers this change')
    elif args.tier == 'full' and not args.ids:
        plan = {h['id']: (set(h['modes']), 'full') for h in manifest['harnesses']}
    rows, skipped = [], []
    for i, (modes, reason) in sorted(plan.items()):
        h = by_id[i]
        modes &= wanted
        if args.tier == 'fast': modes -= gui_modes(h)
        if 'manual' in h and reason != 'named': skipped.append(f"{i}: manual ({h['manual']})"); continue
        if not h['steps']: skipped.append(f"{i}: no runnable steps ({h.get('manual', '')})"); continue
        if not modes: skipped.append(f'{i}: no mode left ({reason})'); continue
        plan[i] = (modes, reason)
        rows.append((h, modes, reason))
    for line in skipped: print('skip', line)
    for h, modes, reason in rows: print(f"plan {h['id']:30} {','.join(sorted(modes, key=MODES.index)):34} {reason}")
    if args.plan: return 0
    out = (args.out or Path(tempfile.mkdtemp(prefix='monoon-harness-'))).resolve()
    out.mkdir(parents=True, exist_ok=True)
    print(f'results: {out}')
    summary = []
    if args.tier and not args.skip_checks and not args.dry_run:
        cmd = ['./monoon', 'test'] + ([] if TEMPLATE.exists() else ['--tier', 'fast'])
        start = time.time()
        with (out / 'monoon-test.log').open('wb') as log:
            code = subprocess.run(cmd, cwd=REPO, stdout=log, stderr=subprocess.STDOUT).returncode
        text = (out / 'monoon-test.log').read_text(encoding='utf-8', errors='replace')
        summary.append({'id': ' '.join(cmd), 'modes': [], 'exit': code, 'seconds': round(time.time() - start), 'failed_step': '',
                        'first_error': '' if code == 0 else first_error(text, out / 'none'), 'log': str(out / 'monoon-test.log')})
        print(f"{summary[-1]['id']}: exit {code}, {summary[-1]['seconds']} s")
    for h, modes, reason in rows:
        row = run_harness(h, modes, out, args)
        summary.append(row)
        if not args.dry_run:
            print(f"{row['id']:30} {','.join(row['modes']):34} exit {row['exit']:3} {row['seconds']:5} s  {row['first_error']}", flush=True)
    (out / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    return 1 if any(r['exit'] for r in summary) else 0


if __name__ == '__main__':
    sys.exit(main())
