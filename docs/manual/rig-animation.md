# 트윈, 스켈레탈 애니메이션

[PlatformerLab](../../samples/PlatformerLab)의 쇼케이스(`Scenes/Showcase.scene`)를 Play하면 시작 표지판 옆에서 `.rig` 뼈대로 만든 인물이 제자리에서 달린다(Skeleton Animator 2D, Time 시계). 오른쪽 방향키나 D로 경사를 올라가 나무 뗏목 발판을 처음 밟으면 발판이 캐릭터를 태운 채 134px 구간을 왕복하고, [건너편 대화](cutscenes.md)가 시작된다. Enter로 끝까지 읽거나 X로 닫으면 같은 발판을 원래 시점에서 볼 수 있다. Space는 점프, R은 같은 구간의 자동 주행이다.

| Add Component | Inspector에서 설정하는 값 |
| --- | --- |
| Position Tween 2D | Δ X/Y는 시작점에서의 변위, Time (s)는 편도 시간이다. Ease로 가속·감속을 고르고 Repeat에서 Once 또는 PingPong을 고른다. |
| Path Follower 2D | Scene에서 편집한 곡선 경로를 일정 속도로 따라간다. [곡선 경로를 따라가는 발판](platformer-physics.md#곡선-경로를-따라가는-발판) |
| Skeleton Animator 2D | Rig에 프로젝트의 `.rig`를 지정한다. Clock의 Time은 Cycle (s)마다 한 바퀴, Travel은 이동 발판의 운반량을 뺀 로컬 가로 이동 거리 Stride마다 한 바퀴 재생한다. 역방향으로 걸으면 주기도 거꾸로 흐른다. |

트윈의 변위는 부모 좌표계를 따른다. 움직이는 충돌체에는 Kinematic 강체를 사용하면 물리가 막힌 구간과 탑승자 운반을 처리한다. 샘플은 Play on start를 끄고 탑승 시 `tick.Objects.PlayTween(Ferry)`로 시작한다. 자동 시작을 켜면 Play와 함께 출발한다. 샘플의 EaseInOut은 양 끝에서 감속하며 편도 2초, 왕복 4초다.

뼈대는 YAML 에셋이다. Project에서 `.rig`를 더블클릭하면 설정된 외부 에디터에서 열 수 있다. `bones`에 부모를 먼저 선언하고 자식의 `parent`에 이름을 쓴다. 각 뼈의 X/Y와 Angle(도)은 부모 기준이고 Length는 뼈의 길이다. Width와 Color는 그 뼈에 따라 움직이는 색 면을 그린다. Width 0은 보이지 않는 연결 뼈다. 키의 Phase는 0 이상 1 미만이며 Degrees는 기본 각도에 더하는 회전이다. 마지막 키와 첫 키 사이도 짧은 회전 방향으로 보간한다. [Runner.rig 전체 예제](../../samples/PlatformerLab/Assets/Runner.rig)

스크립트의 `tick.Objects.TryGetBonePose(id, "FrontFoot", out var foot)`은 현재 물리 틱에서 그 뼈의 월드 좌표(Start, End)를 돌려준다.

발 IK(Foot IK 2D)는 2026-11-26 엔진에서 제거했다(사용자 결정). 경사에서 발을 지면에 맞추던 `feet` 설정과 `BonePose2D`의 Ground·Planted 값도 함께 없앴다. 이전 기록: [Mac 실행 검증](../../engine/tests/results/rig-animation/README.md)
