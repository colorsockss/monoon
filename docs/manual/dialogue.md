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

1. 약초 마당에서 약초상 앞으로 걸어가 **Return**(또는 **T**)을 누른다. 대사가 한 글자씩 나타나며 **Space**나 **Return**으로 끝까지 넘긴다. **Ask about the request → Decline for now**를 선택하면 작별 인사를 듣는다. 다시 대화를 시작해 **Ask about the request → Accept the request**를 고르면 수집이 시작된다.
2. **Go look for herbs**로 대화를 끝낸 뒤 마당의 약초 세 포기 위를 걸어 가방에 담는다.
3. 약초상에게 돌아가 **Ask about the reward → Claim the reward**를 고르면 수정 한 개를 받는다. 약초가 부족하거나 가방이 가득 차면 상태 줄에서 이유를 확인하고 다시 시도할 수 있다. 수령 후 다시 요청해도 보상은 한 번만 지급된다.
4. 대화 중에도 **F5**로 슬롯 1에 저장할 수 있다. Play나 앱을 다시 시작하고 **F9**를 누르면 저장한 대사와 선택지, 가방, 퀘스트 진행도와 수령 여부가 함께 복원된다. 대화가 닫힌 상태에서 저장하면 닫힌 채로 이어진다.

선택지는 **1~4**로 고른다. 대화 상자는 선택지를 네 개까지 보여 주고, 대화가 열려 있는 동안 주인공은 멈춘다. 이전 버전의 가방·퀘스트 저장을 열면 대화는 닫힌 상태에서 시작한다. [약초상 대화 에셋](../../samples/InventoryLab/Assets/Dialogue/Herbalist.data) · [Mac 실행 검증](../../engine/tests/results/dialogue/README.md)

## 서식 있는 대사와 글자 단위 표시

약초상의 대사 칸(**Dialogue → Line**)은 **Rich text tags**를 켜고 **Reveal /s**를 45로 둔다. 대사는 초당 45자씩 나타나고, 표시 중에 **Space**나 **Return**을 누르면 남은 글자를 한 번에 보여 준다. 선택지 **1~4**는 표시 중에도 바로 고를 수 있다.

| 노드 | 대사에 쓴 서식 |
| --- | --- |
| `greeting` | `<b>Herbalist</b>`로 이름을 굵게 표시 |
| `offer` | `<icon=herb>` 약초 아이콘, `<color=#9BE27A>` 색, `<size=24><b>three bundles</b></size>` 크기와 굵기 |
| `meadow` | 대사 칸에 번역 키 `herbalist.meadow`를 넣었다. [약초상 번역 테이블](../../samples/InventoryLab/Assets/Text/Herbalist.table)의 `en` 문장이 기본으로 보이고, `ko` 열에는 대사 칸에서 세 줄로 넘어가는 긴 한국어 문장이 있다. 둘 다 색 태그를 포함한다 |
| `paid` | `<color=#C9A0FF><b>crystal</b></color>` |

[HerbYard](../../samples/InventoryLab/Scripts/Showcase/HerbYard.cs)는 `ui.Translate(node.Text, node.Text)`로 대사를 넣는다. 테이블에 있는 키는 현재 언어의 문장으로 바뀌고, 나머지 대사는 적은 그대로 표시된다. InventoryLab은 기본 언어를 `en-US`로 두고 KeyRoom과 같은 나눔고딕을 게임 글꼴로 써서 한국어 번역도 같은 글꼴로 그린다. 대사·테이블·씬과 스크립트 문자열의 글자 모두 플레이어가 SDL_ttf로 TTF에서 바로 그린다. Scene Play도 같은 경로를 쓰므로 세 실행의 화면이 바이트 단위로 같다. 서식 태그의 사용법은 [게임 화면 UI](game-ui.md#서식-있는-텍스트와-글자-단위-표시)에 있다. [Mac 검증 기록](../../engine/tests/results/rich-text/README.md)

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
