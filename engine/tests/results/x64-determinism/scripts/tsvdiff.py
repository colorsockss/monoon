"""tsvdiff.py A.tsv B.tsv: rows that differ, first differing row (first column), max numeric difference, sha256 of each."""
import hashlib, sys
a, b = (open(p, encoding='utf-8').read().splitlines() for p in sys.argv[1:3])
h = [hashlib.sha256(open(p, 'rb').read()).hexdigest()[:16] for p in sys.argv[1:3]]
diff = [(x, y) for x, y in zip(a, b) if x != y]
mx = 0.0
for x, y in diff:
    for u, v in zip(x.split(), y.split()):
        try: mx = max(mx, abs(float(u) - float(v)))
        except ValueError: pass
first = diff[0] if diff else None
print(f'{h[0]} {h[1]} rows {len(a)}/{len(b)} differ {len(diff)} max {mx:.4f}' +
      (f' first {first[0].split()[0]} | {first[0][:90]} | {first[1][:90]}' if first else ' identical'))
