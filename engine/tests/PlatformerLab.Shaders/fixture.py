"""Make an isolated PlatformerLab copy with the externally stepped test clock."""
import shutil
import sys
from pathlib import Path

test = Path(__file__).resolve().parent
sample = test.parents[2] / "samples/PlatformerLab"
fixture = Path(sys.argv[1]).resolve()
shutil.copytree(sample, fixture)
shutil.copy(test / "ShaderClock.cs", fixture / "Scripts")
scene = fixture / "Scenes/Platform.scene"
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
