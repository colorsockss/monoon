from hashlib import sha256
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Shared'))
from sample_route import Route, keys, main

ROUTE = Route('ce247eed-9c61-50a8-bdaf-486165fbaccc', '352b64f0-d4ce-56d0-a9e9-388dbbec0cb3',
              'Trailhead camera', 'LedgeRun stage ', r'LedgeRun stage (\d+): \d+ tokens, \d+ gems, \d+ ticks\.',
              4458, (96, 160, 421, 1510, 2295, 2970, 3027, 3048, 3252, 3297, 3401, 3957, 4025, 4071, 4155, 4250, 4320, 4410), 4)

BASELINE = Path(__file__).resolve().parents[1] / 'results' / 'ledgerun'

events = []
for line in Path(__file__).with_name('route.tsv').read_text(encoding='utf-8').splitlines():
    if line and not line.startswith('#'):
        at, control, down = line.split('\t')
        events.append(keys(int(at), control, down == 'true'))

def check_save(output, mode):
    folder = output / ('profile' if mode == 'play' else 'storage')
    saves = list(folder.rglob('slot-*.save'))
    if len(saves) != 1 or saves[0].name != 'slot-003.save':
        raise RuntimeError(f'{output}: expected the clear in slot 3 only, got {saves}')
    saved = dict(re.findall(r'^  ([\w.]+): (\w+)$', saves[0].read_text(), re.M))
    if saved.get('unlocked') != '4':
        raise RuntimeError(f'{output}: slot 3 did not unlock all stages')
    actual = (output / 'route.txt').read_text()
    expected = (BASELINE / 'macos-arm64-route.txt').read_text()
    if actual != expected:
        raise RuntimeError(f'{output}: clear log differs from the recorded route')
    clears = re.findall(r'stage (\d+): (\d+) tokens, (\d+) gems, (\d+) ticks', actual)
    if not any(int(clear[2]) > 0 for clear in clears):
        raise RuntimeError(f'{output}: the route collected no gems')
    if int(clears[-1][3]) < 900:
        raise RuntimeError(f'{output}: the Summit clear took less than 15 seconds')
    if saved.get('bestExpeditionTicks') != str(sum(int(clear[3]) for clear in clears)):
        raise RuntimeError(f'{output}: the full expedition best was not stored in slot 3')
    for stage, tokens, gems, ticks in clears:
        wanted = {'clear': 'true', 'bestTokens': tokens, 'bestGems': gems, 'bestTicks': ticks}
        for name, value in wanted.items():
            if saved.get(f'stage{stage}.{name}') != value:
                raise RuntimeError(f'{output}: invalid saved stage {stage} {name}')


def check_captures(output):
    if sys.platform != 'darwin':
        return
    for row in (BASELINE / 'macos-arm64-sha256.tsv').read_text().splitlines():
        name, expected = row.split('\t')
        actual = sha256((output / name).read_bytes()).hexdigest()
        if actual != expected:
            raise RuntimeError(f'{output}: {name} differs from the macOS SDL baseline')


if __name__ == '__main__':
    main(ROUTE, events)
    if sys.argv[1] in ('play', 'editor'):
        output = Path(sys.argv[-1])
        check_save(output, sys.argv[1])
        check_captures(output)
