# KeyRoom 씬 전환 검증

2026-11-13, macOS arm64. 에디터 Play와 패키징된 KeyRoom 두 빌드를 실행해 **M → Close 클릭 → 왼쪽에서 열쇠 획득 → 오른쪽 문 통과 → 두 번째 방에서 M으로 안내 열기·닫기**를 수행했다. 두 번째 방에서는 F5로 저장한 슬롯의 방 ID와 열쇠 플래그도 확인했다. 테스트용 프로필을 사용했다.

| 실행 대상 | 첫 방에서 안내 열기·닫기 | 비동기 전환 | 두 번째 방과 열쇠 |
| --- | --- | --- | --- |
| MoNoon 에디터 Play | [열기](editor-guide.png) · [Close 클릭](editor-closed.png) | [Scene 패널](editor-transition.png) | [안내 열기](editor-second-guide.png) · [닫은 뒤](editor-second.png) |
| macOS Release | [열기](release-guide.png) · [Close 클릭](release-closed.png) | [게임 창](release-transition.png) | [안내 열기](release-second-guide.png) · [닫은 뒤](release-second.png) |
| macOS Native AOT | [열기](aot-guide.png) · [Close 클릭](aot-closed.png) | [게임 창](aot-transition.png) | [안내 열기](aot-second-guide.png) · [닫은 뒤](aot-second.png) |

[엔진 검사](../../Engine.Checks/SceneStreamingChecks.cs)는 카메라 없는 구역을 기존 방에 추가해 벽 충돌을 확인한다. 구역 제거 후 플레이어가 같은 위치에서 벽을 통과하고, 구역에서 생성한 프리팹이 제거되며, 그 구역만 읽었던 데이터 에셋을 새로 읽는지 확인한다. 기존 오브젝트와 같은 ID가 있는 씬, 중복 추가, 로딩 취소, 없는 파일, 텍스처 준비 실패 후 재시도, 마지막 공유 에셋 해제도 검사한다. Windows x64·Linux x64는 Release 패키지를 내보내고 빌드 콘텐츠로 엔진 검사를 실행했다. 실제 창에서의 확인은 macOS에서 수행했다. [실행 로그](checks.txt)

저장소 루트에서 `dotnet run --project engine/tests/Engine.Checks -c Release -- .`로 엔진 검사를 재현할 수 있다.
