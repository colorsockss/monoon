# Transform 2D

Hierarchy에서 월드 오브젝트를 선택하고 Inspector의 **Transform**을 펼친다. 편집 중에는 값을 바꾸고 Undo할 수 있다. Play 중에는 로컬 위치·회전·스케일을 조정하고 원하는 필드만 원본 씬에 적용할 수 있다. [Play Inspector 사용법](play-diagnostics.md#play-중-값-조정과-선택-적용)

| 필드 | 의미 |
| --- | --- |
| Position / Local pos. | 부모 좌표계에서 회전·스케일을 적용하기 전의 배치 위치. 루트는 월드 좌표를 사용한다. |
| Size | 스프라이트와 [충돌 도형](collision.md)의 기준 너비와 높이(픽셀). Scale과 별개다. |
| Rotation | 피벗을 중심으로 도는 각도(도). 화면에서 양수는 시계 방향이다. |
| Scale X / Y | 각 로컬 축의 배율. 1은 원본 크기, 음수는 반전이다. 0은 사용할 수 없다. |
| Pivot X / Y | 원본 사각형 안의 회전·스케일 중심. (0, 0)은 좌상단, (0.5, 0.5)는 중앙, (1, 1)은 우하단이다. |

자식의 위치와 축은 부모의 회전·배율·피벗을 따라간다. 자식을 다른 부모로 드래그하면 **보이던 위치와 방향이 유지**되도록 로컬 값이 바뀐다. 부모가 비균일하게 확대되거나 반전된 경우에는 외형 보존에 필요한 기울기(shear)도 씬·프리팹에 저장한다. 화면 UI는 기존 Rect와 앵커로 배치하며, 카메라의 화면 방향은 고정이고 배율은 Camera Zoom으로 설정한다.

스크립트에서는 `tick.LocalToWorld(point)`와 `tick.WorldToLocal(point)`로 위치를 변환한다. 예를 들어 KeyRoom의 상자는 `tick.LocalToWorld(new Vector2(52, -5))`에 코인을 생성하므로, 상자를 돌리면 생성 지점도 따라 돈다. 다른 오브젝트의 현재 변환은 `tick.Objects.TryGetWorldMatrix(id, out var matrix)`로 조회한다.

행렬로 점을 옮기거나 보간할 때는 `Vector2.Transform`, `Vector2.TransformNormal`, `Vector2.Lerp` 대신 `Affine2D.Transform`, `Affine2D.TransformNormal`, `Affine2D.Lerp`를 쓴다. 시뮬레이션 상태(위치, 속도, 충돌 판정)를 바꾸는 스크립트 코드에서는 반드시 이 함수를 쓴다. .NET의 함수는 CPU에 FMA가 있을 때만 곱셈과 덧셈을 합쳐 계산한다. 그래서 x64에서는 Release(JIT)와 Native AOT의 결과가 마지막 자리에서 달라질 수 있다. `Affine2D` 함수는 모든 CPU와 빌드에서 곱셈과 덧셈을 따로 반올림한다. 모든 CPU에서 하드웨어 명령으로 빠르게 돌고 결과도 같다. 엔진 코드도 이 함수를 쓴다. Box2D 같은 네이티브 라이브러리의 결과는 아키텍처마다 다를 수 있다. [x64 결정론 기록](../../engine/tests/results/x64-determinism/README.md)

**따라 해보기:** KeyRoom에서 Chest를 선택해 Rotation을 30, Scale을 (1.5, 0.75), Pivot을 (0.5, 0.5)로 바꾸고 Play한다. Chest Lid의 로컬 회전은 0인데 화면에서는 상자와 함께 기울어진다. Stop 후 Chest Lid를 Wall로 옮겨 보자. 로컬 회전은 30으로 바뀌지만 뚜껑은 같은 위치에 보인다. 원본 예제를 유지하려면 프로젝트 복사본에서 실습한다.

[Mac 에디터의 저장·재열기 검증](../../engine/tests/results/transform-2d/README.md)

## Play 중 변환과 부모 변경

KeyRoom 첫 방에서 **E**로 상자를 열고 **C**로 코인을 든다. 방향키로 걸으면 코인이 플레이어를 따라간다. **C**를 다시 누르면 코인이 그 자리에 같은 각도와 크기로 남는다. 들었을 때와 내려놓았을 때 각각 **F5**로 저장한 뒤 상태를 바꾸고 **F9**를 눌러 보자. 내려놓은 코인에 닿으면 획득한다. 에디터에서 Stop을 누르면 원본 씬의 상자와 계층으로 돌아간다.

스크립트는 `tick.Objects`로 월드 오브젝트의 현재 변환과 부모를 읽고 바꾼다. 아래 예제는 코인을 플레이어의 자식으로 붙이고 손에 들 각도와 크기를 지정한다. 내려놓을 때는 부모를 `null`로 바꾼다.

```csharp
var objects = tick.Objects;
if (objects.SetParent(coin, tick.ObjectId, keepWorld: false) &&
    objects.TryGetLocalTransform(coin, out var local))
{
    objects.SetLocalTransform(coin, local with
    {
        Position = new Vector2(56, -30),
        Rotation = 30,
        Scale = new Vector2(1.5f, 0.75f)
    });
}

// 내려놓기 입력을 받았을 때
objects.SetParent(coin, null, keepWorld: true);
```

| API | 동작 |
| --- | --- |
| `TryGetLocalTransform(id, out local)` | 현재 부모 기준 위치, 시계 방향 각도(도), 두 축의 배율과 기울기를 읽는다. 위치와 피벗은 Inspector와 같은 규칙을 쓴다. |
| `SetLocalTransform(id, local)` | 같은 틱에 자손의 월드 변환과 충돌 도형도 갱신한다. `with`로 한 필드만 바꾸면 나머지 값이 유지된다. |
| `TryGetParent(id, out parent)` | 오브젝트가 있으면 `true`를 반환한다. 씬 루트에 있는 오브젝트의 부모는 `null`이다. |
| `SetParent(id, parent, keepWorld)` | 기본값 `true`는 월드 변환 전체를 보존한다. 새 부모의 축이 반전되거나 비균일하게 확대되면 필요한 로컬 기울기도 계산한다. `false`는 현재 로컬 변환을 새 부모에 그대로 적용한다. |

순환 계층, 다른 씬 소속의 부모, 0배율처럼 역변환할 수 없는 변환을 지정하면 변경 함수는 `false`를 반환하고 기존 상태를 유지한다. 화면 UI 배치는 Rect와 앵커를 사용한다. 게임 저장에 포함할 오브젝트는 `TrackForSave`로 등록한다. [월드 저장](player-data.md#월드와-런타임-프리팹-저장) · [KeyRoom 실행 기록](../../engine/tests/results/runtime-transforms/README.md)
