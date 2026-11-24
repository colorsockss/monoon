import re, html, collections, sys
# usage: table.py <root> : before/after markdown tables from base/<s> and r3/<s> maps
root = sys.argv[1]
src = open(__file__.replace('table.py', 'analyze.py')).read().split('buckets = collections')[0]
sys.argv = ['x', root + '/base/KeyRoom/native/MoNoonPlayer.map.xml']; exec(src)
def summary(p):
    a = collections.Counter(); cross = collections.Counter(); kinds = collections.Counter(); rec = 0; fmt = 0; tot = 0
    for line in open(p, encoding='utf-8'):
        m = node.search(line)
        if not m: continue
        kind, name, n = m.group(1), html.unescape(m.group(2)), int(m.group(3)); tot += n
        name = re.sub(r'^(_ZTV\d+|__GenericDict_|__writableData|__GCStaticBase_|__NonGCStaticBase_|unbox_|<Boxed>|Boxed_)', '', name)
        kinds[kind] += n
        a[asm_of(name)] += n
        for k, rx in (('STJ', 'System_Text_Json'), ('VYaml', 'VYaml'), ('LINQ', 'System_Linq_(?!Expressions)'), ('Immutable', 'System_Collections_Immutable'),
                      ('System.Drawing', 'System_Drawing'), ('IO.Compression', 'System_IO_Compression'), ('M.E.', 'Microsoft_Extensions'), ('Uri', 'S_P_Uri')):
            if re.search(rx, name): cross[k] += n
        if 'GeneratedFormatter' in name: fmt += n
        if kind == 'MethodCode' and RECORD.search(name) and name.startswith('Engine_Core'): rec += n
    return tot, a, cross, kinds, rec, fmt
rows = {(lab, s): summary(f'{root}/{lab}/{s}/native/MoNoonPlayer.map.xml') for lab in ('base', 'r3') for s in ('SpriteLab', 'KeyRoom')}
def line(label, f):
    v = [f(rows[(lab, s)]) for s in ('SpriteLab', 'KeyRoom') for lab in ('base', 'r3')]
    print(f'| {label} | ' + ' | '.join(f'{x:,}' for x in v) + ' |')
print('| 항목 | SpriteLab 전 | SpriteLab 후 | KeyRoom 전 | KeyRoom 후 |\n| --- | ---: | ---: | ---: | ---: |')
line('map 노드 합계', lambda r: r[0])
for k in ['CoreLib', 'Engine.Core', 'Engine.Desktop', 'MoNoonPlayer', 'GameScripts', 'VYaml', 'STJ', 'LINQ', 'Collections.Immutable', 'Collections.Concurrent',
          'Collections', 'IO.Compression', 'M.E.*', 'Console', 'TypeLoader', 'Reflection', 'Box2D', 'Brutal.Mixer', 'Brutal.Ttf', 'Brutal other', '(other)']:
    line(k, lambda r, k=k: r[1][k])
print('\n| 교차 집계(인스턴스화 포함) | SpriteLab 전 | SpriteLab 후 | KeyRoom 전 | KeyRoom 후 |\n| --- | ---: | ---: | ---: | ---: |')
for k in ('VYaml', 'STJ', 'LINQ', 'Immutable', 'System.Drawing', 'IO.Compression', 'M.E.', 'Uri'):
    line(k, lambda r, k=k: r[2][k])
line('VYaml 생성 포매터(Deserialize)', lambda r: r[5])
line('record 합성 멤버 코드(Engine.Core)', lambda r: r[4])
print('\n| 노드 종류 | SpriteLab 전 | SpriteLab 후 | KeyRoom 전 | KeyRoom 후 |\n| --- | ---: | ---: | ---: | ---: |')
for k in ('MethodCode', 'GCInfo', 'ConstructedEEType', 'DehydratedData', 'Metadata', 'ArrayOfFrozenObjects'):
    line(k, lambda r, k=k: r[3][k])
