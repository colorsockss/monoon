# 대화 편집과 약초상

[InventoryLab](../../samples/InventoryLab/)의 **Project**에서 **Herbalist.data**를 검색해 더블클릭한다. 새 대화는 Project 패널의 **+ → New Dialogue…**에서 만든다. 한 노드는 화자가 말하는 대사 하나와 플레이어가 고를 수 있는 선택지들로 구성된다.

| 편집 항목 | 사용법 |
| --- | --- |
| **Node / + Node** | 편집할 노드를 고르거나 새 노드를 추가한다. **Set as start**로 첫 대사를 정한다. |
| **ID / Rename** | 노드 ID를 바꾼다. 같은 에셋 안에서 이 노드를 가리키는 선택지와 시작점도 함께 바뀐다. 배포한 게임의 저장 슬롯은 노드 ID를 사용하므로 이름을 바꿀 때 기존 저장의 ID도 이전해야 한다. |
| **Speaker / Line** | 화자 이름과 대사를 입력한다. 대사에는 줄바꿈을 넣을 수 있다. |
| **+ Choice / Remove** | 선택지를 추가하거나 제거한다. **Choice**는 화면에 표시할 문구이고 **Next**는 선택 후 표시할 노드다. **(End dialogue)**를 고르면 대화를 닫는다. |
| **Action** | 선택할 때 연결할 게임 행동의 ID다. 비워 두면 바로 다음 노드로 간다. InventoryLab은 의뢰 수락에 `accept`, 보상 수령에 `claim`을 사용한다. |
| **Save / Cancel** | Save는 YAML 데이터 에셋을 기록하고 Cancel은 편집을 취소한다. 빈 대사·선택지나 연결되지 않은 목적지는 저장 전에 확인한다. 저장 후 에디터의 Undo로 변경을 되돌릴 수 있다. |

## InventoryLab에서 플레이하기

1. **T**를 누르거나 **약초상과 대화**를 클릭한다. **의뢰를 알아본다 → 이번에는 사양한다**를 선택하면 작별 인사를 듣는다. 다시 대화를 시작해 **의뢰를 알아본다 → 의뢰를 받는다**를 고르면 수집이 시작된다.
2. **약초를 찾으러 간다**로 대화를 끝낸 뒤 가방 화면에서 **1**을 세 번 눌러 약초 세 개를 담는다.
3. **T → 보상을 문의한다 → 보상을 받는다**를 고르면 수정 한 개를 받는다. 약초가 부족하거나 가방이 가득 차면 같은 대사에서 이유를 확인하고 다시 시도할 수 있다. 수령 후 다시 요청해도 보상은 한 번만 지급된다.
4. 대화 중에도 **S**로 슬롯 1에 저장할 수 있다. Play나 앱을 다시 시작하고 **L**을 누르면 저장한 대사와 선택지, 가방, 퀘스트 진행도와 수령 여부가 함께 복원된다. 가방 화면에서 저장하면 그 화면에서 이어진다.

선택지는 클릭하거나 **1~3**으로 고른다. 선택지가 네 개 이상인 노드는 **E** 또는 **다음 선택지**로 다음 세 개를 표시한다. **T**로 언제든 대화를 닫을 수 있다. 이전 버전의 가방·퀘스트 저장을 열면 대화는 닫힌 상태에서 시작한다. [약초상 대화 에셋](../../samples/InventoryLab/Assets/Dialogue/Herbalist.data) · [Mac 실행 검증](../../engine/tests/results/dialogue/README.md)

## 다른 게임에 가져오기

**Project → Import Package…**로 [Dialogue.monopackage](../../packages/Dialogue.monopackage)를 가져온다. **Assets/Dialogue/Herbalist.prefab**을 Scene에 놓거나 오브젝트에 **DialoguePlayer**를 붙이고 Inspector의 **Story**에 대화 데이터 에셋을 지정한다. 기본 패키지에는 예제 대사와 대화 재생기가 들어 있다. 선택지에 적힌 행동은 가져온 프로젝트의 게임 규칙에 연결한다.

게임 스크립트의 `ComponentReference<DialoguePlayer>`에서 `OnEnable`에 `Resolve(life.Objects).Session`을 얻는다. `Begin()`은 시작 대사를 열고 `Current`는 현재 화자·대사·선택지를 제공한다. `Choose(index, tryAction)`의 인덱스는 0부터 시작한다. 행동 콜백이 성공하면 다음 대사로 이동하고, 실패하면 현재 선택지를 유지한다.

```csharp
talk.Choose(index, action => action switch
{
    "accept" => quest.TryAccept(),
    "claim" => quest.TryClaim(bag.TryAdd),
    _ => false
});
```

`Position`을 게임 저장 데이터에 함께 기록하고 `Restore(position)`으로 복원한다. 빈 문자열은 닫힌 대화다. 복원은 해당 대사를 다시 표시하며 이전 선택지의 행동을 재실행하지 않는다. [대화 재생 코드](../../samples/InventoryLab/Scripts/Dialogue/DialoguePlayer.cs) · [샘플 저장 코드](../../samples/InventoryLab/Scripts/WorkbenchSave.cs)
