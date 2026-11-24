import sys, re, html, collections, pickle, os
# usage: why.py <dgml> <regex> [max targets]: prints, for each matching node, one chain of "who depends on me" up to a root.
path, rx = sys.argv[1], re.compile(sys.argv[2]); maxn = int(sys.argv[3]) if len(sys.argv) > 3 else 5
cache = path + '.pkl'
if os.path.exists(cache):
    labels, rev = pickle.load(open(cache, 'rb'))
else:
    labels = {}; rev = collections.defaultdict(list)
    nre = re.compile(r'<Node Id="(\d+)" Label="([^"]*)"'); lre = re.compile(r'<Link Source="(\d+)" Target="(\d+)" Reason="([^"]*)"')
    for line in open(path, encoding='utf-8-sig'):
        m = nre.search(line)
        if m: labels[int(m.group(1))] = html.unescape(m.group(2)); continue
        m = lre.search(line)
        if m: rev[int(m.group(2))].append((int(m.group(1)), html.unescape(m.group(3))))
    pickle.dump((labels, dict(rev)), open(cache, 'wb'))
hits = [i for i, l in labels.items() if rx.search(l)]
print(f'{len(hits)} matching nodes')
for t in hits[:maxn]:
    # BFS backwards to a node with no incoming edges (root)
    prev = {t: None}; q = collections.deque([t]); end = None
    while q:
        x = q.popleft()
        srcs = rev.get(x, [])
        if not srcs: end = x; break
        for s, r in srcs:
            if s not in prev: prev[s] = (x, r); q.append(s)
    print('\n#', labels[t][:200])
    x = end; chain = []
    while x is not None and x != t:
        nx, r = prev[x]; chain.append(f'  {labels[x][:170]}  --[{r}]-->'); x = nx
    for c in chain: print(c)
