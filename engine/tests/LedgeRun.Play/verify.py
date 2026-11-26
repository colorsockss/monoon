from pathlib import Path
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Shared'))
from sample_route import Route, keys, main

ROUTE = Route('ce247eed-9c61-50a8-bdaf-486165fbaccc', '352b64f0-d4ce-56d0-a9e9-388dbbec0cb3',
              'Trailhead camera', 'LedgeRun stage ', r'LedgeRun stage (\d+): \d+ tokens, \d+ gems, \d+ ticks\.',
              4050, (3, 95, 780, 1155, 1370, 2180, 3090, 3760, 4006, 4030), 4)

events = []
for line in Path(__file__).with_name('route.tsv').read_text(encoding='utf-8').splitlines():
    if line and not line.startswith('#'):
        at, control, down = line.split('\t')
        events.append(keys(int(at), control, down == 'true'))

if __name__ == '__main__':
    main(ROUTE, events)
