from pathlib import Path
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Shared'))
from sample_route import Route, click, keys, main

ROUTE = Route('56a3a1e6-45d4-5055-90b4-d22196ec482f', '32ccf437-7ad0-53ae-962e-287d655678a6',
              'Expedition camera', 'IsoBand zone ',
              r'IsoBand zone (\d+): (?:\d+ defeated, \d+ XP, \d+ crowns\.|Returned to Haven\.)',
              3480, (3, 286, 550, 1420, 2240, 2900, 3460), 4)

events = []
for line in Path(__file__).with_name('route.tsv').read_text(encoding='utf-8').splitlines():
    if not line or line.startswith('#'):
        continue
    at, kind, a, b = line.split('\t')
    if kind == 'click':
        events.extend(click(int(at), a, b))
    else:
        events.append(keys(int(at), a, b == 'true'))

if __name__ == '__main__':
    main(ROUTE, events)
