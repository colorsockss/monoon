# KeyRoom Input Actions 패널: 상호작용·틱·버퍼·벡터 (16.12 에디터)

[editor.py](editor.py)는 `samples/KeyRoom`을 `project`로 복사하고, 같은 규칙을 YAML에만 써 넣은 기준 복사본을 `reference`에 만든다. 시작 훅 [StartupHook.cs](StartupHook.cs)는 패키지 에디터에서 `project`를 열고 **Input Actions 패널만으로** 같은 값을 입력한다. 샘플에는 동작 쇼케이스(ShowcaseMoves)의 Charge(J, Hold, 30)·Flick(U, Tap, 12)·Dash(O, DoubleTap, 15)가 이미 있어, 잘못된 값 검사는 Charge 행에서 하고 `DoubleTap` 표시는 Dash 행에서 본다.

- 추가하는 액션: Spin(Z, Hold, 20), Leap(Y, Buffer 8), AimLeft·AimRight·AimUp·AimDown(V·B·T·G)
- 벡터: Walk(MoveLeft·MoveRight·MoveUp·MoveDown), Aim(AimLeft·AimRight·AimUp·AimDown)

| 확인 | 기준 |
| --- | --- |
| 저장 바이트 | Save 뒤 `project/GameSettings`가 `reference/GameSettings`를 엔진이 읽고 저장한 바이트(`GameSettings.Load` → `Save`)와 같다 |
| 잘못된 값 | Ticks 0·3601, Buffer −1, Vector의 없는 액션, 대소문자만 다른 벡터 이름: 패널이 닫히지 않고 런타임 `GameSettings` 검증과 같은 문구를 보인다. Ticks `2.5`는 정수 문구. 그동안 파일은 바뀌지 않는다 |
| Undo | Undo 한 번에 원래 샘플 설정 바이트, Redo 한 번에 저장한 바이트. 다시 열어 바꾸지 않고 Save하면 파일 쓰기와 Undo 단계가 없다 |
| 다시 열기 | 저장한 Interaction·Ticks·Buffer·벡터가 패널에 보인다 |
| 정렬 | 메인 창 1280·1600·1920px와 64자 액션 이름에서 헤더·첫 행·마지막 행의 0–4열 왼쪽 끝이 같고, 벡터 행의 이름·첫 부분·삭제 버튼이 액션 행의 0·1·6열과 같다. 행 높이가 같다. `DoubleTap`이 콤보 상자 안에서 잘리지 않는다. 열마다 위치를 `result.json`에, 패널을 `input-map-*.png`로 남긴다 |

저장소 루트에서 실행한다. 창을 여는 실행이라 GUI 잠금이 필요하다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/KeyRoom.InputMapEditor -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 engine/tests/KeyRoom.InputMapEditor/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon \
  engine/.artifacts/bin/InputMapEditorProbe/release/InputMapEditorProbe.dll "$(mktemp -d)/run"
```

상호작용 규칙의 런타임 판정은 [KeyRoom.InputInteractions](../KeyRoom.InputInteractions/README.md)가 Release·Native AOT 재생으로 확인한다. [결과](../results/editor-input-audio/README.md)
