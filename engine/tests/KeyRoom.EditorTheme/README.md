# KeyRoom 에디터 디자인 토큰 회귀

배포 에디터로 KeyRoom 복사본을 열고 **Door**를 선택한다. 메인 창과 Inspector를 1280px과 1920px 너비에서 캡처한다. Door 이름을 긴 이름으로 바꿔 1280px에서 다시 캡처한다. 이어서 예전 별도 창 9개를 차례로 열어 캡처하고 닫는다: Console, Animator Controller, Sprite Animation(`Showcase/DoorOpen.clip`), 시트 셀 선택, Input Actions, Data Asset, Data Table, Dialogue, Tileset. 로드맵 12.4부터 시트 셀 선택을 뺀 8개는 도킹 패널이므로 패널 영역을 캡처하고 탭의 **×**로 닫는다. 시트 셀 선택은 대화 상자 창이다. 마지막으로 Scene Play에서 프레임 프로파일러를 켠 화면을 캡처한다.

저장소 루트에서 실행한다. 결과 폴더는 실행마다 새 경로를 지정한다.

```sh
qa=engine/tests/KeyRoom.EditorTheme
engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom \
  engine/.artifacts/bin/EditorThemeProbe/release/EditorThemeProbe.dll "$(mktemp -d)/theme"
```

[editor.py](editor.py)는 샘플을 결과 폴더에 복사하고 `DOTNET_STARTUP_HOOKS`로 [검사 훅](StartupHook.cs)을 로드한다. 원본 샘플은 바꾸지 않는다. 합격 기준은 다음과 같다.

- 세 Inspector 캡처마다 최상위 행이 8개 이상이다. 모든 레이블의 왼쪽 x가 하나이고 모든 입력 열의 왼쪽 x도 하나다.
- 레이블과 입력 필드의 세로 중심 차이가 1px 이하다. TextBox·NumericUpDown·ComboBox·Button 입력 필드의 높이가 모두 같다.
- 레이블 열보다 긴 레이블은 말줄임표와 툴팁을 가진다.
- 긴 이름은 Hierarchy 안에서 말줄임표로 줄고 전체 이름이 툴팁에 있다. Inspector 너비는 이름을 바꾸기 전과 같다.
- 메인 창과 별도 창 9개의 글자 크기가 12px이다. `EditorFontSize` 등 토큰과 38개의 아이콘 기하가 리소스에 있다.

결과 폴더에는 `main-1280.png`, `main-1920.png`, `main-1280-long-name.png`, `inspector-*.png`, 창별 PNG, `profiler-1280.png`, 측정값 `result.json`이 남는다. Scene 뷰는 SDL 네이티브 뷰라서 `RenderTargetBitmap` 캡처에서는 비어 있다. [Mac 결과](../results/editor-theme/README.md)

Windows 패키지 에디터(`editor/MoNoon.exe`)에서도 같은 명령으로 실행한다(Linux 에디터는 2026-11-23부터 지원하지 않는다). 느린 환경에서는 `MONOON_QA_SLOW`(기본 1)로 대기·제한 시간을 늘린다. [x64 게스트 결과](../results/editor-ui-x64/README.md)
