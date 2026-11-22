"""Replay the sample through its normal input actions and check every fixed tick."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
LEDGE = 'a2f7dad5-3289-4f71-80ad-0658ff909109'
SLOPE = 'f2ae5d95-77d2-5ef3-b2c8-d1777109540b'

def prepare(sample, dest):
    shutil.copytree(sample,dest,ignore=shutil.ignore_patterns('.monoon','.DS_Store','bin','obj'))
    shutil.copyfile(HERE/'TraceRunner.source', dest/'Scripts/TraceRunner.cs')
    p = dest/'Scenes/Platform.scene'
    p.write_text(p.read_text(encoding='utf-8').replace('    script: "Runner"', '    script: TraceRunner\n  - type: script\n    script: "Runner"',1),encoding='utf-8')

def check(out):
    rows = []
    for line in (out/'trace.tsv').read_text(encoding='utf-8').splitlines():
        i,x,y,vy,support,nx,ny = line.split(' ')
        rows.append((int(i),float(x),float(y)+30,float(vy),support,float(nx),float(ny)))
    assert [r[0] for r in rows] == list(range(380)), (len(rows),rows[:2])
    assert rows[5][2] == 330 and rows[5][2]-30 > 288
    assert any(r[2] < 288 and r[3] < 0 and not r[4] for r in rows[8:40])
    first = next(r for r in rows if r[4] == LEDGE)
    assert rows[first[0]-1][3] > 0 and abs(first[2]-288)<.001, first
    assert all(abs(r[2]-288)<.001 and r[4]==LEDGE for r in rows[first[0]:73])
    result = {'firstLandingTick':first[0], 'landingFeetY':first[2], 'jumpApexFeetY':min(r[2] for r in rows[:73])}
    for label,part,sign in [('ascent',rows[73:211],1),('descent',rows[221:375],-1)]:
        climb = [r for r in part if 168 < r[1] < 344]
        assert climb, label
        seams = [218,256,294,332]
        crossed = [s for s in seams if min(r[1]+22 for r in climb) < s < max(r[1]+22 for r in climb)]
        assert crossed == seams, (label,crossed,part[-1])
        assert all(r[4]==SLOPE for r in climb), (label,next(r for r in climb if r[4]!=SLOPE))
        advance = [sign*(b[1]-a[1]) for a,b in zip(climb,climb[1:])]
        error = max(abs(r[2] - (330-(r[1]+22-180)*90/190)) for r in climb)
        assert min(advance)>2.45 and max(advance)<2.55 and error<.01,(label,min(advance),max(advance),error)
        result[label] = {'groundedTicks':len(climb),'seams':len(crossed),'maxSurfaceErrorPx':round(error,4),'minAdvancePx':round(min(advance),4)}
    (out/'result.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result),flush=True)

def play(exe,out):
    out.mkdir(parents=True)
    env=dict(os.environ,MONOON_TILE_TRACE=str((out/'trace.tsv').resolve()),MONOON_USER_DATA=str((out/'profile').resolve()))
    subprocess.run([str(exe.resolve()),'--replay-input',str(HERE/'Traverse.inputreplay'),str((out/'slot').resolve())],env=env,check=True)
    check(out)

if __name__=='__main__':
    {'prepare':prepare,'play':play,'check':check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
