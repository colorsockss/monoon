import sys, re, collections, pickle, html
# usage: features_cut.py <codegen.dgml.xml> <map.xml>
# What-if per engine feature: remove the engine nodes (Engine.Core/Desktop/Player/Brutal methods, types, statics) whose names
# match the feature, then sum the map bytes of everything no longer reachable. Approximates compiling the feature out.
dg, mp = sys.argv[1], sys.argv[2]
labels, rev = pickle.load(open(dg + '.pkl', 'rb'))
fwd = collections.defaultdict(list); need = {}
for t, ss in rev.items():
    for s, r in ss: fwd[s].append(t)
    if labels[t].startswith('('): need[t] = len({s for s, _ in ss})
roots = [i for i in labels if i not in rev]
def reach(removed):
    seen = set(roots) - removed; got = collections.Counter(); stack = list(seen)
    while stack:
        x = stack.pop()
        for t in fwd.get(x, ()):
            if t in seen or t in removed: continue
            if t in need:
                got[t] += 1
                if got[t] < need[t]: continue
            seen.add(t); stack.append(t)
    return seen
F = {
 'particles': r'Particle',
 'ui+text+dialogue': r'ScreenUi|_Ui[A-Z]|UiTheme|RichText|SdlText|TtfText|Brutal_Sdl_Ttf|SdlTtf|Localiz|WorldText|Text2D|DrawDialogue|UpdateUi|Dialogue|Glyph',
 'lighting': r'Light2D|PointLight|GlobalLight|Shadow|DrawLighting|Lighting',
 'navigation': r'Navigation|NavMesh|NavAgent|GridNavigation|GridTurns|Maze|GridRun|Pathfind|NavGrid',
 'tween+cutscene+paths': r'Tween|Cutscene|MotionPath|PathFollower|ComponentAnimation|ComponentTrack|ComponentKey|Timeline',
 'audio': r'Audio|SdlSounds|Brutal_Sdl_Mixer|SdlMixer|Mixer|Rhythm|SyncAudio',
 'physics': r'Rigidbody|Joint2D|Joint_|CharacterMotor|Physics|Box2D|Collider|Contact|CastBody|SweepBody|QueryCast|AddShape|CreateBody|CreateJoint|MovePlatform|Effector|TileShape|AddTileShapes|TileCollision|CollisionGeometry|StepRigidBodies|SceneGame_Body',
 'tilemap': r'Tilemap|TileWorld|Tileset|TileDefinition|TileChunk|RuntimeTiles|SoftwareTileTextures|Autotile|AutoTile|TileAnimation|Terrain|UpdateTileWorlds|AddTileCandidates|TilePage|TileCell',
 'skeleton/rig': r'Skeleton|_Rig[A-Z_]|FootIK|FootChain|BoneKey|ApplySubtreePose',
 'lines/trails': r'Line2D|Lines2D|Trail2D',
 'inventory/quest/deck/turn': r'Inventory|Quest|Deck|_Card|TurnReplay|GridReplay|RecordedTurn|GridCommand',
 'prefab pools': r'SpritePool|PrefabPool|Pool2D',
}
OWN = re.compile(r'^(_ZTV\d+|__GenericDict_|__NONGCSTATICS|__GCSTATICS|__FrozenObj_|unbox_|Boxed_)?(Engine_Core_|Engine_Desktop_|MoNoonPlayer_|Brutal_)')
own = [i for i, l in labels.items() if OWN.match(l)]
node = re.compile(r'<(\w+) Name="([^"]*)" Length="(\d+)"')
sizes = collections.Counter()
for line in open(mp, encoding='utf-8'):
    m = node.search(line)
    if m: sizes[html.unescape(m.group(2))] += int(m.group(3))
def name(i): return re.sub(r' (constructed|unconstructed)$', '', labels[i])
def bytes_of(ids):
    names = set()
    for i in ids:
        n = name(i); names.add(n)
        if not n.startswith('__GenericDict_'): names.add('__GenericDict_' + n)
    return sum(sizes.get(n, 0) for n in names)
base = reach(set())
print(f'map node bytes reachable: {bytes_of(base):,}')
results = []
allrx = []
subset = sys.argv[3].split(',') if len(sys.argv) > 3 else None
for f, rx in F.items():
    if subset and f not in subset: continue
    r = re.compile(rx); removed = {i for i in own if r.search(labels[i])}; allrx.append(rx)
    gone = (base - reach(removed)) | (removed & base)
    results.append((bytes_of(gone), f, len(removed)))
for b, f, n in sorted(results, reverse=True): print(f'  {b:>10,}  {f}  ({n} matched engine nodes)')
r = re.compile('|'.join(allrx)); removed = {i for i in own if r.search(labels[i])}
gone = (base - reach(removed)) | (removed & base)
print(f'  {bytes_of(gone):>10,}  ALL OF THE ABOVE')
print(f'  (sum of singles {sum(b for b, _, _ in results):,})')
