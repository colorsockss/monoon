"""Mac (osx-arm64) vs Linux x64 guest outputs of the same harness steps, byte by byte (run on the Mac after linux.sh fetch).
  compare.py <linux results> <mac run-harness out folder>... > compare.txt   (writes compare.json next to the linux results)
Logs, profiles, recordings the harness writes itself and build receipts are left out; everything else under each Release/AOT
output folder is compared by relative path."""
from pathlib import Path
import hashlib, json, sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'DisplayLab.IntegerScale'))
from verify import read_png  # pure-Python PNG reader (this Mac's Python has no Pillow)


def pixels(a, b):
    """Differing pixels and the largest channel difference of two same-size PNGs."""
    wa, ha, ca, ra = read_png(a); wb, hb, cb, rb = read_png(b)
    if (wa, ha, ca) != (wb, hb, cb): return f'size {wa}x{ha} vs {wb}x{hb}'
    n = worst = 0
    for x, y in zip(ra, rb):
        if x == y: continue
        for i in range(0, len(x), ca):
            d = max(abs(x[i + k] - y[i + k]) for k in range(ca))
            if d: n += 1; worst = max(worst, d)
    return f'{n}/{wa * ha} px ({100 * n / (wa * ha):.2f}%), max {worst}'

linux, macs = Path(sys.argv[1]), [Path(p) for p in sys.argv[2:]]
SKIP_PARTS = {'profile', 'slot', 'smoke-profile', 'mac-profile', 'builds', 'NativeAOT', 'exports'}
SKIP_SUFFIX = {'.log', '.receipt', '.inputreplay', '.f32', '.sh'}


def files(root):
    projects = {g.parent for g in root.rglob('GameSettings')}  # project copies (the guest keeps none)
    return {p.relative_to(root).as_posix(): p for p in root.rglob('*')
            if p.is_file() and p.suffix not in SKIP_SUFFIX and not SKIP_PARTS & set(p.relative_to(root).parts) and p.name != 'build.receipt'
            and not any(d in projects for d in p.parents) and not p.parent.name.startswith('builds')}


def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()


report = {}
for out in sorted((linux / 'h').glob('*/out')):
    hid = out.parent.name
    mac = next((m / hid for m in macs if (m / hid).is_dir()), None)
    if not mac: report[hid] = 'no Mac run'; continue
    a, b = files(out), files(mac)
    common = sorted(a.keys() & b.keys())
    same = [k for k in common if a[k].read_bytes() == b[k].read_bytes()]
    tops = {k.split('/')[0] for k in a}
    report[hid] = {'same': len(same), 'differ': {k: [sha(a[k])[:12], sha(b[k])[:12]] + ([pixels(a[k], b[k])] if k.endswith('.png') else [])
                                                 for k in common if k not in same},
                   'linux_only': sorted(a.keys() - b.keys()), 'mac_only': sorted(k for k in b.keys() - a.keys() if k.split('/')[0] in tops)}
(linux / 'compare.json').write_text(json.dumps(report, indent=1) + '\n')
for hid, r in report.items():
    if isinstance(r, str): print(f'{hid}: {r}'); continue
    print(f"{hid}: same {r['same']}, differ {len(r['differ'])}, linux-only {len(r['linux_only'])}, mac-only {len(r['mac_only'])}")
    for k, v in r['differ'].items(): print(f'    differ {k} linux {v[0]} mac {v[1]}' + (f' {v[2]}' if len(v) > 2 else ''))
    for k in r['mac_only']: print(f'    mac only {k}')
