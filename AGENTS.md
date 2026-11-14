# 에디터 UI 정렬

- UI를 만들거나 수정할 때 같은 패널의 제목, 레이블, 입력 필드, 버튼이 기존 행과 같은 기준선에 맞도록 한다. 기존 그리드 열 너비, 여백, 행 간격을 우선 재사용한다.
- 창 크기를 바꾸거나 긴 이름이 표시될 때도 정렬이 유지되는지 확인한다. 선택 상태와 드래그 강조 표시가 바뀌어도 주변 컨트롤이 밀리지 않도록 한다.

# 검증용 도구 설치

- 이 Mac에 QEMU 같은 검증용 도구를 새로 설치할 때는 도구 이름과 필요한 이유를 먼저 알리고, 사용자가 명시적으로 승인한 뒤에 설치한다. 검증을 진행해 달라는 요청만으로는 설치에 동의한 것으로 보지 않는다.

# 플랫폼 오류를 재현할 환경

먼저 오류가 난 대상을 기록한다: 에디터의 Scene 편집, 에디터 안의 Play, 독립 SDL 창(Save & Run), 또는 내보낸 게임(Release / Native AOT). 같은 프로젝트라도 실행 경로가 다르다. 명령은 저장소 루트 기준이다.

| 대상 | 엔진·에디터를 살필 환경 | 빌드한 게임을 살필 환경 |
| --- | --- | --- |
| [macOS arm64](docs/troubleshooting/macos.md) | 이 Apple Silicon Mac의 `editor/MoNoon.app`. Scene은 패치한 SDL 3.4.12 + Cocoa NSView를 사용한다. | 같은 Mac에서 `game-builds/KeyRoom/KeyRoom.app`과 `NativeAOT/KeyRoom.app`을 구분한다. 둘 다 플레이 기록이 있다. |
| [Windows x64](docs/troubleshooting/windows.md) | Avalonia Scene 경로. 에디터 문제는 Windows 데스크톱에서 소스로 재현한다. 패키지 에디터 검증 기록은 없다. | 보존한 QEMU Windows 11 Enterprise LTSC x64에서 `KeyRoom-win-x64/MoNoonPlayer.exe` (Release). 시작 오류는 배포 폴더의 SDL3.dll부터 확인한다. |
| [Linux x64](docs/troubleshooting/linux.md) | Avalonia Scene 경로. 에디터 문제는 데스크톱 세션에서 소스로 재현한다. 패키지 에디터 검증 기록은 없다. | 보존한 QEMU Debian 12 amd64의 X11/Xvfb에서 `KeyRoom-linux-x64/MoNoonPlayer` (Release). 창 초기화와 HUD 텍스트 오류를 구분한다. |

- x64 게스트는 이 Mac의 `.test-env/`를 재사용한다. 가상 디스크·기반 이미지·키를 삭제하거나 저장소에 추가하지 않는다. [재실행 명령과 합격 기준](docs/troubleshooting/README.md)을 따른다.
- InventoryLab의 Windows·Linux x64 회귀는 각 게스트에서 Release와 Native AOT를 직접 빌드하고 같은 입력을 헤드리스로 재생한다. [게스트 실행기](engine/tests/InventoryLab.Replay/README.md) · [네 배포본의 기준 저장 비교](engine/tests/results/input-replay-x64/README.md)
- 보고서에는 OS/아키텍처, 실행 대상과 빌드 종류, 첫 예외, 재현 입력, 로그 또는 캡처를 남긴다. 게임의 게스트 실행 결과를 같은 OS의 에디터·Native AOT·물리 GPU 검증으로 간주하지 않는다.
