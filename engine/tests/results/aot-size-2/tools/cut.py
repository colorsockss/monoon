import sys, re, html, collections, pickle
# usage: cut.py <codegen.dgml.xml> <map.xml> <regex of dgml labels to remove> [--top N] [--keep REGEX]
# Estimates the bytes that vanish if the matching dependency-graph nodes were never added: forward reachability from the
# roots without them. A "(A, B)" conditional node needs both of its inputs. Map nodes are matched to dgml labels by name.
# Conservative only in the sense that ILC may still find other paths in a changed program; this is a what-if on today's graph.
dg, mp, rx = sys.argv[1], sys.argv[2], re.compile(sys.argv[3])
top = int(sys.argv[sys.argv.index('--top') + 1]) if '--top' in sys.argv else 25
labels, rev = pickle.load(open(dg + '.pkl', 'rb'))
fwd = collections.defaultdict(list); need = {}
for t, ss in rev.items():
    for s, r in ss: fwd[s].append(t)
    if labels[t].startswith('('): need[t] = len({s for s, _ in ss})
def reach(removed):
    seen = set(); got = collections.Counter()
    stack = [i for i in labels if i not in rev and i not in removed]
    seen.update(stack)
    while stack:
        x = stack.pop()
        for t in fwd.get(x, ()):
            if t in seen or t in removed: continue
            if t in need:
                got[t] += 1
                if got[t] < need[t]: continue
            seen.add(t); stack.append(t)
    return seen
removed = {i for i, l in labels.items() if rx.search(l)}
print(f'removing {len(removed)} nodes, e.g. {[labels[i][:90] for i in list(removed)[:3]]}')
base = reach(set()); cut = reach(removed)
gone = {re.sub(r' (constructed|unconstructed)$', '', labels[i]) for i in base - cut} | {re.sub(r' (constructed|unconstructed)$', '', labels[i]) for i in removed}
size = collections.Counter(); per = collections.Counter(); total = 0
node = re.compile(r'<(\w+) Name="([^"]*)" Length="(\d+)"')
sys.path.insert(0, __file__.rsplit('/', 1)[0])
src = open(__file__.replace('cut.py', 'analyze.py')).read().split('buckets = collections')[0]
saved = sys.argv; sys.argv = ['x', mp]; exec(src); sys.argv = saved
for line in open(mp, encoding='utf-8'):
    m = node.search(line)
    if not m: continue
    name = html.unescape(m.group(2)); n = int(m.group(3))
    key = name[len('__GenericDict_'):] if name.startswith('__GenericDict_') else name
    if name in gone or key in gone:
        total += n
        nn = re.sub(r'^(_ZTV\d+|__GenericDict_|unbox_)', '', name)
        size[asm_of(nn)] += n
        per[re.sub(r'^(Engine_Core_Engine_Core_|S_P_CoreLib_System_|VYaml_VYaml_Serialization_)', '', nn).split('__')[0][:90]] += n
print(f'map node bytes that vanish: {total:,} (aggregate tables such as metadata and dehydrated type data shrink too, not counted)')
for k, v in size.most_common(top): print(f'  {v:>9,} {k}')
print('-- top items')
for k, v in per.most_common(top): print(f'  {v:>9,} {k}')
