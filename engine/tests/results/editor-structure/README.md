# 에디터 코드 구조 정리 (동작·화면 변경 없음)

2026-11-23 · macOS 27.0.1 · Apple M4 Max · .NET SDK 10.0.101 · 기준 커밋 `b6f10d2`(wip/phase1). 측정 중 QEMU Debian x64 게스트가 실행 중이었다(시간 측정이 아니라 통과 여부와 픽셀 비교만 했다).

`engine/src/Engine.Editor`의 평평한 폴더 88개 파일을 주제별 폴더로 옮기고, 여러 형식이 섞인 파일과 큰 파일(`SceneViewport.cs` 946줄, `MainWindow.axaml.cs` 738줄)을 partial 파일로 나눴다. 이름 공간은 모두 `Engine.Editor` 그대로다. 코드 블록은 그대로 옮겼고, 이름 변경·새 오버로드·새 기능은 없다. 런타임(Engine.Core·Desktop·Player)과 배포본은 건드리지 않았다.

## 작업 단위와 커밋

| 단위 | 커밋 | 내용 |
| --- | --- | --- |
| WP1 | `0551f37` | `git mv`만으로 Shell/·Docking/·Scene/·Inspector/·Hierarchy/·Project/·Panels/·Debugging/·Model/로 이동. 스타일 axaml 3개, `EditorTokens.cs`, csproj, `MacScene.targets`, `Assets/`는 루트에 남겨 `avares://MoNoon/...` URI가 그대로다. `Engine.Checks.csproj`의 편집기 파일 링크 10개와 docs·결과 README 12개의 링크를 새 경로로 바꿨다. |
| WP2a | `68d6d5c` | 한 파일에 한 형식: `AnimatorWindow.cs`의 MainWindow 열기 코드 → `Panels/MainWindow.Animator.cs`, `ValuesWindow` → `Panels/ValuesWindow.cs`, `MainWindow.Tools.cs` → `MainWindow.InputActions.cs`, `DockZone`·`DockNode`·`FloatingPanel` → `Docking/DockNode.cs`, `FrameChart` → `Debugging/FrameChart.cs`, `AttachedComponent`·`SpriteFields` → `Inspector/ComponentRows.cs`. |
| WP2b | `79349ee` | `SceneViewport`를 `.Play`·`.Pointer`(카메라·드래그)·`.Render`(`PreviewText`·`PreviewCanvas` 포함)·`.UiPreview` partial로 나눴다. 초기화식이 있는 필드는 모두 `SceneViewport.cs`에 남겨 생성 순서가 같다. 옮긴 줄의 다중 집합이 원본과 같음을 스크립트로 확인했다. |
| WP3 | `0bef023` | `MainWindow.axaml.cs`(206줄)에는 필드, 생성자 연결, `RunAction`, `Save`·`CanLeave`(원본과 바이트 동일), 닫기, 제목만 남겼다. `Shell/MainWindow.PlayState.cs`(Play Inspector 동기화·`TogglePlay`·`SetControls`), `Shell/MainWindow.Scenes.cs`(프로젝트·씬 열기·만들기·전환, `RunGame`), `Hierarchy/MainWindow.Selection.cs`, `Hierarchy/MainWindow.ObjectEdits.cs`(추가·삭제·편집 기록), `Inspector/MainWindow.Inspector.cs`(Transform·컴포넌트 목록), `Shell/MainWindow.Shortcuts.cs`로 옮겼다. `FindSample`·`FirstScene`을 정적 `ProjectFolders`로 빼면 호출부를 바꿔야 하므로 만들지 않았다. |
| WP4 | `bbf5967` | `ToolPanels = ["Console", "Profiler", .. EditorKinds]`(같은 8개 ID, 정적 초기화 순서 때문에 `EditorKinds`를 먼저 선언). "former window/dialog" 주석을 현재 구조에 맞게 고쳤다. |
| WP5 | `867f8dd` | `extensionMenu`·`extensionItem`은 Extensions 메뉴에서 실제로 쓰이므로 지우지 않고 `MainWindow.Workspace.cs`에서 `MainWindow.Extensions.cs`로 옮겼다. |
| WP6 | `bdbc714` | Dialogue·Values·Animator 패널에 똑같이 들어 있던 `Box`·`Button`(`ActionButton`)·`Put`(`Place`)를 `Panels/EditorControls.cs` 하나로 합치고 `using static`으로 불러 호출부를 유지했다. 동작이 다른 InputMap의 `Button`·`Put`, 모양이 다른 저장 확인 대화 상자 3개와 Inspector 행 빌더는 그대로 뒀다. IDE0051·IDE0052를 경고로 켠 빌드(임시 `.editorconfig`, 커밋하지 않음)에서 사용하지 않는 private 멤버는 0개였다. 같은 설정에 넣은 일부러 안 쓰는 멤버는 둘 다 잡혀 분석기가 동작함을 확인했다. |

WP7(`*Window` 패널 이름 바꾸기)은 하지 않았다.

## 폴더별 줄 수

이전 값은 기준 커밋의 같은 파일을 새 폴더 배치로 셌다(.cs·.axaml).

| 폴더 | 이전 파일 | 이전 줄 | 이후 파일 | 이후 줄 |
| --- | ---: | ---: | ---: | ---: |
| 루트(스타일·토큰) | 4 | 689 | 4 | 689 |
| Shell | 11 | 2292 | 14 | 2040 |
| Docking | 3 | 944 | 4 | 946 |
| Scene | 9 | 1970 | 13 | 2018 |
| Inspector | 13 | 1660 | 15 | 1859 |
| Hierarchy | 3 | 694 | 5 | 795 |
| Project | 10 | 1444 | 10 | 1444 |
| Panels | 20 | 2628 | 23 | 2633 |
| Debugging | 5 | 677 | 6 | 679 |
| Model | 10 | 995 | 10 | 995 |
| 합계 | 88 | 13993 | 104 | 14098 |

늘어난 105줄은 새 partial 파일의 using·이름 공간·클래스 머리말이다. 가장 큰 파일은 이전 `SceneViewport.cs` 946, `MainWindow.axaml.cs` 738, `DockHost.cs` 581에서 이후 `Docking/DockHost.cs` 551, `Inspector/MainWindow.Presentation.cs` 463, `Shell/MainWindow.axaml` 458, `Panels/ClipEditorWindow.axaml.cs` 451, `Hierarchy/MainWindow.Hierarchy.cs` 416이다. `SceneViewport.cs`는 228줄, `MainWindow.axaml.cs`는 206줄이 됐다.

## 검사 훅이 닿는 이름

`engine/tests/*/StartupHook.cs` 27개의 문자열 리터럴 중 편집기 선언·`x:Name`·스타일 클래스·리소스 키와 같은 이름이다. 정리 전후 집합을 스크립트로 비교했고, 차이는 편집기 멤버가 아닌 두 문자열뿐이다: `Place`(EditorExtensions 샘플 Inspector의 행 레이블, 지운 것은 AnimatorWindow의 private `Place`)와 `panels`(EditorDocking의 단계 이름). 형식을 직접 참조하는 훅 27개는 정리 뒤 모두 다시 빌드됐다.

- MainWindow 메서드(인자 형식 없이 `GetMethod`): `Select` `ShowSelection` `FinishObjectChange` `Save` `SwitchScene` `RestoreEdit` `EditValues` `EditClip` `EditAnimator` `EditTileset` `EditInput` `SetProfiler` `ShowConsole` `EditPrefabSource` `EditUiPreview` `RebuildHierarchy` `OpenEntry` `RefreshExtensions` `AddObject` `AttachComponent` `WriteLog` `SaveLayout`, 정적 `LayoutPreset` `DockPanels`
- MainWindow 멤버: `Scene` `ScenePath` `history`(`CanUndo`) `clipEditor` `consoleRows`(`Level` `Message` `File` `Line`) `browserFolder`
- SceneViewport: `Scene` `game` `KeepSimulationActive` `Profile`(`Work`) `native`(MacSceneHost `canvas` `view`) `timer` `tileAssets` `previewPose` `Scale` `Origin` `Gizmo` `PlayWorld` `Selected` `HiddenIds` `PreviewUi` `EditCenter` `EditItems` `BeginDrag` `DragTo` `ReleaseAt` `EndDrag` `BeginGesture` `UpdateGesture` `CancelGesture` 중첩 `Grip` `RefreshScene` `IsPlaying` `Play` `Stop`
- DockHost: `Label` `PreviewRect` `preview` `Panels` `GetPanel` `IsOpen` `IsFloating` `FloatingWindow` `Apply` `Layout`, 형식 `DockNode` `DockZone`
- ClipEditorWindow `ChooseCells` `History` `Seek` `Close` `framePreview`, SheetGridView `RestoreSelection`, FrameChart `FrameSelected`, EditorUserState `Folds`
- 형식 이름 문자열: `MacSceneHost` `ComponentSection` `ClipEditorWindow` `TilesetWindow`
- `FindControl` 이름(모두 `MainWindow.axaml` 등 기존 이름 범위에 그대로): `AddButton` `ApplyPlayValues` `AssetSearch` `Cells` `DebugButton` `DeleteKey` `Dock` `FolderTree` `FolderTreePane` `FrameDebug` `FrameGraph` `GameHost` `GameResolution` `GameScale` `GameStats` `Hierarchy` `HierarchyNoMatch` `HierarchySearch` `Info` `Inspector` `InspectorActions` `InspectorCaption` `InspectorPanel` `KeyProperty` `KeyTime` `KeyValue` `MoveCostBox` `MoveTool` `ObjectGroups` `ObjectName` `ObjectTag` `OverridesButton` `ParentFolderButton` `PlayButton` `PointsBox` `PosX` `PositionRowLabel` `ProjectActions` `ProjectBrowser` `ProjectName` `ProjectPanel` `ProjectPath` `ProjectPathScroll` `ProjectViewToggle` `Properties` `RectTool` `ResolutionLabel` `RoleMatches` `RoleQuery` `RotateTool` `SaveAsset` `SaveButton` `ScaleTool` `SceneHost` `SecondsBox` `ShapeMode` `StageScroll` `StatsButton` `Status` `TerrainName` `Viewport` `WorkRows` `WorkTitle` `WriteKey`
- 스타일 클래스·리소스 키: `dockTab` `panelHeader` `hierarchyRow` `fieldLabel` `HeaderToggle`, `Editor*` 토큰(`EditorFontSize` 등), `Icon*` 키 38개, 접근성 이름 `▶  Play`

`MainWindow.axaml`은 나누지 않았다(이름 범위 유지).

## 검사 결과

정리 전 에디터는 기준 커밋을 따로 풀어 둔 사본에서 같은 스크립트로 만들었다. 각 하네스는 `editor.py`가 있는 23개 전부를 README의 준비 명령(`verify.py prepare`, `replay.py prepare`, 샘플 복사)과 함께 `gui-lock.sh`로 감싸 실행했다. 판정은 `editor.py` 종료 코드다(배포본 대조 단계는 이 정리의 범위 밖이라 실행하지 않았다).

| 검사 | 정리 전(b6f10d2) | 정리 후 |
| --- | --- | --- |
| 각 단위 뒤 에디터 빌드 + `./monoon test` | 통과(WP1 전 기준) | WP1·2a·2b·3·4+5·6 모두 `All checks passed.` |
| 훅 프로젝트 27개 빌드 | 통과 | 통과 |
| AudioLab.Vorbis `editor.py` | 통과 | 통과 |
| BarrageLab.Lines `editor.py` | 통과 | 통과 |
| CollisionLab.FrameProfiler `editor.py` | 통과 | 통과 |
| CollisionLab.SplitLoading `editor.py` | 통과 | 통과 |
| InventoryLab.RichText `editor.py` | 통과 | 통과 |
| KeyRoom.ComponentKeys `editor.py` | 통과 | 통과 |
| KeyRoom.CopyPaste `editor.py` | 통과 | 통과 |
| KeyRoom.EditorDocking `editor.py` (5단계) | 통과 | 통과 |
| KeyRoom.EditorExtensions `editor.py` | 통과 | 통과 |
| KeyRoom.EditorPanelsDetail `editor.py` | 통과 | 통과 |
| KeyRoom.EditorTheme `editor.py` | 통과 | 통과 |
| KeyRoom.FieldRename `editor.py` | 통과 | 통과 |
| KeyRoom.PrefabStructure `editor.py` | 통과 | 통과 |
| KeyRoom.Rebinding `editor.py` | 통과 | 통과 |
| KeyRoom.ScriptReload `editor.py` | 통과 | 통과 |
| KeyRoom.ScriptWaits `editor.py` | 통과 | 통과 |
| PlatformerLab.Joints `editor.py` | 통과 | 통과 |
| PlatformerLab.Paths `editor.py` | 통과 | 통과 |
| PlatformerLab.TileCollision `editor.py` | 통과 | 통과 |
| Samples.GameText `editor.py` (KeyRoom·PlatformerLab) | 통과 | 통과 |
| SeedLab.GridNavigation `editor.py` | 통과 | 통과 |
| SeedLab.TileTerrain `editor.py` | 통과 | 통과 |
| SpriteLab.Aseprite `editor.py` | 통과 | 통과 |

23개 모두 정리 전후 통과했다. 알려진 환경 실패(InventoryLab.Bundles `measure_mac.py`, KeyRoom.PlayInspector Scene Play)는 `editor.py` 하네스가 아니어서 이 표에 없다.

## 화면 비교

임시 훅(저장소에 넣지 않음)으로 KeyRoom 사본을 새 프로필로 열고 Door를 선택한 뒤 메인 창과 Inspector를 1280×800·1600×900·1920×1080에서 `RenderTargetBitmap`으로 저장했다.

- Inspector 3장: 정리 전·후 PNG 바이트 동일.
- 메인 창 3장: 처음 비교에서 Hierarchy 스크롤 막대 영역(x 156–171)만 달랐다. 정리 전 에디터를 정리 후 캡처 직후에 다시 찍자 정리 후와 바이트 동일했다. 정리 전 첫 촬영 때만 스크롤 막대가 펼친 상태였다(마우스 위치 등 화면 환경 차이로 본다). 같은 시점에 찍은 정리 전·후 6장은 모두 바이트 동일하다.
- KeyRoom.EditorTheme 하네스가 남긴 PNG 15장도 비교했다. 패널·대화 상자·Inspector 10장(animator, clip, data, dialogue, input-actions, inspector-1280·1920, sheet-picker, table, tileset)은 바이트 동일하다. 메인 창 3장은 위와 같은 스크롤 막대 영역만 달랐고, 정리 전 에디터로 하네스를 다시 돌리자 정리 후와 바이트 동일했다. console(로그 행 시각 영역)과 profiler-1280(실행 중 프레임 그래프)은 정리 전 에디터의 두 실행끼리도 달라서 비교 대상에서 뺐다.

## 확인하지 않은 것

- Windows·Linux x64 에디터(사용자 요청으로 Windows는 보류, Linux 게스트도 이번에 실행하지 않음).
- `editor.py`가 없는 훅 하네스(KeyRoom.PlayInspector·ObjectGroups·UiLayout, CameraLab.ScreenEffects, AudioLab.Widgets)와 InventoryLab.Bundles `measure_mac.py`. 이 둘은 손쉬운 사용·키 입력 권한이 없어 기준 커밋에서도 시간 초과가 알려져 있다.
- 각 하네스의 Release·Native AOT 재생과 대조 단계(편집기 코드가 들어가지 않으므로 생략).
- 파일 이동으로 MainWindow partial 사이 필드 초기화식의 실행 순서가 바뀌었다(컴파일러는 파일 순서를 따른다). 초기화식은 서로 독립된 객체 생성뿐이고 위 검사가 모두 같게 나왔지만, 별도로 증명하지는 않았다.
