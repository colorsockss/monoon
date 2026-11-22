# KeyRoom 수집품 그룹 조회

2026-11-17에 각 OS의 배포 에디터에서 KeyRoom을 Scene Play로 실행했다. 검사는 복사한 `Coin.prefab`의 분류를 비운 다음, 프리팹 Inspector에 태그 `Coin`과 그룹 `수집품`을 입력하고 저장했다. 프리팹 창을 다시 열어 두 값이 유지되는지 먼저 확인했다.

| 배포 에디터 | 상자 열기 전 → 생성 틱 → 획득 틱 | 코인을 들고 문에 도달 → 씬 전환 직후 이전 목록 / 새 목록 | 기록 |
| --- | --- | --- | --- |
| macOS arm64 · MoNoon.app | 0 → 1 → 0 | 1 → 0 / 0 | [실제 ID](osx-arm64/result.json) · [Inspector](osx-arm64/prefab-inspector.png) · [생성](osx-arm64/spawn-query.png) · [이탈](osx-arm64/exit-query.png) |
| Windows 11 Enterprise LTSC x64 · QEMU | 0 → 1 → 0 | 1 → 0 / 0 | [실제 ID](win-x64/result.json) · [Inspector](win-x64/prefab-inspector.png) · [생성](win-x64/spawn-query.png) · [이탈](win-x64/exit-query.png) |
| Debian 12 x64 · QEMU/X11 | 0 → 1 → 0 | 1 → 0 / 0 | [실제 ID](linux-x64/result.json) · [Inspector](linux-x64/prefab-inspector.png) · [생성](linux-x64/spawn-query.png) · [이탈](linux-x64/exit-query.png) |

첫 실행은 E로 상자를 열고 오른쪽으로 걸어 트리거에서 코인을 획득했다. 두 번째 실행은 E, C로 코인을 플레이어의 자식으로 붙이고 왼쪽에서 열쇠를 얻은 다음 오른쪽 문을 통과했다. 두 번째 방의 씬 ID를 검사했고 획득 플래그는 여전히 꺼져 있었다. 생성 전부터 보관한 태그 목록과 그룹 목록도 각 단계에서 같은 ID를 반환했다.

Inspector와 Save Prefab에는 Avalonia 입력 이벤트를 보냈다. 이동과 상호작용에는 배포 에디터가 실행한 게임 세션의 고정 틱을 사용했다. PNG는 Inspector와 Play의 Debug 패널에 실제 표시된 컨트롤이다. Debian에서는 에디터에 포함한 나눔고딕 대체 글꼴로 두 입력란에 한글 그룹명이 표시되는 것도 확인했다. [회귀 실행 방법](../../KeyRoom.ObjectGroups/README.md)

Mac에서는 배포용 Release와 Native AOT로 KeyRoom을 빌드하고 기존 코인 운반·획득·저장 리플레이도 실행했다. 두 배포본 모두 기존 기준 슬롯 비교를 통과했다. 엔진 회귀에서는 추가 씬의 생성·파괴 콜백에서의 조회, 자원 준비 실패 후 멤버 누출, 목록을 순회하며 제거하는 경우도 확인했다. [Release 빌드 기록](osx-arm64/release.receipt) · [Native AOT 빌드 기록](osx-arm64/native-aot.receipt) · [배포 게임 회귀 절차](../../KeyRoom.WorldCheckpoints/README.md)
