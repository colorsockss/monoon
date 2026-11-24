# 스크립트 필드 타입·Play 편집 확대·에디터 확장 API (13.8·13.11·13.13)

2026-11-24 · macOS 27.0.1 arm64 · Apple M4 Max · .NET SDK 10 · 기준 wip/next1 `699eb53`에서 시작해 마지막 검증 전에 wip/next1 `1c34b81`을 병합한 이 브랜치. 실행 대상은 패키징한 Mac 에디터(`editor/MoNoon.app`, Release)의 Scene 편집과 Scene Play, InventoryLab·KeyRoom의 Release와 Native AOT 배포본(`--profile distribution`)이다. 측정 시작과 끝에 `qemu-system` 프로세스는 없었다. 시간 비교는 하지 않았다(trace 바이트와 저장 JSON만 비교). 하네스: [InventoryLab.ScriptFields](../../InventoryLab.ScriptFields/README.md)(13.8), [KeyRoom.PlayFields](../../KeyRoom.PlayFields/verify.py)(13.11), [KeyRoom.EditorExtensions](../../KeyRoom.EditorExtensions/README.md)의 drawer 단계(13.13).

## 배포본 영향 (13.8 완료 기준)

같은 체크아웃에서 InventoryLab을 osx-arm64 `--profile distribution`으로 빌드해 비교했다(바이트). "기준"은 `699eb53`의 엔진과 샘플, "엔진만"은 이 브랜치의 엔진에 SupplyTuning을 뺀 InventoryLab, "목록·사전 제외"는 SupplyTuning에서 `List<int> Restock`·`Dictionary<string, int> Prices`·`PriceOf`를 뺀 것이다.

| 항목 | 기준 | 엔진만 | 목록·사전 제외 | 이 브랜치(SupplyTuning 전체) | 차이(전체 − 기준) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Native AOT `MoNoonPlayer` | 8,387,936 | 8,387,936 | 8,404,544 | 8,421,072 | **+33,136 (+0.40%)** |
| Native AOT app 전체 | 66,480,847 | 66,480,847 | 66,497,455 | 66,513,983 | +33,136 |
| Release `MoNoon.GameScripts.dll` | 105,984 | 105,984 | 113,664 | 116,224 | +10,240 |
| Release `Engine.Core.dll`(게임별 trim) | 2,969,600 | 2,969,600 | 2,974,208 | 2,976,256 | +6,656 |
| Release app 전체 | 83,726,300 | 83,726,300 | 83,738,588 | 83,743,196 | +16,896 |

- 새 종류를 쓰지 않는 게임은 Release·Native AOT 모두 **0 B**다. 읽기 코드(`ScriptValues`)는 비제네릭 메서드와, 게임의 생성 코드가 부를 때만 만들어지는 제네릭 메서드뿐이라 쓰지 않으면 잘린다. `GameJson` 컨텍스트에는 형식을 더하지 않았다(새 `JsonTypeInfo` 0개).
- `List<int>`와 `Dictionary<string, int>` 두 필드의 Native AOT 몫은 16,528 B다. 값 형식 제네릭(`List<int>`, `Dictionary<string,int>`, `ScriptValues.Array<int>` 등)은 게임이 선언한 원소 타입마다 컴파일되므로 이 비용은 게임 선택에 비례한다. 엔진 쪽에는 값 형식 제네릭·값 튜플 사전·`record struct`·값 형식 LINQ를 새로 넣지 않았다.
- 스크립트 필드 속성(`[ColorUsage]`, 13.13 예제의 `[InputAction]`)은 `[Conditional("MONOON_EDITOR")]`라 게임 어셈블리·AOT 실행 파일에 이름이 0건이다(`verify.py scan`). KeyRoom에 더한 `InputActionAttribute` 클래스 정의는 `MoNoon.GameScripts.dll`에 남는다(속성 적용 데이터는 없음).
- 프레임 비용: 읽기는 스크립트를 만들 때 한 번이다. 틱마다 하는 일은 없다. 13.11·13.13은 에디터 전용이다.
- 빌드 크기 기준(`./monoon test`의 KeyRoom·PlatformerLab·InventoryLab +1%): 아래 검증 표. 기준값은 갱신하지 않았다(증가가 1% 안).
- 배포본 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring): Release·Native AOT 모두 0개.
- BRUTAL: 스크립트 필드 직렬화·Inspector·에디터 확장은 [BRUTAL 기능 정리](../../../../docs/brutal.md)에 해당 기능이 없어 쓰지 않았다.

## 결정

- **저장 형식**: 기존 `values` JSON에 그대로 둔다. 배열·목록은 JSON 배열, 사전은 JSON 객체(삽입 순서, int 키는 불변 문화권 숫자), 구조체는 멤버 객체, 색은 `#RRGGBB[AA]`, 플래그는 `"A, B"`. 씬 스키마는 바꾸지 않았다(39 불필요). 기본값과 같으면 키를 지우는 규칙도 같다. 기존 프로젝트는 그대로 읽힌다(필드 정의만 늘었다).
- **읽기**: 생성 팩토리가 `component.TryValue(키, out var e)`로 JSON을 받아 `ScriptValues.Int64/Color/Array/List/Dictionary/Enum`을 부른다. 구조체는 멤버마다 `TryGetProperty`로 읽고 없는 멤버는 초기화식 값을 남긴다. 잘못된 JSON 종류는 초기화식 값을 유지하고(필드 타입이 바뀐 경우, `ReadOverride`와 같은 규칙), 선언되지 않은 플래그 이름은 오류다.
- **기본값**: Roslyn 연산 트리에서 상수만 읽는다. 상수가 아닌 초기화식은 표시하지 않고 **Not shown: set at run time**으로 알린다. `[Serializable]`이 없는 소스 구조체는 "add [System.Serializable]"로 알린다. 플래그 enum의 체크 목록은 0이 아닌 멤버만, 아무것도 없으면 `""`.
- **구조체 깊이**: 한 단계(사용자 결정 8). 멤버는 숫자·bool·string·enum·Vector2·Color. 배열 원소는 기본 타입만(enum·Vector2 목록은 범위 밖).
- **Play 편집(13.11)**: Stop은 지금처럼 적용하지 않은 값을 버린다. 필드마다 체크가 "유지", **Apply selected**는 선택 오브젝트의 체크한 값, **Apply all**은 이번 Play의 모든 편집(모든 오브젝트, 체크 무관). 둘 다 Undo 한 단계다. 내장 값은 런타임에 바꿀 API가 있는 것만 넣었다: 오브젝트 Active, Sprite Renderer Tint, 충돌체 켜짐(숫자는 기존 Local Transform 행). Apply 버튼 묶음은 같은 칸의 가로 StackPanel이라 편집 모드의 **Overrides…** 위치가 바뀌지 않는다.
- **확장 API(13.13)**: drawer 키는 필드의 속성 이름(Attribute 접미사 생략 가능)이 먼저, 없으면 C# 타입 이름. drawer는 값 칸만 그리고 레이블·오버라이드 막대·행 메뉴는 기본 행을 쓴다. `FieldDrawer.Set`/`EditorContext.SetField`는 기본값이면 키를 지우고 Undo 한 단계로 기록하며, Inspector를 다시 만들지 않는다(입력 포커스 유지). `EditorContext.Row`는 Inspector의 `InspectorRow`와 같은 함수(`InspectorRowControl`)로 합쳤다.
- **13.11 Undo 뒤 헤더**: 꺼진 충돌체를 적용하면 깨끗한 씬이 바로 저장되며 스키마 32로 쓰인다. Undo 두 번으로 오브젝트 텍스트는 Play 전과 같지만 메모리의 머리말 `schemaVersion`은 32로 남는다(기존 저장 동작, 이번 변경과 무관).

## 바꾼 것

| 커밋 | 파일 | 내용 |
| --- | --- | --- |
| 13.8 | `Engine.Core/ScriptValues.cs`(새 파일), `GameBehaviour.cs`, `ScriptDefinition.cs`, `InspectorAttributes.cs` | 비리플렉션 읽기, `TryValue`·`ReadFlags`, `ScriptField`의 `Flags`·`ColorAlpha`·`Element`·`Key`·`IsList`·`Members`, `[ColorUsage]` |
| 13.8 | `Engine.Authoring/ProjectScripts.FieldTypes.cs`(새 파일), `ProjectScripts.cs`, `ScriptInspectorInfo.cs`, `GameBuild.cs` | 새 종류의 기본값·생성 코드, 건너뛴 이유, 빌드 검사의 플래그 목록 |
| 13.8 | `Engine.Editor/Inspector/MainWindow.ScriptCollections.cs`(새 파일), `Model/ScriptValueEdits.cs`(새 파일), `Project/MainWindow.Scripts.cs`, `Inspector/MainWindow.MultiInspector.cs` | 목록·사전·구조체 행, 플래그·색·long/uint 행, 다중 선택에서는 읽기 전용 |
| 13.8 | `samples/InventoryLab/Scripts/SupplyTuning.cs`(새 파일) | 종류마다 필드 하나 |
| 13.8 | `engine/tests/Engine.Checks/ScriptFieldTypeChecks.cs`(새 파일), `InspectorChecks.cs`, `HistoryChecks.cs`, `Engine.Checks.csproj` | 아래 검증. 13.3 검사의 "지원하지 않는 타입" 예를 `List<int>` → `HashSet<int>`로 바꿈 |
| 13.8 | `engine/tests/InventoryLab.ScriptFields/`(새 하네스) | Inspector 편집·재열기·세 경로 trace |
| 13.11 | `Engine.Editor/Inspector/MainWindow.PlayInspector.cs`, `Shell/MainWindow.axaml` | bool·enum·Vector2·string·색 Play 행, Active·Tint·Collider, Apply all |
| 13.11 | `engine/tests/KeyRoom.PlayFields/`(새 하네스) | 아래 검증 |
| 13.13 | `Engine.Editor/Shell/EditorExtensions.cs`, `Shell/MainWindow.Extensions.cs`, `Project/MainWindow.Scripts.cs`, `Inspector/MainWindow.MultiInspector.cs`, `Engine.Authoring/ScriptInspectorInfo.cs`·`ProjectScripts.cs` | `AddFieldDrawer`·`FieldDrawer`, `RecordEdit()`·`SetField`, 행 함수 하나로, 필드 속성·타입 이름 |
| 13.13 | `samples/KeyRoom/Editor/KeyRoomTools.cs`, `Scripts/InputActionAttribute.cs`(새 파일), `RoomGuide.cs`, `RebindButton.cs` | `[InputAction]` drawer(입력 액션 목록) |
| 13.13 | `engine/tests/KeyRoom.EditorExtensions/StartupHook.cs`, `editor.py` | drawer 단계 |
| 문서 | `docs/manual/scripting.md`, `editor.md`, `play-diagnostics.md`, `workspace.md` | 사용법 |

## 검증

| 검사 | Scene 편집 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- | --- |
| 13.8 Engine.Checks `ScriptFieldTypeChecks`(`--only history` 안): 16종 기본값 JSON, 저장 값 16개를 생성 팩토리가 읽은 값, 저장 없음 → 초기화식, 잘못된 종류 → 초기화식, 선언 안 된 플래그 → 오류, 구조체 부분 저장 병합, 씬 저장→열기→저장 바이트 동일 | 통과 | | | |
| 13.8 HistoryChecks: 목록 추가·삭제, 사전 행 추가·키 변경, 구조체 멤버(기본값 복귀 시 키 삭제) 편집이 각 Undo/Redo 정확, 전체 Undo·Redo 저장 바이트 동일(KeyRoom 90단계·Field10000 30단계) | 통과 | | | |
| 13.8 Inspector UI: 12개 필드를 행으로 편집, Seed·Unlocked·Prices·FirstWave 각 Undo 1회 → 이전 JSON, Redo → 다음 JSON | 통과 | — | — | — |
| 13.8 정렬 1280/1600/1920px(행 34개, 긴 사전 키): 값 열 x 1개(1130/1450/1770), +·× 오른쪽 끝 1개(1272/1592/1912), 긴 키 상자 폭 = 값 상자 폭 | 통과 | — | — | — |
| 13.8 저장·재열기: 새 에디터에서 Seed·SlotMask·색·플래그·목록·사전 값 그대로 | 통과 | — | — | — |
| 13.8 FieldTrace 13줄(`261d59a3875df046`)이 세 경로 바이트 동일, 기대값과 같음 | — | 통과 | 통과 | 통과 |
| 13.8 게임 코드에 속성 이름 0건(`verify.py scan`) | — | — | 통과 | 통과 |
| 13.11 Play: Frozen(bool)·Facing(enum)·Push(Vector2)·Banner(string)·Glow(색)·Tint·Collider가 실행 중인 게임에 반영, 적용 전 원본 씬 변화 없음, 행 값 열 x가 Local X와 같음(1130/1450/1770) | — | 통과 | — | — |
| 13.11 Apply selected(Banner 체크 끔) → 나머지 6개 저장, Apply all → Banner 저장, 적용 안 한 Frozen 편집은 Stop에서 버림, Stop 뒤 Undo 2회로 오브젝트 텍스트가 Play 전과 같음 | — | 통과 | — | — |
| 13.13 `[InputAction]` drawer: RoomGuide.Action에 입력 액션 목록, Interact 선택 → 저장 1단계, Undo → 기본값(키 없음), Redo → Interact, Guide 행과 같은 레이블·값 열. 기존 11.10 단계(메뉴·Inspector·패널·컴파일 오류) 통과 | 통과 | — | — | — |
| 13.13 `Editor/` 소스·확장 코드·에디터 라이브러리가 배포본에 없음(11.10 검사) | — | — | 통과 | 통과 |
| `./monoon test`(YamlDefaultChecks 133종, DeterminismScanChecks 227파일, 위 검사 포함) — `1c34b81` 병합 뒤 | 통과 | | | |
| 빌드 크기 기준(6개 osx-arm64 배포 빌드 +1% 이내) — 병합 뒤. InventoryLab AOT 실행 파일 8,437,664 B(기준 8,387,936, +0.59%, 병합된 다른 작업 포함), KeyRoom AOT 8,736,544 B(기준 8,719,936, +0.19%) | | | 통과 | 통과 |

위 표의 에디터·Scene Play·Release·Native AOT 검사는 `1c34b81` 병합 뒤 다시 만든 에디터 패키지와 플레이어로 모두 다시 실행해 같은 결과를 얻었다(trace `261d59a3875df046`, 금지 파일 0개).

캡처: [SupplyTuning 1280](supply-1280.png) · [1920](supply-1920.png) · [Play 행 1280](play-1280.png) · [drawer 1280](drawer-1280.png). 결과 JSON: [edit](edit.json) · [play](play.json) · [extensions](extensions.json) · [trace](trace.txt).

## 재현

저장소 루트에서. 13.8은 [하네스 README](../../InventoryLab.ScriptFields/README.md)의 명령. 13.11·13.13:

```sh
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet build engine/tests/KeyRoom.PlayFields -c Release -m:1 -p:UsedAvaloniaProducts=
work=$(mktemp -d)
python3 engine/tests/KeyRoom.PlayFields/verify.py prepare samples/KeyRoom "$work/KeyRoom"
sh engine/scripts/gui-lock.sh python3 engine/tests/KeyRoom.PlayFields/verify.py editor editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
  engine/.artifacts/bin/PlayFieldsProbe/release/PlayFieldsProbe.dll "$work/play"
dotnet build engine/tests/KeyRoom.EditorExtensions -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 engine/tests/KeyRoom.EditorExtensions/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom \
  engine/.artifacts/bin/ExtensionsProbe/release/ExtensionsProbe.dll "$work/extensions"
./monoon test
```

## 확인하지 않은 것

- Linux x64 게스트의 Release·Native AOT 헤드리스 재생(로드맵 13.8 완료 기준의 일부): `verify.py play`·`compare`는 창 없이 돌아가므로 같은 준비 프로젝트로 coordinator가 웨이브 끝에 실행한다. Windows는 보류 중이다.
- 기존 KeyRoom.PlayInspector(9.1, OS 입력 드라이버)는 다시 돌리지 않았다. 13.11의 bool·enum 확인은 새 프로브 하네스(KeyRoom.PlayFields)로 했다. 숫자 행(`PlayNumber`) 코드는 바꾸지 않았다.
- 배열 원소 끌어서 순서 바꾸기(참조 목록에는 있음), 플래그·벡터 원소 목록, 두 단계 이상의 구조체, 클래스 직렬화는 넣지 않았다.
