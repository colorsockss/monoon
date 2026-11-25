# CameraLab 데드존·앞보기·그룹 프레이밍 검증 (16.11)

`verify.py prepare`는 CameraLab을 별도 폴더에 복사하고 시작 씬을 프레이밍 쇼케이스(`Scenes/ShowcaseFraming.scene`)로 바꾼다. 씬은 그대로 두고 관찰 스크립트만 더한다(2026-11-26부터, 이전에는 기존 CameraLab 씬의 카메라 설정을 바꾸고 Space 자동 이동을 재생했다).

- Follow Camera: Deadzone 120×80, Lead time(앞보기) 0.25초, 줌 1.5. Frame Camera: 그룹 `Party`(주인공과 동료), Padding 48. [FramingTour](../../../samples/CameraLab/Scripts/Showcase/FramingTour.cs)가 두 사람을 걷게 하고 마지막 약초에서 Frame Camera로 넘긴다.
- 관찰 스크립트 [TraceCameras](TraceCameras.source)가 게임 틱마다 두 사람의 위치와 두 카메라의 중심·줌(꺼진 카메라는 `-`)을 `MONOON_CAMERA_TRACE`에 한 줄씩 쓴다(`float.ToString("R")`).

입력 없이 1,000프레임을 재생한다(연출은 약 960틱에 끝난다).

| 확인 | 기준 |
| --- | --- |
| 실행 경로 일치 | Release·Native AOT·Scene Play의 트레이스가 공통 틱까지 바이트 단위로 같다. Scene Play는 재생이 끝난 뒤 Play를 멈출 때까지 몇 틱 더 기록한다. |
| 데드존 | 280틱 전까지 주인공이 모닥불 옆에서 40단위 넘게 움직여도 Follow 카메라 중심이 한 번도 바뀌지 않는다 |
| 앞보기 | 동쪽 길로 나설 때 주인공 중심이 아직 데드존(±60) 안인데 Follow 카메라가 움직이기 시작하고, 넘겨줄 때까지 매 틱 동쪽으로 따라간다 |
| 그룹 프레이밍 | Frame 카메라는 넘겨받은 틱부터만 켜져 있다. 줌이 1.45 아래로 내려갔다가 1.49 위로 돌아오고, 경계가 허용하는 0.502(450/896)와 1.5 사이에 있다. 끝에는 두 사람 발 상자 중심의 중점을 2단위 안에서 비춘다 |
| Inspector | 시작 훅이 Play 전에 Follow Camera를 선택해 창 1280·1600·1920px에서 Half-life·Deadzone·Lead time·Frame·Padding·X / Y 행의 레이블과 입력 열 위치를 기록하고 Inspector를 PNG로 저장한다 |

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
