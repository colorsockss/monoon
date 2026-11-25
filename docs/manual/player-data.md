# 데이터 에셋과 플레이어 저장

## KeyRoom에서 저장하고 이어 하기

| 조작 | 동작 |
| --- | --- |
| 숫자열 **1**, **2** | 사용할 슬롯을 선택한다. 새 실행의 기본 슬롯은 1이다. |
| **N** | 상자에서 나온 코인을 한 번에 오른쪽으로 16픽셀 옮긴다. |
| **C** | 가까운 코인을 플레이어에게 붙이거나 들고 있던 코인을 현재 위치에 내려놓는다. |
| **F5** | 현재 방과 진행 플래그, 열린 상자와 남은 코인의 상태를 선택한 슬롯에 저장한다. |
| **F9** | 선택한 슬롯의 방을 불러온다. 플레이어의 위치도 등록해 저장한 경우 그 위치에서 재개한다. |
| **H** 또는 **Controls** 버튼 | 안내를 열거나 닫는다. 이 선택은 두 슬롯에 공통이며 다음 실행에도 적용된다. |

에디터에서 KeyRoom을 열고 Play를 누른다. 오른쪽으로 몇 걸음 가서 **E**로 상자를 열고 **F5**를 누른다. 한 걸음 위로 올라가 오른쪽의 코인을 획득한 후 왼쪽에서 열쇠를 먹고, 위쪽 문을 지나 두 번째 방으로 간다. **2 → F5**를 누른다. 앱을 다시 열고 **1 → F9**를 누르면 첫 방에 열린 상자와 코인 한 개가 있다. **2 → F9**를 누르면 두 번째 방과 열쇠 표시가 복원된다. Mac 키보드에서 기능 키를 미디어 키로 쓰고 있다면 **Fn**을 함께 누른다.

## 월드와 런타임 프리팹 저장

KeyRoom 첫 방에서 상자에 다가가 **E → N → N → F5**를 누른다. 코인은 상자의 자식으로 생성되고 월드 X가 416에서 448로 이동한다. **N**을 한 번 더 누르고 **F9**로 불러오면 X가 448로 돌아온다. Scene Play에서는 Hierarchy의 **Chest → Coin**을 선택해 Inspector의 **World 448, 167**을 확인할 수 있다. 한 걸음 위로 올라가 오른쪽으로 걸어 코인을 획득한 뒤 **F5 → F9**를 누르면 상자가 열린 채 코인이 사라진 상태로 재개된다.

스크립트가 저장할 오브젝트와 속성을 등록한다. 프리팹 인스턴스는 멤버 하나를 지정하면 같은 원본의 하위 오브젝트까지 등록된다. 인스턴스를 붙인 부모는 위치와 활성 상태를 함께 등록하므로 부모도 이동할 수 있다.

```csharp
var coin = tick.Objects.Instantiate(Loot, tick.LocalToWorld(new Vector2(52, -5)), tick.ObjectId);
tick.Objects.TrackForSave(coin);
tick.Objects.TrackForSave(Lid, WorldSaveFields.Active);
```

| 선택할 속성 | 복원하는 상태 |
| --- | --- |
| `Existence` | 씬 오브젝트와 프리팹 멤버의 제거 여부, 생성된 인스턴스와 부모 연결 |
| `Transform` | 부모 기준 위치와 런타임 회전. 플레이 중 바꾼 회전·스케일·기울기와 부모 관계도 복원한다. 원본 크기와 피벗은 씬이나 프리팹에서 읽는다. |
| `Active` | 개별 오브젝트의 활성 상태. 부모의 활성 여부도 적용한다. |
| `Collider` | 충돌체가 있는 오브젝트의 충돌체 활성 상태 |
| `All` | 위 항목 전체. `TrackForSave(id)`의 기본값이다. |

F5 처리에서 `state.SaveWorld(data)`를 호출한 후 `state.Storage.SaveSlot(slot, data)`로 기록한다. F9 처리에서는 슬롯을 읽은 후 `state.LoadSavedScene(room, data, onEntered)`를 호출한다. 살아 있는 인스턴스는 같은 GUID를 되찾아 내부 참조와 부모 관계가 유지된다. 다른 주 씬을 방문한 뒤 저장해도 이전 방의 등록 상태가 같은 슬롯에 남는다. 추가 로드한 보조 씬은 등록 대상에서 제외된다.

부모나 로컬 변환을 바꾼 상태가 저장될 때 월드 데이터는 버전 2를 쓴다. 기존 버전 1도 그대로 읽는다. 복원 시 저장한 변환으로 충돌 도형을 다시 계산하므로 기울여 내려놓은 코인은 같은 가장자리에서 획득된다. [런타임 Transform 사용법](transform-2d.md#play-중-변환과-부모-변경)

복원은 교체할 씬에서 스크립트의 첫 `OnCreate` 전에 끝난다. 게임이 예전 슬롯에서 누락된 오브젝트를 생성해 주는 경우 `life.Objects.HasRestoredWorld`로 월드 상태가 있는지 확인할 수 있다. KeyRoom은 예전 v2 슬롯에 미획득 코인이 있으면 원래 위치에 생성하고, 다음 F5부터 실제 위치와 ID를 쓴다. 프리팹 이름이나 경로는 바꾸어도 에셋과 멤버 GUID를 유지해야 한다. 원본의 멤버 구성을 바꿀 때는 슬롯 마이그레이션이 필요하다.

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

이전 스키마는 `LoadSlot(slot, targetVersion, migrate)` 또는 `LoadSettings(targetVersion, migrate)`로 읽는다. 콜백은 원래 버전부터 한 단계씩 호출된다. KeyRoom은 v1의 `hasKey`를 v2의 `key`로 옮기고 상자와 코인 상태를 초기화한다. 변환은 메모리에서 수행하고 명시적으로 저장할 때 원본 파일을 교체한다. 현재 게임보다 높은 버전의 정상 슬롯을 덮어쓰거나 유효한 사본이 하나도 없는 손상 슬롯을 저장하면 오류가 발생한다. 동시 쓰기가 진행 중이면 다른 쓰기 요청에 `IOException`이 발생하며 다시 시도할 수 있다. [KeyRoom 체크포인트 스크립트](../../samples/KeyRoom/Scripts/Checkpoint.cs)

## 직전 저장으로 복구하기

슬롯 1을 처음 저장하면 `slot-001.save`와 SHA-256을 기록한 `slot-001.save.sha256`이 생긴다. 같은 슬롯에 다시 저장하면 직전 정상 파일이 `slot-001.save.bak`에 남는다. 세 파일을 함께 복사하면 다른 기기에서도 같은 복구 기록을 사용할 수 있다. 기존 YAML 슬롯은 그대로 열리며, 다음 저장부터 보호된다.

최신 파일이 사라지거나 기록한 SHA-256과 일치하지 않으면 `LoadSlot`이 같은 슬롯의 직전 사본을 검사한다. 복구할 수 있으면 원래 바이트로 최신 파일을 복원하고 그 진행 상태를 반환한다. 손상된 최신 파일 위로 다시 저장해도 정상 백업이 유지된다. 두 사본 모두 손상되면 오류를 반환하고 파일을 보존한다. 슬롯 삭제는 백업과 체크섬도 함께 지운다.

```csharp
var data = store.LoadSlot(1, 3, out bool recovered, migrate);
if (recovered)
    ui.SetText(statusLabel, "직전 저장을 복구했습니다.");
```

InventoryLab의 약초 마당에서 의뢰를 받고 약초 세 포기를 주운 뒤 보상 수정 한 개를 받고 **F5**로 첫 저장을 만든다. **U → F5**로 약초를 하나 사용해 두 번째 저장을 만든다. **F7**로 최신 슬롯을 반으로 자른 뒤 **F9**를 누르면 상태 줄에 **Damaged save: loaded the last good copy.**가 뜨고 약초 3개·수정 1개·보상 수령 완료가 돌아온다. 보상을 다시 청구해도 수정은 1개다. 같은 과정을 배포본에서 창 없이 재생하는 검사는 [check_recovery.py](../../engine/tests/InventoryLab.Replay/check_recovery.py)다. [InventoryLab 검증 기록](../../engine/tests/results/slot-recovery/README.md)

## 파일 위치

기본 루트는 .NET `LocalApplicationData` 아래의 `MoNoon`이다. `MONOON_USER_DATA` 환경 변수로 루트를 바꿀 수 있다. 하위 폴더는 다음과 같다.

| 실행 대상 | 루트 아래의 경로 |
| --- | --- |
| 에디터 Play와 소스 프로젝트의 Save & Run | `Play/<projectId>/` |
| 배포 게임 Release와 Native AOT | `Games/<projectId>/` |

각 폴더에는 `slot-001.save` 같은 슬롯 파일과 `player.settings`가 생긴다. 둘 다 VYaml로 읽고 쓰는 YAML이다. `GameSettings.projectId`는 게임을 업데이트할 때 유지한다. 복사한 프로젝트를 별도 게임으로 배포하려면 새 GUID를 지정한다.

[KeyRoom macOS 검증 기록](../../engine/tests/results/player-data/README.md)
