"""Replay the fought coastal campaign and verify that its result reloads."""
import csv
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / 'Shared'))
from sample_route import Route, click, keys, main

RESULTS = HERE.parent / 'results' / 'hexrealm'
P = 'cbb32c55-fb51-581f-84c0-8c71d1f74721'
S = '00247366-a56a-5b55-9708-240396719f71'
TURN = r'HexRealm turn (\d+): [0-9a-f]{16}'
RESULT = r'HexRealm result 1: ([0-9a-f]{16})'
RESUME = r'HexRealm resume ([0-9a-f]{16})'


def pos(q, r):
    return round(640 + (48 + 48*q + 24*r - 590) * 1.26), round(360 + (46 + 42*r - 280) * 1.26)


def orders():
    with (HERE / 'route.tsv').open(encoding='utf-8') as f:
        for row in csv.DictReader(f, delimiter='\t'):
            yield (row['order'], int(row['from_q']), int(row['from_r']), int(row['to_q_or_option']),
                   int(row['to_r']), int(row['turn_after']), int(row['winner_after']), row['attack'] == 'True')


def route():
    events = []
    capture = [20]
    def tap(frame, control):
        events.extend((keys(frame, control, True), keys(frame+1, control, False)))
    events.extend(click(30, 212, 453))
    frame = 66
    capture.append(frame-1)
    first_attack = True
    for order, x, y, a, b, turn, winner, attack in orders():
        if order in ('Found','Improve','Train','Move'):
            events.extend(click(frame, *pos(x, y)))
            frame += 5
        if order == 'Move':
            x, y = pos(a, b)
            events.append((frame, f'{{kind: pointer, x: {x}, y: {y}}}'))
            if first_attack and attack:
                capture.append(frame + 3)
            frame += 5
            events.extend(((frame, f'{{kind: pointer, control: "Mouse.Right", down: true, x: {x}, y: {y}}}'),
                           (frame+1, f'{{kind: pointer, control: "Mouse.Right", down: false, x: {x}, y: {y}}}')))
            if first_attack and attack:
                first_attack = False
                capture.append(frame + 10)
            frame += 17 if attack else 10
        elif order == 'End':
            if turn == 4:
                events.extend(click(frame, *pos(3, 5)))
                capture.append(frame + 5)
                frame += 10
            tap(frame, 'Return')
            if turn == 4:
                capture.append(frame + 103)
            if turn == 5:
                capture.append(frame + 10)
            frame += 112
        else:
            tap(frame, {'Found':'B', 'Improve':['I','M','O'][a-1] if order == 'Improve' else '',
                        'Train':str(a+1), 'Research':'R'}[order])
            frame += 7
    capture.append(frame + 61)
    tap(frame + 71, 'F5')
    tap(frame + 85, 'N')
    tap(frame + 120, 'F9')
    capture.append(frame + 158)
    tap(frame + 166, 'N')
    return Route(P, S, 'Board controller', 'HexRealm ', TURN, frame+211, tuple(sorted(capture)), sum(order[0] == "End" for order in orders())), events


def verify_result(mode, output):
    path = output / ('editor.log' if mode == 'editor' else 'player.log')
    trace = (output / 'route.txt').read_text(encoding='utf-8') if mode == 'editor' else path.read_text(encoding='utf-8')
    won = re.findall(RESULT, trace)
    resumed = re.findall(RESUME, trace)
    if len(won) != 1 or resumed != won:
        raise RuntimeError(f'{output}: expected one victory and a matching restored result; got {won}, {resumed}')
    turns = '\n'.join(match.group() for match in re.finditer(TURN, trace)) + '\n'
    (output / 'route.txt').write_text(turns, encoding='utf-8')
    result = f'HexRealm result 1: {won[0]}\nHexRealm resume {resumed[0]}\n'
    (output / 'result.txt').write_text(result, encoding='utf-8')
    expected = RESULTS / 'macos-arm64-route.txt'
    if expected.read_text(encoding='utf-8') != turns + result:
        raise RuntimeError(f'{output}: turn or result hash differs from {expected}')
    print(result.strip())


if __name__ == '__main__':
    spec, events = route()
    main(spec, events)
    if sys.argv[1] in ('play', 'editor'):
        verify_result(sys.argv[1], Path(sys.argv[-1]))
