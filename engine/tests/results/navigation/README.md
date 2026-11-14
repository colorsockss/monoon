# PlatformerLab: 보행 경로와 적 AI

2026-11-14, macOS 27.0.1 / Apple Silicon arm64. 새 에디터에서는 PlatformerLab 복사본을 열고 Play를 눌렀다. Release와 Native AOT는 각 앱의 게임 실행 파일을 열었다. 별도 저장 경로로 시작해 윗발판 순찰을 관찰한 뒤 오른쪽 방향키를 약 1.6초 누르고, 경사에서 적을 기다렸다가 왼쪽 방향키를 약 2.1초 눌러 시야를 벗어났다. 자동 경로, 순간이동, AI 상태 강제 설정은 사용하지 않았다.

| 실제 창 | 순찰 | 발견·추적 | 놓친 뒤 복귀 | 순찰 재개 | 게임 측정값 |
| --- | --- | --- | --- | --- | --- |
| Scene Play | [시작](scene-play-patrol.png) | [추적](scene-play-chase.png) | [복귀](scene-play-return.png) | [재순찰](scene-play-resumed.png) | [YAML](scene-play.save) |
| Release arm64 | [시작](release-patrol.png) | [추적](release-chase.png) | [복귀](release-return.png) | [재순찰](release-resumed.png) | [YAML](release.save) |
| Native AOT arm64 | [시작](native-aot-patrol.png) | [추적](native-aot-chase.png) | [복귀](native-aot-return.png) | [재순찰](native-aot-resumed.png) | [YAML](native-aot.save) |

세 게임에서 **Patrol → Chase → Return → Patrol**이었다. 각자 추적 중 최대 10개 지점이 있는 경로를 찾았고, 물리 접지 기록에서 윗발판과 경사로를 모두 밟았다. 출발점에서 수평으로 가장 멀어진 거리는 각각 201 / 204 / 204px였다. 시야를 놓친 뒤 원래 발판까지 돌아왔고, 이어서 다음 순찰 끝점 근처까지 각각 53 / 54 / 54px 걸었다. 기록은 첫 복귀가 아니라 그 이후 순찰 구간을 실제로 걸었을 때 남긴다. 실제 키 입력과 창 캡처는 고정 틱에 동기화하지 않아 세 실행의 정확한 좌표와 통과 지점 수에는 작은 차이가 있다.

Navigation Region 2D의 [기본 Inspector](region-inspector.png)와 [880×660 Inspector](region-narrow.png)에서 크기·경사·레이어 마스크 필드의 정렬을 확인했다. [전체 회귀 검사: 253개 통과](checks.txt). [내비게이션 검사](../../Engine.Checks/NavigationChecks.cs)에는 벽에 가려진 시야, 비활성 대상, 좁은 벽과 단절된 착지대의 경로 거부, 지형 변경 후 재탐색, 고정 틱에서 경사 추적과 복귀가 포함된다. 기존 경사·발판·스트리밍과 KeyRoom 검사도 통과했다.

[사용 설명서](../../../../docs/manual/navigation.md) · [샘플 AI](../../../../samples/PlatformerLab/Scripts/Lookout.cs)
