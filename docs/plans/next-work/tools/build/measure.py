"""Headless correctness + startup/memory measurement for MoNoon player variants (no window, no audio device).
usage: python3 -I measure.py <label> <sample> <bin-dir> [runs]"""
import os, sys, subprocess, shutil, time, tempfile, statistics, hashlib, re
from pathlib import Path
WT = Path('/Users/paare/codex_project/brutal_engine/.claude/worktrees/agent-a1cc2177431e98601')
TESTS = WT / 'engine/tests'
sys.path.insert(0, str(TESTS / 'KeyRoom.WorldCheckpoints')); sys.path.insert(0, str(TESTS))
IDS = {'KeyRoom': ('64fb4cd6-2469-5cec-ba6d-9a49e61f42e7', 'dd96f9dd-668b-5df5-8786-252e6eb77e9e'),
       'InventoryLab': ('ade09cc3-a769-56d7-9832-a2bea14c7e71', '6a7b735e-336d-5674-b2e7-a136db5a7771'),
       'PlatformerLab': ('3b309c28-75ce-562e-92ac-f94ee48b18c7', '4fe9f1eb-5dcd-5f90-a033-78a61e876a0d')}
label, sample, bin_dir = sys.argv[1], sys.argv[2], Path(sys.argv[3])
runs = int(sys.argv[4]) if len(sys.argv) > 4 else 7
player = bin_dir / 'MoNoonPlayer'
scratch = Path(tempfile.mkdtemp(prefix=f'm-{label}-', dir='/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/research/t2-runs'))
base_env = dict(os.environ, SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')

def replay_file(frames):
    p, s = IDS[sample]
    f = scratch / f'frames{frames}.inputreplay'
    f.write_text(f'version: 1\nproject: {p}\nstartScene: {s}\nframeTicks: [{", ".join(["166667"] * frames)}]\nevents: []\n')
    return f

def run(args, env_extra):
    prof = Path(tempfile.mkdtemp(dir=scratch))
    env = dict(base_env, MONOON_USER_DATA=str(prof), **env_extra)
    t = time.perf_counter()
    r = subprocess.run(['/usr/bin/time', '-l', str(player), *args, str(prof)], env=env, cwd=player.parent, capture_output=True, text=True, timeout=120)
    wall = (time.perf_counter() - t) * 1000
    if r.returncode != 0: raise SystemExit(f'{label} failed: {r.stdout[-800:]} {r.stderr[-1500:]}')
    rss = int(re.search(r'(\d+)\s+maximum resident set size', r.stderr)[1])
    foot = re.search(r'(\d+)\s+peak memory footprint', r.stderr)
    return wall, rss, int(foot[1]) if foot else 0, prof

def trace_ms(path, event):
    for line in path.read_text().splitlines():
        if line.startswith(event + ','): return float(line.split(',')[1])

# 1) correctness
ok = 'n/a'
if sample == 'InventoryLab':
    fx = TESTS / 'fixtures/InventoryLab'
    _, _, _, prof = run(['--replay-input', str(fx / 'Herbalist.inputreplay')], {})
    got = (prof / 'slot-001.save').read_bytes(); want = (fx / 'Herbalist.save').read_bytes()
    ok = 'PASS Herbalist save identical' if got == want else f'FAIL Herbalist {hashlib.sha256(got).hexdigest()[:12]}'
elif sample == 'KeyRoom':
    import ci
    folder = scratch / 'kr'; folder.mkdir()
    if not (bin_dir / 'MoNoonBuild').exists(): raise SystemExit('copy MoNoonBuild first')
    import io, contextlib
    buf = io.StringIO()
    try:
        with contextlib.redirect_stdout(buf): ci.check_player(bin_dir, 'osx-arm64', folder)
        ok = f'PASS KeyRoom world checkpoints ({buf.getvalue().count("PASS")} checks)'
    except Exception as e:
        ok = f'FAIL {e}'

# 2) startup to first offscreen frame (software renderer draws frame 0 to PNG), warm runs
one = replay_file(1)
walls, ready, rss1 = [], [], []
for i in range(runs + 1):
    snap = scratch / f'snap{i}'
    tr = scratch / f'trace{i}.csv'
    w, rss, foot, _ = run(['--replay-input', str(one)], {'MONOON_REPLAY_SNAPSHOTS': f'{snap};0', 'MONOON_STARTUP_TRACE': str(tr)})
    if i == 0: first = w; continue  # first run after build: closest to cold we get without purge
    walls.append(w); ready.append(trace_ms(tr, 'session_ready')); rss1.append(rss)

# 3) 600 frames paced offscreen (10 s at 60 Hz): steady-state memory; 600 frames unpaced: update cost/frame
long = replay_file(600)
_, rss_p, foot_p, _ = run(['--replay-input', str(long)], {'MONOON_REPLAY_OFFSCREEN': '1'})
unpaced = [run(['--replay-input', str(long)], {})[0] for _ in range(3)]
short = [run(['--replay-input', str(one)], {})[0] for _ in range(3)]
per_frame_us = (statistics.median(unpaced) - statistics.median(short)) / 599 * 1000
med = statistics.median
print(f'{label}\t{sample}\t{ok}\tfirst_run_ms={first:.0f}\twarm_first_frame_ms={med(walls):.0f} ({min(walls):.0f}-{max(walls):.0f})'
      f'\tmain_to_session_ready_ms={med(ready):.1f}\tstartup_rss_MiB={med(rss1)/1048576:.1f}'
      f'\tpaced600_rss_MiB={rss_p/1048576:.1f}\tpaced600_footprint_MiB={foot_p/1048576:.1f}\tupdate_us_per_frame={per_frame_us:.0f}')
shutil.rmtree(scratch, ignore_errors=True)
