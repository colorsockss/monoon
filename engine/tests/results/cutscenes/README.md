# PlatformerLab 이동 발판 컷신 검증

2026-11-14 · macOS 27.0.1 (26A434) · Apple Silicon arm64 · 논리 해상도 800 × 450. 각 행마다 앱을 새로 열고 독립된 사용자 데이터 경로로 시작했다. Scene Play에서는 에디터의 **Play** 버튼을 눌렀다. Release와 Native AOT는 각각 내보낸 `PlatformerLab.app`의 실행 파일을 직접 열었다.

| 실행 | 종료 방법 | 대사 캡처 | 종료 후 캡처 | 복귀 후 오른쪽 이동 / 점프 상승 |
| --- | --- | --- | --- | --- |
| Scene Play | 끝까지 읽기 | [1](scene-play-complete-first.png) · [2](scene-play-complete-second.png) · [3](scene-play-complete-third.png) | [복귀](scene-play-complete-return.png) · [점프](scene-play-complete-jump.png) | +38 / 32 px |
| Scene Play | 첫 줄에서 건너뛰기 | [1](scene-play-skip-first.png) | [복귀](scene-play-skip-return.png) · [점프](scene-play-skip-jump.png) | +34 / 34 px |
| Release | 끝까지 읽기 | [1](release-complete-first.png) · [2](release-complete-second.png) · [3](release-complete-third.png) | [복귀](release-complete-return.png) · [점프](release-complete-jump.png) | +37 / 24 px |
| Release | 첫 줄에서 건너뛰기 | [1](release-skip-first.png) | [복귀](release-skip-return.png) · [점프](release-skip-jump.png) | +36 / 28 px |
| Native AOT | 끝까지 읽기 | [1](native-aot-complete-first.png) · [2](native-aot-complete-second.png) · [3](native-aot-complete-third.png) | [복귀](native-aot-complete-return.png) · [점프](native-aot-complete-jump.png) | +34 / 26 px |
| Native AOT | 첫 줄에서 건너뛰기 | [1](native-aot-skip-first.png) | [복귀](native-aot-skip-return.png) · [점프](native-aot-skip-jump.png) | +37 / 28 px |

오른쪽 방향키를 3.05초 눌러 실제 경사를 올라 발판을 밟았다. 컷신에서는 화면 오른쪽 끝에 있던 금색 깃발이 확대되고 일반 HUD 대신 대사 패널이 나타났다. 방향키와 Space를 다시 보내도 첫 대사 영역의 픽셀은 그대로였다. Enter를 0.5초 누르면 둘째 줄만 표시됐고, 다음 Enter로 마지막 줄, 마지막 Enter로 일반 화면이 돌아왔다. 별도 실행에서는 첫 줄에서 X를 눌러 복귀했다.

표의 이동 거리는 **캐릭터 헬멧과 발판 왼쪽 끝의 상대 좌표**를 스크린샷에서 구한 값이다. 발판 자체의 움직임은 값에 포함되지 않는다. 종료 후 오른쪽 방향키를 0.24초 누르고, 별도로 Space를 눌렀다. 상승 거리는 점프 직전과 직후 캡처의 헬멧 높이 차이로, 캡처 시점에 따라 차이가 있다.

[런타임 검사](../../Engine.Checks/CutsceneChecks.cs)는 발판 이동 중 승객 상대 위치, 점프 억제, 누르고 있는 버튼과 새 입력의 구분, 마지막 대사에서 패드 South와 점프 중복 방지, 원래 쓰던 비기본 카메라 복원, 디렉터 비활성화, 자동 주행 중 즉시 건너뛰기도 검사한다. 패드 입력은 자동 검사로 확인했고 물리 게임패드는 사용하지 않았다. [전체 회귀 검사](checks.txt): 292개 통과, 이 중 컷신 21개.

디렉터 Inspector의 Dialogue 필드가 [기본 창](director.png)과 [880 × 660 창](director-narrow.png)에서 Name 필드와 같은 열에 정렬되는 것도 확인했다.

[사용 매뉴얼](../../../../docs/manual/cutscenes.md) · [샘플 대사](../../../../samples/PlatformerLab/Assets/FerryCrossing.cutscene)
