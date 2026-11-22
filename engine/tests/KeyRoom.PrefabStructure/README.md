# KeyRoom 프리팹 구조 오버라이드 회귀

`Scenes/PrefabWorkshop.scene`에는 `Assets/Chest.prefab`의 인스턴스 세 개가 있다. 검사기는 배포 에디터에서 **Chest 2**에 Lock 자식과 Audio Source를 추가한다. 인스펙터의 **Overrides…** 창에서 Lock만 **Apply to Source**로 적용한 뒤 Undo·Redo하고 씬을 저장한다. 같은 사본을 Release와 Native AOT로 빌드해 세 상자의 자물쇠와 효과음을 런타임에서 확인한다.

아래 명령은 Mac의 저장소 루트에서 실행한다. [에디터와 플레이어 템플릿](../../../docs/manual/build.md)을 먼저 갱신한다.

```sh
qa=engine/tests/KeyRoom.PrefabStructure
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
    engine/.artifacts/bin/PrefabStructureProbe/release/PrefabStructureProbe.dll "$work/editor"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
```

- `prepare`는 사본의 시작 씬과 내보낼 씬에만 워크숍을 추가한다. 카메라에 관찰 스크립트 `TraceChests`를 붙인다. 원본 샘플의 빌드 목록은 바꾸지 않는다.
- 에디터 검사는 `DOTNET_STARTUP_HOOKS`로 로드된다. 목록에는 `Chest 2 · audioSource (added)`와 `Lock (added object)`가 있어야 한다. Lock을 적용하면 프리팹 원본과 세 상자에 Lock이 생기고 효과음은 Chest 2에만 남는다. Undo 한 번이면 원본과 다른 두 상자에서 Lock이 빠진다. 씬을 다시 열었을 때 남는 구조 오버라이드는 효과음 하나다. 단계별 상태는 `editor.txt`에, 창 캡처는 `overrides.png`와 `compact.png`에 저장된다.
- `play`는 이벤트 없는 10프레임 리플레이를 헤드리스로 실행한다. 관찰 스크립트가 x 좌표 순으로 `x 자물쇠수 효과음` 세 줄을 기록한다. 기대값은 `210 1 0`, `370 1 1`, `530 1 0`이다. [검증 코드](verify.py) · [에디터 검사](StartupHook.cs)
