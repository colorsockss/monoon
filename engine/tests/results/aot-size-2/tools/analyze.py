import sys, re, collections, html
# usage: analyze.py <map.xml> [--top N] [--grep RX]
# Attributes ILC map nodes (code, GC info, EETypes, generic dictionaries, statics) to buckets:
#  - VYaml generated formatters (per component), STJ, LINQ, record synth members, generic instantiations over value types,
#  - engine features (type-name patterns), BCL by assembly.
argv = sys.argv[1:]
path = argv[0]
top = int(argv[argv.index('--top') + 1]) if '--top' in argv else 40
grep = re.compile(argv[argv.index('--grep') + 1]) if '--grep' in argv else None
node = re.compile(r'<(\w+) Name="([^"]*)" Length="(\d+)"')
CODE = {'MethodCode', 'GCInfo', 'MethodExceptionHandlingInfo', 'MethodAssociatedData', 'UnboxingStub', 'MethodGenericDictionary'}
TYPE = {'ConstructedEEType', 'EEType', 'TypeGenericDictionary', 'InterfaceDispatchMap', 'SealedVTable', 'GenericComposition',
        'NonGCStatics', 'GCStatics', 'GCStaticsPreInitData', 'WritableData', 'ReflectionVisibleGenericDefinitionEEType', 'GCStaticEEType'}
ASM = [('S_P_CoreLib_', 'CoreLib'), ('S_P_Reflection', 'Reflection'), ('S_P_TypeLoader_', 'TypeLoader'), ('S_P_StackTraceMetadata', 'StackTraceMetadata'),
       ('System_Text_Json_', 'STJ'), ('System_Linq_Expressions', 'Linq.Expressions'), ('System_Linq_', 'LINQ'), ('System_Collections_Immutable', 'Collections.Immutable'),
       ('System_Collections_Concurrent', 'Collections.Concurrent'), ('System_Collections_', 'Collections'), ('System_IO_Compression', 'IO.Compression'),
       ('System_Console', 'Console'), ('System_Text_Encodings_Web', 'Encodings.Web'), ('System_Text_RegularExpressions', 'Regex'),
       ('System_Private_Uri', 'Uri'), ('System_Memory', 'Memory'), ('System_Runtime', 'Runtime*'), ('System_Threading', 'Threading*'), ('System_Diagnostics', 'Diagnostics*'),
       ('System_Security', 'Security*'), ('System_ComponentModel', 'ComponentModel*'), ('System_', 'System.* other'),
       ('Microsoft_Extensions_', 'M.E.*'), ('VYaml_', 'VYaml'), ('Engine_Core_', 'Engine.Core'), ('Engine_Desktop_', 'Engine.Desktop'),
       ('MoNoonPlayer_', 'MoNoonPlayer'), ('MoNoon_GameScripts', 'GameScripts'), ('Brutal_Box2D', 'Box2D'), ('Brutal_Sdl_Ttf', 'Brutal.Ttf'),
       ('Brutal_Sdl_Mixer', 'Brutal.Mixer'), ('Brutal_', 'Brutal other'), ('CommunityToolkit', 'CommunityToolkit')]
def asm_of(n):
    n = n.lstrip('_')
    for p, a in ASM:
        if n.startswith(p): return a
    for pre in ('__writableData', '__GCStaticBase_', '__NonGCStaticBase_', '__GCStaticEEType_', '__ThreadStaticBase', 'unbox_'):
        if n.startswith(pre.lstrip('_')): return asm_of(n[len(pre.lstrip('_')):])
    return '(other)'
FEATURES = [
 ('physics', r'Rigidbody2D|Joint2D|CharacterMotor|ScenePhysics|Physics2D|Box2D|FootChain|ColliderGrid|SceneContacts|CollisionGeometry|TileCollision|PhysicsMaterial|Collider2D|BoxCollider|CircleCollider|PolygonCollider|CapsuleCollider|EdgeCollider|Effector2D|Platformer|Slope|OneWay'),
 ('audio', r'Audio|SdlSounds|MacAudioOutput|Brutal_Sdl_Mixer|SdlMixer|Rhythm|Mixer'),
 ('ui/text', r'ScreenUi|_Ui[A-Z]|UiTheme|UiGrid|RichText|SdlText|TtfText|Brutal_Sdl_Ttf|SdlTtf|Localization|WorldText|TextRenderer|Glyph|Font'),
 ('tilemap', r'Tilemap|TileGrid|TileWorld|Tileset|TileDefinition|TilePalette|RuntimeTiles|SoftwareTileTextures|TileChunk|RenderGrid|AutoTile|Autotile|TileAnimation|Terrain'),
 ('skeleton/rig', r'Skeleton|_Rig[A-Z_]|RigBone|FootIK|SkeletonPose'),
 ('particles', r'Particle'),
 ('lighting', r'Light2D|PointLight|GlobalLight|ShadowCaster|Lighting'),
 ('fx/lines/shader', r'Line2D|Lines2D|Trail2D|Effects2D|ScreenEffect|ShaderClip|SpriteShader|BakedShader|ShaderSheet|PostEffect'),
 ('cutscene/tween', r'Cutscene|MotionPath|PathFollower|Tween|ComponentAnimation|Timeline'),
 ('navigation', r'Navigation|NavMesh|NavAgent|NavGrid|GridNavigation|GridTurns|MazeLayout|GridRunRules|Pathfind'),
 ('dialogue/quest/inventory/deck', r'Dialogue|Quest|Inventory|Deck|Card|Turn[A-Z]|TurnReplay|Generation|Procedural'),
 ('animator', r'Animator|AnimationController|AnimationState|AnimationClip|AnimationCondition|AnimationTransition|AnimationParameter|SpriteClip'),
 ('replay/crash/profile', r'InputReplay|ReplayInput|ReplayCapture|CrashReport|CrashFrame|CrashDiagnostics|FrameProfile|PlayDiagnostics|Profiler'),
 ('mods/updates/bundles/stream', r'GameMods|ContentUpdate|ContentBundle|BundleMount|SceneStream|Streaming|SplitLoading'),
 ('savedata/playerdata', r'PlayerData|SaveData|SaveSlot|Checkpoint|PlayerPrefs'),
 ('input', r'InputAction|InputBinding|Rebind|Gamepad|Keyboard|InputMap|InputSystem'),
 ('prefab/pool', r'Prefab|Pool'),
 ('camera', r'Camera'),
]
FEATURES = [(g, re.compile(rx)) for g, rx in FEATURES]
RECORD = re.compile(r'__(PrintMembers|ToString|GetHashCode|Equals(_\d+)?|_Clone__|get_EqualityContract|Deconstruct|op_Equality|op_Inequality)$')
def unesc(s): return html.unescape(s)
buckets = collections.defaultdict(collections.Counter)  # dimension -> key -> bytes
kinds = collections.Counter(); grepped = []; total = 0
for line in open(path, encoding='utf-8'):
    m = node.search(line)
    if not m: continue
    kind, name, n = m.group(1), unesc(m.group(2)), int(m.group(3))
    name = re.sub(r'^(_ZTV\d+|__GenericDict_|__writableData|__GCStaticBase_|__NonGCStaticBase_|__GCStaticEEType_\w*?_|__ThreadStaticBase_|unbox_|__InterfaceDispatchMap_|__SealedVTable_|__GenericComposition_)', '', name)
    kinds[kind] += n; total += n
    if grep and grep.search(name): grepped.append((n, kind, name))
    a = asm_of(name)
    buckets['asm'][a] += n
    if kind not in CODE and kind not in TYPE: buckets['other-kind'][kind] += n; continue
    outer = name.split('<', 1)[0]
    if 'GeneratedFormatter' in name:
        f = re.search(r'(\w+?)GeneratedFormatter', name)
        meth = 'Deserialize' if '__Deserialize' in name else ('Serialize' if '__Serialize' in name else 'other')
        buckets['vyaml-formatter'][f.group(1).split('_')[-1] if f else '?'] += n
        buckets['vyaml-formatter-method'][meth] += n
    if a == 'STJ' or 'System_Text_Json' in name: buckets['cross'][ 'STJ (incl. inst. over STJ)'] += n
    if a == 'LINQ' or 'System_Linq' in name: buckets['cross']['LINQ (incl. inst.)'] += n
    if a == 'VYaml' or 'VYaml' in name: buckets['cross']['VYaml (incl. inst. & formatters)'] += n
    if kind == 'MethodCode' and RECORD.search(name) and a in ('Engine.Core', 'Engine.Desktop', 'MoNoonPlayer'):
        buckets['record-synth'][re.sub(r'^.*__', '', name)] += n
    if '<' in name:
        args = name.split('<', 1)[1]
        vt = 'canon-shared code' if 'System___Canon' in args else 'exact instantiation'
        gen = re.sub(r'_\d+$', '', outer.split('__')[0])
        buckets['generic-def'][gen] += n
        buckets['generic-share'][vt] += n
        # owner of the type argument (e.g. Engine.Core)
        buckets['generic-arg-asm'][asm_of(args)] += n
    if a in ('Engine.Core', 'Engine.Desktop', 'MoNoonPlayer', 'Box2D', 'Brutal.Ttf', 'Brutal.Mixer') or (a in ('CoreLib', 'Collections', 'LINQ', 'VYaml', 'STJ') and ('Engine_Core' in name or 'Engine_Desktop' in name)):
        for g, rx in FEATURES:
            if rx.search(name): buckets['feature'][g] += n; break
        else: buckets['feature']['(engine other)'] += n
    if a in ('Engine.Core', 'Engine.Desktop'):
        t = re.sub(r'^(?:__\w+?)?Engine_(?:Core|Desktop)_Engine_(?:Core|Desktop)_', '', outer).split('__')[0]
        buckets['engine-type'][t] += n
print(f'total node bytes {total:,}')
for dim in ['asm', 'cross', 'feature', 'vyaml-formatter-method', 'vyaml-formatter', 'record-synth', 'generic-share', 'generic-arg-asm', 'generic-def', 'engine-type', 'other-kind']:
    c = buckets[dim]; s = sum(c.values())
    print(f'\n== {dim} (sum {s:,})')
    for k, v in c.most_common(top): print(f'  {v:>10,}  {k}')
if grep:
    grepped.sort(reverse=True); print(f'\n== grep {grep.pattern}: {sum(g[0] for g in grepped):,} in {len(grepped)} nodes')
    for g in grepped[:top]: print(f'  {g[0]:>8,} {g[1]:20} {g[2][:200]}')
