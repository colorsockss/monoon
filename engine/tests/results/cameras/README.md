# CameraLab — macOS 카메라 검증

2026-11-14, Apple Silicon arm64, macOS 27.0.1. [CameraLab](../../../../samples/CameraLab)의 샘플 복사본에서 에디터 **Play**를 시작하고, 별도로 내보낸 Release와 Native AOT 앱은 각각 macOS 앱으로 열었다. 각 실행에서 Space를 눌러 남동쪽과 북서쪽을 차례로 방문했다. 게임이 기록한 월드·카메라 좌표와 OS에서 캡처한 실제 화면을 각각 비교했다.

| 실행 경로 | 시작 | 남동쪽 | 북서쪽으로 복귀 | 각 실행이 기록한 YAML |
| --- | --- | --- | --- | --- |
| Scene Play | [시작](editor-ready.png) | [도착](editor-1.png) | [복귀](editor-2.png) | [가는 길](editor-1.save) · [오는 길](editor-2.save) |
| Release arm64 | [시작](release-ready.png) | [도착](release-1.png) | [복귀](release-2.png) | [가는 길](release-1.save) · [오는 길](release-2.save) |
| Native AOT arm64 | [시작](aot-ready.png) | [도착](aot-1.png) | [복귀](aot-2.png) | [가는 길](aot-1.save) · [오는 길](aot-2.save) |

**추적과 경계.** 맵은 1600×896이고 양쪽 플레이 화면은 각각 400×400 월드 픽셀이다. 60Hz 고정 틱에서 Half-life=0.16초로 추적했다. 동쪽으로 가는 30번째 틱에 플레이어 중심은 (620, 440), 주 카메라 중심은 (454.45, 345.40)이었다. 도착한 플레이어는 (1584, 880), 카메라 중심은 (1400, 696)으로 프레임 오른쪽·아래쪽이 맵 끝에 맞았다. 돌아올 때는 (200, 200)에서 왼쪽·위쪽이 맞았다. 두 번째 카메라도 반대편 플레이어를 따라 반대 경계에 멈췄다.

**실행 간 일치.** 왕복 각각 1·30·60·150번째 틱의 플레이어·카메라 좌표, 미니맵 월드 크기, 좌우 분할 위치를 비교했다. 세 실행의 결과 파일은 방향별로 바이트 단위로 일치했다. 두 독립 앱은 창을 920×670으로 바꾼 상태에서도 통과했다. [Release 리사이즈](release-resized.png) · [AOT 리사이즈](aot-resized.png)

**렌더링.** 논리 화면의 분할 경계는 x=400, 미니맵 영역은 (584, 18, 200, 112)이다. 각 캡처에서 게임 영역 오프셋과 배율을 반영한 뒤 좌우 맵 배경, 두 플레이어, 미니맵 내부의 두 배경과 플레이어 표식을 표본 비교했다. 작은 표식은 보간·캡처 리샘플링으로 색에 차이가 있어 예상 위치 1px 주변을 비교했다. 세 경로 모두 예상 위치와 색을 확인했다. [픽셀 측정값](pixels.txt)

**Inspector와 회귀.** [880×660 창](inspector-narrow.png)에서 추적·경계·뷰포트 입력 행의 정렬을 확인했다. 복사본의 Half-life를 0.16에서 0.32로 바꾼 뒤 Cmd+S로 저장하고 앱을 닫았다가 다시 열어 [0.32가 유지됨](inspector-reopened.png)을 확인했다. [카메라 검사](../../Engine.Checks/CameraChecks.cs)는 고정 틱 및 중간 렌더 프레임 보간, 작은 맵의 줌, 홀수 해상도 분할, 카메라 비활성화, 클립 복구와 HUD, 프리팹 대상 재연결도 검사한다. KeyRoom의 기존 카메라 전환을 비롯한 [전체 회귀 검사](checks.txt)가 통과했다.

재실행은 저장소 루트에서 다음과 같이 각 앱을 빌드하고 위 순서로 Space를 두 번 누른다.

```sh
editor/MoNoon.app/Contents/MacOS/MoNoon --build-release samples/CameraLab game-builds/CameraLab
editor/MoNoon.app/Contents/MacOS/MoNoon --build-native-aot samples/CameraLab game-builds/CameraLab
```
