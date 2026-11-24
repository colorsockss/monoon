import sys, re, html, collections
# usage: diff.py a.map.xml b.map.xml [--top N] [--raw]: node bytes only/more in a and in b, grouped by asm + type or instantiation
ARGS = sys.argv; top = int(ARGS[ARGS.index('--top')+1]) if '--top' in ARGS else 30
raw = "--raw" in ARGS
src = open(__file__.replace('diff.py', 'analyze.py')).read().split('buckets = collections')[0]
sys.argv = ["x", ARGS[1]]; exec(src)
AGG = ('DehydratedData', 'Metadata', 'StackTraceMethodMapping', 'ArrayOfFrozenObjects', 'GenericTypesHashtable', 'GenericMethodsHashtable',
       'ExternalReferencesTable', 'InterfaceDispatchCellSection', 'TypeMetadataMap', 'NativeLayoutInfo', 'StaticsInfoHashtable', 'ReflectionInvokeMap', 'FieldRvaData')
def load(p):
    d = collections.Counter()
    for line in open(p, encoding='utf-8'):
        m = node.search(line)
        if m: d[(m.group(1), html.unescape(m.group(2)))] += int(m.group(3))
    return d
a, b = load(ARGS[1]), load(ARGS[2])
def key(name):
    n = re.sub(r'^(_ZTV\d+|__GenericDict_|__writableData|__GCStaticBase_|__NonGCStaticBase_|unbox_|<Boxed>)', '', name)
    if raw: return n[:160]
    s = asm_of(n)
    if '<' in n: s += ' inst:' + re.sub(r'_\d+$', '', n.split('<')[0].split('__')[0])[-50:] + ' <' + asm_of(n.split('<', 1)[1]) + '>'
    elif s in ('Engine.Core', 'Engine.Desktop', 'STJ', 'VYaml', 'CoreLib', 'LINQ'):
        s += ' ' + re.sub(r'^(Engine_(Core|Desktop)_Engine_(Core|Desktop)_|S_P_CoreLib_System_|System_Text_Json_System_Text_Json_|VYaml_VYaml_|System_Linq_System_Linq_)', '', n).split('__')[0][:60]
    return s
for title, x, y in (('only/more in A', a, b), ('only/more in B', b, a)):
    c = collections.Counter(); tot = 0
    for k, v in x.items():
        d = v - y.get(k, 0)
        if d > 0 and k[0] not in AGG:
            c[key(k[1])] += d; tot += d
    print(f'== {title}: {tot:,}')
    for k, v in c.most_common(top): print(f'  {v:>9,} {k}')
for k in AGG:
    print(f'{k:32} {sum(v for kk, v in a.items() if kk[0] == k):>10,} {sum(v for kk, v in b.items() if kk[0] == k):>10,}')
