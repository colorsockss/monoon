# 에디터 구현 기록

범위: 씬 편집·프리팹, 프로젝트·에셋, Play 진단·프로파일러, 작업 보존·Undo·병합, 프로젝트 에디터 확장, 도킹 레이아웃과 패널 디자인, Inspector(13장).
사용법은 [에디터 매뉴얼](../manual/editor.md)과 각 매뉴얼에, 측정 원본은 각 결과 기록에 있다. 이 문서는 어떻게 만들었는지와 기록에 남은 이유만 적는다. 대괄호는 2026-11-25에 로드맵에서 뺀 예전 [로드맵](../roadmap.md) 번호다.

- [공통 원칙](#공통-원칙)
- [씬 편집과 프리팹](#씬-편집과-프리팹) [3.1, 7.2, 10.4, 11.11]
- [프로젝트와 에셋](#프로젝트와-에셋) [3.2, 6.2, 8.2]
- [Play 진단과 프로파일러](#play-진단과-프로파일러) [3.3, 9.1, 10.3]
- [작업 보존과 Undo 기록](#작업-보존과-undo-기록) [3.4, 13.1]
- [외부 IDE 디버깅](#외부-ide-디버깅) [6.3]
- [프로젝트 에디터 확장](#프로젝트-에디터-확장) [11.10, 13.13]
- [도킹 레이아웃과 패널 디자인](#도킹-레이아웃과-패널-디자인) [12.1–12.7]
- [Inspector](#inspector) [7.1, 8.3, 13.2–13.17]

## 공통 원칙

- **에디터는 런타임을 참조하고, 반대는 없다.** 편집 UI와 모델은 `engine/src/Engine.Editor`(Avalonia), 빌드·임포트·스크립트 컴파일은 `Engine.Authoring`, 게임 실행은 `Engine.Core`·`Engine.Desktop`·`Engine.Player`에 있다. 결과 기록마다 Release·Native AOT 배포본에 Avalonia·SkiaSharp·에디터 파일이 없는지 검사했다. 이유: AGENTS.md의 에디터·런타임 경계 규칙.
- **편집 상태는 씬 파일 텍스트가 기준이다.** Undo, 복구, 병합, 복사·붙여넣기, 프리팹 비교가 모두 씬 YAML(`SceneDocument`) 표현을 쓴다. 그래서 같은 직렬화 규칙 하나로 바이트 비교 검사를 할 수 있다.
- **UI 없는 편집 모델.** 다중 편집·컴포넌트 메뉴·컬렉션 필드 편집은 `Engine.Editor/Model/`(`MultiEdits.cs`, `ComponentEdits.cs`, `ScriptValueEdits.cs` 등)에 UI 없이 두어 `Engine.Checks`가 Undo 기록까지 직접 검사한다.
- **사용자별 상태는 프로젝트 밖에 둔다.** 레이아웃(`editor-layout.json`), 숨김·잠금·접기·Inspector 잠금(`editor-state.json`)은 사용자 폴더에 저장한다(`MONOON_USER_DATA`로 바꿀 수 있다). 프로젝트 폴더와 게임 빌드가 바뀌지 않는다.
- **에디터의 리플렉션은 에디터 안에서만.** Play 중 스크립트 값 표시·편집은 에디터가 리플렉션으로 읽는다(`Debugging/SceneDebugValues.cs`, `Inspector/MainWindow.PlayInspector.cs`). 게임 쪽 필드 연결은 스크립트 컴파일 때 생성하는 `ProjectScriptFactory`(`Engine.Authoring/ProjectScripts.cs`)가 한다. 이유: AGENTS.md의 리플렉션 대신 소스 생성 규칙.
- **정렬 규칙.** Inspector 행은 레이블 열 56px + 간격 4px(`EditorFieldLabelWidth`·`EditorFieldGap`)이다. 새 표시(오버라이드 막대, 경고 줄, 드래그 테두리)는 이 열을 밀지 않도록 여백 안이나 테두리 색으로만 그린다. 이유: AGENTS.md의 에디터 UI 정렬 규칙.
- **플랫폼.** Mac Scene은 패치한 SDL 3.4.12를 Cocoa NSView로 넣어 그리고, Windows Scene은 Avalonia가 그린다. 12장 일부는 Windows·Linux x64 QEMU 게스트의 패키지 에디터에서도 확인했지만, 2026-11-23에 Linux 에디터 지원을 하지 않기로 했다(사용자 결정). Windows 검증은 장비 준비 뒤로 보류했다.

## 씬 편집과 프리팹

**다중 선택·기즈모 [3.1].** 기즈모는 `Scene/SceneViewport.Gizmos.cs`가 그리고 처리한다. Snap 단위는 이동 16, 회전 15°(π/12), 크기 10%다. 그룹은 선택 영역 중심을 기준으로 바꾸고, 드래그 하나가 Undo 하나다. 프리팹 오버라이드·중첩·변형은 `Engine.Core`의 `Prefabs.cs`, `PrefabProperties.cs`, `PrefabMerge.cs`가 처리한다. 프리팹 기준 상태는 씬 YAML 표현으로 비교한다. 컴포넌트 판별자, 스크립트 필드, 임포트 설정이 에셋과 같은 뜻을 갖게 하려는 것이다(`PrefabMerge.cs` 주석). [검증](../../engine/tests/results/scene-editing/README.md) · [사용법](../manual/editor.md#다중-선택과-기즈모)

**프리팹 원본 편집 [7.2].** `.prefab`을 더블클릭하면 `MainWindow`를 하나 더 만들어 그 프리팹(`SceneDocument.OpenPrefabSource`)만 연다. Hierarchy·Inspector를 새로 만들지 않고 기존 에디터를 그대로 쓴다. 저장은 `RecordPrefabAssetChange`가 원본 파일과 씬을 Undo 한 단계로 묶고(`Model/PrefabFileEdit.cs`), 나중에 생긴 외부 수정은 지우지 않는다. 속성별 Apply·Revert는 `SceneDocument.EditPrefabProperty`다. [검증](../../engine/tests/results/prefab-source/README.md) · [사용법](../manual/editor.md#프리팹-원본과-속성별-오버라이드)

**구조 변경의 개별 적용 [10.4].** 추가·삭제한 자식과 컴포넌트를 `StructuralOverride`(`Engine.Core/PrefabStructure.cs`)로 한 행씩 만들고, `EditPrefabStructure`가 그 행만 원본에 적용하거나 인스턴스에서 되돌린다. 삭제한 자식은 원래 ID와 부모로 복원한다. 남은 자식이 다른 곳에 있거나 씬 전용 참조를 가진 경우는 적용을 거부한다. macOS arm64와 Windows·Linux x64 게스트의 에디터·Release·Native AOT에서 결과가 같았다. [검증](../../engine/tests/results/prefab-structure/README.md) · [사용법](../manual/editor.md#자식컴포넌트-추가와-삭제의-개별-적용)

**복사·붙여넣기 [11.11].** Duplicate의 본문을 `SceneDocument.CopyObjects`(`Engine.Core/SceneDuplication.cs`)로 분리해 Duplicate와 Paste가 같은 경로로 새 ID, 내부 참조, 프리팹 연결(`RemapLink`), 스크립트 필드를 다시 연결한다. 클립보드에는 선택한 하위 트리만 담은 씬 YAML에 `# MoNoon objects` 첫 줄을 붙여 쓴다. 다른 프로세스에서도 읽히게 하려는 것이다. 붙여 넣을 텍스트는 씬 파일과 같은 `Validate`를 통과해야 한다. 끊긴 참조는 기존 `Missing object or component` 표시와 빌드 거부 검사를 그대로 쓴다. 실제 ⌘C·⌘V 키 입력은 확인하지 않았다. [검증](../../engine/tests/results/copy-paste/README.md) · [사용법](../manual/editor.md#오브젝트와-프리팹)

## 프로젝트와 에셋

**검색·임포트·의존성·테이블 [3.2].** 검색과 `t:` 종류(`sprite`, `script`, `table` 등), 양방향 의존성, 누락 참조 검사는 `Engine.Authoring/ProjectCatalog.cs`, 여러 파일 가져오기는 `ProjectImport.cs`다. 가져오기는 충돌이나 읽을 수 없는 파일이 하나라도 있으면 아무것도 복사하지 않는다. 데이터 에셋·테이블 편집기는 `Panels/ValuesWindow.cs`이고 형식은 Text·Number·Boolean·YAML이다. 테이블은 Inspector 필드로 연결된 경우에만 게임 빌드에 들어간다(씬 도달성). [검증](../../engine/tests/results/project-assets/README.md) · [사용법](../manual/project-assets.md)

**참조를 보존하는 이동·이름 변경 [6.2].** `Model/ProjectPathEdit.cs`의 기록 하나가 파일 이동과 영향을 받는 모든 직렬화 참조를 함께 갖는다. 변경을 먼저 준비한 뒤 적용하고, Undo 때는 현재 문서를 다시 읽어 이동 뒤 저장한 다른 필드를 덮어쓰지 않는다. 대소문자만 바꾸는 이름 변경은 중간 경로를 거쳐 대소문자를 구분하지 않는 볼륨에서도 동작한다. C# 파일은 `.cs.meta`에 스크립트 ID를 두어 이동·이름 변경 뒤에도 컴포넌트가 유지된다. [검증](../../engine/tests/results/project-moves/README.md) · [사용법](../manual/project-assets.md#에셋-이동과-이름-변경)

**외부 PNG 자동 재임포트 [8.2].** `Project/ProjectSpriteMonitor.cs`가 파일 감시와 창 포커스 복귀 때 디스크를 확인한다. 파일 감시는 저장 한 번에 이벤트를 여러 번 보내거나 앱이 멈춘 동안 이벤트를 잃을 수 있어서다(파일 주석). 시트 `.meta`는 셀마다 원본 PNG의 GUID와 마지막으로 가져온 내용 해시를 가진다. 원본이 바뀌면 그 셀의 픽셀만 다시 쓰고 클립의 프레임 순서·시간은 건드리지 않는다. 셀보다 커진 원본은 거부한다. macOS arm64와 Windows·Linux x64 배포 에디터에서 검증 클립의 SHA-256이 같았다. [검증](../../engine/tests/results/external-sprite-reimport/README.md) · [사용법](../manual/project-assets.md#외부-png-자동-재임포트)

## Play 진단과 프로파일러

**실행 진단 [3.3].** 충돌체·질의 기록은 `Engine.Core/PlayDiagnostics.cs`의 `PhysicsDiagnostics`다. 고정 배열에 최근 64개 질의를 담고 60틱 동안 표시하며, 끄면 기록을 비운다. 콘솔은 `GameSession.Log` → 에디터 Console(최근 1,000개)이고, 배포 플레이어에서는 같은 로그를 `Brutal.Logging`으로 표준 출력·오류에 쓴다. Runtime 섹션의 스크립트 값은 에디터가 읽기 전용으로 표시한다. [검증](../../engine/tests/results/play-diagnostics/README.md) · [사용법](../manual/play-diagnostics.md)

**Play Inspector와 선택 적용 [9.1, 13.11].** `Inspector/MainWindow.PlayInspector.cs`가 실행 중인 월드에 값을 바로 넣고, 저작 값과 다른 값을 "Play 편집"으로 모은다. **Apply selected**는 선택한 오브젝트에서 체크한 값, **Apply all**은 이번 Play의 모든 편집을 원본 씬에 Undo 한 번으로 옮긴다. 적용하지 않은 값은 Stop에서 버린다. 로컬 Transform은 같은 부모 공간에서만 같은 배치를 뜻하므로 부모가 같을 때만 적용한다. 13.11에서 bool·enum·Vector2·string·색 필드와 Active·Tint·충돌체 켜짐을 더했다. [9.1 검증](../../engine/tests/results/play-inspector/README.md) · [13.11 검증](../../engine/tests/results/inspector-fields/README.md) · [사용법](../manual/play-diagnostics.md#play-중-값-조정과-선택-적용)

**느린 프레임 작업 분석 [10.3].** 측정은 런타임의 `Engine.Core/FrameProfiler.cs`다. 프레임당 노드 256개·깊이 32의 고정 배열에 `Stopwatch` 타임스탬프를 적어 기록 중 관리 할당이 없다(샘플 2만 개·CSV 50프레임에서 0 B). 프로파일러가 설치되지 않으면 측정 지점 비용은 null 확인 한 번이다. 개발용 빌드(`--profile development`)에서만 `MONOON_FRAME_PROFILE` CSV를 쓰고(`FrameProfileWriter.cs`), 표는 `monoon profile`(`Engine.Cli/FrameProfileReport.cs`)과 에디터 Profiler(`Debugging/PlayProfiler.cs`, 180프레임)가 같은 데이터로 그린다. CollisionLab 1만 개 씬에서 질의 프레임의 겹침·레이캐스트·스윕이 각 1,344회로 세 실행 경로에서 같았다. 씬 전환 프레임의 에디터 시간이 커서, 이후 Play Hierarchy는 오브젝트 추가·삭제·부모·이름이 바뀔 때만 다시 만든다. [검증](../../engine/tests/results/frame-profiler/README.md) · [에디터 측정](../../engine/tests/results/perf-editor/README.md) · [사용법](../manual/play-diagnostics.md#느린-프레임의-작업-분석)

## 작업 보존과 Undo 기록

**복구·병합·백업·패키지 [3.4].**
- 복구: `Model/SceneRecovery.cs`. 편집 세션마다 `.monoon/recovery/`에 초안을 두고, 열린 임대(lease)로 살아 있는 에디터와 비정상 종료한 에디터를 구분한다. 기록은 확정된 편집을 450 ms 타이머로 모아 쓴다(`Shell/MainWindow.Workspace.cs`).
- 병합: `Model/SceneMerge.cs`. 처음 연 상태·현재 편집·디스크의 3방향 비교다. 오브젝트는 ID, 컴포넌트는 종류와 스크립트 이름으로 대응시킨다. 이전 형식 씬도 편집과 같은 리더로 정규화한 뒤 비교한다. 한쪽에서 추가한 오브젝트는 원래 이웃 옆에 넣어 형제 순서를 지킨다.
- 마이그레이션 백업: `Engine.Authoring/MigrationBackup.cs`가 변환 전 원본과 메타데이터를 `.monoon/migrations/`에 ZIP으로 남긴다.
- 패키지·템플릿: `Model/WorkspaceArchives.cs`. `.monopackage`·`.monotemplate`이고, 경로·에셋 ID가 충돌하면 가져오기를 멈춘다. `Editor/` 확장은 스크립트 메타데이터 없이 함께 담긴다.
- [검증](../../engine/tests/results/workspace/README.md) · [사용법](../manual/workspace.md)

**입력 확정 시 기록과 오브젝트 단위 차분 [13.1].**
- 이전 상태: 키 입력마다 씬 전체를 스냅샷했다. 1만 오브젝트에서 키 입력당 14.4 ms·32.5 MB였다([조사 I-1](../plans/next-work/1-engine-inspector.md)).
- 1차(I-1): 텍스트·숫자 필드는 값을 바로 반영하고 Enter·포커스 이동·저장·Undo·Play 때 한 번만 기록한다. 이때 `GotFocus`를 Tunnel로 등록해 한 번도 불리지 않던 결함을 고쳤다. Avalonia의 `GotFocus`는 Bubble로만 오기 때문이다. [I-1 기록](../../engine/tests/results/inspector-i1/README.md)
- 2차: `Model/SceneHistory.cs`. 상태를 씬 YAML의 머리·오브젝트 항목·꼬리로 자르고, 바뀌지 않은 항목 배열은 참조로 공유한다. Undo·Redo는 열린 씬과 다른 항목만 디코딩한다. 계획의 결정 7은 "확정 비용이 50 ms를 넘을 때만 차분 기록"이었고 I-1 뒤 확정 비용은 50 ms 아래였지만, 사용자 결정으로 2차까지 진행했다. 구간 단위(splice) 기록도 가능했으나 100단계에 약 8 MB라 단순한 오브젝트 단위를 택했다. `SceneObject`에 변경 추적이 없어 확정마다 한 번 직렬화하는 비용(약 15 ms)은 남는다.
- 측정(Field10000): 키 입력 18.5 ms·34.7 MB → 3.7 ms·0.1 MB, Undo 100단계 보관 1,065 → 13 MB. 무작위 혼합 편집 검사가 모든 단계에서 Undo·Redo 바이트 일치를 확인한다. [검증](../../engine/tests/results/inspector-undo-diff/README.md) · [사용법](../manual/editor.md#저장과-단축키)

## 외부 IDE 디버깅

[6.3] **Project → Open C# Project…**가 `.monoon/ide/`에 게임별 솔루션과 VS Code 설정을 만든다(`Project/ScriptIdeProject.cs`). 중단점은 MoNoon 에디터가 여는 로컬 Debug Adapter Protocol 세션(`Debugging/SceneDebugServer.cs`)으로 처리한다. Scene Play용 컴파일에서만 Roslyn 재작성기(`Model/SceneDebugInstrumentation.cs`)가 각 문장 앞에 `SceneScriptBreakpoint` 확인을 넣는다. 줄바꿈을 더하지 않아 스택 추적의 줄 번호는 원본과 같다. 배포용 로더는 이 재작성 없이 패키지 DLL을 실행한다. 기록된 배경: 이 Mac(macOS 27)에서 VS Code `vsdbg-ui`와 Rider의 기본 .NET PID 연결이 `libdbgshim.dylib`에서 종료됐다. [검증](../../engine/tests/results/ide-debugging/README.md) · [사용법](../manual/scripting.md#외부-ide로-scene-play-디버깅)

## 프로젝트 에디터 확장

**`Editor/` 폴더의 C# 확장 [11.10].** `Shell/EditorExtensions.cs`. 프로젝트 `Editor/**/*.cs`를 게임 스크립트와 같은 Roslyn 컴파일 경로로 별도 어셈블리로 만들어 언로드 가능한 컨텍스트에 올리고, `IEditorExtension.Register`를 부른다. 다시 불러올 때는 메뉴·패널을 지우고 컨텍스트를 Unload한다. BRUTAL에 에디터 확장 기능이 없어 기존 Roslyn 컴파일을 재사용했다. `ProjectScripts.Locations`가 `Tests/`와 함께 `Editor/`를 건너뛰므로 Scene Play·Release·Native AOT·`monoon test project`에 들어가지 않는다. 이전의 YAML 선언 `Editor/*.tools`(KeyRoom Place Coin)는 이 C# 확장으로 옮기고 처리 코드를 지웠다. 그 대가로 프리팹을 옮겨도 확장 코드의 경로 문자열은 자동으로 바뀌지 않는다. [검증](../../engine/tests/results/editor-extensions/README.md) · [Linux 게스트 기록](../../engine/tests/results/chapter11-linux-x64/README.md) · [사용법](../manual/workspace.md#프로젝트-에디터-확장)

**API 보강 [13.13].** `EditorContext.RecordEdit()`는 확장이 직접 바꾼 씬을 차분 기록(13.1)에 한 단계로 남긴다. 무엇이 바뀌었는지는 기록이 찾으므로 확장이 변경 목록을 넘기지 않는다. `SetField`는 기본값과 같으면 저장 키를 지운다. `AddFieldDrawer`는 속성 이름 또는 C# 타입으로 값 칸만 바꾸고, 레이블·오버라이드 표시·행 메뉴는 기본 행을 그대로 쓴다. KeyRoom의 `[InputAction]` drawer가 예제다. 속성은 `[Conditional("MONOON_EDITOR")]`라 게임 어셈블리에 적용 데이터가 없다. [검증](../../engine/tests/results/inspector-fields/README.md)

## 도킹 레이아웃과 패널 디자인

**도킹 기반 [12.1–12.3].**
- Dock.Avalonia 11.3.12.1을 받아 확인했으나 채택하지 않았다. 컨트롤 템플릿이 별도 테마 패키지에 있었고 그 패키지는 승인된 내려받기에 없었다. 템플릿 20여 개를 직접 쓰는 것보다 직접 구현이 코드가 적고 Scene 뷰 문제도 피할 수 있다고 판단했다. 추가한 패키지는 없다.
- `Docking/DockHost.cs`·`DockNode.cs`: Grid·분할선·탭 줄로 만든 트리(행·열·탭 묶음)다. 패널은 평생 `DockHost`의 자식으로 남고 위치만 옮긴다. 다시 부모를 바꾸면 Mac Scene 뷰(SDL NSView)와 Play 세션이 매번 떨어지기 때문이다. 플로팅 창도 패널 컨트롤만 창으로 옮겼다가 돌려 놓는다.
- 끄는 동안 놓일 자리는 팝업 창으로 그린다. 네이티브 Scene 뷰가 메인 창 안의 Avalonia 컨트롤을 덮기 때문이다. 창 가장자리 16px은 전체 분할이다.
- 최소 크기(Inspector 218px, Hierarchy·Project 140px, Scene 240px)로 Inspector 레이블·입력 열이 창 크기와 무관하게 유지된다. 레이아웃은 `editor-layout.json`에 저장하고, 화면보다 큰 저장값은 기본 배치·크기(1220×740)로 되돌린다.
- [검증](../../engine/tests/results/editor-docking/README.md) · [사용법](../manual/editor.md#패널-배치와-레이아웃)

**도구 창의 패널화 [12.4].** 예전 `Window` 클래스들이 `Docking/EditorPanel.cs`(UserControl)를 상속하도록 바꿨다. EditorPanel은 기존 코드가 쓰던 Title, `Close(result)`, 창 이벤트를 그대로 제공해 편집기 코드를 거의 고치지 않았다. 모달(`ShowDialog`)을 없애 편집기가 열린 동안에도 메뉴와 Play를 쓸 수 있다. 에셋 종류마다 패널 하나로 정했다. 예전 클립 편집기가 이미 한 번에 하나였고, 패널 이름이 고정되면 레이아웃 저장·Window 메뉴·플로팅 위치 복원이 단순해진다. [검증](../../engine/tests/results/editor-docking/README.md) · [사용법](../manual/editor.md#도구-패널)

**Scene·Game 탭 [12.5].** Game 탭은 두 번째 렌더러를 만들지 않고 Play·Stop 때 같은 `SceneViewport`를 Scene 패널과 Game 패널 사이에서 옮긴다. 세션·입력·프로파일러·디버그 그리기가 모두 이 뷰에 있고, 두 번째 호스트는 SDL NSView 렌더러를 하나 더 만들어 모든 스프라이트를 다시 올리고 세션을 넘겨야 하기 때문이다. 그래서 Play 중 Scene과 Game을 나란히 볼 수는 없다. 해상도 목록은 320×180부터 1920×1080까지 다섯 개다(`Scene/MainWindow.GameView.cs`). [사용법](../manual/editor.md#scene과-game-탭-도구-모음)

**디자인 토큰과 아이콘 [12.6].** 색·글자 크기·간격(4px 단위)·모서리·선 두께를 `EditorStyles.axaml` 토큰으로 모으고, 코드로 만드는 행은 `EditorTokens.cs`로 같은 값을 읽는다. 아이콘은 저장소 안의 16px 벡터 경로(`EditorIcons.axaml`)다. 에디터 용량을 늘리지 않도록 Inter·Pretendard는 넣지 않고 OS 시스템 글꼴을 쓴다. 12px 글자도 기존 56px 레이블 열에 들어가 열을 넓히지 않았다. 축 레이블 열만 9px에서 12px로 넓혔다. 게임 런타임 소스는 바뀌지 않았다. [검증](../../engine/tests/results/editor-theme/README.md) · [사용법](../manual/editor.md#아이콘과-툴팁)

**패널별 세부 디자인 [12.7].**
- Hierarchy 숨기기·잠그기는 씬 파일이 아니라 `editor-state.json`에 씬 경로별 ID 목록으로 저장한다. 게임 빌드가 프로젝트 폴더만 읽으므로 빌드 도구를 고칠 필요가 없고, 상태 변경 전후 Release 빌드 269개 파일이 같았다. 다른 사용자와 공유되지 않고, 씬 이름을 바꾸면 상태가 사라진다.
- 레이블·입력 간격을 4px 격자에 맞춰 5px에서 4px로 바꿨다(열 60 = 56 + 4).
- Inspector 제목 줄을 없애고, 여러 선택·Play·프리팹 인스턴스일 때만 28px 작업 줄을 보인다. Play 중에는 작업 줄이 계속 있어 Apply 버튼 상태가 바뀌어도 행이 움직이지 않는다.
- 당시 런타임 컴포넌트에 켜짐 속성이 없어 컴포넌트 활성 토글은 넣지 않았다. 이 토글은 13.2에서 런타임 속성과 함께 추가했다.
- [검증](../../engine/tests/results/editor-panels/README.md) · [사용법](../manual/editor.md#네-패널에서-하는-일)

## Inspector

조사와 결정은 [Inspector 조사](../plans/next-work/1-engine-inspector.md)와 [다음 작업 결정표](../plans/next-work/README.md)(2026-11-24 사용자 확정)에 있다.

**참조 목록·enum·Vector2 [7.1].** `Inspector/MainWindow.ReferenceLists.cs`가 오브젝트·에셋 참조 배열을 드래그 추가·정렬·제거하는 행을 만든다. 13.8의 기본 타입 배열·목록도 같은 배치(번호·입력·×)를 쓴다. [InventoryLab 검증](../../engine/tests/results/script-reference-lists/README.md) · [KeyRoom 검증](../../engine/tests/results/script-asset-fields/README.md) · [사용법](../manual/scripting.md#에셋-참조-목록과-enum-vector2)

**다중 편집 [8.3, 13.5].** 8.3은 충돌 마스크를 비트별로 혼합 표시·편집했다(`Model/CollisionMaskSelection.cs`). 13.5는 `Model/MultiEdits.cs`로 넓혔다. 필드는 프리팹 오버라이드 목록과 같은 저장 JSON 경로(`PrefabProperty`: 컴포넌트 키·그룹·필드)이고, 선택한 각 오브젝트의 그 키 하나만 다시 쓴 뒤 컴포넌트를 디코딩한다. 그래서 프리팹 인스턴스에는 하나씩 고쳤을 때와 같은 오버라이드가 생긴다. 확정 하나가 Undo 하나다. [8.3 검증](../../engine/tests/results/inspector-multi-edit/README.md) · [13.5 검증](../../engine/tests/results/inspector-edit/README.md) · [사용법](../manual/editor.md#inspector에서-여러-오브젝트-함께-편집하기)

**오브젝트 활성·컴포넌트 켜짐 [13.2].** `SceneObject.Active`·`SceneComponent.Enabled`를 추가하고 끈 경우만 파일에 쓴다. 결정 8 "기본값 true는 쓰지 않는다"에 따라 기존 씬·프리팹 50개는 바이트가 그대로다. 끈 항목이 있는 씬만 콘텐츠 버전 32로 저장해 이전 플레이어가 잘못 읽지 않게 했다. 꺼진 컴포넌트는 월드에서 빠지지만 충돌체는 스크립트가 다시 켤 수 있게 꺼진 채 남긴다. Rigidbody 2D·Tilemap·Camera 2D·UI Rect·UI Canvas는 `CanDisable`이 false다. 활성·켜짐 trace가 Scene Play·Release·Native AOT에서 바이트 단위로 같았다. 충돌체 외 컴포넌트를 실행 중에 켜고 끄는 API는 없다. [검증](../../engine/tests/results/inspector-components/README.md) · [사용법](../manual/editor.md#오브젝트-활성과-컴포넌트-켜짐)

**필드 속성과 툴팁 [13.3].** `Tooltip`·`Range`·`Min`·`Header`·`Space`·`Multiline`·`HideInInspector`·`SerializeField`를 Unity와 같은 이름으로 `Engine.Core/InspectorAttributes.cs`에 두었다. Unity 코드를 옮기기 쉽고, 10.1의 `FormerlySerializedAs`가 이미 같은 이름이라서다(결정 2·8). 속성은 `[Conditional("MONOON_EDITOR")]`라 게임 어셈블리에 적용 데이터가 0 B다. Roslyn은 이 속성을 원본 심볼의 `GetAttributes()`에 남기므로 에디터는 별도 메타데이터 없이 원본에서 읽는다(`Engine.Authoring/ProjectScripts.cs`의 `Presentation` → `ScriptInspectorInfo`). private `[SerializeField]` 필드는 생성 팩토리가 `UnsafeAccessor`로 채워 리플렉션이 없다. [검증](../../engine/tests/results/inspector-components/README.md) · [사용법](../manual/scripting.md#inspector-필드-속성과-툴팁)

**컴포넌트 메뉴 [13.4].** 검색 상자와 분류 7개(`Inspector/ComponentPicker.cs`), 톱니 메뉴의 Reset·Copy·Paste Values·Paste as New·Move Up/Down·Remove(`Model/ComponentEdits.cs`)다. 복사본은 씬 파일의 컴포넌트 YAML 항목 그대로다. 명령마다 Undo 한 번이고 차분 Undo 무작위 검사에 포함했다. [검증](../../engine/tests/results/inspector-components/README.md) · [사용법](../manual/editor.md#컴포넌트-메뉴)

**오버라이드 인라인 표시 [13.6].** `Scene.PropertyOverrides(root)`를 Inspector를 만들 때와 기록을 확정할 때만 계산한다(타이핑 중에는 하지 않음). 점과 2px 막대는 패널 안쪽 여백 8px 안에 음수 여백으로 그려 값 열이 움직이지 않는다(1280·1600·1920px 확인). 행 우클릭의 Revert·Apply to Source는 **Overrides…** 창과 같은 명령이다. [검증](../../engine/tests/results/inspector-edit/README.md) · [사용법](../manual/editor.md#inspector의-오버라이드-표시)

**참조 필드와 에셋 드래그 놓기 [13.7, 13.17].** 모든 참조 필드를 `Panels/ReferenceField.cs` 한 줄 컨트롤로 바꿨다. 놓기는 기존 `ReferenceDrop`을 재사용하고, 형식이 다르면 테두리 색만 바꿔 주변 행을 밀지 않는다. Finder 파일은 기존 가져오기 경로로 `Assets/`에 복사한 뒤 연결한다. Ping은 대상 행을 1초 동안 강조한다. Field10000 피커 검색 중앙값은 3–7 ms였다. 글꼴 참조(UI Defaults 창)는 범위 밖이다. [검증](../../engine/tests/results/inspector-assets/README.md) · [사용법](../manual/editor.md#에셋오브젝트-필드)

**스크립트 필드 타입 확장 [13.8].** `long`·`uint`·`Color`·`[ColorUsage]` 문자열·플래그 enum·기본 타입 배열·`List<T>`·`Dictionary<string|int, T>`·한 단계 `[Serializable]` 구조체를 더했다. 생성 팩토리가 `Engine.Core/ScriptValues.cs`로 기존 `values` JSON을 읽어 저장 형식은 그대로다. 계획 문서는 "구조체 1단계·배열까지"를 제안했지만 사용자 결정 8로 `List<T>`·사전까지 넣었다. 구조체 깊이는 한 단계다. 값 형식 제네릭은 게임이 쓰는 원소 타입마다 컴파일되므로 Native AOT 비용은 게임 선택에 비례한다. 엔진만으로는 0 B였고, 모든 종류를 쓴 InventoryLab이 +33 KB(+0.40%)였다. [검증](../../engine/tests/results/inspector-fields/README.md) · [사용법](../manual/editor.md#스크립트-배열목록사전구조체-필드)

**색 피커 [13.9].** 결정 9에 따라 `Avalonia.Controls.ColorPicker` 패키지를 에디터에만 쓴다. 14.7 파티클 패널에서 먼저 들여와 에디터 패키지가 약 630 KB 늘었고, 13.9는 hex 전용 색 필드 8곳을 같은 `Panels/ColorField.cs`로 바꿨다. 13.7·13.9·13.15·13.17 묶음의 증가는 `MoNoon.dll` +24,576 B다. 게임 배포본에는 영향이 없다. [검증](../../engine/tests/results/inspector-assets/README.md) · [사용법](../manual/editor.md#색-필드)

**숫자 레이블 끌기와 Range [13.10].** `Inspector/MainWindow.Scrub.cs`. 1px에 한 단계, Range·Min·Max에서 멈춘다. 끄는 동안은 묶어 두고 놓을 때 Undo 한 번이다. Shift/Alt 배율은 없다. [검증](../../engine/tests/results/inspector-edit/README.md) · [사용법](../manual/editor.md#숫자-레이블-끌기와-range-슬라이더)

**잠금·Debug 보기·경고 줄 [13.12, 13.14].** 잠금은 씬별로 `editor-state.json`에 저장한다. Debug 보기는 저장 키와 원시 JSON, private 필드와 초기화식을 읽기 전용으로 보인다. 둘 다 씬과 Release 빌드를 바꾸지 않는다. 경고 줄은 저장 때 쓰는 검사 규칙을 그대로 불러 "저장 때 거부될 값"을 미리 보이고, 편집·Undo 때 다시 계산한다. [검증](../../engine/tests/results/inspector-components/README.md) · [사용법](../manual/editor.md#inspector-잠금과-debug-보기)

**파티클 곡선·그라데이션 행 [13.15].** `Inspector/MainWindow.ParticleCurves.cs`가 14.7의 CurveField·GradientField를 재사용한다. 확정마다 `.particles`를 다시 읽어 그 항목만 바꿔 쓴다. 같은 에셋을 Particle 패널에서 고치고 있어도 덮어쓰지 않게 하려는 것이다. ParticleLab Curves 300틱 trace가 세 경로에서 같았다. [검증](../../engine/tests/results/inspector-assets/README.md) · [사용법](../manual/editor.md#파티클-곡선과-그라데이션)

**에셋 Inspector [13.16].** `Inspector/MainWindow.AssetInspector.cs`. PNG는 Pivot(`.meta`)과 Atlas 그룹을 바꾼다. 아틀라스 이동은 두 아틀라스를 다시 패킹해 여러 파일을 Undo 한 단계로 묶는다(`PrefabFileEdit`). PNG별 필터는 설정으로 만들지 않고 실제 값을 읽기 전용으로 보인다. 런타임에 PNG별 샘플링 정보가 없고, 만들려면 `Engine.Core`·`Engine.Desktop`과 내보내기 형식을 바꿔야 해서 "에디터 전용" 범위를 넘기 때문이다. 증가는 Release `Engine.Core.dll` +1.5 KB, Native AOT 0 B다. [검증](../../engine/tests/results/inspector-edit/README.md) · [사용법](../manual/editor.md#에셋-inspector)
