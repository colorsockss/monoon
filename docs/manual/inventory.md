# 선택형 인벤토리

[InventoryLab](../../samples/InventoryLab/)을 열고 **Play**를 누른다. 시작 씬은 약초 마당(**Scenes/Showcase.scene**)이고 오른쪽 아래가 세 칸짜리 가방이다. 채워지지 않은 같은 아이템 묶음부터 보충하고, 남은 수량은 앞쪽의 빈 칸부터 담는다. 전부 들어갈 공간이 있을 때만 획득하며 담지 못한 약초는 마당에 남는다.

| 동작 | 방법 |
| --- | --- |
| 아이템 담기 | 방향키로 걸어 약초 위를 지난다. 약초는 한 칸에 3개까지 쌓이고, 담을 때마다 약초 위로 `+1`이 떠오른다. 의뢰 보상 수정은 [약초상](quests.md)에게서 받는다. |
| 칸 선택·사용 | **Q / E**로 이전·다음 칸을 고르면 금색 막대가 그 칸 아래로 옮겨 간다. **U**로 하나를 꺼낸다. 마지막 한 개를 사용하면 그 칸을 다른 아이템에 쓸 수 있다. |
| 저장·불러오기 | **F5**(또는 **S**)로 슬롯 1에 가방과 의뢰 진행 상황, 열려 있는 약초상 대사를 함께 저장하고 **F9**(또는 **L**)로 불러온다. 에디터 Play의 **F7**은 저장 파일을 반으로 잘라 저장 중 중단을 흉내 내고, 다음 **F9**은 직전 사본에서 이어간다. 다시 실행하면 빈 가방으로 시작한다. 에디터 Play와 독립 게임은 각각의 저장 위치를 사용한다. [약초 수집 의뢰](quests.md) · [직전 저장 복구](player-data.md#직전-저장으로-복구하기) |
| 큰 가방과 목록 | **C**로 재고 씬(**Scenes/ShowcaseStock.scene**)을 연다. 4×5 가방 칸을 끌어 놓거나 키보드·패드로 옮기고, 장부 목록에서 아이템을 사용한다. **B**로 마당에 돌아온다. [가방 칸 끌어 놓기](game-ui.md#가방-칸-끌어-놓기) · [데이터로 채우는 목록](game-ui.md#데이터로-채우는-목록) |

마당의 약초 네 포기를 모두 주우면 두 칸이 **약초 3 · 약초 1**이 되고, 이어서 받은 의뢰 보상 수정은 세 번째 칸에 들어간다. 첫 칸에서 **U**를 누르면 그 칸의 약초가 2개로 줄어든다. [약초 마당 스크립트](../../samples/InventoryLab/Scripts/Showcase/HerbYard.cs)

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

슬롯 인덱스는 0부터 시작한다. **TryAdd**와 **TryTake**는 공간이나 수량이 부족하면 기존 내용 전체를 보존하고 false를 돌려준다. **Capture()** 결과는 **PlayerStorage.SaveSlot**에 저장할 수 있다. **Restore(data)**는 칸 위치와 수량을 복원한다. 저장 버전, 가방 용량, 아이템 ID, 묶음 상한이 기존 저장과 맞지 않으면 가방을 바꾸지 않고 `InvalidDataException`을 던지므로 게임 규칙을 변경할 때는 저장 마이그레이션을 적용한다.

[아이템 정의 표](../../samples/InventoryLab/Assets/Inventory/Items.table) · [가방 코드](../../samples/InventoryLab/Scripts/Inventory/InventoryBag.cs) · [Mac 실행 기록](../../engine/tests/results/inventory/README.md)
