# CameraLab 데드존·앞보기·그룹 프레이밍 검증 (16.11)

`verify.py prepare`는 CameraLab을 별도 폴더에 복사하고 씬을 바꾼다.

- Player Camera: Follow는 그대로, Deadzone 120×80, Lead time(앞보기) 0.25초.
- Partner Camera: Follow 대신 Frame 그룹 `Players`, Padding 48. 두 플레이어 오브젝트에 그룹 `Players`를 넣는다.
- 관찰 스크립트 [TraceCameras](TraceCameras.source)가 게임 틱마다 두 플레이어 위치와 두 카메라의 중심·줌을 `MONOON_CAMERA_TRACE`에 한 줄씩 쓴다(`float.ToString("R")`).

입력(프레임): Space(5, 남동쪽 자동 이동 150틱) → ←(180–240) → ↑(250–280) → →(290–296) → Space(320, 북서쪽으로 복귀). 520프레임.

| 확인 | 기준 |
| --- | --- |
| 실행 경로 일치 | Release·Native AOT·Scene Play의 트레이스가 공통 틱(520)까지 바이트 단위로 같다. Scene Play는 재생이 끝난 뒤 Play를 멈출 때까지 몇 틱 더 기록한다. |
| 데드존 | 남동쪽 끝에서 멈춘 플레이어가 왼쪽으로 걷기 시작해도 주 카메라가 5틱 이상 그대로다 |
| 앞보기 | 플레이어 중심이 아직 데드존(±60) 안일 때 주 카메라가 움직이기 시작한다 |
| 그룹 프레이밍 | 두 번째 카메라 줌이 경계가 허용하는 0.446(400/896)과 1 사이에서 바뀌고, 두 플레이어가 엇갈리는 틱 뒤 15틱 안에 최대, 40틱 뒤 0.6 미만 |
| Inspector | 시작 훅이 Play 전에 Player Camera를 선택해 창 1280·1600·1920px에서 Half-life·Deadzone·Lead time·Frame·Padding·X / Y 행의 레이블과 입력 열 위치를 기록하고 Inspector를 PNG로 저장한다 |

저장소 루트에서 실행한다. 창을 여는 Scene Play만 GUI 잠금이 필요하다.

```sh
q=engine/tests/CameraLab.Framing; work=$(mktemp -d)
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet build "$q" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$q/verify.py" prepare samples/CameraLab "$work/CameraLab"
./monoon build "$work/CameraLab" release --profile distribution --output "$work/builds"
./monoon build "$work/CameraLab" native-aot --profile distribution --output "$work/builds"
python3 "$q/verify.py" play "$work/builds/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$q/verify.py" play "$work/builds/NativeAOT/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
python3 "$q/verify.py" prepare samples/CameraLab "$work/play/CameraLab"
sh engine/scripts/gui-lock.sh python3 "$q/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/play/CameraLab" \
  engine/.artifacts/bin/FramingProbe/release/FramingProbe.dll "$work/scene-play"
python3 "$q/verify.py" check "$work/scene-play" "$work/release" "$work/aot"
```

계산 규칙의 단위 검사(데드존 가장자리, 속도 × 초 앞보기, 멈춤·대상 재활성화, 그룹 중심·줌·여백, Bounds 하한, schemaVersion 33, YAML, 검증)는 `./monoon test`의 [CameraFramingChecks](../Engine.Checks/CameraFramingChecks.cs)이며 `--only camera-framing`으로 따로 실행한다.
