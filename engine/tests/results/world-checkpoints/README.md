# KeyRoom 월드와 런타임 프리팹 저장

macOS 27.0.1 arm64에서 MoNoon.app의 Scene Play와 Distribution 프로필의 Release·Native AOT 앱을 실행했다. 각 창에 키보드 입력을 보냈고 각각 빈 사용자 저장 폴더에서 시작했다.

| 실행 | F9 후 살아 있는 코인 | 획득 후 F9 |
| --- | --- | --- |
| Scene Play | [Hierarchy와 Inspector](play-restored.png) · [저장 파일](play-restored.save) | [열린 상자와 코인 카운터](play-collected.png) |
| Release | [게임 화면](release-restored.png) · [저장 파일](release-restored.save) | [획득 화면](release-collected.png) |
| Native AOT | [게임 화면](aot-restored.png) · [저장 파일](aot-restored.save) | [획득 화면](aot-collected.png) |

1. 첫 방에서 **E → N → N → F5**를 눌렀다. Chest의 월드 위치는 (332, 131), 자식 Coin의 로컬 위치는 (84, −5)였다. 생성된 코인은 한 개였다.
2. **N**을 한 번 더 눌러 로컬 X를 100으로 바꿔 슬롯 2에 저장했다. 슬롯 1을 **F9**로 두 번 불러오고 슬롯 2에 다시 저장했다. 세 실행 모두 각자의 첫 저장과 바이트 단위로 일치했다. 프리팹 에셋, 부모 GUID, 코인의 인스턴스 GUID, 로컬 X 84와 Y −5가 유지됐다. Scene Play에서는 Chest 아래 Coin을 직접 선택해 Inspector의 월드 위치 (416, 126)와 Trigger를 확인했다.
3. 오른쪽으로 걸어 코인을 먹고 슬롯 1에 **F5**로 저장했다. 슬롯 2를 불러와 같은 ID의 코인이 살아 있는지 확인한 다음 슬롯 1을 두 번 불러왔다. 다시 **E**와 **N**을 눌러도 뚜껑은 비활성 상태, 코인은 0개, 획득 플래그는 참이었다.
4. Native AOT 앱에 Scene Play의 미획득 슬롯을 복사하고 **F9 → F5**로 다른 슬롯에 저장했다. 두 파일 1,129바이트가 일치했다. [Scene Play 원본](play-restored.save) · [Native AOT에서 다시 저장한 슬롯](aot-from-play.save)

각 실행을 새로 시작하므로 처음 생성하는 코인의 GUID는 다르다. 코인을 먹은 뒤의 슬롯은 세 실행에서 모두 612바이트로 동일했다. SHA-256은 `0becae59f181da639d3442621ea0857c45ccc2a1aad317ab28ba3ce157184fb0`이다. [획득 후 공통 저장 파일](collected.save)

엔진 전체 회귀 검사도 통과했다. 이동한 부모 아래 중첩 프리팹, 프리팹 멤버·씬 오브젝트 제거, 충돌체와 활성 상태, 다른 방에서 저장한 뒤 재시작, 잘못된 프리팹 참조가 있어도 기존 씬 유지, 이전 v1·v2 슬롯 읽기를 검사했다. [월드 회귀 검사](../../Engine.Checks/WorldCheckpointChecks.cs) · [KeyRoom 슬롯 회귀 검사](../../Engine.Checks/PlayerDataChecks.cs) · [사용법](../../../../docs/manual/player-data.md#월드와-런타임-프리팹-저장)

## 깨끗한 체크아웃 검증

2026-11-17, macOS 27.0.1 arm64와 .NET SDK 10.0.101에서 각 커밋을 별도 디렉터리에 복제했다. 빌드 전 두 체크아웃의 `git status --porcelain` 출력은 비어 있었다. 이 Mac에 설치된 SDK와 NuGet 패키지 캐시를 사용했다.

| 체크아웃 | 빌드와 검사 결과 |
| --- | --- |
| `main` · `5add013` | `Engine.Checks` Release 빌드 성공. 이 커밋에는 `WorldCheckpoints.cs`와 `WorldCheckpointChecks.cs`가 빠져 있고 KeyRoom F9는 기존 `LoadScene`을 호출한다. |
| 로컬 `verify/world-checkpoints-7.4` · `2a3c604` | 같은 기반에 7.4 엔진·KeyRoom·회귀 변경만 담았다. 플레이어 템플릿을 소스에서 새로 만들고 엔진 전체 회귀, macOS KeyRoom Release·Native AOT 배포 빌드를 통과했다. |

두 배포본에서 각각 별도 프로필로 헤드리스 입력을 재생했다. 코인이 든 기존 Mac 슬롯을 F9로 불러 다른 슬롯에 F5로 저장하자 원본 1,129바이트와 일치했다. 오른쪽으로 걸어 코인을 먹고 F5로 저장한 뒤 게임을 다시 실행해 F9를 두 번 누르고 E로 상자를 다시 열었다. 두 배포본 모두 획득 후 기준 파일 612바이트와 일치했다. [미획득 기준](play-restored.save) · [획득 후 기준](collected.save)
