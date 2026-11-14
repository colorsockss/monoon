# Canvas UI 검증 기록 · 2026-11-12

씬 v7, 논리 해상도 800×450의 KeyRoom을 사용했다. Apple Silicon Mac의 에디터 검증은 프로젝트 복사본에서 수행했다. 아래 캡처는 각 프로세스의 실제 창이다.

| 실행 대상 | 확인한 동작 |
| --- | --- |
| [MoNoon Scene 편집](macos-editor/01-edit.png) | 시작 카메라 프레임에 조건부 열쇠 표시와 안내가 보인다. [좁은 Inspector](macos-editor/07-narrow-inspector.png)는 긴 이름의 UI 버튼을 선택한 상태다. |
| [에디터 Play](macos-editor/02-play.png) | [Controls 클릭](macos-editor/03-click-open.png)으로 안내를 열고 [Enter](macos-editor/04-enter-close.png)로 닫았다. ⌘S가 씬을 쓴 뒤 에디터 프로세스를 종료했다. [재열기](macos-editor/05-reopened.png) 후 Play에서 게임 패널에 포커스를 주고 [Tab → Enter](macos-editor/06-reopened-keyboard.png)로 안내를 열었다. |
| Mac Release | [클릭으로 열기](macos-release/02-click-open.png)와 [키보드로 닫기](macos-release/03-enter-close.png). 세로로 긴 640×600 창에서 [검은 여백 클릭](macos-release/04-tall-bars.png)은 무시되고, [표시 영역 안의 버튼 클릭](macos-release/05-tall-click-open.png)만 안내를 열었다. |
| Mac Native AOT | [같은 버튼 클릭](macos-native-aot/02-click-open.png)과 [Enter](macos-native-aot/03-enter-close.png)를 확인했다. |
| Windows x64 Release | QEMU Windows 11 Enterprise LTSC 24H2(26100.1742)에서 [Tab 포커스](windows-x64/controls/base/02-tab-focus.png) → [Enter로 열기](windows-x64/controls/base/03-key-open.png) → [닫기](windows-x64/controls/base/04-key-close.png)를 확인했다. [가로 창 안쪽 클릭](windows-x64/controls/wide/05-click-open.png) · [여백 클릭 후](windows-x64/controls/wide/miss-bar.png) · [게스트 측정값](windows-x64/results.json) |
| Linux x64 Release | QEMU Debian 12 x86_64, X11/Xvfb에서 [Tab 포커스](linux-x64/controls/base/02-tab-focus.png) → [Enter로 열기](linux-x64/controls/base/03-key-open.png) → [닫기](linux-x64/controls/base/04-key-close.png)를 확인했다. [세로 창 안쪽 클릭](linux-x64/controls/tall/05-click-open.png) · [바깥·여백 클릭 후](linux-x64/controls/tall/07-misses-closed.png) · [게스트 측정값](linux-x64/results.json) |

두 x64 게스트에서 측정한 버튼 경계는 픽셀 단위로 같았다. 원점은 창의 콘텐츠 왼쪽 위이며, 오른쪽·아래 끝은 영역에 포함하지 않는다.

| 창 크기 | 게임 영역과 여백 | Controls 경계: 왼쪽·위 → 오른쪽·아래 |
| --- | --- | --- |
| 800×450 | 800×450, 여백 없음 | (698, 14) → (786, 42) |
| 960×432 | 768×432, 좌우 96px | (766, 13) → (851, 40) |
| 640×600 | 640×360, 상하 120px | (558, 131) → (629, 154) |

각 크기에서 버튼의 두 대각선 모서리 **안쪽**을 차례로 클릭해 안내를 열고 닫았다. 네 변 바로 **바깥쪽**과 검은 여백은 다시 포커스를 준 다음 각각 클릭해 포커스만 해제되고 안내는 계속 닫혀 있는지 확인했다. Windows 첫 검사에서는 한 프레임을 기다리는 고정 시간 안에 포커스 해제를 확인하지 못했다. 최종 검사는 각 입력 전후의 화면 변화를 기다렸고 양쪽 게스트에서 통과했다. 두 환경 모두 방향키·WASD로 둘째 방 진입까지 통과했으며, 실행 파일·엔진 DLL·버튼 스크립트·YAML의 해시는 현재 배포본과 일치했다.

[엔진 통합 검사](../../Engine.Checks/Program.cs)는 총 85개 항목을 통과했다. v6 읽기와 v7 저장, 카메라 없는 메뉴, 런타임 UI 상태와 편집 데이터 분리, 앵커·클릭·포커스, 열쇠 획득과 둘째 방 전환을 포함한다. 게스트에서 사용한 [데스크톱 검사기와 재실행 명령](../../KeyRoom.Desktop/README.md)은 별도로 관리한다.
