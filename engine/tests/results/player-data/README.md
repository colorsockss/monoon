# KeyRoom 데이터와 저장 검증

2026-11-13, macOS arm64. 설치된 도구로 패키징한 에디터와 macOS Release·Native AOT 게임 창에 실제 키 입력을 보냈다. 테스트 전용 `MONOON_USER_DATA` 아래에서 Play와 배포 게임의 프로필을 분리했다.

| 확인 대상 | 조작과 관찰 |
| --- | --- |
| 데이터 에셋 편집 | 에디터의 Project → + → New Data Asset에서 긴 이름의 에셋을 만들고 YAML의 `chestReach`를 12로 저장했다. Project에서 더블클릭해 같은 내용을 다시 읽었다. 350×268 창에서도 두 입력 필드의 시작점과 Save 버튼이 맞았다. [생성](data-editor-compact.png) · [다시 열기](data-editor-reopened.png) |
| 에디터 Play | E로 상자를 열고 F5로 슬롯 1에 저장했다. 코인을 먹은 뒤 왼쪽에서 열쇠를 얻고 문을 지나 두 번째 방에서 2 → F5로 슬롯 2에 저장했다. H로 안내를 열고 에디터를 종료했다. 다시 실행해 안내가 열린 상태, 첫 방의 미수집 코인, 두 번째 방의 열쇠를 차례로 확인했다. [재실행](editor-restart.png) · [슬롯 1](editor-slot1.png) · [슬롯 2](editor-slot2.png) |
| macOS Release | 새 배포 프로필에서 같은 경로로 두 슬롯을 저장했다. 앱을 다시 실행한 뒤 두 슬롯을 각각 불러왔다. [저장](release-saved.png) · [재실행](release-restart.png) · [슬롯 1](release-slot1.png) · [슬롯 2](release-slot2.png) |
| macOS Native AOT | Release에서 열어 둔 안내가 시작부터 표시됐다. F9로 첫 방의 열린 상자와 남아 있는 코인을, 2 → F9로 두 번째 방과 열쇠를 복원했다. 다시 첫 슬롯에서 코인을 먹고 저장했다. Release를 재실행해 같은 슬롯에서 COIN: 1이 표시되는 것을 확인했다. [시작](aot-restart.png) · [첫 방](aot-slot1.png) · [두 번째 방](aot-slot2.png) · [다시 Release에서 복원](release-after-aot.png) |

[엔진 검사](../../Engine.Checks/PlayerDataChecks.cs)는 중첩 YAML 값, 에셋 이동, 슬롯 삭제와 설정 분리, 손상·상위 버전 보호, 버전 순차 변환, 쓰기 경합, 이미 열린 상자를 반복해서 로드할 때 코인이 중복되지 않는 경우를 포함한다. v1 세이브에서 열쇠를 복원하면 첫 방의 문 충돌도 열린 상태가 된다. Windows x64와 Linux x64는 Release 내보내기와 콘텐츠 검사까지 수행했다. 두 게스트의 실행 검증은 이 기록의 범위 밖이다. [검사 로그](checks.txt)

저장소 루트에서 다음과 같이 엔진 검사를 재현한다.

```sh
dotnet run --project engine/tests/Engine.Checks -c Release -- .
```
