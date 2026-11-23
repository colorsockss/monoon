# KeyRoom 오브젝트 복사·붙여넣기와 씬 간 이동

검사기는 배포 에디터에서 `Scenes/PrefabWorkshop.scene`에 **Rig**(자식 **Rig Arm**)를 만든다. Rig의 관찰 스크립트 `TraceCopies`는 함께 복사할 프리팹 인스턴스 **Chest 1**과 복사하지 않을 **Floor**를 가리킨다. 두 오브젝트를 **Edit → Copy**로 복사해 같은 씬과 `Scenes/SecondRoom.scene`에 붙여 넣는다. 각 단계의 Undo·Redo와 Cut을 확인하고 저장한 사본을 Scene Play, Release, Native AOT로 실행한다.

아래 명령은 Mac의 저장소 루트에서 실행한다. [에디터와 플레이어 템플릿](../../../docs/manual/build.md)을 먼저 갱신한다.

```sh
qa=engine/tests/KeyRoom.CopyPaste
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
    engine/.artifacts/bin/CopyPasteProbe/release/CopyPasteProbe.dll "$work/editor"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
python3 "$qa/verify.py" forbidden "$work/builds/KeyRoom.app"
python3 "$qa/verify.py" forbidden "$work/builds/NativeAOT/KeyRoom.app"
```

- `prepare`는 사본의 시작 씬을 SecondRoom으로 바꾸고 `Scripts/TraceCopies.cs`를 추가한다. 원본 샘플은 바꾸지 않는다.
- 에디터 검사는 `DOTNET_STARTUP_HOOKS`로 로드된다. Edit 메뉴 항목은 네이티브 메뉴가 클릭에 쓰는 진입점으로 실행한다. 검사는 OS 클립보드에 텍스트를 쓰므로 실행 뒤 클립보드에는 복사한 오브젝트가 남는다.
- 같은 씬: 새 ID 네 개, `Rig (1)`·`Chest 1 (1)` 이름, Copied → 붙여 넣은 Chest, Outside → 기존 Floor, 프리팹 연결. Undo 한 번에 붙여 넣기 전 스냅숏, Redo에 같은 ID로 돌아와야 한다. Cut은 부모와 자식을 지우고 Undo 한 번에 복원한다.
- SecondRoom: Outside가 다른 씬의 Floor를 가리켜 Inspector에 `Missing object or component`가 표시돼야 한다. 1280px 창에서 이 행의 레이블·필드가 Copied 행과 같은 열에 있는지 비교하고 `inspector-missing-1280.png`를 남긴다. 끊긴 참조로 Scene Play(`copied 1 chest 1 outside 0`)를 실행한 뒤, 빌드가 거부하지 않도록 Outside를 North Wall로 다시 연결하고 저장한다.
- `play`는 이벤트 없는 10프레임 리플레이를 헤드리스로 실행한다. 기대값은 `copied 1 chest 1 outside 1`이다. [검증 코드](verify.py) · [에디터 검사](StartupHook.cs)

OS 클립보드가 쓴 값을 돌려주지 않는 환경(창 관리자 없는 Xvfb 등)의 진단에는 `MONOON_QA_NO_OS_CLIPBOARD=1`을 준다. 이때만 OS 클립보드 검사 세 개를 `SKIPPED`로 기록하고 나머지를 계속한다. 원래 검사의 합격 기준은 아니다. [Linux 결과](../results/chapter11-linux-x64/README.md)
