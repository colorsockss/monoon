"""Exercise a real KeyRoom copy saved before a C# field rename."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
WORKSHOP = '98b900c4-6465-5df6-b547-6f8744d7123f'  # Scenes/MovementWorkshop.scene, a test-only scene outside the build list

def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    source = dest / 'Scripts/PlayerMovement.cs'
    script = source.read_text(encoding='utf-8')
    # A distinct initializer proves that both stored values, including the source 240, are used.
    assert 'public float MoveSpeed = 240f;' in script and '[FormerlySerializedAs("Speed")]' in script
    renamed = script.replace('MoveSpeed = 240f','MoveSpeed = 177f')
    (dest / 'renamed.source').write_text(renamed, encoding='utf-8')
    source.write_text(renamed.replace('    [FormerlySerializedAs("Speed")]\n','').replace('MoveSpeed','Speed'), encoding='utf-8')
    caller = dest / 'Scripts/PushCrate.cs'
    caller.write_text(caller.read_text(encoding='utf-8').replace('movement.MoveSpeed','movement.Speed'), encoding='utf-8')
    prefab = dest / 'Assets/MovementPlayer.prefab'
    digest = lambda: hashlib.sha256(prefab.read_bytes()).hexdigest().upper()
    current = digest()
    prefab.write_bytes(prefab.read_bytes().replace(b'MoveSpeed:', b'Speed:'))
    scene = dest / 'Scenes/MovementWorkshop.scene'
    text = scene.read_text(encoding='utf-8').replace(current, digest()).replace('MoveSpeed:', 'Speed:')
    assert text.count('Speed: 240') == 3 and text.count('Speed: 320') == 1
    handles = {}
    for block in text.split('\n- id: ')[1:]:
        key = block.splitlines()[0]
        for name in ['Inherited', 'Overridden']:
            if '\n  name: '+name+' player\n' in block: handles[name] = key
    anchor = '  - type: script\n    script: MovementWorkshop'
    # A read-only observer in the disposable project records both world displacements.
    scene.write_text(text.replace(anchor, '  - type: script\n    script: TraceSpeeds\n    values: {Inherited: "'+
        handles['Inherited']+'", Overridden: "'+handles['Overridden']+'"}\n'+anchor), encoding='utf-8')
    shutil.copyfile(HERE/'TraceSpeeds.source', dest/'Scripts/TraceSpeeds.cs')
    # The copy starts in the workshop and builds it.
    settings = dest / 'GameSettings'
    s = settings.read_text(encoding='utf-8')
    start = s.split('startScene: ', 1)[1].split('\n', 1)[0]
    settings.write_text(s.replace('startScene: ' + start, 'startScene: ' + WORKSHOP).replace('includedScenes:\n', f'includedScenes:\n- {WORKSHOP}\n', 1), encoding='utf-8')

def recording(path):
    frames = [166667]*30
    events = [(8,'Right',True),(23,'Right',False)]
    text = f'version: 1\nproject: {PROJECT}\nstartScene: {WORKSHOP}\nframeTicks: [{", ".join(map(str,frames))}]\nevents:\n'
    for frame,key,down in events:
        text += f'- frame: {frame}\n  timeTicks: {frame*166667}\n  input: {{kind: key, control: "{key}", down: {str(down).lower()}}}\n'
    path.write_text(text,encoding='utf-8')

def check(out):
    rows=(out/'trace.txt').read_text(encoding='utf-8').splitlines()
    assert len(rows)==1,rows
    a,b=map(float,rows[0].split())
    assert abs(a-60)<.001 and abs(b-80)<.001,(a,b)
    result=dict(ticks=15,sourceDistance=a,overrideDistance=b)
    (out/'result.json').write_bytes((json.dumps(result)+'\n').encode())
    print(result,flush=True)

def play(exe,out):
    out=out.resolve()
    out.mkdir(parents=True)
    recording(out/'Movement.inputreplay')
    env=dict(os.environ,MONOON_RENAME_TRACE=str(out/'trace.txt'),MONOON_USER_DATA=str(out/'profile'))
    subprocess.run([str(exe.resolve()),'--replay-input',str(out/'Movement.inputreplay'),str(out/'slot')],env=env,check=True)
    check(out)

if __name__=='__main__':
    {'prepare':prepare,'play':play,'check':check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
