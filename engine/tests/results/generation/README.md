# SeedLab: 같은 시드의 지도 재현과 충돌

2026-11-14, macOS 27.0.1 (26A434), Apple Silicon arm64. 에디터에서 SeedLab을 열어 Scene Play 버튼을 눌렀고, 별도로 내보낸 Release와 Native AOT 앱도 각각 직접 실행했다. 세 실행은 별도 사용자 데이터 경로를 사용했다.

| 실행 | 세 시드의 실제 창 | 시드 42에서 조작한 결과 |
| --- | --- | --- |
| Scene Play | [104729](scene-play-initial.png) · [104730](scene-play-next.png) · [42](scene-play-custom.png) | [통로 이동](scene-play-walk.png) · [벽 충돌](scene-play-wall.png) · [재생성](scene-play-reset.png) |
| Release | [104729](release-initial.png) · [104730](release-next.png) · [42](release-custom.png) | [통로 이동](release-walk.png) · [벽 충돌](release-wall.png) · [재생성](release-reset.png) |
| Native AOT | [104729](native-aot-initial.png) · [104730](native-aot-next.png) · [42](native-aot-custom.png) | [통로 이동](native-aot-walk.png) · [벽 충돌](native-aot-wall.png) · [재생성](native-aot-reset.png) |

1. 첫 화면의 **104729**에서 실제 **N** 키로 **104730**, **P**로 다시 **104729**를 생성했다.
2. 화면의 **Custom seed** 필드를 클릭해 **42**를 입력하고 **Apply seed**를 클릭했다. WASD로 열린 방향으로 한 칸 이동하고 **R**로 같은 지도를 재생성해 캐릭터가 입구로 돌아오는지 확인했다. 입구에서 **A**를 누르고 있어도 왼쪽 벽을 통과하지 않았다.
3. 각 단계의 창 캡처에서 벽·통로·입구·출구를 색으로 판별해 행 순서대로 **375개 타일 ID(각 1바이트)**를 읽었다. 텍스트나 엔진이 보고한 시드 값만 비교하지 않고, 화면에 나타난 전체 배열을 독립 계산 결과와 대조했다. 아래는 타일 배열의 SHA-256 앞 12자리다. [단계별 전체 해시](screen-cells.txt).

| 시드 | 세 실행 방식의 지도 해시 | 출구 (타일 X, Y) |
| --- | --- | --- |
| 104729 | `bf764b9fd9bd` | (11, 11) |
| 104730 | `9e31a7687ae2` | (19, 9) |
| 42 | `ba09b9f29567` | (9, 3) |

[자동 검사](../../Engine.Checks/GenerationChecks.cs)는 동일 시드의 전체 충돌 지도와 새 지도 교체의 원자성, 연결성과 최장 거리 출구, 시드 범위 오류 때 현재 게임 유지, 원본 씬 보존도 검사한다. 시드 42에서 실제 이동 입력을 고정 틱마다 적용해 금색 출구에 도착하고 완료 메시지가 표시되는지 확인했다. [전체 회귀 검사](checks.txt): 307개 통과.

[SeedLab 사용법](../../../../docs/manual/generation.md)
