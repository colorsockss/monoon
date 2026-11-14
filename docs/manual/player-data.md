# 데이터 에셋과 플레이어 저장

## KeyRoom에서 저장하고 이어 하기

| 조작 | 동작 |
| --- | --- |
| 숫자열 **1**, **2** | 사용할 슬롯을 선택한다. 새 실행의 기본 슬롯은 1이다. |
| **F5** | 현재 방과 열쇠·상자·코인 진행을 선택한 슬롯에 저장한다. |
| **F9** | 선택한 슬롯을 불러와 해당 방의 시작 위치에서 재개한다. |
| **H** 또는 **Controls** 버튼 | 안내를 열거나 닫는다. 이 선택은 두 슬롯에 공통이며 다음 실행에도 적용된다. |

에디터에서 KeyRoom을 열고 Play를 누른다. 시작 위치에서 **E**로 상자를 열고 **F5**를 누른다. 오른쪽으로 이동해 코인을 획득한 후 왼쪽에서 열쇠를 먹고, 오른쪽 문을 지나 두 번째 방으로 간다. **2 → F5**를 누른다. 앱을 다시 열고 **1 → F9**를 누르면 첫 방에 열린 상자와 코인 한 개가 있다. **2 → F9**를 누르면 두 번째 방과 열쇠 표시가 복원된다. Mac 키보드에서 기능 키를 미디어 키로 쓰고 있다면 **Fn**을 함께 누른다.

## 프로젝트 데이터 만들기

1. Project의 **+ → New Data Asset…**에서 이름과 필드의 타입·값을 입력하고 **Save**를 누른다. 현재 열어 둔 Assets 하위 폴더 또는 Assets에 `.data` 파일이 생긴다.
2. 기존 파일은 Project에서 더블클릭해 수정한다. `version`은 1 이상의 정수이고 `values`는 키와 값의 매핑이다. 값에는 문자열, 불리언, 숫자, 목록, 중첩 매핑을 쓸 수 있다. 원문은 **Edit YAML**에서 편집한다. 여러 행이 필요하면 [데이터 테이블](project-assets.md#데이터-에셋과-테이블)을 사용한다.
3. 컴포넌트 스크립트에 `public DataReference Rules;` 필드를 선언한다. Inspector에서 에셋을 선택하고 씬을 저장한다. 스크립트에서 `life.Objects.ReadData(Rules)`를 호출하면 씬에서 읽은 데이터의 복사본을 얻는다. 에셋과 `.meta`를 함께 이동하면 같은 ID로 찾는다.
4. 빌드 시 포함된 씬과 프리팹에서 참조하는 데이터가 배포물에 들어간다. 잘못된 YAML이나 사라진 파일은 내보낼 때 해당 오브젝트에서 오류가 난다.

KeyRoom의 Chest → OpenChest → Rules에 연결된 에셋은 상자를 열 수 있는 월드 거리를 정한다. `chestReach`를 72에서 12로 줄인 뒤 Play하면 시작 위치에서 상자에 더 가까이 가야 열린다. [데이터 에셋](../../samples/KeyRoom/Assets/KeyRoomRules.data) · [사용 스크립트](../../samples/KeyRoom/Scripts/OpenChest.cs)

```yaml
version: 1
values:
  chestReach: 72
```

## 스크립트에서 저장하기

`SessionState.Storage`는 게임별 파일 저장소다. 슬롯 번호는 1~999이며 게임이 사용할 개수를 정한다. `ListSlots()`는 기존 번호를 오름차순으로 돌려준다. `DeleteSlot(n)`은 지정 슬롯을 지운다. 빈 슬롯이나 처음 읽는 설정은 `null`이다. 저장 위치가 없는 임시 씬에서는 `IsAvailable`을 확인한다.

```csharp
var store = tick.Session.Storage;
var progress = new GameData { Version = 1 };
progress.Set("coins", 4);
store.SaveSlot(1, progress);
double coins = store.LoadSlot(1, 1)?.GetNumber("coins") ?? 0;

var settings = store.LoadSettings(1) ?? new GameData();
settings.Set("showControls", true);
store.SaveSettings(settings);
```

이전 스키마는 `LoadSlot(slot, targetVersion, migrate)` 또는 `LoadSettings(targetVersion, migrate)`로 읽는다. 콜백은 원래 버전부터 한 단계씩 호출된다. KeyRoom은 v1의 `hasKey`를 v2의 `key`로 옮기고 상자와 코인 상태를 초기화한다. 변환은 메모리에서 수행하고 명시적으로 저장할 때 원본 파일을 교체한다. 더 높은 버전이나 읽을 수 없는 파일에 대한 덮어쓰기는 오류로 반환한다. 동시 쓰기가 진행 중이면 다른 쓰기 요청에 `IOException`이 발생하며 다시 시도할 수 있다. [KeyRoom 체크포인트 스크립트](../../samples/KeyRoom/Scripts/Checkpoint.cs)

## 파일 위치

기본 루트는 .NET `LocalApplicationData` 아래의 `MoNoon`이다. `MONOON_USER_DATA` 환경 변수로 루트를 바꿀 수 있다. 하위 폴더는 다음과 같다.

| 실행 대상 | 루트 아래의 경로 |
| --- | --- |
| 에디터 Play와 소스 프로젝트의 Save & Run | `Play/<projectId>/` |
| 배포 게임 Release와 Native AOT | `Games/<projectId>/` |

각 폴더에는 `slot-001.save` 같은 슬롯 파일과 `player.settings`가 생긴다. 둘 다 VYaml로 읽고 쓰는 YAML이다. `GameSettings.projectId`는 게임을 업데이트할 때 유지한다. 복사한 프로젝트를 별도 게임으로 배포하려면 새 GUID를 지정한다.

[KeyRoom macOS 검증 기록](../../engine/tests/results/player-data/README.md)
