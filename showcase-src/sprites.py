# Lists sprite renderers of a scene/prefab: name, size, colour or asset, draw mode.
import re, sys
for path in sys.argv[1:]:
    print("==", path)
    text = open(path).read()
    for block in re.split(r"\n(?=- id: )", text):
        if "type: spriteRenderer" not in block and '"spriteRenderer"' not in block: continue
        g = lambda k: (re.search(rf"^\s+\"?{k}\"?: \"?([^\"\n]+)", block, re.M) or [None, ""])[1]
        print(f"  {g('name'):24} {g('width'):>5}x{g('height'):<5} {g('squareColor'):8} {g('asset'):28} {g('drawMode')} {'[prefab]' if 'prefab:' in block else ''}")
