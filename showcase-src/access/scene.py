# Writes AccessLab's showcase scene Scenes/ShowcaseOptions.scene. Run after art.py:  python3 showcase-src/access/scene.py
#
# Left: a night festival where FireworkShow (Scripts/Showcase) launches rockets that shake and flash the screen. Right: a player
# settings panel (name field, language dropdown, text size / shake / flash sliders, high contrast toggle, save). OptionSetting
# applies each change to Ui.Preferences at once, so every label re-translates and re-flows, and the next burst obeys the new
# shake and flash scales.
import pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/AccessLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("9334557b-6911-5bb6-851d-5e22dc632773")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"


def text(en, ko, ja, size=13, color="#F4F6F4", wrap=False, extra=""):
    return (f'  - type: uiText\n    text: "{en}"\n    translations: {{"ko": "{ko}", "ja": "{ja}"}}\n    size: {size}\n    color: "{color}"\n'
            + ("    wrap: true\n" if wrap else "") + extra)


def script(name, values):
    return f"  - type: script\n    script: Showcase/{name}\n    values: {{" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n"


s = Scene("options")
s.obj("Sky", 0, 0, 800, 300, '  - type: spriteRenderer\n    squareColor: "#141C38"\n    order: -30\n')
s.obj("Sky low", 0, 190, 800, 60, '  - type: spriteRenderer\n    squareColor: "#172040"\n    order: -29\n')
s.obj("Sky horizon", 0, 250, 800, 50, '  - type: spriteRenderer\n    squareColor: "#1B2547"\n    order: -29\n')
s.obj("Stars", 0, 0, 800, 220, sprite("Stars.png", drawMode="Tiled", order=-28))
s.obj("Ground", 0, 300, 800, 150, sprite("Grass.png", drawMode="Tiled", order=-20, tint='"#5C7488"'))
lw, lh = png_size("Launcher.png")
for i, x in enumerate((150, 300)):
    s.obj(f"Launcher {i + 1}", x, 330 - lh, lw, lh, sprite("Launcher.png", order=2))
    s.obj(f"Rockets {i + 1}", x + lw / 2, 330 - lh + 4, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Rockets.particles')}\n    trigger: Manual\n    order: 3\n")
    # As in ParticleLab, a scene emitter for the burst asset, so the rocket's death sub-emitter has its particles loaded.
    s.obj(f"Burst {i + 1}", 0, 0, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Burst.particles')}\n    trigger: Manual\n    order: 3\n", parent=f"Rockets {i + 1}")
hw, hh = png_size("HeroIdleE.png")
s.obj("Hero", 52, 372 - hh, hw, hh, sprite("HeroIdleE.png", order=10))
bw, bh = png_size("Herbalist.png")
s.obj("Herbalist", 380, 372 - bh, bw, bh, sprite("Herbalist.png", order=10, flipX="true"))
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")

# Speech bubble over the herbalist: one line in three languages.
# UI that is not in the settings panel widens with the text scale (scaleHorizontalWithText) and is sized so 200 % still fits
# left of the panel.
s.obj("Speech", 20, 250, 200, 34, "  - type: uiRect\n    scaleHorizontalWithText: true\n" + panel(), parent="Canvas")
s.obj("Speech text", 10, 4, 180, 26, "  - type: uiRect\n    scaleHorizontalWithText: true\n" + text("The festival fireworks!", "축제 불꽃놀이예요!", "お祭りの花火ですよ！", 14, wrap=True), parent="Speech")

# Settings panel: a vertical, scrolling layout, so longer translations and larger text re-flow and scroll.
PW = 300
s.obj("Settings", -12, 12, PW, 426, "  - type: uiRect\n    anchor: TopRight\n" + panel()
      + "  - type: uiLayout\n    axis: Vertical\n    gap: 4\n    padding: 12\n    stretch: true\n  - type: uiScroll\n", parent="Canvas")
row = 0
def child(name, h, comps):
    global row
    row += 1
    return s.obj(f"{row:02d} {name}", 0, 0, PW - 24, h, "  - type: uiRect\n" + comps, parent="Settings")
child("Title", 30, text("Player settings", "플레이어 설정", "プレイヤー設定", 20, wrap=True))
def label(en, ko, ja): return child(en, 18, text(en, ko, ja, 12, "#BFD0DC", wrap=True))
label("Name", "이름", "名前")
name = child("Name field", 28, "  - type: uiTextField\n    maxLength: 24\n" + text("Type a name", "이름을 입력하세요", "名前を入力", 14, "#E4ECF2", extra="    insetX: 8\n    insetY: 3\n"))
label("Language", "언어", "言語")
values = lambda action, extra={}: {"Action": action} | extra
lang = child("Language list", 28, '  - type: uiDropdown\n    options: ["한국어", "日本語", "English"]\n    initial: 2\n' + script("OptionSetting", values(1)))
label("Text size", "글자 크기", "文字サイズ")
child("Text size slider", 26, "  - type: uiSlider\n    minimum: 75\n    maximum: 200\n    step: 25\n    initial: 100\n" + script("OptionSetting", values(2)))
label("High contrast", "고대비", "ハイコントラスト")
child("Contrast toggle", 26, "  - type: uiToggle\n    initial: false\n" + script("OptionSetting", values(3)))
label("Screen shake", "화면 흔들림", "画面の揺れ")
child("Shake slider", 26, "  - type: uiSlider\n    minimum: 0\n    maximum: 100\n    step: 25\n    initial: 100\n" + script("OptionSetting", values(4)))
label("Flashes", "번쩍임", "フラッシュ")
child("Flash slider", 26, "  - type: uiSlider\n    minimum: 0\n    maximum: 100\n    step: 25\n    initial: 100\n" + script("OptionSetting", values(5)))
save = s.oid(f"{row + 1:02d} Save")   # the button's own text becomes the receipt, so it stays in view while focused
child("Save", 30, "  - type: uiButton\n" + text("Save settings", "설정 저장", "設定を保存", 14, extra="    insetX: 10\n    insetY: 4\n")
      + script("OptionSetting", values(6, {"NameField": f'"{name}"', "Language": f'"{lang}"', "Receipt": f'"{save}"'})))

tags = [("lang", "Three languages", "ko · ja · en"), ("scale", "Text scale", "75-200%"),
        ("contrast", "High contrast", "focus ring"), ("calm", "Calmer screen", "less shake, flash")]
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 12 + i * 28, tw, 24, f"  - type: uiRect\n    showIf: {flag}\n    scaleHorizontalWithText: true\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
s.obj("Caption", 12, -8, 226, 26, "  - type: uiRect\n    anchor: BottomLeft\n    scaleHorizontalWithText: true\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 4, 210, 18, '  - type: uiRect\n    scaleHorizontalWithText: true\n  - type: uiText\n    richText: true\n    text: "<b>AccessLab</b>   Player settings"\n    size: 13\n', parent="Caption")
s.obj("Show", 0, 0, 1, 1, script("FireworkShow", {"Rockets": "[" + ", ".join(f'"{s.oid(f"Rockets {i + 1}")}"' for i in range(2)) + "]"}))
(ROOT / "Scenes/ShowcaseOptions.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseOptions.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseOptions.scene')}\n")
print("scene", meta.read_text().split()[1], "rockets", asset("Rockets.particles"))
