"""Create a playable InventoryLab build demo in a new directory. Run from any checkout."""
import argparse
from pathlib import Path
import shutil

SAMPLES = Path(__file__).resolve().parents[3] / "samples"
# Over the herb yard (533 × 300 world units): a torch-shader lantern on the right fence, a paving strip along the bottom.
SCENERY = """- id: 1068d86a-438d-437d-b730-d311ba833a15
  name: Market lantern
  x: 500
  y: 20
  width: 24
  height: 32
  components: [{type: spriteShader2D, shader: 0fb966b8-d9b4-4ac6-8875-a5a0514fd681}]
- id: 60497785-94b7-49ec-a649-e72a2dc0149b
  name: Market paving
  x: 0
  y: 292
  components:
  - type: tilemap2D
    tileset: 04a6e926-3487-575d-a5e8-83908efd75c5
    streamRadius: 2
    cellSize: {width: 8, height: 8}
    chunks:
    - {x: 0, y: 0, cells: [{index: 31, tile: 1}]}
    - {x: 1, y: 0, cells: [{index: 0, tile: 1}]}
    - {x: 3, y: 0, cells: [{index: 0, tile: 1}]}
"""


def prepare(dest):
    shutil.copytree(SAMPLES / "InventoryLab", dest, ignore=shutil.ignore_patterns(".DS_Store", ".monoon"))
    assets = dest / "Assets" / "MarketArt"
    assets.mkdir()
    ridge = SAMPLES.parent / "engine/tests/fixtures/PlatformerRidge/Assets"
    for folder, name in ((SAMPLES / "PlatformerLab/Assets", "Torch.shader"), (ridge, "Ridge.tileset"), (ridge, "Terrain.png")):
        for suffix in ("", ".meta"):
            shutil.copy2(folder / (name + suffix), assets)
    scene = dest / "Scenes/Showcase.scene"
    text = scene.read_text(encoding="utf-8")
    assert text.count("\ndefaultCameraId:") == 1
    scene.write_text(text.replace("\ndefaultCameraId:", "\n" + SCENERY + "defaultCameraId:"), encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    prepare(parser.parse_args().directory)
