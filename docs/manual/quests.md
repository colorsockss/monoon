# 수집 퀘스트

[InventoryLab](../../samples/InventoryLab/)에서 **Play**를 누른다. 가방 아래 의뢰 행에 목표 수량과 보상이 보인다. **T**로 약초상과 대화해서 의뢰를 받거나 보상을 수령할 수도 있다. [대사와 선택지 편집](dialogue.md)

| 조작 | 결과 |
| --- | --- |
| **의뢰 받기** 또는 **J** | 약초 수집 의뢰를 수락한다. 이때부터 가방에 실제로 담은 약초를 센다. 이미 가진 약초, 다른 아이템, 가방이 가득 차서 거절된 획득은 집계에서 제외한다. |
| **약초 담기** 또는 **1** | 진행도가 0/3에서 3/3까지 올라간다. 도중에 약초를 사용해도 수집 기록은 유지된다. |
| **보상 받기** 또는 **J** | 3/3에서 수정 한 개를 받는다. 약초는 가방에 남는다. 공간이 부족하면 칸을 비우고 다시 받을 수 있다. 성공하면 버튼이 **보상 수령 완료**로 바뀐다. |
| **S / L** | S로 가방·진행도·수령 여부를 슬롯 1에 함께 저장한다. 에디터 Play나 게임을 다시 시작한 뒤 L로 복원한다. 수령까지 보존하려면 보상을 받은 뒤 S를 누른다. 기존 인벤토리 저장을 열면 가방을 복원하고 의뢰는 미수락으로 시작한다. |

## 프로젝트에 추가하기

1. **Project → Import Package…**에서 [Quests.monopackage](../../packages/Quests.monopackage)를 선택하고 **Assets/Quests/Journal.prefab**을 Scene에 배치한다. 이 패키지는 독립적으로 가져올 수 있다. InventoryLab은 인벤토리 패키지의 가방을 보상 지급에 사용한다.
2. [Collection.table](../../samples/InventoryLab/Assets/Quests/Collection.table)을 더블클릭한다. 행 하나가 의뢰 하나다. 행 키는 저장에 쓸 고정 ID다. **title**은 의뢰 이름, **item / goal**은 수집할 아이템과 양, **reward / rewardCount**는 보상 아이템과 양이다.
3. 게임 스크립트에 `public ComponentReference<QuestLog> Quests;`를 선언하고 Inspector에서 프리팹 인스턴스를 지정한다. `using MoNoon.Quests;`를 추가한 뒤 **OnEnable**에서 `Quests.Resolve(life.Objects).Journal`로 일지를 얻는다.

```csharp
var quest = journal["herbalist"];
quest.TryAccept();

if (bag.TryAdd("herb", 1))
    journal.RecordCollection("herb", 1);

// 가방에 보상 전량이 들어가야 수령 완료가 된다.
quest.TryClaim(bag.TryAdd);
```

**Collected**, **Goal**, **Stage**로 UI를 갱신한다. 여러 의뢰가 수락된 상태면 같은 아이템을 기다리는 의뢰들이 함께 진행된다. `TryClaim`에 넘기는 함수는 보상 전량을 지급하면 true, 지급할 수 없으면 상태를 보존하고 false를 반환해야 한다.

**journal.Capture()**는 수락한 의뢰별 수집량과 수령 여부를 담는다. 게임의 `GameData.Values`에 넣어 보상 대상 데이터와 함께 저장하고, 읽을 때 **journal.Restore(...)**로 복원한다. InventoryLab의 v3 슬롯은 하나의 YAML 파일에 가방과 일지, 현재 대사의 노드 ID를 담는다. v2 슬롯에 저장된 의뢰도 이어서 진행할 수 있다. [샘플 저장 코드](../../samples/InventoryLab/Scripts/WorkbenchSave.cs) · [Mac 실행 기록](../../engine/tests/results/quests/README.md)
