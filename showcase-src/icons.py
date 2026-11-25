# Adds item icons to InventoryLab's UI text (rich text <icon=name>).  Run once from samples/InventoryLab.
import pathlib, re, uuid

FILES = {"herb": "Herb", "crystal": "Crystal", "gem": "Gem", "key": "Key", "compass": "Compass", "bread": "Bread", "rope": "Rope", "lantern": "Lantern",
         "map": "Map", "flask": "Flask", "bandage": "Bandage", "arrow": "Arrow", "shield": "Shield"}


def asset(name):
    meta = pathlib.Path(f"Assets/Icons/{FILES[name]}.png.meta")
    if not meta.exists(): meta.write_text(f"id: {uuid.uuid4()}\n")
    return meta.read_text().split()[1]


def icons(**names):  # icon name in the text -> PNG
    return "    richText: true\n    icons: {" + ", ".join(f"{k}: {asset(v)}" for k, v in names.items()) + "}\n"


def edit(path, change):
    p = pathlib.Path(path); s = p.read_text()
    assert "richText: true\n    icons: {herb" not in s or "Inventory.scene" in path, f"{path} already has icons"
    p.write_text(change(s))


def text_line(s, old, new, extra, count=1):
    """Replace one uiText `text:` line and put the rich text settings right under it."""
    line = f'    text: "{old}"\n'
    assert s.count(line) == count, (old, s.count(line))
    return s.replace(line, f'    text: "{new}"\n' + extra)


# Workbench: the bag holds herb / gem / key, and the item key is the icon name so scripts can write <icon={item}>.
bag = icons(herb="herb", gem="crystal", key="key")
def workbench(s):
    for n, item, label in ((1, "herb", "herb"), (2, "gem", "crystal"), (3, "key", "key")):
        s = text_line(s, f"{n}     Add {label}", f"{n}    <icon={item}>  Add {label}", bag)
    for n, mark in ((1, "●"), (2, "○"), (3, "○")):
        s = text_line(s, f"{mark}   {n}     Empty", f"{mark}   {n}     Empty", bag)
    return text_line(s, "Herb   0 / 3     ·     Reward   1 Crystal", "<icon=herb> Herb   0 / 3     ·     Reward   <icon=gem> 1 Crystal", bag)
edit("Scenes/Inventory.scene", workbench)

def workbench_script(s):
    for old, new in (('$"{bag.Definition(stack.Item).Label}   {stack.Count}', '$"<icon={stack.Item}> {bag.Definition(stack.Item).Label}   {stack.Count}'),
                     ('$"{bag.Definition(quest.Item).Label}   {quest.Collected}', '$"<icon={quest.Item}> {bag.Definition(quest.Item).Label}   {quest.Collected}'),
                     ('$"Reward   {quest.RewardCount} ', '$"Reward   <icon={quest.Reward}> {quest.RewardCount} ')):
        assert s.count(old) == 1, old; s = s.replace(old, new)
    return s
edit("Scripts/InventoryWorkbench.cs", workbench_script)

# Bag grid: a large icon above the name. Slots swap their text, so every slot knows every icon.
grid_items = {"Herb": "herb", "Gem": "gem", "Brass key": "key", "Compass": "compass", "Bread": "bread", "Rope": "rope", "Lantern": "lantern", "Map": "map", "Flask": "flask"}
grid = icons(**{v: v for v in grid_items.values()})
def bag_grid(s):
    for label, icon in grid_items.items():
        s = text_line(s, label, f"<size=26><icon={icon}></size>\\n{label}", "")
    s, n = re.subn(r'(  name: "Slot \d+"\n(?:.*\n)*?    text: "[^\n]*"\n)', lambda m: m.group(1) + grid, s)
    assert n == 20, n
    s, n = re.subn(r'(    draggable: true\n  - type: uiText\n(?:    .*\n)*?    insetY: )18\n', r"\g<1>4\n", s)
    assert n == 20, n
    return s
edit("Scenes/BagGrid.scene", bag_grid)

def bag_script(s):
    old = '        string from = ui.TextOf(drop.Source) ?? "", to = ui.TextOf(drop.Target) ?? "";\n        ui.SetText(drop.Source, to);\n        ui.SetText(drop.Target, from);\n'
    assert s.count(old) == 1
    # The slot text is "<icon>\nName"; the status line names the item only.
    return s.replace(old, old + "        from = from[(from.LastIndexOf('\\n') + 1)..]; to = to[(to.LastIndexOf('\\n') + 1)..];\n")
edit("Scripts/Bag/BagGrid.cs", bag_script)

# Catalog: the list source reports each row's icon name as a field.
nouns = ["herb", "crystal", "key", "rope", "lantern", "map", "flask", "bandage", "arrow", "shield"]
edit("Scenes/Catalog.scene", lambda s: text_line(s, "{name}", "<icon={icon}>  {name}", icons(**{n: n for n in nouns})))
def catalog_script(s):
    old = '        "name" => items[index].Name,\n'
    assert s.count(old) == 1
    return s.replace(old, old + '        "icon" => items[index].Name[(items[index].Name.IndexOf(\' \') + 1)..].ToLowerInvariant(),\n')
edit("Scripts/Catalog/CatalogScreen.cs", catalog_script)
print("icons wired:", ", ".join(sorted(FILES)))
