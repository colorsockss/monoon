"""A CollisionLab copy for the harnesses that drive the stress fields.

The sample ships its showcases only; Field1000 and Field10000 are test-only scenes and FieldSelector (1, 2: open a field,
3: load the large field in steps, 4: add it, 0: cancel, Escape: back) is a test-only control. prepare() copies the sample,
starts it in ShowcaseCasts (the probe course, which keeps the lab's scene id), puts FieldSelector on that scene's camera and
adds both fields to the included scenes.

    import sys; sys.path.insert(0, '<repo>/engine/tests/Shared'); import collision_lab; collision_lab.prepare(sample, dest)
"""
from pathlib import Path
import re
import shutil

LAB = '5750e5bb-1700-5a5b-94c2-b6bd02a0356c'           # Scenes/ShowcaseCasts.scene (also the project id)
SMALL, LARGE = '2fe2804d-e1df-55de-8adb-fcf5a0f4217d', 'ae245142-c47c-52aa-8b4f-5c867bdef887'
SELECTOR = ('  - type: script\n    script: FieldSelector\n'
            f'    values: {{"Small": {{"Id": "{SMALL}"}}, "Large": {{"Id": "{LARGE}"}}, "Lab": {{"Id": "{LAB}"}}}}\n')


def prepare(sample, dest, ignore=('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj')):
    sample, dest = Path(sample), Path(dest)
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns(*ignore))
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    text = re.sub(r'(?m)^startScene: .*$', f'startScene: {LAB}', text, count=1)
    for scene in (SMALL, LARGE):
        if f'- {scene}\n' not in text: text = text.replace('includedScenes:\n', f'includedScenes:\n- {scene}\n', 1)
    settings.write_text(text, encoding='utf-8')
    scene = dest / 'Scenes/ShowcaseCasts.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '  - type: camera2D\n'
    assert text.count(anchor) == 1
    scene.write_text(text.replace(anchor, anchor + SELECTOR, 1), encoding='utf-8')
    return dest


if __name__ == '__main__':
    import sys
    print(prepare(sys.argv[1], sys.argv[2]))
