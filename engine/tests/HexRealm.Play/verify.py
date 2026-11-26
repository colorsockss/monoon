from pathlib import Path
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Shared'))
from sample_route import Route, keys, main

ROUTE = Route('cbb32c55-fb51-581f-84c0-8c71d1f74721', '00247366-a56a-5b55-9708-240396719f71',
              'Board controller', 'HexRealm turn ', r'HexRealm turn (\d+): [0-9a-f]{16}',
              195, (2, 7, 45, 110, 167, 190), 40)

events = []
def tap(at, key):
    events.extend((keys(at, key, True), keys(at + 1, key, False)))

tap(5, 'B')
for turn in range(40):
    at = 10 + 4 * turn
    if turn % 5 == 0:
        tap(at - 2, '4')
    tap(at, 'Return')
tap(173, 'F5')
tap(181, 'F9')

if __name__ == '__main__':
    main(ROUTE, events)
