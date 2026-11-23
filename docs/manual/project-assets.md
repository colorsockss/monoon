# Project에서 에셋 찾기와 편집

Project 도구 줄의 검색 필드(**Search project**)는 현재 폴더와 관계없이 프로젝트의 에셋, Scripts 아래 C# 파일, GameSettings를 찾는다. 이름이나 경로 일부를 입력한다. 여러 단어는 모두 일치해야 하며 대소문자는 구분하지 않는다. 예를 들어 `door t:sprite`는 Door 이미지, `t:script chest`는 OpenChest.cs, `t:table`은 데이터 테이블을 찾는다. 검색 결과 두 번째 줄은 폴더다. Enter로 첫 결과를 열고 Esc로 검색을 지운다.

## 임포트와 복구

**Project → + → Import Assets…**에서 파일을 하나 또는 여러 개 선택한다. PNG, WAV, Ogg Vorbis, 씬, 프리팹, 클립, 애니메이터, 타일셋, 아틀라스, 데이터 에셋, 테이블, C# 스크립트를 지원한다. 씬은 Scenes, 스크립트는 Scripts, 나머지는 Assets로 가져온다. 해당 범주의 하위 폴더가 열려 있으면 그곳에 넣는다. 기존 파일은 덮어쓰지 않는다.

다른 MoNoon 프로젝트에서 연결된 에셋을 가져올 때는 원본 옆에 `.meta`를 둔 채 **에셋 파일들만** 함께 선택한다. 임포터가 ID와 시트 정보를 읽는다. 파일이 사라졌다면 원래 이름·위치로 복원한다. 충돌하거나 읽을 수 없는 파일이 선택에 섞여 있으면 나머지도 복사하지 않는다.

## 의존성과 누락된 참조

파일을 우클릭하고 **Asset References…**를 연다. **Dependencies**는 파일이 직접 쓰는 대상, **Used by**는 그 파일을 직접 사용하는 곳이다. 결과에는 파일과 객체·필드 또는 프레임 번호가 나온다. 결과를 더블클릭하면 그 파일의 의존성으로 이동한다. **Show in Project**는 파일을 Project 패널에서 선택한다.

**Project → + → Check Missing References…**는 시작 씬에서 사용하지 않는 에셋까지 포함하여 **디스크에 저장된 프로젝트**를 검사한다. 씬이나 프리팹을 수정했다면 저장하고, 파일을 복원했다면 **Rescan**한다. 없어진 파일, 알 수 없는 ID, 참조 타입 불일치, 사라진 오브젝트·스크립트와 읽을 수 없는 문서를 표시한다. 코드가 실행 중에 문자열로 만드는 경로는 검사 대상이 아니다.

KeyRoom에서는 **Coin.prefab → Dependencies**에서 `CollectCoin.LootTable`이 [Loot.table](../../samples/KeyRoom/Assets/Loot.table)을 사용한다. 반대로 [KeyRoomRules.data](../../samples/KeyRoom/Assets/KeyRoomRules.data)의 **Used by**에서는 `Chest / OpenChest.Rules`을 확인할 수 있다.

## 에셋 이동과 이름 변경

Project에서 파일이나 폴더를 우클릭해 **Rename…** 또는 **Move to Folder…**를 선택한다. F2로 이름을 바꾸거나, 폴더 행에 드래그해 옮길 수도 있다. 상위 폴더로 옮길 때는 ↑ 버튼에 드롭한다. 이동 대화상자의 `.`은 프로젝트 루트다.

문 PNG를 옮기면 원본과 `.meta`가 함께 이동한다. 씬·프리팹의 스프라이트 경로가 바뀌고 클립과 스크립트의 PNG 참조는 같은 ID를 사용한다. C# 파일이나 `Scripts` 폴더 전체를 옮기면 각 C# 파일의 기존 스크립트 ID가 `.cs.meta`에 보존된다. Inspector의 **Edit Script**는 옮긴 파일을 열고 **Asset References…**는 새 경로를 표시한다. C# 파일 이름을 바꿔도 클래스와 기존 컴포넌트 ID는 유지된다.

변경은 디스크에 즉시 적용된다. 한 번의 **Cmd+Z**(Windows·Linux는 **Ctrl+Z**)로 파일·메타·참조를 함께 되돌린다. 씬 편집 내용을 저장하려면 **Save Scene**을 사용한다. [문 스프라이트와 Scripts 폴더로 확인한 결과](../../engine/tests/results/project-moves/README.md)

## 데이터 에셋과 테이블

| 편집할 대상 | 조작 |
| --- | --- |
| 단일 설정 모음 | **+ → New Data Asset…** 또는 기존 `.data` 더블클릭. **+ Field**로 키를 추가하고 Text, Number, Boolean, YAML 중 타입을 고른다. 목록·중첩 매핑은 YAML 필드로 편집한다. **Edit YAML**은 문서 전체의 원문 편집으로 전환한다. |
| 같은 구조의 여러 레코드 | **+ → New Data Table…** 또는 기존 `.table` 더블클릭. **+ Column**으로 이름과 타입(Text, Number, Boolean)을 정하고 **+ Row**로 행을 추가한다. 첫 칸은 중복되지 않는 행 키다. 열 이름을 바꾸더라도 셀은 해당 열에 그대로 남는다. |
| 저장 또는 취소 | **Save**는 씬과 별도로 해당 에셋을 YAML로 저장한다. 중복 키, 타입이 맞지 않는 값, 편집 중 외부에서 바뀐 파일은 덮어쓰지 않는다. **Cancel**은 변경을 버린다. 열이 많으면 가로로 스크롤한다. |

스크립트에서 테이블을 쓰려면 `public TableReference LootTable;`을 선언한 뒤 Inspector에서 에셋을 지정한다. 예를 들어 `life.Objects.ReadTable(LootTable).Row("coin").GetText("flag")`는 해당 행의 문자열을 읽는다. 행 키를 바꾸면 이를 사용하는 코드의 키도 바꾼다. 포함된 씬이나 프리팹에서 이 필드를 통해 참조한 테이블은 게임 빌드에 함께 들어간다.

[KeyRoom 검증 화면](../../engine/tests/results/project-assets/README.md) · [데이터 에셋과 플레이어 저장](player-data.md)

## 외부 PNG 자동 재임포트

Project에서 PNG를 선택하면 목록 아래에 이미지가 표시된다. 외부 이미지 편집기로 프로젝트의 같은 경로에 저장하면 선택한 미리보기, 그 이미지를 쓰는 Scene 스프라이트, 열어 둔 애니메이션 클립이 갱신된다. 창이 포커스를 되찾거나 프로젝트를 다시 열 때도 디스크의 내용을 확인한다.

**KeyRoom에서 확인하기**

1. Project에서 `Door.clip`을 열고 Clip 패널에서 첫 프레임을 선택한다. Project에서 `Door.png`를 선택한다.
2. 외부 편집기에서 `Assets/Door.png`의 문 색을 바꾸고 같은 경로에 저장한다. Scene의 닫힌 문, Project 미리보기, 클립 첫 프레임과 타임라인 썸네일의 색이 바뀐다.
3. Clip 패널을 다시 선택하거나 프로젝트를 닫았다가 연다. 네 프레임의 시트 GUID, 셀 사각형과 재생 시간은 그대로다. 클립을 저장할 필요는 없다.

`.aseprite` 원본도 같은 방식으로 감시한다. 저장하면 생성된 시트와 클립이 갱신된다. [Aseprite 파일 가져오기](sprites.md#aseprite-파일-가져오기)

KeyRoom의 `DoorSheet.png.meta`에는 각 셀에 사용할 원본 PNG의 GUID와 마지막으로 가져온 내용 해시가 연결되어 있다. 원본이 바뀌면 해당 셀의 픽셀만 다시 쓰고, 프레임 순서와 시간은 클립에 남겨 둔다. 이미 잘라낸 셀 바깥까지 그림을 늘리거나 원본 캔버스 크기를 바꾸면 에디터가 맞지 않는 셀을 알려 준다. 올바른 크기로 다시 저장하면 기존 배치로 갱신된다. [시트 셀 사용법](sprites.md#시트의-일부만-프레임으로-쓰기) · [에디터 검증 기록](../../engine/tests/results/external-sprite-reimport/README.md)
