# 2D 내비게이션과 AI

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

## 탑다운 그리드 길찾기

SeedLab에서 Play를 누른 뒤 **G**로 **Courier routes**를 연다. 흰색 배달원이 출발점에 있고 금색 마름모가 목적지다. 청록색 점은 계획된 경로를 표시한다.

| 키 | 조작 |
| --- | --- |
| **Space** | 걷기 또는 일시 정지. 도착한 뒤 누르면 같은 조건으로 다시 출발한다. |
| **O** | 가운데 문을 열거나 닫는다. 걸어가는 중에 누르면 현재 칸에서 다시 탐색한다. 배달원이 문 칸을 벗어나면 다시 닫을 수 있다. |
| **V** | 상하좌우 4방향과 대각선을 포함한 8방향을 전환한다. |
| **M** | 갈색 진흙 칸을 모두 마른 진흙 타일로 바꾸거나 되돌린다. 진입 비용은 일반 바닥의 12배에서 1배가 된다. |
| **B** | 북쪽 통로의 다섯 칸에 진흙을 칠하거나 지운다. 걸어가는 중에 누르면 현재 칸에서 다시 탐색한다. |
| **R** | 문과 지형 설정을 유지하고 배달원을 출발점으로 돌려놓는다. |
| **G** | 미로로 돌아간다. |

처음에는 닫힌 문과 진흙을 피해 위쪽 통로로 간다. **O**를 누르면 점들이 가운데 문을 지나는 직선으로 바뀐다. **R**로 같은 출발점에서 조건을 비교할 수 있다. 문을 닫고 **M**으로 진흙 비용을 낮추면 아래쪽 진흙길을 선택한다. 하단의 **Plan**은 마지막으로 계산한 경로의 걸음 수와 총비용이다. 문, 진흙, 바닥의 비용과 문의 충돌은 Route 타일셋에 저장되어 있다. 키를 누르면 스크립트가 해당 셀의 타일만 바꾼다. [지형 비용과 실행 중 셀 바꾸기](tilemaps.md#지형-비용과-실행-중-셀-바꾸기)

### 게임 스크립트에서 경로 요청하기

`GridNavigation`에 직사각형의 너비·높이와 행 우선 순서의 정수 비용을 전달한다. 0은 통행 불가, 1 이상은 그 칸에 들어갈 때의 가중치다. 한 걸음의 비용은 직선 10, 대각선 14에 목적 칸의 가중치를 곱한다. 대각선은 양옆으로 맞닿은 두 칸이 모두 열려 있을 때만 허용한다.

```csharp
using System.Drawing;
using Engine.Core;

var grid = new GridNavigation(3, 2, new[] { 1, 0, 1, 1, 12, 1 });
var start = new Point(0, 0);
var goal = new Point(2, 0);
var detour = grid.FindPath(start, goal, GridDirections.Four);
grid.SetEntryCost(new Point(1, 0), 1);
var shortcut = grid.FindPath(start, goal, GridDirections.Eight);
```

`Cells`는 시작 칸부터 목표 칸까지 순서대로 담는다. 시작과 목표가 같은 통행 가능 칸이면 한 칸과 비용 0을 돌려준다. 경로가 없거나 양 끝이 막혔으면 빈 목록을 돌려준다. 지형이 바뀌면 `SetEntryCost`로 반영하고 NPC의 현재 칸에서 `FindPath`를 다시 호출한다. 타일맵에서 비용을 읽으려면 `tick.Objects.TileNavigation`으로 그리드를 만든다. 이 그리드는 `SetTile`로 바뀐 셀의 비용을 자동으로 반영한다. [SeedLab 스크립트](../../samples/SeedLab/Scripts/CourierRoute.cs) · [플랫폼별 검증](../../engine/tests/results/grid-navigation/README.md)
