# Transform 2D

Hierarchy에서 월드 오브젝트를 선택하고 Inspector의 **Transform**을 펼친다. 편집 중에는 값을 바꾸고 Undo할 수 있으며, Play 중에는 읽기 전용이다.

| 필드 | 의미 |
| --- | --- |
| Position / Local pos. | 부모 좌표계에서 회전·스케일을 적용하기 전의 배치 위치. 루트는 월드 좌표를 사용한다. |
| Size | 스프라이트와 [충돌 도형](collision.md)의 기준 너비와 높이(픽셀). Scale과 별개다. |
| Rotation | 피벗을 중심으로 도는 각도(도). 화면에서 양수는 시계 방향이다. |
| Scale X / Y | 각 로컬 축의 배율. 1은 원본 크기, 음수는 반전이다. 0은 사용할 수 없다. |
| Pivot X / Y | 원본 사각형 안의 회전·스케일 중심. (0, 0)은 좌상단, (0.5, 0.5)는 중앙, (1, 1)은 우하단이다. |

자식의 위치와 축은 부모의 회전·배율·피벗을 따라간다. 자식을 다른 부모로 드래그하면 **보이던 위치와 방향이 유지**되도록 로컬 값이 바뀐다. 부모가 비균일하게 확대되거나 반전된 경우에는 외형 보존에 필요한 기울기(shear)도 씬·프리팹에 저장한다. 화면 UI는 기존 Rect와 앵커로 배치하며, 카메라의 화면 방향은 고정이고 배율은 Camera Zoom으로 설정한다.

스크립트에서는 `tick.LocalToWorld(point)`와 `tick.WorldToLocal(point)`로 위치를 변환한다. 예를 들어 KeyRoom의 상자는 `tick.LocalToWorld(new Vector2(52, -5))`에 코인을 생성하므로, 상자를 돌리면 생성 지점도 따라 돈다. 다른 오브젝트의 현재 변환은 `tick.Objects.TryGetWorldMatrix(id, out var matrix)`로 조회한다.

**따라 해보기:** KeyRoom에서 Chest를 선택해 Rotation을 30, Scale을 (1.5, 0.75), Pivot을 (0.5, 0.5)로 바꾸고 Play한다. Chest Lid의 로컬 회전은 0인데 화면에서는 상자와 함께 기울어진다. Stop 후 Chest Lid를 Wall로 옮겨 보자. 로컬 회전은 30으로 바뀌지만 뚜껑은 같은 위치에 보인다. 원본 예제를 유지하려면 프로젝트 복사본에서 실습한다.

[Mac 에디터의 저장·재열기 검증](../../engine/tests/results/transform-2d/README.md)
