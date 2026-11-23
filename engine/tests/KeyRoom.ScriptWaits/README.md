# KeyRoom 스크립트 대기 회귀

`verify.py prepare`는 KeyRoom을 별도 폴더에 복사하고 관찰 스크립트 [TraceWaits](TraceWaits.source)를 붙인다. 이 스크립트는 매 화면 프레임의 `UpdateUi`에서 재생 프레임 번호, 게임 틱, 정지 상태, `key`·`doorHint` 플래그가 바뀌면 한 줄을 기록한다.

입력 기록은 120프레임(각 1/60초)이다.

| 프레임 | 입력 | 의도 |
| --- | --- | --- |
| 5 → 22 | ← 누름 → 뗌 | 열쇠를 얻어 문이 열리고 `DoorLock.ShowHint`가 게임 시간 0.5초 대기를 시작 |
| 28 | P | 대기 중 정지 |
| 40, 46, 52 | . (StepGame) | 정지한 채 한 틱씩 세 번 진행 |
| 70 | P | 재개 |

각 실행에서 안내가 열쇠보다 정확히 30틱 뒤에 나타나는지, 정지는 대기 도중이고 정지 중에는 안내가 없는지, 세 번의 Step이 3틱만 진행했는지 확인한다. `compare`는 Scene Play·Release·Native AOT의 열쇠·안내 프레임과 틱이 모두 같은지 비교한다.

Mac에서 저장소 루트 기준으로 실행한다. 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md#에디터와-템플릿-관리)를 최신 소스로 빌드한다. 창을 여는 명령은 다른 GUI 검증과 겹치지 않게 실행한다.

```sh
qa=engine/tests/KeyRoom.ScriptWaits
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"

engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
  engine/.artifacts/bin/EditorProbe/release/EditorProbe.dll "$work/scene-play"

python3 "$qa/verify.py" compare "$work/scene-play/scene-play.trace" "$work/release/release.trace" "$work/native-aot/native-aot.trace"
```

Release와 Native AOT는 `--replay-input`으로 창 없이 재생한다. Scene Play는 시작 훅이 패키지 에디터에서 Play를 누르고 같은 기록을 `InputReplay.Play`로 재생한 뒤 Stop한다. 같은 동작의 단위 검사는 `./monoon test`의 [ScriptRoutineChecks](../Engine.Checks/ScriptRoutineChecks.cs)와 KeyRoom 프로젝트 테스트 [DoorTests](../../../samples/KeyRoom/Tests/DoorTests.cs)에 있다.
