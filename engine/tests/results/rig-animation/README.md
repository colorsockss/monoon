# PlatformerLab 경사 보행과 이동 발판 왕복

2026-11-14, macOS 27.0.1, Apple Silicon arm64. 같은 샘플을 MoNoon의 Scene Play와 내보낸 Release·Native AOT 앱에서 각각 열었다. 각 창이 렌더링된 뒤 오른쪽 방향키로 걸어 경사 중간에 멈췄다. 다시 오른쪽으로 걸어 주황색 발판에 오른 뒤 키를 놓고 왕복을 관찰했다. 세 실행 모두 독립 저장 경로를 사용했다.

| 실행 | 경사와 발 | 발판 왕복 | 뒷발 / 앞발 디딤 표본 | 최대 발바닥 높이 오차 | 최대 탑승 미끄러짐 | 실제 게임 기록 |
| --- | --- | --- | --- | --- | --- | --- |
| Scene Play | [보행](scene-play-walk.png) · [발 확대](scene-play-feet.png) · [경사 정지](scene-play-incline.png) | [탑승](scene-play-board.png) → [반대쪽](scene-play-out.png) → [복귀](scene-play-back.png) | 33 / 33 | 0.000031 px | 0.000366 px | [YAML](scene-play.save) |
| Release | [보행](release-walk.png) · [발 확대](release-feet.png) · [경사 정지](release-incline.png) | [탑승](release-board.png) → [반대쪽](release-out.png) → [복귀](release-back.png) | 33 / 33 | 0.000031 px | 0.000366 px | [YAML](release.save) |
| Native AOT | [보행](native-aot-walk.png) · [발 확대](native-aot-feet.png) · [경사 정지](native-aot-incline.png) | [탑승](native-aot-board.png) → [반대쪽](native-aot-out.png) → [복귀](native-aot-back.png) | 33 / 33 | 0.000031 px | 0.000366 px | [YAML](native-aot.save) |

- 청록색 뒷발과 노란 앞발의 확대 이미지는 실제 창의 보행 두 순간과 경사 정지 순간을 차례로 잘라 3배 확대했다. 플레이어의 크기와 시점은 키 입력·캡처 시각에 따라 다를 수 있다.
- 발바닥의 월드 위치와 기울기를 샘플 경사 콜라이더의 두 꼭짓점으로 계산한 직선과 비교했다. 발마다 디딤·스윙이 교대했고 스윙 중 발끝이 경사 위로 2px 이상 올라갔다. 지지 구간에서 연속 틱 사이의 미끄러짐과 각도 오차도 YAML에 기록했다.
- 발판은 X=470에서 X=604까지 이동하고 다시 X=470으로 돌아왔다. 실제 물리 좌표로 도착과 복귀를 확인한 시점에만 슬롯 5를 기록한다. 탑승 미끄러짐은 탑승 이후 플레이어와 발판의 상대 변위 중 최댓값이다.
- [엔진 회귀 검사](checks.txt)는 271개가 통과했다. [트윈·뼈대 검사](../../Engine.Checks/RigChecks.cs)에는 부모가 회전한 트윈, 편도·여러 주기를 건너뛴 시간 처리, 뼈 연결과 길이, 점프 중 IK 해제도 포함한다.

기본 창의 [Runner Inspector](rig-inspector.png)와 880 × 660 창의 [뼈대·IK](rig-narrow.png), [발판 트윈](tween-narrow.png)에서 레이블과 입력 필드가 같은 열에 정렬되는 것을 확인했다.

[사용 매뉴얼](../../../../docs/manual/rig-animation.md) · [샘플 뼈대](../../../../samples/PlatformerLab/Assets/Runner.rig)
