# KeyRoom 런타임 Transform 검증

## Mac 에디터와 배포 게임

2026-11-17, macOS 27.0.1 arm64에서 패키징한 MoNoon.app의 Scene Play와 KeyRoom의 Release·Native AOT 앱을 각각 열었다. 각 창에 **E → C → 아래로 이동 → C**를 입력했다. Player 자식으로 옮겨진 코인은 함께 이동했고, 내려놓는 순간 계층의 루트로 옮겨져 같은 월드 위치에 남았다. **F5 → 상태 변경 → F9**로 들었을 때와 내려놓았을 때의 저장을 각각 복원했다. [Scene Play에서 든 모습](scene-play/held.png) · [Scene Play에서 내려놓은 모습](scene-play/dropped.png) · [Release 창](osx-arm64/release/window-dropped.png) · [Native AOT 창](osx-arm64/native-aot/window-dropped.png)

세 창에서 같은 기준 슬롯도 불러왔다. 코인을 들고 있는 슬롯을 다시 저장한 결과와 **C**로 내려놓은 후의 슬롯이 각각 기준 파일과 바이트 단위로 일치했다. 충돌 경계 바로 앞 슬롯을 불러왔을 때는 코인이 남아 있었고, 오른쪽으로 걸어 닿자 획득됐다. Scene Play를 Stop한 뒤 원본 씬 파일의 SHA-256이 실행 전과 같았다. 다시 Play한 첫 슬롯에는 `chest: false`, `coin: false`가 기록됐다. [들고 있는 기준](../../KeyRoom.WorldCheckpoints/fixtures/carry/held.save) · [내려놓은 기준](../../KeyRoom.WorldCheckpoints/fixtures/carry/dropped.save) · [씬 해시](scene-play/scene.sha256) · [새 Play 슬롯](scene-play/fresh-play.save)

## 고정 입력으로 비교하는 값

같은 순서의 키 입력을 60Hz로 재생했다. 각 행은 게임을 새로 시작해 직전 슬롯을 이어서 사용한다. 실행마다 새로 생성되는 코인 GUID만 첫 저장에서 대응시켰다. 한 실행 안에서 저장 전후의 GUID는 그대로 비교했다. [입력 기록 6개와 기준 슬롯](../../KeyRoom.WorldCheckpoints/fixtures/carry) · [플랫폼별 재실행 명령](../../KeyRoom.WorldCheckpoints/README.md)

| 입력 | 확인한 결과 |
| --- | --- |
| E, C, 아래로 8틱, F5 | 코인 부모는 Player, 로컬 위치는 (56, −30), 월드 위치는 (330, 116). 회전 30°, 스케일 (1.5, 0.75), 트리거 활성. |
| F9, 슬롯 2, F5 | 들고 있던 슬롯 1,520바이트 전체 일치. |
| F9, C, F5 | 코인 부모는 루트. 월드 위치 (330, 116)과 두 축의 방향·배율 유지. |
| F9, 슬롯 2, F5 | 내려놓았던 슬롯 1,488바이트 전체 일치. |
| F9, 오른쪽으로 8틱, F5 | Player가 (306, 146)일 때 코인이 남아 있다. |
| F9, 오른쪽으로 2틱, F5 | Player가 (314, 146)에 도착해 코인을 획득한다. 저장한 프리팹은 0개다. |

세 운영체제에서 각각 배포 프로필로 빌드한 실행 파일로 위 입력을 창 없이 재생했다. 여섯 배포본 모두 각 단계의 전체 슬롯이 맥 기준과 일치했다. 각 폴더에 해당 빌드의 영수증과 저장 파일 6개가 있다.

| 실행 환경 | Release | Native AOT |
| --- | --- | --- |
| macOS 27.0.1 arm64 | [6단계 통과](osx-arm64/release) | [6단계 통과](osx-arm64/native-aot) |
| Windows 11 Enterprise LTSC x64 VM, 빌드 26100.1742 | [6단계 통과](win-x64/release) | [6단계 통과](win-x64/native-aot) |
| Debian 12 x64 VM | [6단계 통과](linux-x64/release) | [6단계 통과](linux-x64/native-aot) |

엔진 회귀에서는 코인 로컬 좌표 (21, 11)과 (23, 11)을 월드로 변환해 반지름 0.1의 겹침 질의를 했다. 들었을 때와 F9로 복원한 뒤 모두 첫 점만 적중했다. 반전·비균일 스케일·기울기가 있는 부모 사이의 이동은 자식의 네 모서리로 확인했다. 순환 계층과 0배율을 거절한 뒤에도 기존 변환이 유지됐다. 기존 월드 버전 1의 미획득·획득 슬롯도 Mac 두 배포본에서 원래 바이트로 복원됐다. [엔진 회귀](../../Engine.Checks/RuntimeTransformChecks.cs) · [기존 기준 슬롯](../../KeyRoom.WorldCheckpoints/fixtures)
