# 프로젝트별 에디터 확장 (로드맵 11.10)

macOS 27.0.1 (26A434) arm64, 이 Apple Silicon Mac에서 저장소 루트 기준으로 실행했다. 실행 대상은 패키지 에디터 `editor/MoNoon.app`의 Scene 편집과, 같은 KeyRoom 사본의 Release·Native AOT 빌드(기본 Distribution 프로필)다.

## 구조

- 폴더: 프로젝트 루트의 `Editor/`(11.9의 `Tests/`와 같은 방식). `ProjectScripts.Locations`가 `Tests/`와 함께 건너뛰므로 Scene Play·Release·Native AOT·`monoon test project` 어디에서도 게임 스크립트로 컴파일되지 않는다. 기존 `Editor/*.tools`(YAML 메뉴 선언)는 이 폴더를 이미 쓰고 있었다.
- 컴파일: 에디터가 `Editor/**/*.cs`를 런타임의 신뢰 어셈블리 전체(MoNoon 에디터, Engine.Core, Avalonia 포함)를 참조해 `MoNoon.EditorExtensions`로 컴파일하고 수집 가능한 로드 컨텍스트에 올린다([EditorExtensions.cs](../../../src/Engine.Editor/Shell/EditorExtensions.cs)). 게임 스크립트 컴파일은 System·Engine.Core만 참조하므로 확장 타입이나 `Engine.Editor`를 쓰면 컴파일 오류가 난다(단위 검사에서 CS0234 확인).
- API: `IEditorExtension.Register(EditorExtensionRegistry)` → `AddMenuCommand`, `AddInspector(스크립트 ID, ScriptInspector)`, `AddPanel(제목, EditorContext → Control)`. `EditorContext`는 `ProjectDirectory`·`Scene`·`Selected`·`IsPlaying`·`PlacePrefab`·`Log`·`Row`. `Row`와 `ScriptInspector.Row`는 Inspector의 `EditorTokens.LabelColumns(FieldLabelWidth)`·`FieldGap` 행을 그대로 만든다. 패널은 `EditorPanel`로 감싸 `DockHost`에 추가하고 Inspector와 같은 `EditorPanelPadding`을 쓴다.
- 기존 확장 명령 이전: `samples/KeyRoom/Editor/KeyRoom.tools`(Place Coin)를 [KeyRoomTools.cs](../../../../samples/KeyRoom/Editor/KeyRoomTools.cs)로 옮기고 `.tools` 처리 코드(메뉴, 패키지 의존성 추적, 경로 이동 시 프리팹 경로 재작성)를 지웠다. 패키지·템플릿은 `Editor/**/*.cs`를 함께 담고, 설치 시 `Editor/` 소스에는 스크립트 메타데이터를 요구하지 않는다. 프리팹은 더 이상 자동으로 따라오지 않으므로 패키지 검사에서 Coin.prefab을 함께 선택한다.
- 다시 불러오기: 프로젝트를 열 때, **Project → Reload Extensions**, 편집 중 `Editor/` 파일 저장 시. 이전 메뉴·패널을 지우고 컨텍스트를 Unload한다. 에디터를 닫을 때 확장 패널을 닫아 저장된 레이아웃에 남지 않게 한다.
- BRUTAL에는 에디터 확장 기능이 없어([BRUTAL 기능 목록](../../../../docs/brutal.md)) 기존 Roslyn 컴파일을 재사용했다.

## 에디터 검증 (KeyRoom.EditorExtensions)

[시작 훅](../../KeyRoom.EditorExtensions/StartupHook.cs)이 KeyRoom 사본을 1280 × 800 창으로 연다. 메뉴는 네이티브 메뉴 막대와 같은 `NativeMenuItem` 클릭 경로로 실행했다.

| 검사 | 결과 |
| --- | --- |
| **Project → Extensions**에 `Place Coin`, **Window**에 `Coins` | 통과 |
| Place Coin: Coin 프리팹 인스턴스 1개 배치(0 → 1), 선택됨 | 통과 |
| CollectCoin Inspector: 기본 LootTable 행 + 확장 행 Coins(`1 in this scene`)·Place | 통과 |
| 1280·1600·1920px에서 세 행의 레이블 x·입력 x 차이 0, 열 간격 60(이름 행과 같음), 레이블·입력 세로 중심 차이 0 | 통과 |
| Coins 패널: 세 행의 입력 x 차이 0, 열 간격 60, 왼쪽 여백 8(Inspector 이름 행과 같음) — 세 창 크기 모두 | 통과 |
| 패널의 Place Coin 버튼으로 배치, 0.5초 갱신 개수 `2` | 통과 |
| Undo 두 번으로 두 배치 모두 제거 | 통과 |
| `Editor/Broken.cs` 컴파일 오류: Console `./Editor/Broken.cs(3,13): error CS1525` 행(파일·줄 3 연결), 확장 메뉴·패널 제거 | 통과 |
| 파일 삭제 후 Reload Extensions로 Place Coin 복구 | 통과 |

캡처(`screencapture -l`): [CollectCoin Inspector 1280](inspector-1280.png) · [Coins 패널 1280](panel-1280.png) · [확장 컴파일 오류 Console](extension-error-1280.png). 측정값 [extensions.json](extensions.json), 실행 출력 [editor-run.txt](editor-run.txt).

Place 버튼 행의 오른쪽 끝은 버튼 너비만큼(24px) 짧다. 입력 필드는 행을 채우고 버튼은 내용 너비를 쓰는 기존 Inspector 규칙과 같다.

## Release·Native AOT

```sh
./monoon build <KeyRoom 사본> release --output <out>/release
./monoon build <KeyRoom 사본> native-aot --output <out>/native-aot
```

| 검사 | Release (280개 파일) | Native AOT (63개 파일) |
| --- | --- | --- |
| `.cs` 파일, `Editor/`·`Tests/` 폴더 | 없음 | 없음 |
| `KeyRoomTools`, `IEditorExtension`, `EditorExtensionRegistry`, `MoNoon.EditorExtensions` 문자열(모든 파일 바이트 검색) | 없음 | 없음 |
| `ScriptReload`, `Scripts reloaded`, `Script reload failed` 문자열 | 없음 | 없음 |
| Engine.Authoring·Engine.Editor·MoNoon.dll·Avalonia·SkiaSharp·Microsoft.CodeAnalysis 파일 | 없음 | 없음 |

파일 목록: [release-files.txt](release-files.txt) · [native-aot-files.txt](native-aot-files.txt). `samples/KeyRoom/KeyRoom.csproj`도 `Editor/**/*.cs`를 컴파일 항목에서 뺐다.

## 단위 검사

`./monoon test`의 [ScriptReloadChecks](../../Engine.Checks/ScriptReloadChecks.cs)는 `Editor/`의 `.cs`가 게임 스크립트 목록에 없는지, 게임 스크립트가 `Engine.Editor`를 참조하면 컴파일 오류인지 확인한다. [WorkspaceChecks](../../Engine.Checks/WorkspaceChecks.cs)는 `Editor/KeyRoomTools.cs`와 Coin.prefab을 고른 패키지에 확장 소스(메타데이터 없음)·프리팹·스프라이트·테이블·스크립트가 들어가고, 설치한 프로젝트에서도 확장이 게임 스크립트가 아닌지 확인한다. 전체 회귀: All checks passed.

## 확인하지 않은 것

- 확장 어셈블리가 Unload 뒤 실제로 메모리에서 해제되는지(Avalonia가 컨트롤 타입을 캐시하면 남을 수 있다).
- 외부 IDE용 `.monoon/ide/` 프로젝트에 `Editor/`가 포함되지 않는다(확장 편집 시 자동 완성 없음).
- 확장이 게임 스크립트 타입을 참조하는 경우(현재 참조 목록에 게임 스크립트 어셈블리가 없다. Inspector는 스크립트 ID 문자열로 연결한다).
- 프리팹을 옮겼을 때 확장 코드의 경로 문자열은 바뀌지 않는다(이전 `.tools`는 자동 재작성됐다).
- 레이아웃 저장 후 다음 실행: 확장 패널은 종료 시 닫아 레이아웃에서 빼지만, **Save Layout as Custom**에 확장 패널이 들어가면 다음 실행에서 Custom 레이아웃이 무효가 된다.
- Windows·Linux x64 에디터.

Linux x64 게스트(2026-11-23): 확장·정렬 값이 Mac과 같고 게스트에서 빌드한 Release·AOT에 확장 코드가 없었다. [Linux 결과](../chapter11-linux-x64/README.md)
