# KeyRoom 조작키 설정 회귀

`verify.py prepare`는 KeyRoom을 별도 폴더에 복사하고 관찰 스크립트 [TraceBindings](TraceBindings.source)를 붙인다. 이 스크립트는 HUD 안내 문구, Interact·CarryCoin·RoomGuide의 현재 입력, 입력 대기 중인 액션, 상자 열림 여부가 바뀔 때마다 프레임 번호와 함께 기록한다. 창 없는 재생은 프레임 사이에 대기하지 않으므로 추가 씬을 읽는 동안에는 프레임마다 20ms를 기다린다.

`play`는 같은 프로필에서 게임을 세 번 실행한다. 각 실행은 새 프로세스이고 `--replay-input`으로 입력을 재생한다.

| 실행 | 입력(프레임) | 확인하는 내용 |
| --- | --- | --- |
| rebind | K(5) → Enter(70) → C(80) → Enter(90) → Esc(100) → → (106) → Enter(110) → 패드 North(116) → K(130) → E(150) | C와 동전 들기의 E 교환, Esc 취소, 패드 North와 RoomGuide의 South 교환, 패드 안내 `Interact: North`, 닫은 뒤 E로 상자가 열리지 않음, `player.settings`의 `inputBindings` |
| restart | E(10) → C(30) → K(50) → Restore defaults 클릭(120) → K(140) | 첫 프레임부터 `Interact: C`, E로는 닫힌 채이고 C로 열림, 복원 후 `Interact: E`와 저장 키 삭제 |
| restored | E(10) | 다시 시작해도 기본값 E로 상자가 열림 |

Mac에서 저장소 루트 기준으로 실행한다. 먼저 [플레이어 템플릿](../../../docs/manual/build.md)을 최신 소스로 빌드한다.

```sh
qa=engine/tests/KeyRoom.Rebinding
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
```

결과 폴더에는 실행별 `.inputreplay`, `.trace`, 실행 직후의 `.player.settings`가 남는다.

에디터 Scene Play에서는 같은 세 입력을 패키지 에디터 안에서 재생한다. 시작 훅이 Play를 시작하고 입력을 재생한 뒤 Play를 멈추는 과정을 세 번 반복한다. 저장 위치는 `Play/<projectId>/player.settings`다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
  engine/.artifacts/bin/EditorProbe/release/EditorProbe.dll "$work/scene-play"
```

같은 동작의 단위 검사는 `./monoon test`의 [RebindingChecks](../Engine.Checks/RebindingChecks.cs)에 있다.
