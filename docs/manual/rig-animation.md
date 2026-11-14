# 트윈, 스켈레탈 애니메이션, 발 IK

[PlatformerLab](../../samples/PlatformerLab)에서 Play를 누르고 오른쪽 방향키나 D로 경사를 올라간다. 청록색 뒷발과 노란 앞발은 번갈아 딛고 발바닥이 경사와 나란히 놓인다. 주황 발판을 처음 밟으면 캐릭터를 태운 채 134px 구간을 왕복하고, [건너편 대화](cutscenes.md)가 시작된다. Enter로 끝까지 읽거나 X로 닫으면 같은 발판을 원래 시점에서 볼 수 있다. Space는 점프, R은 같은 구간의 자동 주행이다.

| Add Component | Inspector에서 설정하는 값 |
| --- | --- |
| Position Tween 2D | Δ X/Y는 시작점에서의 변위, Time (s)는 편도 시간이다. Ease로 가속·감속을 고르고 Repeat에서 Once 또는 PingPong을 고른다. |
| Skeleton Animator 2D | Rig에 프로젝트의 `.rig`를 지정한다. Clock의 Time은 Cycle (s)마다 한 바퀴, Travel은 이동 발판의 운반량을 뺀 로컬 가로 이동 거리 Stride마다 한 바퀴 재생한다. 역방향으로 걸으면 주기도 거꾸로 흐른다. |
| Foot IK 2D | Skeleton Animator와 같은 오브젝트에 붙인다. Probe는 발의 기준 높이에서 위·아래로 탐색할 거리, Lift는 발을 옮길 때의 높이, 레이어 마스크는 발이 디딜 지형이다. |

트윈의 변위는 부모 좌표계를 따른다. 움직이는 충돌체에는 Kinematic 강체를 사용하면 물리가 막힌 구간과 탑승자 운반을 처리한다. 샘플은 Play on start를 끄고 탑승 시 `tick.Objects.PlayTween(Ferry)`로 시작한다. 자동 시작을 켜면 Play와 함께 출발한다. 샘플의 EaseInOut은 양 끝에서 감속하며 편도 2초, 왕복 4초다.

뼈대는 YAML 에셋이다. Project에서 `.rig`를 더블클릭하면 설정된 외부 에디터에서 열 수 있다. `bones`에 부모를 먼저 선언하고 자식의 `parent`에 이름을 쓴다. 각 뼈의 X/Y와 Angle(도)은 부모 기준이고 Length는 뼈의 길이다. Width와 Color는 그 뼈에 따라 움직이는 색 면을 그린다. Width 0은 보이지 않는 연결 뼈다. 키의 Phase는 0 이상 1 미만이며 Degrees는 기본 각도에 더하는 회전이다. 마지막 키와 첫 키 사이도 짧은 회전 방향으로 보간한다. [Runner.rig 전체 예제](../../samples/PlatformerLab/Assets/Runner.rig)

`feet`에는 허벅지·종아리·발 뼈 이름, 발의 기준점 HomeX/Y, 보행 주기 오프셋 Phase와 무릎 굽힘 방향 Bend(1 또는 -1)를 지정한다. 종아리를 허벅지 끝에, 발을 종아리 끝에 붙인다. 샘플에서는 두 발의 Phase를 반 주기 벌려 한 발이 지면에 고정된 동안 다른 발을 옮긴다. 발마다 레이캐스트한 접점과 법선을 사용하므로 경사 높이와 발바닥 각도가 함께 바뀐다. 닿을 수 있는 거리까지만 다리를 펴고, 점프하거나 지면이 끊어지면 해당 다리에 원래 뼈대 포즈를 사용한다. 이 포즈는 캐릭터의 충돌체 크기에 영향을 주지 않는다.

스크립트의 `tick.Objects.TryGetBonePose(id, "FrontFoot", out var foot)`은 현재 물리 틱에서 관절의 월드 좌표(Start, End), 지면 오브젝트(Ground), 발을 디뎠는지(Planted)를 제공한다. [Mac 실행 검증](../../engine/tests/results/rig-animation/README.md)
