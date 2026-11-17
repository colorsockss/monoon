# KeyRoom 게임 정의 이벤트

macOS 27.0.1 arm64에서 MoNoon.app과 Distribution 프로필의 두 독립 SDL 앱을 실행했다. 배포 앱은 Inspector에서 다시 연결해 저장한 KeyRoom 사본으로 빌드했다.

## Inspector와 저장

**Key → KeySequence**에서 **Door · DoorLock.Unlock**을 해제하고 씬을 저장했다. 이 상태로 Play하면 열쇠 획득은 저장되지만 문은 `Assets/Door.png`와 Solid 충돌체를 유지했다. 오른쪽으로 이동한 뒤 F5로 저장한 슬롯도 첫 방이었다. [연결을 끊은 상태](disconnected-play.png)

**Connect… → Wall / Door → DoorLock.Unlock → Connect**를 눌러 저장하고 Cmd+Z 한 번으로 연결 해제를 확인했다. 다시 연결한 뒤 에디터를 종료하고 같은 씬을 다시 열었다. Inspector와 YAML에 `KeyAcquired`, 문 GUID, `DoorLock`, `Unlock` 조합이 한 건씩 있었다. [연결 대화상자](picker.png) · [저장 후 다시 열기](reopened.png)

에디터를 880 × 680으로 줄이고 긴 수신자 이름을 지정한 사본에서 연결 창을 너비 270으로 줄였다. Object와 Method 입력란은 같은 X 좌표와 너비를 유지했고 Cancel과 Connect 버튼은 한 행에 표시됐다. [작은 연결 창](compact.png)

## 호출 수명

아래 표의 횟수는 각 단계에서 새로 발생한 횟수다. Scene Play는 에디터 콘솔에서, Release와 Native AOT는 실제 앱의 출력에서 같은 메시지를 수집했다. 각 실행은 독립된 빈 사용자 저장 폴더에서 시작했다.

| 입력과 상태 | KeyAcquired 발신 | DoorLock.Unlock 수신 | 결과 |
| --- | ---: | ---: | --- |
| 첫 방에서 획득 전 슬롯 1 저장 후 왼쪽 이동 | 1 | 1 | 열린 문으로 두 번째 방 진입 |
| 슬롯 1을 F9로 불러와 다시 획득 | 1 | 1 | 획득 상태로 첫 방을 슬롯 2에 저장 |
| 슬롯 2를 F9로 불러오기 | 0 | 0 | 열린 상태만 한 번 복원하고 두 번째 방 진입 |
| 슬롯 1로 재진입 → F6으로 문 제거 → 열쇠 위치로 이동 | 1 | 0 | 열쇠 획득 저장, 제거한 수신자 호출 없음 |
| 슬롯 1로 재진입 → F7로 열쇠 제거 → 같은 위치로 이동 | 0 | 0 | 열쇠 미획득 상태로 저장 |
| 슬롯 1로 다시 진입 → 획득 후 오른쪽 이동 | 1 | 1 | 두 번째 방 도착 후 F5 저장 |

세 실행 모두 발신 4회, 문 수신 3회, 열린 상태 복원 1회였다. 아래 로그는 제거 기록까지 포함해 행 순서가 일치한다. 마지막 F5 파일은 각각 107바이트로 기존 macOS 기준과 바이트 단위로 같았다. SHA-256은 `00d5309db260658bb54a80d78c4e2b4e7d05e473550e69d70a84e6acc9833805`다. [맥 기준 저장](../script-asset-fields/second-room.save)

| 실행 | 화면 | 호출 기록 |
| --- | --- | --- |
| Scene Play | [콘솔](scene-console.png) · [문 제거 후](scene-removed.png) | [에디터 로그](scene-events.txt) |
| Release | [열쇠 획득 후](release-opened.png) | [Release 로그](release-events.txt) |
| Native AOT | [열쇠 획득 후](aot-opened.png) | [Native AOT 로그](aot-events.txt) |

엔진 회귀 검사에서는 프리팹 인스턴스별 대상, 비활성화와 재활성화, 수신 도중 자기 제거, 제거·폐기 후 보관된 발신 델리게이트, 추가 씬의 GUID 충돌과 언로드도 확인했다. [자동 검사](../../Engine.Checks/GameEventChecks.cs)
