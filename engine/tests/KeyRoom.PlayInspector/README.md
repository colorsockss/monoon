# Play Inspector 데스크톱 회귀

배포한 MoNoon 에디터와 KeyRoom 복사본을 사용한다. 외부 드라이버가 Hierarchy, MoveSpeed 입력란, 체크박스, Apply selected 및 Stop을 조작하고 OS의 Undo와 Save 단축키를 보낸다. 에디터를 시작할 때만 로드하는 검사 어셈블리가 컨트롤의 화면 위치를 구하고 현재 게임 세션과 디스크의 씬을 검사한다.

## 실행

저장소 루트에서 다음 두 파일을 준비한다.

```sh
dotnet build engine/tests/KeyRoom.PlayInspector -c Release
python3 engine/scripts/package-editor.py keyroom --output /tmp/monoon-inspector
```

macOS에서는 Swift 입력 드라이버를 빌드하고 에디터를 실행한다. 마지막 인수 앞의 결과 폴더는 실행마다 새 경로를 지정한다.

```sh
swiftc engine/tests/KeyRoom.PlayInspector/mac-input.swift -o /tmp/monoon-inspector/mac-input
python3 engine/tests/KeyRoom.PlayInspector/verify_play.py \
  editor/MoNoon.app/Contents/MacOS/MoNoon \
  /tmp/monoon-inspector/KeyRoom-sample.zip \
  engine/.artifacts/bin/Probe/release/Probe.dll \
  /tmp/monoon-inspector/run --mac-input /tmp/monoon-inspector/mac-input
```

Windows에서는 같은 드라이버에 배포 에디터 경로를 넘긴다. `--mac-input`은 생략한다. 기존 데스크톱 회귀 환경의 Python과 Pillow를 사용한다(Linux 에디터는 2026-11-23부터 지원하지 않는다). VM에 파일만 복사할 때는 `KeyRoom.Desktop/verify.py`와 `verify_editor.py`도 드라이버 옆에 둔다.

## 측정과 합격 기준

- 실행 중인 게임의 입력 액션에 Up을 누르고 60 Hz로 정확히 6틱 진행한다. 첫 틱과 여섯 번째 틱의 누적 변위를 기록한다. 같은 수의 Down 틱을 보내 시도마다 출발 위치를 복원한다.
- MoveSpeed 240이면 각각 4와 24, 320이면 약 5.3333과 32여야 한다. 단순 Stop 뒤 다음 Play는 240으로 시작해야 한다.
- Local X 280과 MoveSpeed 320을 입력한 뒤 X의 체크만 끈다. 적용 직후 원본 씬과 디스크에서 다시 읽은 씬을 기준 사본과 비교한다. MoveSpeed만 변경되어야 한다. 다음 Play의 속도도 320이어야 한다.
- Stop 후 Undo 한 번과 Save로 원본 씬 전체가 복원되고 Undo 스택이 비어야 한다. 성공하면 `result.json`과 `inspector.png`가 남는다.
