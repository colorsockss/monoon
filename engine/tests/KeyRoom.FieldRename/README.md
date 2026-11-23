# KeyRoom 스크립트 필드 이름 변경 회귀

기존 Scene Play, Release 및 Native AOT에서 같은 두 플레이어의 이동을 확인한다. 테스트는 샘플 복사본에서 이전 이름으로 저장한 프로젝트를 준비한다. 코드 기본값을 177로 정해 저장된 240이 무시되면 바로 실패하도록 했다. 실제 배포 샘플의 기본값은 240이다.

검사기는 배포 에디터를 열고 프리팹 원본과 두 인스턴스의 **Speed**를 확인한다. 씬을 저장한 뒤 외부 C# 파일의 선언과 호출부를 **MoveSpeed**로 변경한다. 프리팹 원본만 먼저 저장하고 디스크의 이전 씬을 열어 오버라이드를 검사한다. 이어서 씬을 저장하고 프리팹과 씬을 다시 연다. 상속 인스턴스에는 필드 오버라이드가 없고 다른 인스턴스에만 MoveSpeed 오버라이드가 남아야 한다.

아래 명령은 Mac의 저장소 루트에서 실행한다. [에디터와 플레이어 템플릿](../../../docs/manual/build.md)을 먼저 갱신한다. 에디터 검사를 거친 같은 사본으로 두 게임을 빌드한다.

```sh
qa=engine/tests/KeyRoom.FieldRename
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
    engine/.artifacts/bin/FieldRenameProbe/release/FieldRenameProbe.dll "$work/editor"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
```

Windows에서도 같은 준비·검사 순서로 패키지 에디터와 플레이어를 실행한다. Linux에서는 플레이어만 실행한다(Linux 에디터는 2026-11-23부터 지원하지 않는다). 입력 리플레이는 시작 방에서 F3으로 비교 씬에 들어간 뒤 오른쪽 방향을 15틱 유지한다. 사본의 관찰 스크립트는 누른 순간과 놓은 순간의 두 월드 X좌표를 읽는다. [검증 코드](verify.py) · [에디터 검사](StartupHook.cs)
