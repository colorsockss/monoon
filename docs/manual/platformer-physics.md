# 플랫포머 물리

[PlatformerLab](../../samples/PlatformerLab)을 열고 **Play**를 누른다. **→ / D**를 눌러 초록색 경사로를 올라간 뒤 주황색 발판을 밟는다. 발판과 캐릭터가 함께 움직이고, 건너편 카메라에서 [탑승 대화](cutscenes.md)가 시작된다. Enter로 대사를 넘기거나 X로 건너뛴다. **Space**로 발판에서 점프하고 **← / A**로 돌아갈 수 있다. 세 실행 방식에서 같은 경로를 비교하려면 처음부터 **R**을 누른다. 직접 조작할 때와 같은 캐릭터 이동 API를 사용한다.

발판을 타고 오른쪽 착지대 끝까지 걸어가면 **Joints** 씬으로 넘어간다. 흔들다리를 건너고 도르래 발판 두 개를 점프로 옮겨 탄 뒤 오른쪽 언덕에 오르면 **Crossed the bridge and the pulley**가 표시된다. 에디터에서 `Scenes/Joints.scene`을 직접 열어 Play해도 된다.

강체, 조인트, 마찰·반발은 BRUTAL의 [Box2D](../brutal.md#물리오디오네트워크) 3.1로 계산한다. 1px을 Box2D 길이 단위 1로 쓰고, 고정 틱마다 Box2D 월드를 한 번(서브스텝 4) 진행한다. 같은 입력을 넣으면 Scene Play, Release, Native AOT에서 위치가 같다.

| Add Component | 설정과 쓰임새 |
| --- | --- |
| **Rigidbody 2D** | **Dynamic**은 중력·충격량·충돌 반응을 받는다. **Kinematic**은 지정한 속도로 움직여 발판을 만든다. **Static**은 고정한다. 고정된 지형에는 강체 없이 일반 콜라이더만 붙여도 된다. |
| **Distance Joint 2D** | **Connect**에서 상대 오브젝트를 고르고 **Length**에 두 오브젝트 중심 사이의 거리를 px로 입력한다. **Spring**을 켜면 고정 막대 대신 **Hz**(진동수)와 **Damping**(감쇠비)의 스프링으로 늘었다 줄어든다. 샘플의 Pendulum은 Anchor에서 64px 거리를 유지한다. |
| **Revolute Joint 2D** | 두 물체를 한 점에서 돌게 묶는다. **Anchor X/Y**는 이 오브젝트 중심에서 회전축까지의 로컬 px다. **Angle limit**을 켜면 상대 물체에 대한 이 오브젝트의 각도를 **Lower °**–**Upper °**(화면에서 시계 방향이 +, −180–180)로 제한한다. **Motor**를 켜면 **Speed °/s**로 돌리되 **Torque**까지만 힘을 쓴다. |
| **Slider Joint 2D** | 시작 자세에서 **Axis X/Y** 방향(월드)으로만 움직이게 한다. **Limit**은 시작 위치에서 축을 따라 잰 **Lower**–**Upper** px, **Motor**는 **Speed**(px/s)와 최대 **Force**, **Spring**은 시작 위치로 되돌리는 스프링이다. |
| **Weld Joint 2D** | 두 물체를 시작 자세로 붙인다. **Linear Hz**·**Angular Hz**가 0이면 단단하고, 0보다 크면 그 진동수의 스프링처럼 휘청인다. |
| **Character Motor 2D** | Dynamic 강체와 고체 콜라이더에 추가한다. **Slope °**는 걸을 수 있는 최대 경사각, **Snap**은 경사면을 내려갈 때 지면을 따라붙는 최대 거리(px)다. 캐릭터는 쓰러지지 않도록 회전을 고정한다. |

조인트의 **Connect**가 **None**이면 월드에 고정된다. 조인트로 이어진 두 물체끼리는 충돌하지 않는다. 한 오브젝트에는 종류별로 조인트를 하나씩 붙일 수 있으므로, 사슬의 마지막 고리처럼 두 곳에 묶어야 하면 상대쪽 오브젝트에 조인트를 붙인다.

**Inspector**에서 강체의 질량(**Mass**), 아래 방향 중력(**Gravity**, px/s²), 반발 계수(**Bounce**, 0–1)를 설정한다. **Friction**(0–100, 기본 0.6)은 이 강체 콜라이더의 마찰 계수다. 맞닿은 두 면은 `√(a·b)`로, 반발은 큰 쪽 값으로 합친다. 강체가 없는 콜라이더는 마찰 0.6, 반발 0이다. 얼음 바닥처럼 지형의 마찰을 바꾸려면 Static 강체를 붙이고 Friction을 낮춘다. **Speed X/Y**는 초기 속도(px/s), **Spin °/s**는 초기 각속도다. **Freeze rotation**은 충격을 받아도 회전을 막는다. 충돌에는 기존 [콜라이더의 레이어와 마스크](collision.md)가 적용되며, **Is Trigger**가 켜진 도형은 발판이 되지 않는다.

스크립트의 `FixedUpdate`에서 `tick.Objects.Motion.DriveCharacter(id, speed, jumpSpeed)`를 호출한다. 속도 부호로 좌우를 정하고, 점프가 눌린 틱에만 양수인 점프 속도를 전달한다. `TryGetMotion`의 `Grounded`, `Support`, `GroundNormal`로 공중·지면·발판을 구별할 수 있다. PlatformerLab의 움직이는 발판은 [Position Tween 2D](rig-animation.md)나 [Path Follower 2D](#곡선-경로를-따라가는-발판)로 구동한다. 속도를 직접 지정하려면 `SetVelocity`를 쓸 수 있다. 탑승 캐릭터를 발판의 자식으로 만들 필요가 없다. 동적 물체를 밀 때는 `ApplyImpulse`를 사용한다. 작용점을 월드 좌표로 전달하면 회전도 생긴다.

## 캐릭터와 Box2D 물체

Character Motor는 Box2D가 플랫포머 캐릭터에 권하는 방식처럼 직접 움직이는 컨트롤러다. Box2D 형상 캐스트로 벽과 바닥을 찾아 경사를 걷고, 높이 1px 이하의 턱(조인트로 이은 판자와 언덕이 만나는 모서리 등)은 앞이 비어 있으면 넘어간다. Box2D 월드에는 키네마틱 물체로 들어가 걸어 들어온 동적 물체를 밀고, 떨어지는 상자를 막는다. 서 있는 동적 물체에는 `Mass × Gravity`의 무게를 발밑에 가하고, 그 물체가 움직인 만큼 캐릭터를 함께 옮긴다. 키네마틱 형상은 발바닥에서 2px 위까지만 두어 무게 외의 힘으로 발판을 누르지 않는다.

## 캡슐 캐릭터와 체인 경사

**Slopes** 씬(`Scenes/Slopes.scene`, 에디터에서 직접 열어 Play)은 8px 타일을 이어 깐 바닥과 Edge Collider 2D로 만든 경사·언덕을 둔다. 캐릭터는 [Capsule Collider 2D](collision.md)와 Character Motor 2D를 쓴다. **→ / D**로 타일 이음새를 지나 14° 경사를 올라가고 **← / A**로 돌아온다. 걷는 중 지면에 선 채 한 틱에 반 걸음도 못 가면 걸림으로 센다. 왕복을 마치면 **Round trip without a snag**가 표시된다. 오른쪽의 회색 캡슐(Dynamic 강체)은 Box2D 체인 위에 떨어져 서고, 경사 중간의 노란 캡슐 트리거를 지날 때마다 [진입 이벤트](collision.md#진입유지이탈-이벤트)가 온다. [SlopeRunner](../../samples/PlatformerLab/Scripts/SlopeRunner.cs)

캐릭터의 키네마틱 Box2D 캡슐도 다각형처럼 발바닥에서 2px 위까지만 둔다. 같은 입력에서 Scene Play·Release·Native AOT의 캐릭터·캡슐 위치 기록이 같다. [Mac 실행 검증](../../engine/tests/results/colliders-capsule-chain/README.md)

## 흔들다리와 도르래 발판

Joints 씬의 다리는 24×6px 판자 8개를 Revolute Joint 2D로 이은 것이다. 각 판자는 왼쪽 위 모서리(Anchor −12, −3)에서 앞 판자와 묶이고, 첫 판자는 Start ledge에, 마지막 판자는 Far ledge가 가진 조인트에 묶인다. 판자 사슬(192px)이 두 언덕 사이(190px)보다 조금 길어 처지고, 캐릭터의 무게로 더 휜다. 판자 위쪽 모서리가 회전축이므로 이웃 판자의 윗면이 이어진다.

Box2D 3에는 도르래 조인트가 없다. 도르래 발판 두 개는 각각 Slider Joint 2D(축 0, 1, 한계 ±16px)로 위아래로만 움직이고 중력을 0으로 둔다(평형추가 무게를 맞춘 상태). [PulleyPair](../../samples/PlatformerLab/Scripts/PulleyPair.cs) 스크립트가 매 고정 틱 물리 계산 전에 두 발판의 세로 속도를 평균 내어 한쪽이 내려가는 만큼 다른 쪽을 올린다. 반올림으로 줄 길이가 어긋나면 초당 4배의 속도로 되돌린다. 질량이 같은 이상적인 도르래처럼, 캐릭터가 한쪽에 올라서면 무게의 절반 가속도로 그 발판이 내려가고 반대쪽이 올라간다.

샘플은 경사 접촉 후 올라간 높이, 발판에 정지한 뒤 100틱 동안 발판이 이동한 거리와 캐릭터의 상대 변위, 진자의 조인트 오차를 측정한다. 성공하면 게임 화면에 **Route complete**가 표시된다. [macOS 실행 기록](../../engine/tests/results/platformer/README.md)에서 Scene Play와 두 배포 빌드의 측정값을 비교할 수 있다. Joints 씬의 세 실행 경로 위치 기록 비교는 [Box2D 전환 결과](../../engine/tests/results/physics-box2d/README.md)에 있다.

## 곡선 경로를 따라가는 발판

Joints 씬의 언덕 오른쪽 끝으로 걸어가면 Paths 씬이 열린다. 주황 발판에 올라서면 발판이 기둥 위로 휘어진 경로를 따라 초당 90px로 이동해 건너편 언덕 높이에 멈추고, 다시 돌아간다. 발판 위에 가만히 서 있으면 캐릭터가 함께 옮겨진다. [Paths 씬](../../samples/PlatformerLab/Scenes/Paths.scene) · [PathRider](../../samples/PlatformerLab/Scripts/PathRider.cs)

**Add Component → Path Follower 2D**를 붙이고 Scene에서 점과 접선 핸들을 편집한다([점 편집](sprites.md#scene에서-점-편집하기)). 점은 오브젝트의 시작 위치에서의 변위(부모 좌표계)이고 첫 점이 시작 위치다. 첫 점의 위치는 고정되어 있으므로 경로 전체를 옮기려면 오브젝트를 옮긴다. **Speed**는 경로를 따라가는 속도(px/s)다. 곡선을 잘게 나눈 길이를 기준으로 위치를 찾으므로 곡선 구간에서도 틱마다 같은 거리를 이동한다. **Repeat**의 Once는 끝에서 멈추고, PingPong은 왕복하며, Loop는 마지막 점에서 첫 점으로 닫힌 경로를 돈다.

움직이는 충돌체에는 Position Tween 2D와 같이 **Kinematic** 강체를 함께 붙인다. 발판의 변위는 키네마틱 바디의 이동으로 Box2D 월드에 전달되고 Character Motor가 그 위의 캐릭터를 운반한다. 한 오브젝트에 트윈과 경로를 함께 둘 수 없다. **Play on start**를 끄면 `tick.Objects.PlayTween(id)`로 출발시킨다(트윈과 같은 호출). 샘플은 캐릭터가 처음 발판을 디딜 때 출발시킨다. 같은 입력에서 Scene Play·Release·Native AOT의 발판·캐릭터 위치 기록과 화면이 같은지는 [Mac 실행 검증](../../engine/tests/results/lines-paths/README.md)에 있다.
