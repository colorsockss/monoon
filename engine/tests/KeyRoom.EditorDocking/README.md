# 에디터 패널 도킹 회귀

배포한 Mac 에디터로 KeyRoom 복사본을 다섯 번 연다. 각 실행은 `DOTNET_STARTUP_HOOKS`로 로드한 [StartupHook](StartupHook.cs)이 진행하고, 실행이 끝나면 `<단계>.json`과 창 캡처를 남긴다. 레이아웃 저장 위치는 `MONOON_USER_DATA`로 결과 폴더 안에 둔다.

| 단계 | 사용자 설정 | 확인하는 내용 |
| --- | --- | --- |
| `first` | 빈 폴더 | 처음 배치가 Default(Hierarchy·Project 176px, Inspector 218px)인지 확인한다. 창 너비 1280·1600·1920px과 긴 오브젝트 이름에서 Inspector의 Name·Position·Tag 행 정렬을 잰다. Project 탭을 Inspector로 끌어 탭으로 묶고, Hierarchy 탭을 Scene 아래쪽으로 끌어 분할한다. 끄는 동안 미리보기가 열려 있고 패널 위치가 바뀌지 않아야 한다. 상하좌우 분할, 창 가장자리, 탭 묶기를 `Move`로 이어서 실행한다. Inspector 왼쪽 분할선을 끌어 넓히고 최소 폭 아래로 줄여 본다. Game 패널을 Scene과 떼어 놓은 뒤 Play를 시작한다. Game 탭이 앞으로 오고 게임 화면이 Game 패널로 옮겨졌는지 확인하고, Play 중에 Game 패널을 분할·탭 숨김·창 가장자리로 옮긴다. Stop 뒤에는 Scene 탭이 앞에 있고 화면이 Scene 패널로 돌아와야 한다. Window 메뉴로 세 레이아웃을 바꾸고 Custom을 저장한다. |
| `restore` | `first`와 같은 폴더 | 마지막 배치·Inspector 폭·창 크기가 복원되는지 본다. Window 메뉴의 Custom과 Reset Layout, 탭 **×**로 닫은 Hierarchy를 Window 메뉴로 다시 여는 동작을 확인한다. |
| `fallback` | 크기 40000px의 패널, 화면 밖 위치(-60000, -60000)의 9000×7000 창을 기록한 파일 | Default 배치, 기본 창 크기 1220×740, 화면 안의 창 위치, Custom 비활성을 확인한다. |
| `panels` | 별도 빈 폴더(`panels-profile`) | 예전 별도 창 8개(Console·Profiler·Animator·Clip·Tileset·Input Actions·Dialogue·Data)를 각 편집기의 실제 진입 메서드로 연다. 별도 창이 생기지 않고 Scene 아래 도구 영역에 탭으로 들어가야 한다. 같은 탭 묶음의 다른 탭을 앞에 둔 뒤 다시 열면 같은 편집기 탭이 앞으로 오고 패널 수가 그대로여야 한다. Data 패널에 테이블을 열면 데이터 에셋을 대체한다. Window 메뉴에 모든 패널이 있는지 본다. Console·Animator·Inspector·Scene을 띄웠다가 창의 **Dock**으로 다시 붙인다(Inspector는 컴포넌트 머리말과 패널 머리줄 28px, 정렬도 확인). Scene을 띄운 채 Play해 게임이 메인 창 Game 탭에 그려지는지, Play 중 Game 패널을 띄우고 다시 붙여도 세션이 이어지는지, Stats·2× 배율·해상도 메뉴, Stop 뒤 화면이 띄운 Scene 창으로 돌아오는지 확인한다. Scene 도구 버튼 네 개로 기즈모가 바뀌는지, Rect 손잡이로 Width가 24 늘고 Esc 취소로 돌아오는지 본다. 마지막으로 Animator 탭을 **×**로 닫고 Console을 정한 위치(520×300)에 띄운다. |
| `panels-restore` | `panels`와 같은 폴더, Animator 창을 화면 밖(-60000, -60000)에 띄운 항목을 추가 | 다시 실행했을 때 Console 창이 같은 위치·크기로, 배치가 종료 전과 같게 복원되는지 본다. 사라진 모니터에 저장된 Animator 창은 화면 안 기본 위치에 열려야 한다. |

Play 확인에서는 1.2초마다 같은 `GameSession`인지, 고정 틱이 30회 넘게 진행했는지, SDL Scene 뷰의 그리기 호출이 늘었는지, NSView 핸들이 그대로인지 기록한다. 패널을 옮길 때마다 열린 패널이 모두 창 안에 있고 최소 크기 이상인지도 검사한다. 탭 끌기는 탭 컨트롤에 포인터 누름·이동·놓기 이벤트를 보내 도킹 코드의 포인터 처리기를 그대로 실행한다. 분할선은 GridSplitter에 Thumb 끌기 이벤트를 보낸다.

Mac의 저장소 루트에서 실행한다. 마지막 인수는 새 결과 폴더다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/KeyRoom.EditorDocking -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/tests/KeyRoom.EditorDocking/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom \
  engine/.artifacts/bin/DockingProbe/release/DockingProbe.dll /tmp/monoon-docking
```

단계 이름을 뒤에 붙이면 그 단계만 실행한다(`restore`와 `fallback`은 같은 결과 폴더의 `first`, `panels-restore`는 `panels` 결과가 필요하다). RenderTargetBitmap 캡처에는 네이티브 Scene 뷰가 비어 보인다. `play-game-moved.png`, `play-game-tab.png`, `float-*.png`, `restored-float-console.png`는 `screencapture -l`로 찍은 실제 창이다. 결과 기록: [editor-docking](../results/editor-docking/README.md)

Windows·Linux 패키지 에디터에서도 같은 명령을 쓴다(실행 파일만 `editor/MoNoon.exe`·`editor/MoNoon`). 두 OS에는 SDL Scene 뷰가 없어(Avalonia가 Scene을 그림) NSView·그리기 호출 검사는 건너뛴다. 실제 창 캡처 자리에 RenderTargetBitmap으로 찍고 `<단계>.json`의 `captures`에 찍은 방법을 남긴다. 에뮬레이션 게스트처럼 느린 환경에서는 `MONOON_QA_SLOW`(기본 1)로 대기 시간과 제한 시간을 함께 늘린다. [x64 게스트 결과](../results/editor-ui-x64/README.md)
