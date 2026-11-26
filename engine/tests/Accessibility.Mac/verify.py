from pathlib import Path
import subprocess as sp,time,sys,os,signal,shutil,tempfile
root=Path(__file__).resolve().parents[3]; temp=Path(tempfile.mkdtemp(prefix="monoon26-gui-")); print(temp,flush=True)
out=root/"engine/tests/results/accessibility";out.mkdir(exist_ok=True)
test=temp/"AccessLab";shutil.copytree(root/"samples/AccessLab",test,ignore=shutil.ignore_patterns('bin','obj'))
profile=temp/"profiles"
tool=root/"engine/.artifacts/clip-qa/window"; pid=0; proc=None; log=open(temp/"editor.log","w")
def invoke(*args):
 for attempt in range(8):
  try:return sp.check_output([tool,args[0],str(pid),*map(str,args[1:])],stderr=sp.PIPE)
  except sp.CalledProcessError as exc:
   if exc.returncode!=7 or attempt==7:raise
   time.sleep(.25)
def info():return list(map(int,invoke("info").split()))
def click(x,y,mode="click"):
 _,px,py,_,_=info();invoke(mode,px+x,py+y)
def snap(k):
 w,_,_,ww,hh=info();src=temp/'screen.png';sp.run(['screencapture','-x','-o','-l',str(w),str(src)],check=True)
 sp.run(['sips','-z',str(hh),str(ww),str(src),'--out',str(out/(k+'.png'))],stdout=sp.DEVNULL,check=True)
def key(k):invoke('key',k);time.sleep(.09)
def close():
 global pid,proc
 if not pid:return
 try: os.kill(pid,signal.SIGTERM)
 except ProcessLookupError:pass
 if proc:proc.wait(timeout=7)
 pid=0;proc=None;time.sleep(.3)
def launch(mode):
 global pid,proc,profile
 close()
 profile=temp/mode
 path=temp/(mode+'.pad');path.touch()
 env=dict(os.environ,MONOON_USER_DATA=str(profile),MONOON_QA_PAD=str(path),DYLD_INSERT_LIBRARIES=os.environ['MONOON_QA_PAD_LIBRARY'])
 if mode=='editor':
  args=[root/'editor/MoNoon.app/Contents/MacOS/MoNoon',test]
 else:
  bundle=root/'game-builds/AccessLab'/('NativeAOT' if mode=='aot' else '')/'AccessLab.app'
  args=[bundle/'Contents/MacOS/MoNoonPlayer']
 proc=sp.Popen(args,env=env,stdout=log,stderr=log);pid=proc.pid
 for i in range(65):
  try:info();break
  except sp.CalledProcessError:time.sleep(.2)
 time.sleep(.85)
 print(mode,pid,info(),flush=True)
def play():
 def in_play():
  dump=sp.check_output([root/"engine/.artifacts/tile-qa/access",str(pid)],stderr=sp.DEVNULL,text=True)
  return any(line.startswith("AXButton") and line.rstrip().endswith("Stop") for line in dump.splitlines())
 for attempt in range(3):
  click(info()[3]//2+30,43) # the Play button sits just right of the centred toolbar
  for j in range(12):
   time.sleep(.2)
   if in_play():return
 raise RuntimeError("Play button did not enter game mode")


ime=os.environ['MONOON_QA_INPUT_SOURCE'];ko='com.apple.inputmethod.Korean.2SetKorean'
original=sp.check_output([ime],text=True).strip()
def pad(mode,kind,idx,value):
 for v in (value,0):
  with (temp/(mode+'.pad')).open('a') as f:f.write(f'{kind} {idx} {v}\n')
  time.sleep(.32)
try:
 for mode in (sys.argv[1:] or ["editor","release","aot"]):
  launch(mode)
  if mode=='editor':play()
  time.sleep(.8);snap(mode+'-ready')
  # Select the input method before entering the field; changing an active IME may commit its marks.
  if sp.check_output([ime],text=True).strip()!=ko:sp.run([ime,ko,'select'],check=True)
  win=info()
  # ShowcaseOptions: the name field sits at (637, 93) in the 800 x 450 game.
  if mode=='editor':click(831,295) # Game view at (181, 200), 1.02 x
  else:click(637,win[4]-450+93)
  time.sleep(1.2) # a fresh player window can take the first jamo before the IME attaches
  for k in [5,40,1,15,46,3]:key(k)
  snap(mode+'-composition')
  key(49);key(51)
  key(48);key(36);key(126);key(36) # language dropdown: open, English -> 日本語, pick
  key(48);key(124);key(124) # text size slider 100 -> 150 %
  time.sleep(.2);snap(mode+'-large')
  pad(mode,'b',12,1);pad(mode,'b',0,1) # D-pad to the contrast toggle, A turns it on
  pad(mode,'b',12,1);pad(mode,'b',12,1) # shake and flash sliders
  pad(mode,'a',1,27000);pad(mode,'b',0,1) # stick reveals the save button, A saves
  time.sleep(.4);snap(mode+'-saved')
  reports=list(profile.rglob('player.settings'))
  print('save:',reports[0].read_text() if reports else 'missing',flush=True)
  assert len(reports)==1,'missing save'
  data=reports[0].read_text()
  for item in ['name: "한글"','accessibility.locale: "ja"','accessibility.textScale: 1.5','accessibility.highContrast: true','saveFocused: true','sawComposition: true','scroll: 45.6']:
   assert item in data,(mode,item,data)
  (out/(mode+'.settings')).write_bytes(reports[0].read_bytes())
  timestamp=reports[0].stat().st_mtime_ns
  invoke('resize',1100 if mode=='editor' else 920,720 if mode=='editor' else 660);time.sleep(.5)
  # Locate the yellow focus rectangle in the captured window, then click its geometric center.
  # This works in the editor panel and in both standalone letterboxed windows. A firework flash can wash the ring out of
  # one capture, so capture again a few times.
  png=out/(mode+'-resized.png')
  for attempt in range(6):
   snap(mode+'-resized')
   ring=sp.run([os.environ['MONOON_QA_RING'],png],capture_output=True,text=True)
   if ring.returncode==0:break
   time.sleep(.4)
  n,x1,y1,x2,y2=map(int,ring.stdout.split())
  assert n>50 and x2-x1>180 and y2-y1>25,(mode,n,x1,y1,x2,y2)
  click((x1+x2)//2,(y1+y2)//2);time.sleep(.35)
  assert reports[0].stat().st_mtime_ns>timestamp and reports[0].read_text()==data,'resized click did not save the same UI state'
  print(mode,'resized',info()[3:],'focus ring',x1,y1,x2,y2,'click saved',flush=True)
finally:
 sp.run([ime,original,'select']);close();log.close()
