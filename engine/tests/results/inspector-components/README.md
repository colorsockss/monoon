# Inspector 컴포넌트 작업 (13.2·13.3·13.4·13.12·13.14)

2026-11-24 · macOS 27.0.1 arm64 · Apple M4 Max · .NET SDK 10.0.101 · 기준 커밋 `dbc47bf`(wip/next1, I-1 확정 시 기록과 오브젝트 단위 차분 Undo 포함). 실행 대상은 패키징한 Mac 에디터(`editor/MoNoon.app`, Release)의 Scene 편집과 Scene Play, KeyRoom의 Release와 Native AOT 배포본(`--profile distribution`)이다. 측정 시작과 끝에 `qemu-system` 프로세스는 없었다. 시간 비교는 하지 않았다(결정성 기준인 trace·캡처 해시와 저장 바이트만 비교).

## 배포본 영향

같은 KeyRoom 샘플을 기준 커밋(`git archive dbc47bf`, 그 커밋의 캐시된 플레이어 템플릿)과 이 브랜치로 각각 빌드해 비교했다(바이트).

| 항목 | 기준 | 이 브랜치 | 차이 |
| --- | ---: | ---: | ---: |
| Release app 전체 | 92,188,132 | 92,200,420 | +12,288 |
| Release `Engine.Core.dll` | 1,471,488 | 1,483,776 | +12,288 |
| Release `MoNoon.GameScripts.dll` | 32,256 | 32,256 | 0 |
| Native AOT app 전체 | 18,149,967 | 18,166,495 | +16,528 |
| Native AOT `MoNoonPlayer` | 8,537,616 | 8,554,144 | +16,528 |

- 늘어난 것은 런타임의 활성·켜짐(저장용 nullable 속성 두 개, 시작 시 한 번 끄는 처리, 풀 대여 시 저작 상태 복원), 컴포넌트별 저장 검사 분리, 속성 클래스 8개의 정의(Release `Engine.Core.dll`에만 있고 Native AOT에서는 쓰이지 않아 잘린다)다. 로드맵의 "런타임 IL 수 KB" 범위다.
- 프레임 비용: 꺼진 컴포넌트는 월드를 만들 때 한 번 빠지고, 비활성 시작은 시작 시 부모를 한 번 훑는다. 틱마다 하는 일은 없다.
- 스크립트 필드 속성(13.3)은 `[Conditional("MONOON_EDITOR")]`라 게임 어셈블리에 적용 데이터가 0 B다. KeyRoom의 `PlayerMovement`에 Header·Range·Tooltip·SerializeField·Min을 붙였지만 `MoNoon.GameScripts.dll` 크기는 같고, Release dll과 Native AOT 실행 파일에서 속성 이름이 0건이다(`verify.py scan`). `[SerializeField]` private 필드는 생성된 연결 코드가 `UnsafeAccessor`로 값을 넣는다(리플렉션 없음, Native AOT 동작 확인).
- 13.4·13.12·13.14는 에디터 전용이다. 배포본의 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) 검사는 Release·Native AOT 모두 0건이다.
- BRUTAL에는 Inspector·씬 직렬화·스크립트 메타데이터에 해당하는 기능이 없어([BRUTAL 기능 정리](../../../../docs/brutal.md)) 사용하지 않았다.

## 결정

- **Conditional 속성 확인(결정 8 선행 조건)**: Roslyn은 `[Conditional]` 속성을 원본 심볼의 `GetAttributes()`에 그대로 남기고 내보낼 때만 뺀다. Engine.Checks가 KeyRoom을 컴파일해 원본에서 Header·Range·Tooltip·Min·Space·Multiline·HideInInspector를 읽고, 내보낸 어셈블리의 형식 참조에 속성이 0건인지 확인한다. 그래서 다른 방식(별도 메타데이터 파일 등)은 필요 없었다.
- **기본값 true는 쓰지 않는다**: VYaml은 `WhenWritingNull`만 지원하므로 `Active`·`Enabled`는 YAML에서 숨기고, 끈 경우만 `false`가 되는 nullable 저장 속성(`active`·`enabled`)을 둔다. 키가 없으면 null → true로 읽혀 `YamlDefaultChecks`의 생략 규칙과 맞다. 샘플의 씬·프리팹 50개를 다시 써도 새 키가 없다.
- **콘텐츠 버전**: 끈 항목이 있는 씬만 32로 저장한다. 이전 플레이어는 32를 거부하므로 비활성 오브젝트를 켠 채로 잘못 실행하지 않는다. 기존 씬은 그대로 31 이하다.
- **끌 수 없는 컴포넌트**: UI Rect·UI Canvas·Rigidbody 2D·Tilemap·Camera 2D(와 옛 HUD)는 다른 컴포넌트의 전제라 체크를 두지 않고, 파일에서 꺼져 있으면 저장 검사가 거부한다. 오브젝트를 비활성으로 두면 된다.
- **런타임의 꺼진 컴포넌트**: 월드 사본에서 빠진다(스크립트를 만들지 않고 렌더러를 그리지 않는다). 충돌체만 남겨 `SetColliderEnabled(false)`로 시작해 스크립트가 켤 수 있다. 충돌체 외 컴포넌트를 실행 중에 켜는 API는 이번 범위에 없다.
- **Range 슬라이더**: 13.10의 행이 아직 없어 슬라이더와 숫자 상자를 같은 입력 열에 나란히 두었다(입력 높이 24px로 행 높이 유지). 13.10에서 레이블 끌기를 더할 때 이 행을 재사용하면 된다.
- **잠금**: 편집 모드에서만 적용한다. Play 중에는 Inspector가 선택을 따른다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `Engine.Core/SceneDocument.cs` | `SceneObject.Active`·`ActiveInHierarchy`, 저장 속성 `active`. 끈 항목이 있으면 버전 32. 컴포넌트별 저장 검사를 `ValidateComponent`로 분리(Inspector 경고가 같은 규칙을 씀) |
| `Engine.Core/SceneComponent.cs` 외 5개 | `SceneComponent.Enabled`, 저장 속성 `enabled`, `CanDisable`(구조 컴포넌트 6종은 false) |
| `Engine.Core/SceneGame.cs`, `RuntimeObjects.cs`, `PrefabPools.cs`, `Transform2D.cs` | 꺼진 컴포넌트 제외·충돌체는 꺼진 채 시작, 비활성 시작과 부모 전파, 프리팹 생성·풀 대여 시 저작 상태 |
| `Engine.Core/InspectorAttributes.cs` | Tooltip·Range·Min·Header·Space·Multiline·HideInInspector·SerializeField(`[Conditional("MONOON_EDITOR")]`) |
| `Engine.Authoring/ProjectScripts.cs`, `ScriptInspectorInfo.cs` | 원본 심볼에서 속성과 `///` 요약을 읽고, 표시하지 않는 필드와 이유를 모은다. `[SerializeField]` 필드는 `UnsafeAccessor`로 연결 |
| `Engine.Editor/Shell/MainWindow.axaml`, `Inspector/ComponentSection.cs`, `ComponentRows.cs` | 16px 체크 테마, 모든 섹션 머리말에 체크 열 예약, 이름 레이블 열 오른쪽 끝의 활성 체크(레이블 왼쪽 끝 유지), 경고 줄, Debug 섹션 |
| `Engine.Editor/Inspector/MainWindow.Inspector.cs` | 활성 토글, Add Component 검색·묶음, 톱니 메뉴 명령, 잠금·Debug 보기, 경고 계산 |
| `Engine.Editor/Inspector/ComponentPicker.cs`, `Model/ComponentEdits.cs` | 묶음 분류, UI 없는 Reset·Copy·Paste Values·Move(Engine.Checks가 링크) |
| `Engine.Editor/Project/MainWindow.Scripts.cs` | 머리글·간격·툴팁·Range 슬라이더·Min·Multiline, 회색 "Not shown" 줄 |
| `Engine.Editor/Docking/DockHost.cs`, `Shell/EditorUserState.cs`, `PanelStyles.axaml`, `EditorTokens.cs` | 탭 도구(`TabTools`), 잠금·Debug 상태 저장, 스타일 |
| `Engine.Editor/Hierarchy/*.cs`, `Scene/SceneViewport.cs` | 비활성 행 0.55, Scene 편집 화면에서 비활성 오브젝트를 그리거나 고르지 않음, 편집마다 경고 갱신 |
| `samples/KeyRoom/Scripts/PlayerMovement.cs` | Header·Range·Tooltip·`[SerializeField, Min(16)] coinReach`(줍기 거리 128 그대로) |
| `engine/tests/Engine.Checks/HistoryChecks.cs`, `InspectorChecks.cs`, `ComponentStateChecks.cs`, `Engine.Checks.csproj` | Undo 무작위 편집에 활성·켜짐 토글, Reset, Paste Values, 순서 변경, Remove 추가. 저장 규칙·런타임·속성·메뉴 검사 |
| `engine/tests/KeyRoom.InspectorComponents/` | [하네스](../../KeyRoom.InspectorComponents/README.md) |
| `engine/tests/PlatformerLab.Joints/StartupHook.cs`, `PlatformerLab.Paths/StartupHook.cs` | 값 열 측정에서 섹션 머리말(새 체크, 접기·체크·톱니 Grid)을 뺀다 |
| `docs/manual/editor.md`, `docs/manual/scripting.md` | 사용법 |

## 검증

| 검사 | 실행 대상·빌드 | 결과 |
| --- | --- | --- |
| 활성·켜짐 trace 120틱(비활성 코인과 자식, 꺼진 충돌체·스크립트·렌더러) | Release | 통과 |
| 같은 trace | Native AOT | 통과 |
| 같은 trace(프로브가 같은 기록 재생) | Scene Play | 통과 |
| 세 경로 trace `78e935ea5787c52e…`와 캡처 20·50·100프레임 바이트 동일, 20≠50(코인이 켜져 그려짐) | 세 경로 | 통과 |
| 게임 코드에 속성 이름 0건 | Release `MoNoon.GameScripts.dll`, Native AOT `MoNoonPlayer` | 통과 |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) | Release·Native AOT | 0건 |
| Undo 무작위 편집(활성·켜짐·Reset·Paste Values·Move·Remove 포함) Undo/Redo 바이트 동일, 전체 Undo/Redo 저장 바이트 동일 | Engine.Checks, KeyRoom·Field10000 | 통과 |
| 저장 규칙(기본값 미기록, 샘플 50개 파일, 버전, 끌 수 없는 카메라 거부)과 런타임 | Engine.Checks | 통과 |
| 속성을 원본에서 읽음, 게임 어셈블리 참조 0건, private 필드 연결 | Engine.Checks | 통과 |
| Add Component 묶음과 "col" 검색, Paste Values·Reset·Move 모델 | Engine.Checks | 통과 |
| 13.2 UI: 이름 행·머리말 체크, 흐림 0.55, Scene에서 빠짐, 카메라 체크 없음, 프리팹 Overrides의 `active`·`spriteRenderer.enabled`, 각 Undo 1회 | Scene 편집 | 통과 |
| 13.3 UI: Movement 머리글, Range 60–480 슬라이더 240→300 Undo 1회, 툴팁 두 가지, Min 16 고정, 재시작 뒤 300 유지 | Scene 편집 | 통과 |
| 13.4 UI: 묶음 7개, "col"에 충돌체 3종, 메뉴 폭 180·220·360px에서 아이콘 x 5·이름 x 25 동일, 문 충돌체 → 상자 Paste as New, Exit → Wall Paste Values, Reset, Move Up/Down, Remove 각 Undo 1회 | Scene 편집 | 통과 |
| 13.14 UI: Rigidbody 2D + 충돌체 제거 → 노란 줄, Undo → 사라짐 | Scene 편집 | 통과 |
| 13.12 UI: 잠금 유지·잠근 대상에 편집·씬별 저장, Debug 보기 읽기 전용, 재시작 뒤 복원, 씬 SHA 동일, Release 빌드 268개 파일 동일(빌드 ID 제외) | Scene 편집·Release | 통과 |
| 정렬: 1280·1600·1920px와 긴 이름에서 섹션 제목·아이콘·체크 x 각 1개, 이름 행 입력 x = Tag 입력 x, Range 행 높이 24px | Scene 편집 | 통과 |
| `./monoon test` | Engine.Checks 전체(템플릿은 `build-players.sh`로 새로 만듦) | 통과 (`All checks passed.`, YAML 기본값 123종, 결정성 API 검사 포함) |
| 에디터 하네스 26개(`editor.py`, 아래 표) | 패키지 에디터 Scene 편집·Scene Play | 통과 |

### 기존 에디터 하네스

모두 GUI 잠금 두 개(`engine/scripts/gui-lock.sh` 안에 공용 잠금) 아래에서 하나씩 실행했다. 처음 일괄 실행에서 9개가 실패했고 원인을 고친 뒤 해당 하네스를 다시 돌려 모두 통과했다.

| 하네스 | 결과 | 비고 |
| --- | --- | --- |
| AudioLab.Vorbis, CollisionLab.FrameProfiler·SplitLoading·EditorPerf·InspectorEdits, InventoryLab.RichText, KeyRoom.ComponentKeys·CopyPaste·PrefabStructure·Rebinding·ScriptReload·ScriptWaits·EditorBuild, PlatformerLab.TileCollision, Samples.GameText, SeedLab.GridNavigation·TileTerrain, SpriteLab.Aseprite | 통과 | |
| KeyRoom.EditorDocking·EditorExtensions·EditorPanelsDetail·EditorTheme | 통과(수정 후) | 처음에는 활성 체크를 "Name" 레이블 왼쪽에 두어 레이블이 20px 밀렸다(레이블 왼쪽 끝 공유 규칙 위반). 체크를 레이블 열 오른쪽 끝으로 옮겼다. |
| KeyRoom.FieldRename | 통과(수정 후) | 하네스가 `[FormerlySerializedAs("Speed")]` 단독 줄을 찾는다. PlayerMovement에서 그 줄을 따로 두었다. |
| PlatformerLab.Joints·Paths, BarrageLab.Lines | 통과(프로브 수정 후) | 두 프로브가 섹션 안의 모든 CheckBox와 "버튼 3개인 Grid"를 값 열로 세어, 새 머리말 체크(`EnabledCheck`)와 머리말 Grid(접기·체크·톱니)를 값 열로 잘못 셌다. 머리말을 빼도록 고쳤다. 값 행의 정렬 기준은 그대로다. |
| CollisionLab.FrameProfiler·KeyRoom.CopyPaste | 통과(수정 후) | 실제 결함: 잠금 도입 때 빈 선택에서도 다중 선택 Inspector가 켜져 `RefreshMultiTransform`이 예외를 냈다. 원래 조건(선택 2개 이상, 잠금 없음)으로 고치고 이 하네스에 빈 선택 검사를 넣었다. |
| KeyRoom.ScriptWaits | 통과 | 첫 실행 실패는 실행 스크립트가 Rebinding과 이름이 같은 프로브를 잘못 빌드한 탓이다. |

이름 행 수정 이전 에디터로 통과한 하네스는 이름 행 정렬을 재지 않으므로 다시 돌리지 않았다.

캡처: [Inspector 1280px](inspector-1280.png) · [비활성 Door](i2-door-inactive.png) · [PlayerMovement](i3-player-movement.png) · ["col" 검색](i4-add-component-col.png) · [경고 줄](i14-rigidbody-warning.png) · [Debug 보기](i12-debug.png) · [Scene Play 50프레임](scene-play-0050.png)

## 재현

[하네스 README](../../KeyRoom.InspectorComponents/README.md)의 명령을 저장소 루트에서 실행한다. 모델 검사만은 `./monoon test`(HistoryChecks 안에서 `InspectorChecks`가 돈다).

## 확인하지 않은 것

- Linux x64 게스트 Release·Native AOT의 같은 trace(로드맵 13.2): VM은 coordinator 몫이다. 게스트 결과를 이 Mac의 결과로 대신하지 않는다. Windows는 보류.
- 실행 중에 충돌체 외 컴포넌트를 켜고 끄는 스크립트 API, Scene 편집 화면에서 꺼진 렌더러를 숨기는 것(지금은 꺼진 렌더러도 편집 화면에는 그린다), UI 목록 바인딩이 복제하는 템플릿의 꺼진 컴포넌트.
- 잠금의 Play 중 유지, 복사한 컴포넌트를 시스템 클립보드로 다른 에디터 창에 넘기는 것.
- coordinator가 옮길 것: `Engine.Checks/Program.cs`의 `onlyNames`에 `history`가 없어 `--only history`가 실패한다(핫스팟이라 고치지 않음). `InspectorChecks`는 지금 `HistoryChecks.Run` 끝에서 부른다. `SceneGame.cs`·`RuntimeObjects.cs`는 로드맵이 요구한 런타임 처리 때문에 고쳤다.
