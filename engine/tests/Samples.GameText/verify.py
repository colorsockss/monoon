"""Replay text-heavy sample screens (KeyRoom bag in Korean, PlatformerLab cutscene dialogue) in exported players and
compare their captures with Scene Play. All three paths draw game text with BRUTAL's SDL_ttf (roadmap 11.13)."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

# Each scenario: project, start scene, frame count, key presses (frame, key) or holds (start, end, key), captured frames.
SCENARIOS = {
    'KeyRoom': dict(project='64fb4cd6-2469-5cec-ba6d-9a49e61f42e7', start='64fb4cd6-2469-5cec-ba6d-9a49e61f42e7', frames=240,
                    # I opens the bag, L switches to Korean; close, walk left to the key, reopen; L back to English.
                    events=[(10, 'I'), (40, 'L'), (90, 'I'), (100, 125, 'Left'), (160, 'I'), (200, 'L')],
                    captures=[5, 70, 190, 230]),
    'PlatformerLab': dict(project='3b309c28-75ce-562e-92ac-f94ee48b18c7', start='4fe9f1eb-5dcd-5f90-a033-78a61e876a0d', frames=420,
                          # Run right onto the moving deck; the ferry cutscene opens its dialogue panel; Return advances.
                          events=[(10, 193, 'Right'), (290, 'Return'), (350, 'Return')],
                          captures=[260, 320, 380]),
    # The showcase yard: Props.atlas cells (coins, flowers, bushes), sign and HUD text, the firefly's Aseprite blink. No input.
    'SpriteLab': dict(project='4394934e-c0de-4350-a585-27a682237b83', start='ca068bfb-56be-582f-8265-252f3f4116da', frames=300,
                      events=[], captures=[5, 120, 250, 290]),
}
MODES = ['scene-play', 'release', 'native-aot']


def recording(name, path):
    s = SCENARIOS[name]
    lines = []
    for event in s['events']:
        down, up, key = (event[0], event[0] + 1, event[1]) if len(event) == 2 else event
        for at, pressed in ((down, 'true'), (up, 'false')):
            lines.append((at, f'- frame: {at}\n  timeTicks: {at * 166667}\n  input: {{kind: key, control: "{key}", down: {pressed}}}\n'))
    lines.sort(key=lambda item: item[0])
    events = ''.join(text for _, text in lines)
    path.write_text(f'version: 1\nproject: {s["project"]}\nstartScene: {s["start"]}\nframeTicks: [' +
                    ', '.join(['166667'] * s['frames']) + ']\nevents:' + ('\n' + events if events else ' []\n'), encoding='utf-8')


def play(name, exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(name, out / 'input.inputreplay')
    env = dict(os.environ, MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, SCENARIOS[name]["captures"]))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(Path(exe).resolve()), '--replay-input', str(out / 'input.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    check(name, out)


def check(name, out):
    for frame in SCENARIOS[name]['captures']:
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame
    print(name, out.name, 'captures', SCENARIOS[name]['captures'], flush=True)


def compare(name, root):
    """Scene Play, Release and Native AOT draw byte-identical captures."""
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    lines = ['frame\t' + '\t'.join(MODES) + '\tidentical']
    for frame in SCENARIOS[name]['captures']:
        hashes = [digest(root / mode / 'captures' / f'{frame:04}.png') for mode in MODES]
        assert len(set(hashes)) == 1, f'{name}: captures differ at frame {frame}: {hashes}'
        lines.append(f'{frame}\t' + '\t'.join(h[:12] for h in hashes) + '\tTrue')
    (root / 'comparison.tsv').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(lines))


if __name__ == '__main__':
    command, name, *paths = sys.argv[1:]
    {'play': play, 'compare': compare}[command](name, *(Path(p) for p in paths))
