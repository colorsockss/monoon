# 플랫포머 내비게이션과 AI

[PlatformerLab](../../samples/PlatformerLab/)의 윗발판에서 분홍색 **Lookout**이 순찰한다. Play를 시작해 잠시 지켜본 뒤 **→ / D**로 경사 중간까지 올라가 멈춘다. HUD가 **Chase**가 되면 적이 발판을 떠나 경사를 내려온다. **← / A**로 경사를 내려가 화면 왼쪽까지 멀어지면 **Return**으로 바뀐다. 적이 윗발판의 출발점에 도착하면 **Patrol**로 돌아가 다시 양 끝을 오간다. **Debug → Colliders**를 켜면 최근 탐색 경로가 파란 선으로 표시된다.

## 걸을 수 있는 영역 만들기

빈 오브젝트에 **Add Component → Navigation Region 2D**를 붙이고 Transform의 위치와 크기로 탐색할 범위를 정한다. 물리는 월드의 아래 방향(+Y)을 사용하며, 영역의 월드 경계 상자 안에서 지면을 찾는다. 걸을 수 있는 지형에는 고체 콜라이더나 Solid 타일이 필요하다.

| Inspector 필드 | 의미 |
| --- | --- |
| Spacing | 가로 방향 경로 지점 간격(px). 낮추면 좁은 지형을 더 세밀하게 샘플링한다. 영역당 가로 최대 1,024열이다. |
| Agent W / H | 통과에 필요한 캐릭터의 너비와 높이(px). 움직이는 캐릭터의 콜라이더 크기에 맞춘다. |
| Slope ° | 걸을 수 있는 최대 경사각. Character Motor 2D 설정에 맞춘다. |
| Mask | 지면과 장애물로 사용할 충돌 레이어. 트리거와 동적 강체는 보행 지형에서 제외된다. |

**FindWalkPath**는 호출 시점의 충돌 도형에서 발밑과 머리 위의 여유 공간을 검사하고, 이어서 걸을 수 있는 지점을 A*로 연결한다. 경사·단차·현재 로드된 타일과 키네마틱 발판의 현재 위치를 반영한다. 시작점이나 목적지가 지면에 붙지 않거나 연결된 길이 없으면 빈 경로를 반환한다. 이 단계는 **보행 경로**를 찾는다. 공중 점프, 사다리, 미래에 도착할 이동 발판에 대한 탑승 예약은 포함하지 않는다.

## 스크립트에서 사용하기

`tick.Objects.Navigation.CanSee(observer, target, radius, blockers)`는 월드 거리와 두 오브젝트 중심 사이의 시야를 검사한다. 벽과 로드된 고체 타일이 시야를 가릴 수 있다. `TryGetFeet(id, out feet)`로 월드 좌표의 발 위치를 얻은 뒤 `FindWalkPath(region, actor, goalFeet)`를 호출한다. 결과는 순서대로 따라갈 발 위치와 지지 오브젝트 ID다. 게임 스크립트가 이동 속도와 행동을 정하고 `tick.Objects.Motion.DriveCharacter`가 실제 충돌을 적용한다.

[Lookout.cs](../../samples/PlatformerLab/Scripts/Lookout.cs)는 발판의 시작점과 오른쪽 끝을 순찰한다. 플레이어가 112px 이내에 보이면 추적을 시작하고, 시야에서 사라진 뒤 0.35초 동안은 **마지막으로 본 위치**를 좇는다. 이후 순찰 시작점으로 복귀한다. 복귀 도중 플레이어를 다시 보면 추적을 재개한다. 목표 위치가 변하거나 0.35초가 지나면 다시 탐색한다. Inspector에서 Lookout의 순찰 폭·속도, 추적 속도·발견 거리와 유예 시간을 바꿀 수 있다.

[이 Mac의 Scene Play·Release·Native AOT 검증 기록](../../engine/tests/results/navigation/README.md)
