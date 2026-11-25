# CameraLab 스크립트 디버그 그리기 검증 (16.14)

프레이밍 쇼케이스(`ShowcaseFraming.scene`)의 [FramingTour.cs](../../../samples/CameraLab/Scripts/Showcase/FramingTour.cs)는 `#if MONOON_DEBUG_DRAW` 안에서 **G**(`DebugDraw` 액션)로 `tick.Debug`의 금색 원(주인공의 발), 하늘색 선(다음 정지 위치까지), 좌표 텍스트와 약초·데드존 표시를 켠다. `verify.py prepare`가 샘플을 복사하고 시작 씬을 이 쇼케이스로 바꾼다(2026-11-26부터, 이전에는 기존 CameraLab 씬의 FieldPlayer). 검사는 G 없이 한 번, 프레임 5에 G를 눌러 한 번 재생한다.

| 경로 | 방법 | 통과 기준 |
| --- | --- | --- |
| Scene Play | [시작 훅](StartupHook.cs)이 Play를 누르고 같은 `.inputreplay`를 `InputReplay.Play`로 재생한 뒤, 실행 중인 게임을 기록용 캔버스에 그린다 | G 전에는 디버그 도형 0개, G 뒤에는 원·선과 원마다 주인공 좌표 텍스트(`431, 526`, 첫 정지 위치에서 기다리는 중) |
| 개발용 Release·Native AOT | `--replay-input`과 `MONOON_REPLAY_SNAPSHOTS`로 프레임 30을 PNG로 저장 | G 뒤 프레임에 금색(255, 215, 0)·하늘색(0, 255, 255) 픽셀 |
| 배포용 Release·Native AOT | 같은 재생 | G 뒤 프레임이 G 없는 프레임과 바이트 단위로 같고, 개발용의 G 없는 프레임과도 같다 |

배포본의 코드 크기는 이 하네스 밖에서 기준 커밋 엔진으로 만든 같은 샘플의 배포 빌드와 비교한다([결과](../results/engine-features-2/README.md#1614-스크립트-디버그-그리기)).

저장소 루트에서 실행한다. 창을 여는 Scene Play만 GUI 잠금이 필요하다.

```sh
q=engine/tests/CameraLab.DebugDraw; work=$(mktemp -d)
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet build "$q" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$q/verify.py" prepare samples/CameraLab "$work/CameraLab"
for p in distribution development; do for m in release native-aot; do
  ./monoon build "$work/CameraLab" $m --profile $p --output "$work/$p"; done; done
dev="$work/development/Development"; dist="$work/distribution"
python3 "$q/verify.py" play "$dev/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/out/development-release"
python3 "$q/verify.py" play "$dev/NativeAOT/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/out/development-aot"
python3 "$q/verify.py" play "$dist/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/out/distribution-release"
python3 "$q/verify.py" play "$dist/NativeAOT/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/out/distribution-aot"
python3 "$q/verify.py" check "$work/out/development-release" "$work/out/distribution-release"
python3 "$q/verify.py" check "$work/out/development-aot" "$work/out/distribution-aot"
sh engine/scripts/gui-lock.sh python3 "$q/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/CameraLab" \
  engine/.artifacts/bin/DebugDrawProbe/release/DebugDrawProbe.dll "$work/out/scene-play"
```

같은 동작의 단위 검사(수명, 일시정지, 할당 0, 기호 없는 컴파일의 호출 제거)는 `./monoon test`의 [ScriptDebugChecks](../Engine.Checks/ScriptDebugChecks.cs)이며 `--only script-debug`로 따로 실행한다.
