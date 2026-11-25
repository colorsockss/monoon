# KeyRoom 조작키 설정 회귀

조작키 메뉴는 무브 쇼케이스(`ShowcaseMoves.scene`)에서 K로 겹쳐 여는 UI 씬 `ShowcaseBindings.scene`이다. 차지·튕기기·대시 세 줄에 키보드와 패드 칸이 하나씩 있고, 상태 문구와 **Restore defaults**가 있다. 바꾼 조작은 메뉴가 `player.settings`에 저장하고 MoveTrainer가 시작할 때 읽는다.

`verify.py prepare`는 KeyRoom을 별도 폴더에 복사하고 시작 씬을 ShowcaseMoves로 바꾼 뒤 관찰 스크립트 [TraceBindings](TraceBindings.source)를 붙인다. 이 스크립트는 화면 아래 캡션의 조작 줄(MoveTrainer가 현재 키로 고쳐 쓴다), Charge·Dash·RoomGuide의 현재 입력, 입력 대기 중인 액션, MoveTrainer의 `interact`·`rebind` 플래그가 바뀔 때마다 프레임 번호와 함께 기록한다. 창 없는 재생은 프레임 사이에 대기하지 않으므로 추가 씬을 읽는 동안에는 프레임마다 20ms를 기다린다.

`play`는 같은 프로필에서 게임을 세 번 실행한다. 각 실행은 새 프로세스이고 `--replay-input`으로 입력을 재생한다.

| 실행 | 입력(프레임) | 확인하는 내용 |
| --- | --- | --- |
| rebind | K(5) → ↓(70) → ↓(75) → Enter(80) → J(90) → Enter(100) → Esc(110) → →(116) → Enter(120) → 패드 North(126) → K(140) → O O(150·155) → J J(180·185) | 대시의 키 칸에서 Enter로 입력 대기, J와 차지의 O 교환과 캡션 `Dash  J J`, Esc 취소, 패드 North와 RoomGuide의 교환(RoomGuide는 오른쪽 트리거를 받음), 닫은 뒤 O 두 번은 대시가 아니고 J 두 번이 대시, `rebind` 알림, `player.settings`의 `inputBindings` |
| restart | O O(10·15) → J J(30·35) → K(50) → Restore defaults 클릭(120) → K(140) | 첫 프레임부터 저장한 조작, 첫 입력 전에 캡션 `Dash  J J`, O로는 대시하지 않고 J로 대시, 복원 후 `Dash  O O`·RoomGuide의 North와 저장 키 삭제 |
| restored | O O(10·15) | 다시 시작해도 기본값 O로 대시 |

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
