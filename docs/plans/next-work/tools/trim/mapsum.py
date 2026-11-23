import re, sys, collections
# Sum MethodCode bytes by assembly prefix and by Engine.Core type bucket.
pat = re.compile(r'<(MethodCode|ConstructedEEType|EEType|GCStaticsPreInitData|WritableData|Metadata|FrozenStringNode|FrozenObject)\b[^>]*Name="([^"]*)" Length="(\d+)"')
kinds = collections.Counter(); asm = collections.Counter(); core = collections.Counter(); total = 0
for line in open(sys.argv[1], encoding='utf-8'):
    m = pat.search(line)
    if not m: continue
    kind, name, n = m.group(1), m.group(2), int(m.group(3))
    kinds[kind] += n
    if kind != 'MethodCode': continue
    total += n
    a = name.split('_')[0]
    if name.startswith('S_P_CoreLib'): a = 'System.Private.CoreLib'
    elif name.startswith('Engine_Core'): a = 'Engine.Core'
    elif name.startswith('Engine_Desktop'): a = 'Engine.Desktop'
    elif name.startswith('Brutal'): a = '_'.join(name.split('_')[:2])
    elif name.startswith('System_'): a = '_'.join(name.split('_')[:2])
    elif name.startswith('Microsoft_'): a = '_'.join(name.split('_')[:3])
    asm[a] += n
    if a == 'Engine.Core':
        t = re.sub(r'^Engine_Core_Engine_Core_', '', name).split('__')[0]
        t = re.split(r'_[A-Z]', t)[0] if t else t
        t = re.sub(r'_\d+.*', '', t)
        core[t] += n
print('kinds', dict(kinds.most_common()))
print('method code total', total)
for k, v in asm.most_common(25): print(f'{v:9d} {k}')
print('--- Engine.Core types (top 45)')
for k, v in core.most_common(45): print(f'{v:9d} {k}')
