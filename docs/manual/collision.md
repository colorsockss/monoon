# 충돌 도형과 공간 질의

게임 오브젝트를 선택하고 **Inspector → Add Component → Built-in components**에서 콜라이더 하나를 붙인다. 복합 도형은 부모 아래에 콜라이더를 가진 자식을 여러 개 둔다. 부모를 이동하면 자식 도형들도 함께 벽에 막힌다.

| 도형 | 크기와 편집 |
| --- | --- |
| Box Collider 2D | Transform 사각형을 사용한다. 부모를 포함한 회전·스케일·반전·기울임을 따른다. |
| Circle Collider 2D | 중심은 Transform 사각형의 중앙이다. **Radius**는 로컬 픽셀 단위다. 0이면 짧은 변의 절반이다. 비균일 스케일·기울임에서도 원을 유지하고 짧은 쪽의 월드 배율을 따른다. |
| Polygon Collider 2D | **Points**에 둘레 순서로 꼭짓점 3–32개를 입력한다. 예: 삼각형은 `0,0; 1,0.5; 0,1`. X·Y의 1은 각각 Transform의 너비·높이다. 소수점은 점을 쓴다. Enter 또는 포커스 이동으로 적용한다. 오목하거나 자기 교차·중복·일직선 꼭짓점이 있으면 이전 도형을 유지한다. |

**Layer**는 도형이 속한 번호(0–31), **Mask**는 이 도형이 반응할 상대 레이어 집합이다. Mask 버튼에서 All layers, None 또는 개별 레이어를 고른다. 두 도형의 마스크가 서로의 레이어를 포함해야 이동을 막거나 트리거 이벤트가 발생한다. 기존 씬은 레이어 0, 모든 레이어 허용으로 열린다. **Is Trigger**는 이동을 막지 않으며, 한 틱 안에 가로질러도 내부를 지나면 트리거 쪽의 `OnTriggerEnter`가 호출된다. 경계만 스치는 경우는 진입으로 세지 않는다.

Tilemap 2D의 **Physics / Mask**는 Solid 셀의 충돌 필터다. **Layer / Order**는 그림 순서다. 직교·아이소메트릭·헥사고날 타일은 격자 외곽선으로 판정하고, 장식 셀은 질의에도 잡히지 않는다.

## 스크립트에서 탐색하기

`tick.Objects.Physics`는 해당 플레이 월드의 활성 콜라이더와 Solid 타일을 조회한다. 좌표·거리·이동량은 모두 월드 픽셀 단위다. 다음 예에서 `center`는 캐릭터 중심이다.

```csharp
var physics = tick.Objects.Physics;
var walls = new PhysicsFilter(1u << 2, Ignore: tick.ObjectId);
var sensors = new PhysicsFilter(1u << 3, IncludeTriggers: true);
var circle = Shape2D.Circle(center, 20);

var nearby = physics.Overlap(circle, sensors);
var sight = physics.Raycast(center, Vector2.UnitX, 800, walls);
var move = physics.Sweep(circle, new Vector2(800, 0), walls);
if (move is {} hit) {
    var target = hit.Target.ObjectId;
    var distance = hit.Distance;
    var contact = hit.Point;
    var normal = hit.Normal;
}
```

- **Overlap**은 내부가 겹치는 모든 대상을 반환한다. 박스·원·볼록 다각형을 `Shape2D.Box(RectangleF)`, `Circle`, `Polygon`으로 만들 수 있다.
- **Raycast**는 방향(정규화 불필요)과 최대 거리, **Sweep**은 도형과 이동 벡터를 받는다. 가장 가까운 대상이 없으면 `null`이다. 거리 0에서 이미 겹치거나 접촉 중인 경우도 반환한다. 끝점 접촉을 포함한다.
- 결과에는 소유 객체 ID, 레이어, 트리거 여부, 타일이면 셀 좌표가 있다. 캐스트는 추가로 이동 비율(0–1), 거리, 대상 위 접점과 바깥쪽 법선을 반환한다. 같은 거리이면 ID, 같은 맵에서는 셀 Y·X 순으로 결정한다.
- 질의 필터는 **대상의 레이어**만 검사한다. 대상 자신의 이동 마스크가 0이어도 탐색할 수 있다. 기본은 모든 레이어·트리거 제외다. `Ignore`는 지정한 오브젝트 하나를 제외한다.
- 스윕과 `tick.Move`는 평행 이동을 연속 검사한다. 스윕은 첫 접촉을 보고하고, 이동은 남은 거리로 벽을 따라 미끄러질 수 있다. 회전 스윕·강체 동역학은 이 단계의 기능에 포함되지 않는다.

[CollisionLab 프로젝트](../../samples/CollisionLab)에서 Play 후 **→ 또는 D**를 한 번 누르면 원이 센서와 두 마스크 게이트를 통과해 삼각형에 멈춘다. 한 게이트는 원의 마스크에서 제외됐고 다른 게이트는 자신의 마스크가 0이다. HUD가 레이·스윕 거리, 실제 정지 위치와 진입 여부를 표시한다. [Mac 실행 결과](../../engine/tests/results/collision/README.md)

Play 중 실제 도형과 질의를 보려면 [Scene 실행 진단](play-diagnostics.md#충돌체와-질의)을 켠다.
