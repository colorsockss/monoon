"""Write engine/tests/fixtures/InventoryLab/Herbalist.inputreplay: the herbalist quest played in the herb yard showcase.

From the start (Scenes/Showcase.scene) the hero steps up to the herbalist, talks (T), asks about the request (1), accepts (1)
and leaves (1); walks right over the second herb, up and left over the first, up and left along the stall over the fourth,
back right to the herbalist; talks (T), asks about the reward (2), claims it (1) and saves (S). Every move key is held for a
whole number of 60 Hz frames (2.5 px each at YardWalker's 150 px/s). The save must equal Herbalist.save byte for byte.
"""
from pathlib import Path

PROJECT, START = 'ade09cc3-a769-56d7-9832-a2bea14c7e71', '0b0b8126-2ff1-5bbb-a7f0-5ef0af802bf8'
# (first frame, key, frames held)
HOLDS = [(10, 'Up', 10), (25, 'T', 1), (60, '1', 1), (80, '1', 1), (100, '1', 1),
         (110, 'Right', 80), (190, 'Up', 10), (200, 'Left', 10), (210, 'Up', 10), (220, 'Left', 100), (320, 'Right', 30),
         (360, 'T', 1), (380, '2', 1), (400, '1', 1), (420, 'S', 1)]
FRAMES = 440


def write(path):
    events = sorted(((at, key, down) for first, key, held in HOLDS for at, down in ((first, 'true'), (first + held, 'false'))),
                    key=lambda e: e[0])
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) + ']\nevents:\n'
                    + ''.join(f'- frame: {at}\n  timeTicks: {at * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n'
                              for at, key, down in events), encoding='utf-8')


if __name__ == '__main__':
    write(Path(__file__).resolve().parents[1] / 'fixtures/InventoryLab/Herbalist.inputreplay')
