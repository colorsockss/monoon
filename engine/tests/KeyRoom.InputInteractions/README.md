# KeyRoom 입력 상호작용·버퍼·2D 벡터 검증 (16.12)

`verify.py prepare`는 KeyRoom을 별도 폴더에 복사하고 `GameSettings`에 액션과 벡터를 더한다. 원본 샘플은 바꾸지 않는다.

| 액션 | 키 | 규칙 |
| --- | --- | --- |
| Charge | J | `interaction: Hold, ticks: 30` |
| Flick | U | `interaction: Tap, ticks: 12` |
| Dash | O | `interaction: DoubleTap, ticks: 15` |
| Leap | Y | `buffer: 8` |
| AimLeft/Right/Up/Down | V / B / T / G | 벡터 `Aim`. 벡터 `Walk`는 기존 MoveLeft…MoveDown이며 첫 벡터라 `Move`가 된다 |

관찰 스크립트 [TraceInput](TraceInput.source)이 첫 방에 붙어, 판정(`press`·`release`·`trigger`)이 있거나 벡터가 바뀐 게임 틱마다 한 줄을 `MONOON_INPUT_TRACE`에 쓴다. 20틱마다 "착지"로 `Consume("Leap")`를 호출한다.

입력(프레임): J 40틱·10틱, U 4틱·20틱, O 10틱 간격 두 번, O 20틱 뒤·10틱 뒤, Y 착지 5틱 전·15틱 전, D(오른쪽)를 누른 채 V+T. 300프레임.

| 확인 | 기준 |
| --- | --- |
| 실행 경로 일치 | Release·Native AOT·Scene Play의 트레이스가 바이트 단위로 같다 |
| Hold | 40틱 누름의 30번째 틱에 한 번, 10틱 누름은 판정 없음 |
| Tap | 4틱 누름을 놓은 틱에만 판정 |
| DoubleTap | 10틱 간격 두 번째 입력에서 판정, 20틱 간격은 없음, 그 뒤 10틱 간격은 판정 |
| Buffer | 착지 5틱 전 입력은 착지 틱에 사용되고, 15틱 전 입력은 8틱 뒤 사라진다. `Triggered`는 버퍼 동안만 참이고 사용 후 거짓 |
| 2D 벡터 | `Walk = (1, 0)`, `Aim = (−0.7071, −0.7071)`, `Move = Walk` |

저장소 루트에서 실행한다. 창을 여는 Scene Play만 GUI 잠금이 필요하다.

```sh
q=engine/tests/KeyRoom.InputInteractions; work=$(mktemp -d)
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet build "$q" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$q/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 "$q/verify.py" play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$q/verify.py" play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
sh engine/scripts/gui-lock.sh python3 "$q/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
  engine/.artifacts/bin/InputProbe/release/InputProbe.dll "$work/scene-play"
python3 "$q/verify.py" check "$work/scene-play" "$work/release" "$work/aot"
```

Linux x64 게스트에서는 `play`와 `check`만 쓴다(coordinator). 규칙의 단위 검사(틱 경계, 오래된 입력, 재바인딩, 기존 Move, 할당 0, YAML, 검증)는 `./monoon test`의 [InputInteractionChecks](../Engine.Checks/InputInteractionChecks.cs)이며 `--only input-interactions`로 따로 실행한다.
