# KeyRoom 압력판과 문

2026-11-16, macOS 27.0.1 arm64에서 MoNoon의 Scene Play, KeyRoom Release와 Native AOT 앱을 각각 열었다. 실행마다 별도 저장 폴더를 사용했다. 첫 방에서 방향키로 열쇠를 획득하고 두 번째 방으로 넘어가 압력판을 시험했다.

| 두 번째 방에서 한 조작 | 문 상태 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- | --- |
| 빈 압력판으로 상자를 밀어 넣고 플레이어는 밖에 둠 | 상자 하나, 닫힘 | [화면](editor-crate-only-before.png) | [화면](release-crate-only-before.png) | [화면](aot-crate-only-before.png) |
| 플레이어도 압력판에 올라감 | 둘, 열림 | [화면](editor-both.png) | [화면](release-both.png) | [화면](aot-both.png) |
| 플레이어가 위로 벗어나 상자만 남김. 판을 돌아 열린 문을 통과함 | 상자 하나, 열림 유지 | [통과 후](editor-passed.png) | [통과 후](release-passed.png) | [통과 후](aot-passed.png) |
| 플레이어가 돌아와 둘이 누른 뒤, 상자를 오른쪽으로 밀어 판 밖으로 보냄 | 플레이어 하나, 열림 유지 | [화면](editor-player-remains.png) | [화면](release-player-remains.png) | [화면](aot-player-remains.png) |
| 마지막 플레이어도 판에서 나와 문 중앙에서 오른쪽으로 걸음 | 빈 판, 닫힘. 문에 막힘 | [정지 위치](editor-blocked.png) | [정지 위치](release-blocked.png) | [정지 위치](aot-blocked.png) |

화면의 HUD에서 물체 수, 남은 물체와 문 상태를 확인했다. 통과·정지는 고정 카메라 화면의 벽과 플레이어 스프라이트 위치로도 대조했다. 닫힌 문의 왼쪽 경계는 월드 X=570이고 플레이어 폭은 40이므로, 정면에서 막힌 플레이어의 왼쪽 경계는 X=530이다. 아래 수치는 캡처에서 환산한 근삿값이다.

| 실행 | 열린 문을 통과한 후 플레이어 X | 닫힌 문에 막힌 플레이어 X | 빌드 |
| --- | ---: | ---: | --- |
| Scene Play | 674 | 530 | `editor/MoNoon.app` |
| Release | 675 | 531 | distribution · `411a4c3d201e4a97a5f40c2c9798da88` |
| Native AOT | 675 | 531 | distribution · `940c52735f9f4d25840beae3905ae6a5` |

전체 `Engine.Checks`가 통과했다. 일반 콜라이더와 트리거 양쪽의 Enter → Stay → Exit, 한 틱 안에 트리거를 관통했을 때의 Enter → Exit, 레이어 마스크, 두 트리거의 중복 방지, 비활성화·재활성화, 콜백 중 상대 제거, 인접 Solid 타일을 지날 때 타일맵 접촉이 유지되는 경우를 검사했다. KeyRoom 회귀는 문 스프라이트와 충돌체 활성 상태, 레이캐스트로도 문 개폐를 확인한다. [접촉 회귀 검사](../../Engine.Checks/ContactChecks.cs) · [사용 설명](../../../../docs/manual/collision.md#진입유지이탈-이벤트)
