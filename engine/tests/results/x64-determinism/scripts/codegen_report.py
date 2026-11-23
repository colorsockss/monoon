"""codegen_report.py <title> <out.txt> <label>=<listing.asm>... : fused-instruction scan + helper listings."""
import re, sys
from collections import Counter
title, out, *pairs = sys.argv[1:]
FUSED = re.compile(r'^\s+(fmadd|fmsub|fnmadd|fnmsub|fmla|fmls|vfn?m(add|sub)\d{3}[sp][sd])\s')
SHOW = ['CallTransform(', 'CallTransformNormal', 'CallLerp2', 'CallLerp3', 'CallLerp4', 'LoopUnfused']
lines = [f'# {title}']
listings = {}
for pair in pairs:
    label, path = pair.split('=', 1)
    methods, cur = {}, None
    for line in open(path, encoding='utf-8', errors='replace'):
        if line.startswith('; Assembly listing for method '):
            cur = line[len('; Assembly listing for method '):].strip(); methods[cur] = []
        elif cur is not None:
            methods[cur].append(line.rstrip('\n'))
    listings[label] = methods
    fused = Counter({m: sum(1 for l in body if FUSED.match(l)) for m, body in methods.items()})
    core = sum(1 for m in methods if 'Engine.Core' in m)
    lines.append(f'## {label}: {len(methods)} methods listed, {core} Engine.Core.*; methods with fused multiply-add instructions:')
    lines += [f'   {n:3} {m}' for m, n in sorted(fused.items()) if n] or ['   none']
for label, methods in listings.items():
    for key in SHOW:
        for m, body in methods.items():
            if m.startswith('Program:' + key):
                lines.append(f'\n## {label} {m}')
                lines += [l for l in body if re.match(r'^\s+[a-z]', l) and 'align' not in l]
                break
open(out, 'w').write('\n'.join(lines) + '\n')
print('\n'.join(lines[:12]))
