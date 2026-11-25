# Adds the skeletal rig to PlatformerLab's showcase (Scenes/Showcase.scene): a figure built from Assets/Runner.rig that jogs in
# place by the start sign (Skeleton Animator 2D on the time clock), and its HUD tag. BeatFlags raises "rig" while the runner
# is near it. The runner itself walks with sprites (user decision 2026-11-25); foot IK was removed from the engine (2026-11-26).
#   python3 showcase-src/platformer/rig.py      (after scene.py; running it again leaves the scene unchanged)
import pathlib, re, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/PlatformerLab"
scene = ROOT / "Scenes/Showcase.scene"
NS = uuid.UUID("3b309c28-75ce-562e-92ac-f94ee48b18c7")
rig_id = str(uuid.uuid5(NS, "showcase/rig/Rig runner"))
text = scene.read_text()
if rig_id not in text:
    rig = (ROOT / "Assets/Runner.rig.meta").read_text().split()[1]
    figure = (f"- id: {rig_id}\n  name: Rig runner\n  x: -20\n  y: 300\n  width: 22\n  height: 30\n  components:\n"
              f"  - type: skeletonAnimator2D\n    rig: {rig}\n    order: 3\n    clock: Time\n    period: 0.7\n")
    sign = re.search(r"(?m)^- id: \S+\n  name: Sign\n", text).start()
    text = text[:sign] + figure + text[sign:]
    tag5 = re.search(r"(?ms)^- id: \S+\n(?:  parentId: \S+\n)?  name: Tag 5\n.*?(?=^- id: |^defaultCameraId:)", text)
    tag6 = re.sub(r"^- id: \S+", f"- id: {uuid.uuid5(NS, 'showcase/rig/Tag 6')}", tag5[0])
    tag6 = (tag6.replace("  name: Tag 5\n", "  name: Tag 6\n").replace("  y: 124\n", "  y: 152\n").replace("showIf: ride", "showIf: rig")
            .replace("<b>Kinematic deck</b>   <color=#BFD0DC>Carries its rider</color>", "<b>Skeletal rig</b>   <color=#BFD0DC>Bones keyed over one stride</color>"))
    text = text[:tag5.end()] + tag6 + text[tag5.end():]
    text = re.sub(r'(script: BeatFlags\n    values: \{[^}]*)\}', lambda m: m[1] + f', "Rig": "{rig_id}"}}', text, count=1)
    scene.write_text(text)
print("Showcase.scene has the rig runner", rig_id)
