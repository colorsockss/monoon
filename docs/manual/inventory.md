# 선택형 인벤토리

[InventoryLab](../../samples/InventoryLab/)을 열고 **Play**를 누른다. 왼쪽은 보급품, 오른쪽은 세 칸짜리 가방이다. 채워지지 않은 같은 아이템 묶음부터 보충하고, 남은 수량은 앞쪽의 빈 칸부터 담는다. 전부 들어갈 공간이 있을 때만 획득한다.

| 동작 | 방법 |
| --- | --- |
| 아이템 1개 담기 | 보급품 버튼을 누르거나 **1** 약초, **2** 수정, **3** 열쇠를 누른다. 한 칸에 각각 3개, 2개, 1개까지 쌓인다. |
| 칸 선택·사용 | 보관함의 칸을 클릭하거나 **Q / E**로 이전·다음 칸을 고른다. **U** 또는 **칸 사용** 버튼으로 하나를 꺼낸다. 마지막 한 개를 사용하면 그 칸을 다른 아이템에 쓸 수 있다. |
| 저장·불러오기 | **S**로 슬롯 1에 가방과 의뢰 진행 상황, 열려 있는 약초상 대사를 함께 저장하고 **L**로 불러온다. 다시 실행하면 빈 가방으로 시작한다. 에디터 Play와 독립 게임은 각각의 저장 위치를 사용한다. [약초 수집 의뢰](quests.md) |

약초 네 개, 수정 두 개를 차례로 담으면 세 칸이 **약초 3 · 약초 1 · 수정 2**가 된다. 이때 열쇠는 들어가지 않는다. 가운데 칸의 약초를 사용하고 다시 열쇠를 담으면 그 자리에 열쇠가 들어간다.

## 다른 게임에 가져오기

1. 대상 프로젝트에서 **Project → Import Package…**로 [Inventory.monopackage](../../packages/Inventory.monopackage)를 연다.
2. Project의 **Assets/Inventory/Bag.prefab**을 Scene으로 끌어온다. 또는 오브젝트에 **InventoryContainer** 스크립트를 붙이고 Inspector의 **Catalog**에 **Items.table**, **Slots**에 가방 칸 수를 지정한다. 각 컴포넌트는 독립된 가방을 갖는다.
3. **Items.table**을 더블클릭해 행 키, **label** 표시 이름과 **stackLimit**을 편집한다. 행 키는 스크립트와 저장 데이터에서 쓰는 아이템 ID다. 같은 종류의 아이템은 같은 키를 사용한다.
4. 게임 스크립트에서 아래처럼 **Container** 필드에 가방 오브젝트를 연결한다. **OnEnable**에서는 모든 컴포넌트의 초기화가 끝나 있다. 획득·소비 효과와 화면 구성은 게임에서 정한다.

```csharp
using Engine.Core;
using MoNoon.Inventory;

public sealed class LootBag : GameBehaviour
{
    public ComponentReference<InventoryContainer> Container;
    private InventoryBag bag;

    public override void OnEnable(ScriptLife life) =>
        bag = Container.Resolve(life.Objects).Bag;

    public bool Collect(string id, int count) => bag.TryAdd(id, count);
    public bool Consume(int slot) => bag.TryTake(slot);
}
```

슬롯 인덱스는 0부터 시작한다. **TryAdd**와 **TryTake**는 공간이나 수량이 부족하면 기존 내용 전체를 보존하고 false를 돌려준다. **Capture()** 결과는 **PlayerStorage.SaveSlot**에 저장할 수 있다. **Restore(data)**는 칸 위치와 수량을 복원한다. 가방 용량, 아이템 ID, 묶음 상한이 기존 저장과 맞지 않으면 오류를 반환하므로 게임 규칙을 변경할 때는 저장 마이그레이션을 적용한다.

[아이템 정의 표](../../samples/InventoryLab/Assets/Inventory/Items.table) · [가방 코드](../../samples/InventoryLab/Scripts/Inventory/InventoryBag.cs) · [Mac 실행 기록](../../engine/tests/results/inventory/README.md)
