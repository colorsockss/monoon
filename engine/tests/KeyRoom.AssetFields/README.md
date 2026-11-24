# Inspector 에셋 필드·편집 회귀 (13.5·13.6·13.7·13.9·13.10·13.15·13.16·13.17)

KeyRoom·CollisionLab·ParticleLab 복사본으로 로드맵 13.7(참조 피커), 13.9(색 피커), 13.15(곡선·그라데이션 행), 13.17(Unity 오브젝트 필드식 에셋 놓기)을 패키지 에디터의 실제 UI 처리기로 확인하고, Inspector에서 고른 결과가 Scene Play·Release·Native AOT에 같게 나오는지 본다.

## 준비 ([verify.py](verify.py) `prepare`)

KeyRoom을 복사하고 다음을 더한다. ID는 고정이다.

| 추가 | 쓰임 |
| --- | --- |
| `Assets/QaChime.wav` | Project에서 Sound 필드로 끄는 WAV |
| `Assets/QaSparks.particles` + **QA Sparks** 방출기 | Particle 패널 Texture 필드에 PNG를 끄는 대상 |
| `Assets/QaLongNameDoorMask…0123456789.png` | 긴 에셋 이름 정렬 확인 |
| **QA Line**·**QA Light** | Line·Light 색 피커 |
| `Scripts/QaWall.cs` (`int Strength`, `bool Breakable`) | `inspect`: WallWorkshop 벽 10개에 붙여 스크립트 필드 다중 편집 |

`finder <폴더>`는 프로젝트 밖에 `FinderChime.wav`를 만든다(Finder 놓기).

## 에디터 단계 ([프로브](StartupHook.cs))

Project 드래그는 Project 행에 PointerPressed, 대상까지 PointerMoved 8번, PointerReleased를 보내 Project 패널의 처리기가 그대로 돈다. Finder 놓기는 `DataTransfer` 파일 항목을 실은 DragOver·Drop 이벤트를 필드에 보낸다.

| 단계 | 프로젝트 | 확인 |
| --- | --- | --- |
| `edit` | KeyRoom | 13.9: Door Sprite tint·QA Line Start color·QA Light Color를 피커에서 세 번 바꾸는 동안 바로 반영되고 기록 0, 닫을 때 기록 1, Undo 1회로 복원·Redo. 13.7/13.17: Key.png → Door Sprite(강조 테두리, 행 이동 없음, Undo 1회), Coin.png → Sound 거부(빨간 테두리, 씬·기록 변화 없음), QaChime.wav → Sound, Finder `FinderChime.wav` → Sound(`Assets/`로 복사 후 연결, Undo 1회), Sprite 필드 클릭 → Project에서 Key.png Ping, 피커의 축소 이미지·검색·선택(Undo 1회), 1280/1600/1920px에서 긴 Mask 이름으로 값 열 왼쪽·오른쪽 끝과 필드 높이 24px, Key의 Frames 배열에 PNG 2개 한 번에(Project 순서, Undo 1회), 벽 2개 다중 선택 Sprite에 Oak.png(Undo 1회), Particle 패널 Texture ← Coin.png(.particles 기록, Undo/Redo로 파일 복원) 및 WAV 거부. 저장 |
| `trace` | 저장한 KeyRoom | Scene Play를 90프레임 재생하고 30·80프레임을 플레이어와 같은 소프트웨어 SDL 캔버스로 캡처 |
| `field` | CollisionLab Field10000 | 첫 오브젝트에 Distance Joint 2D를 붙이고 Connect 피커를 5번 열어 `Collider 9994`를 검색. 열기, 필터, 검색 한 번의 UI 왕복(Dispatcher Background까지) 시간. 100 ms 미만이면 통과 |
| `inspect` | KeyRoom ([InspectEdit.cs](InspectEdit.cs)) | 13.5: WallWorkshop 벽 10개에 QaWall(Strength 서로 다름)을 붙여 저장한 뒤 함께 선택. 1280/1600/1920px에서 값 열 왼쪽 끝 1개, Width 76 공통·Strength·X `—`·Is Trigger 반쯤 찬 체크. Width 80·Is Trigger·Strength 7이 각각 기록 1번으로 10개 모두에 들어가고 width·isTrigger가 벽마다 오버라이드가 됨. Undo 3번으로 씬 텍스트와 벽별 오버라이드 목록 복원, Redo 3번. 13.6: 두 오크를 너비마다 골라 Courtyard oak의 Size 행만 막대(2px, 패널 여백 안)·굵은 레이블, Transform 머리말 점, 표시 유무로 값 열 위치가 같음. Size 행 우클릭 Revert(기록 1, 이름 오버라이드는 남음, Undo 1), Name 행 Apply to Source(Oak.prefab 기록, Undo 1로 파일·씬 복원). 13.10: MoveSpeed 레이블을 60px 끌어 240→300(슬라이더 따라감, 기록 1, Undo/Redo), 400px 끌면 480에서 멈춤, Door Position X 축 레이블 −15px = X−15(기록 1). 13.16: Project에서 Door.png 클릭(누르고 같은 자리에서 놓기) → 크기·피벗 0,0, 값 열 왼쪽 끝 1개. Door를 20° 돌려 저장(`inspect-base.scene`)한 뒤 Import pivot 0.5/0.5(`.meta`와 Door, 각 기록 1, Undo/Redo), Atlas → New atlas(`Door Atlas.atlas`, 기록 1, Undo로 파일 삭제, Redo). 저장. clip·data·prefab·particles·긴 이름 PNG의 요약 행과 버튼, Open Clip Editor, 오브젝트 선택으로 복귀 |
| `curves` | ParticleLab Curves | Flame의 Size 곡선·색 그라데이션 행이 런타임 `ParticleLut.Bake`와 같은 표를 그리는지, 키 값 0.6→1.5와 알파 키 1→0.5가 각각 기록 1번으로 `.particles`에 쓰이는지, Undo 2번으로 파일 복원, 저장 |

## 실행

Mac 저장소 루트에서. 창을 여는 명령은 GUI 잠금으로 감싼다. [헤드리스 빌드·재생 묶음](../results/inspector-assets/README.md#재현)은 결과 문서에 있다.

```sh
qa=engine/tests/KeyRoom.AssetFields
work=$(mktemp -d)
engine/scripts/build-editor-mac.sh
dotnet build $qa -c Release -m:1 -p:UsedAvaloniaProducts=
dll=engine/.artifacts/bin/AssetFieldsProbe/release/AssetFieldsProbe.dll
app=editor/MoNoon.app/Contents/MacOS/MoNoon
python3 $qa/verify.py prepare samples/KeyRoom "$work/edit/KeyRoom"
python3 $qa/verify.py finder "$work/finder"
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor $app "$work/edit/KeyRoom" $dll "$work/ui" edit "$work/finder/FinderChime.wav"
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor $app "$work/edit/KeyRoom" $dll "$work/scene-play" trace
rsync -a --exclude .monoon --exclude bin --exclude obj samples/CollisionLab/ "$work/field/CollisionLab/"
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor $app "$work/field/CollisionLab" $dll "$work/field/out" field
rsync -a --exclude .monoon --exclude .monoon-compiled --exclude bin --exclude obj samples/ParticleLab/ "$work/curves/ParticleLab/"
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor $app "$work/curves/ParticleLab" $dll "$work/curves/out" curves
# Release·Native AOT: 저장한 KeyRoom을 빌드해 play → compare(세 경로 캡처 동일) → differs(손대지 않은 복사본과 다름)
# inspect (13.5·13.6·13.10·13.16) — 결과: ../results/inspector-edit/README.md
python3 $qa/verify.py prepare samples/KeyRoom "$work/ie/edit/KeyRoom"
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor $app "$work/ie/edit/KeyRoom" $dll "$work/ie/ui" inspect
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor $app "$work/ie/edit/KeyRoom" $dll "$work/ie/scene-play" trace
rsync -a --exclude .monoon --exclude .monoon-compiled "$work/ie/edit/KeyRoom/" "$work/ie/base/KeyRoom/"
cp "$work/ie/ui/inspect-base.scene" "$work/ie/base/KeyRoom/Scenes/KeyRoom.scene"   # 피벗만 바꾸기 전 씬
./monoon build "$work/ie/edit/KeyRoom" release --profile distribution --output "$work/ie/builds"
./monoon build "$work/ie/edit/KeyRoom" native-aot --profile distribution --output "$work/ie/builds"
./monoon build "$work/ie/base/KeyRoom" release --profile distribution --output "$work/ie/base-builds"
python3 $qa/verify.py play "$work/ie/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/ie/release"
python3 $qa/verify.py play "$work/ie/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/ie/aot"
python3 $qa/verify.py play "$work/ie/base-builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/ie/base-release"
python3 $qa/verify.py compare "$work/ie/scene-play" "$work/ie/release" "$work/ie/aot"
python3 $qa/verify.py differs "$work/ie/release" "$work/ie/base-release"
```

[결과](../results/inspector-assets/README.md)
