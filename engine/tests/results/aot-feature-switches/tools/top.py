import sys, re, html, collections
# usage: top.py <map.xml> <regex> [n] : largest map nodes matching
node = re.compile(r'<(\w+) Name="([^"]*)" Length="(\d+)"')
rx = re.compile(sys.argv[2]); n = int(sys.argv[3]) if len(sys.argv) > 3 else 30
c = collections.Counter()
for l in open(sys.argv[1], encoding='utf-8'):
    m = node.search(l)
    if m and rx.search(html.unescape(m.group(2))): c[m.group(1) + ' ' + html.unescape(m.group(2))[:160]] += int(m.group(3))
print(f'sum {sum(c.values()):,} over {len(c)}')
for k, b in c.most_common(n): print(f'{b:>7} {k}')
