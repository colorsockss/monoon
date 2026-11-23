"""Static assembly-reference closure of a Release player (no IL rewriting, so ReadyToRun code survives).
usage: python3 -I closure.py <Contents/MacOS dir> <out-dir-to-prune-copy or ->"""
import sys, struct, os, shutil
from pathlib import Path

def refs(path):
    # Minimal ECMA-335 reader: returns AssemblyRef names.
    b = path.read_bytes()
    pe = struct.unpack_from('<I', b, 0x3c)[0]
    nsec = struct.unpack_from('<H', b, pe + 6)[0]
    opt = pe + 24; magic = struct.unpack_from('<H', b, opt)[0]
    dd = opt + (96 if magic == 0x10b else 112)
    cli_rva = struct.unpack_from('<I', b, dd + 14 * 8)[0]
    secs = [struct.unpack_from('<8sIIII', b, opt + struct.unpack_from('<H', b, pe + 20)[0] + i * 40) for i in range(nsec)]
    def off(rva):
        for _, vs, va, rs, ro in secs:
            if va <= rva < va + max(vs, rs): return rva - va + ro
        raise ValueError
    cli = off(cli_rva); md = off(struct.unpack_from('<I', b, cli + 8)[0])
    vlen = struct.unpack_from('<I', b, md + 12)[0]; p = md + 16 + vlen
    nstreams = struct.unpack_from('<H', b, p + 2)[0]; p += 4; streams = {}
    for _ in range(nstreams):
        o, s = struct.unpack_from('<II', b, p); p += 8
        e = b.index(b'\0', p); name = b[p:e].decode(); p = (e + 4) & ~3; streams[name] = md + o
    t = streams['#~']; strings = streams['#Strings']
    hs = b[t + 6]; valid = struct.unpack_from('<Q', b, t + 8)[0]
    rows = [0] * 64; q = t + 24
    for i in range(64):
        if valid >> i & 1: rows[i] = struct.unpack_from('<I', b, q)[0]; q += 4
    si = 4 if hs & 1 else 2; gi = 4 if hs & 2 else 2; bi = 4 if hs & 4 else 2
    def idx(*tables): return 4 if max(rows[x] for x in tables) >= 1 << 16 else 2
    def cidx(bits, *tables): return 4 if max(rows[x] for x in tables) >= 1 << (16 - bits) else 2
    tdor = cidx(2, 2, 1, 27); rs = cidx(2, 0, 26, 35, 1); hc = cidx(2, 4, 8, 23); mrp = cidx(3, 2, 1, 26, 6, 27)
    hfm = cidx(1, 4, 10); mf = cidx(1, 4, 6); hs_ = cidx(1, 20, 23); mdo = cidx(1, 6, 10); impl = cidx(2, 38, 35, 39)
    cat = cidx(3, 6, 10); hca = cidx(5, *[x for x in (6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43) ])
    tom = cidx(1, 2, 6)
    size = {0: 2 + si + gi * 3, 1: rs + si * 2, 2: 4 + si * 2 + tdor + idx(4) + idx(6), 3: idx(4), 4: 2 + si + bi, 5: idx(6), 6: 4 + 2 + 2 + si + bi + idx(8),
            7: idx(8), 8: 4 + si, 9: idx(2) + tdor, 10: mrp + si + bi, 11: 2 + hc + bi, 12: hca + cat + bi, 13: hfm + bi, 14: 2 + cidx(2, 2, 6, 32) + bi,
            15: 2 + 4 + idx(2), 16: 4 + idx(4), 17: bi, 18: idx(2) + idx(20), 19: idx(20), 20: 2 + si + tdor, 21: idx(2) + idx(23), 22: idx(23),
            23: 2 + si + bi, 24: 2 + idx(6) + hs_, 25: idx(2) + mdo + mdo, 26: si, 27: bi, 28: 2 + mf + si + idx(26), 29: 4 + idx(4), 30: 8,
            31: 4, 32: 4 + 8 + 4 + bi + si + si, 33: 4, 34: 12, 35: 8 + 4 + bi + si + si + bi}
    q2 = q
    for i in range(35): q2 += rows[i] * size.get(i, 0)
    names = []
    for r in range(rows[35]):
        name_off = q2 + r * size[35] + 8 + 4 + bi
        so = struct.unpack_from('<I' if si == 4 else '<H', b, name_off)[0]
        e = b.index(b'\0', strings + so); names.append(b[strings + so:e].decode())
    return names

root = Path(sys.argv[1]); dlls = {p.stem: p for p in root.glob('*.dll')}
start = ['MoNoonPlayer', 'Engine.Core', 'Engine.Desktop']
scripts = root / 'Game/.monoon-compiled/MoNoon.GameScripts.dll'
seen, todo = set(), list(start)
if scripts.exists(): todo += [r for r in refs(scripts)]
while todo:
    n = todo.pop()
    if n in seen or n not in dlls: continue
    seen.add(n); todo += refs(dlls[n])
keep = sum(dlls[n].stat().st_size for n in seen); drop = sum(p.stat().st_size for n, p in dlls.items() if n not in seen)
print(f'closure {len(seen)}/{len(dlls)} dlls keep {keep:,} drop {drop:,}')
big = sorted(((p.stat().st_size, n) for n, p in dlls.items() if n not in seen), reverse=True)[:12]
print('largest dropped:', ', '.join(f'{n} {s/1048576:.1f}M' for s, n in big))
print('kept framework:', ' '.join(sorted(n for n in seen if n.startswith(('System', 'Microsoft')))))
if len(sys.argv) > 2 and sys.argv[2] != '-':
    out = Path(sys.argv[2]); shutil.copytree(root, out, symlinks=True)
    for n, p in dlls.items():
        if n not in seen: (out / p.name).unlink()
