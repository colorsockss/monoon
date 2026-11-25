"""Make an isolated PlatformerLab copy with the externally stepped test clock."""
import shutil
import sys
from pathlib import Path

test = Path(__file__).resolve().parent
sample = test.parents[2] / "samples/PlatformerLab"
fixture = Path(sys.argv[1]).resolve()
shutil.copytree(sample, fixture)
shutil.copy(test / "ShaderClock.cs", fixture / "Scripts")
# The torch shader flames are in the course showcase (the retired Platform scene had them until 2026-11-26): start there.
settings = fixture / "GameSettings"
settings.write_text(settings.read_text().replace("startScene: 40845ca7-0281-5a45-878a-c1da3b04f822", "startScene: e4366c3e-3cfa-5c9b-b11e-7e2f25ffb84f"))
scene = fixture / "Scenes/ShowcaseCourse.scene"
clock = """- id: 20be7cce-f7de-47e7-975f-930ccc395ee0
  name: Shader capture clock
  x: 0
  y: 0
  width: 1
  height: 1
  components:
  - type: script
    script: ShaderClock
"""
text = scene.read_text()
assert text.count("objects:\n") == 1
scene.write_text(text.replace("objects:\n", "objects:\n" + clock, 1))
