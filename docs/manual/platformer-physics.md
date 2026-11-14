# 플랫포머 물리

[PlatformerLab](../../samples/PlatformerLab)을 열고 **Play**를 누른다. **→ / D**를 눌러 초록색 경사로를 올라간 뒤 주황색 발판을 밟는다. 발판과 캐릭터가 함께 움직이고, 건너편 카메라에서 [탑승 대화](cutscenes.md)가 시작된다. Enter로 대사를 넘기거나 X로 건너뛴다. **Space**로 발판에서 점프하고 **← / A**로 돌아갈 수 있다. 세 실행 방식에서 같은 경로를 비교하려면 처음부터 **R**을 누른다. 직접 조작할 때와 같은 캐릭터 이동 API를 사용한다.

| Add Component | 설정과 쓰임새 |
| --- | --- |
| **Rigidbody 2D** | **Dynamic**은 중력·충격량·충돌 반응을 받는다. **Kinematic**은 지정한 속도로 움직여 발판을 만든다. **Static**은 고정한다. 고정된 지형에는 강체 없이 일반 콜라이더만 붙여도 된다. |
| **Distance Joint 2D** | **Connect**에서 상대 오브젝트를 고르고 **Length**에 두 오브젝트 중심 사이의 거리를 px로 입력한다. 하나 이상의 물체가 Dynamic이어야 움직인다. **None**이면 제약이 작동하지 않는다. 샘플의 Pendulum은 Anchor에서 64px 거리를 유지한다. |
| **Character Motor 2D** | Dynamic 강체와 고체 콜라이더에 추가한다. **Slope °**는 걸을 수 있는 최대 경사각, **Snap**은 경사면을 내려갈 때 지면을 따라붙는 최대 거리(px)다. 캐릭터는 쓰러지지 않도록 회전을 고정한다. |

**Inspector**에서 강체의 질량(**Mass**), 아래 방향 중력(**Gravity**, px/s²), 반발 계수(**Bounce**, 0–1)를 설정한다. **Speed X/Y**는 초기 속도(px/s), **Spin °/s**는 초기 각속도다. **Freeze rotation**은 충격을 받아도 회전을 막는다. 충돌에는 기존 [콜라이더의 레이어와 마스크](collision.md)가 적용되며, **Is Trigger**가 켜진 도형은 발판이 되지 않는다.

스크립트의 `FixedUpdate`에서 `tick.Objects.Motion.DriveCharacter(id, speed, jumpSpeed)`를 호출한다. 속도 부호로 좌우를 정하고, 점프가 눌린 틱에만 양수인 점프 속도를 전달한다. `TryGetMotion`의 `Grounded`, `Support`, `GroundNormal`로 공중·지면·발판을 구별할 수 있다. PlatformerLab의 움직이는 발판은 [Position Tween 2D](rig-animation.md)로 구동한다. 속도를 직접 지정하려면 `SetVelocity`를 쓸 수 있다. 탑승 캐릭터를 발판의 자식으로 만들 필요가 없다. 동적 물체를 밀 때는 `ApplyImpulse`를 사용한다. 작용점을 월드 좌표로 전달하면 회전도 생긴다.

샘플은 경사 접촉 후 올라간 높이, 발판에 정지한 뒤 100틱 동안 발판이 이동한 거리와 캐릭터의 상대 변위, 진자의 조인트 오차를 측정한다. 성공하면 게임 화면에 **Route complete**가 표시된다. [macOS 실행 기록](../../engine/tests/results/platformer/README.md)에서 Scene Play와 두 배포 빌드의 측정값을 비교할 수 있다.
