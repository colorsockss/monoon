# Inspector 편집 확장 (13.5·13.6·13.10·13.16)

2026-11-24 · macOS 27.0 arm64 · Apple M4 Max · .NET SDK 10 · 기준 wip/next1 `5fd9bdb`에서 시작해 중간에 wip/next1 `f48fc00`(16.11·16.12·16.14), 마지막 커밋 전에 `d9d6dca`(16.5·16.9·16.10)를 병합한 이 브랜치. 실행 대상은 패키징한 Mac 에디터(`editor/MoNoon.app`, Release)의 Scene 편집과 Scene Play, KeyRoom의 Release와 Native AOT 배포본(`--profile distribution`)이다. 측정 시작과 끝에 `qemu-system` 프로세스는 없었다. 시간 비교는 하지 않았다(결정성 기준인 캡처 해시와 저장 바이트만 비교). 하네스: [KeyRoom.AssetFields `inspect` 단계](../../KeyRoom.AssetFields/README.md).

## 배포본 영향

13.5·13.6·13.10은 에디터 전용이다(`Engine.Editor`와 테스트만 바뀜). 13.16에서 런타임 쪽 변경은 PNG `.meta` 레코드(`AssetMeta`, `Engine.Core/ProjectYaml.cs`)에 nullable `PivotX`·`PivotY` 두 개를 더한 것 하나다. 기본값(null)은 YAML에 쓰지 않으므로 기존 `.meta` 파일은 그대로다. 게임은 이 값을 읽지 않는다(에디터가 오브젝트의 Transform Pivot으로 옮겨 씬에 저장한다).

| 항목 | wip/next1 `f48fc00` | 이 브랜치 | 차이 |
| --- | ---: | ---: | ---: |
| osx-arm64 플레이어 템플릿 `Engine.Core.dll`(Release 배포본에 그대로 들어감) | 1,546,240 | 1,547,776 | +1,536 B |
| KeyRoom Native AOT `MoNoonPlayer` | 8,653,728 | 8,653,728 | 0 |

- 비교 방법: `f48fc00`과 이 브랜치의 플레이어 소스 키로 만든 공유 캐시 템플릿(`7d808f8e48258433`·`ff1e11a11f70a920`, 두 키 사이의 런타임 소스 차이는 `ProjectYaml.cs` 한 파일)을 비교했다. AOT는 같은 체크아웃에서 `MONOON_PLAYERS_DIR`만 바꿔 KeyRoom을 두 번 빌드했다.
- 프레임 비용·시작 시간: 런타임 코드 경로는 바뀌지 않았다. `./monoon test`의 빌드 크기 기준(osx-arm64 배포 빌드 +1% 이내)을 통과했다.
- 배포본 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring): Release·Native AOT 모두 0개.
- 아틀라스 그룹은 기존 `.atlas`·`.monoon-atlas/` 경로를 그대로 써서, 고른 PNG의 아틀라스 시트가 Release·Native AOT에 들어간다(아래 검증).
- BRUTAL: Inspector UI, 프리팹 오버라이드, 아틀라스 패킹(에디터 SkiaSharp)은 [BRUTAL 기능 정리](../../../../docs/brutal.md)에 해당 기능이 없어 쓰지 않았다.

## 결정

- **다중 편집 경로(13.5)**: 값은 오버라이드 목록과 같은 `PrefabProperty` 키(컴포넌트 키 `boxCollider2D/`·`script/QaWall`, 묶음 `values`, 필드)로, 각 오브젝트의 컴포넌트 YAML에서 그 키만 바꾸고 다시 읽어 바꾼다(`Model/MultiEdits.cs`, Engine.Checks가 링크). 그래서 프리팹 인스턴스에는 한 개씩 고칠 때와 같은 오버라이드가 생기고, Undo(오브젝트 단위 차분)는 값과 오버라이드를 함께 되돌린다. Transform은 기존 단일 편집과 같은 속성(`Scene.Resize` 포함)을 오브젝트마다 바꾼다.
- **기본값 필드 표시**: 스냅샷에는 기본값 키가 없다(예: 모든 벽의 Is Trigger가 꺼져 있으면 키가 없음). 내장 컴포넌트는 형식마다 한 번, 새 인스턴스의 속성을 하나씩 바꿔 YAML에 생기는 키로 저장 이름을 찾는다(bool·int·float·double·string·enum, 캐시). 스크립트는 정의(`ScriptField`)를 쓴다. 마스크(uint)는 기존 8.3 버튼, 에셋 필드는 13.17 필드가 맡는다.
- **기록 시점(13.10)**: 레이블 끌기와 `[Range]` 슬라이더 끌기는 입력 중인 텍스트와 같은 "대기 중 편집"으로 묶고 포인터를 놓을 때 한 번 기록한다(`RecordEdit`이 끄는 동안 묶음을 끌기 객체로 바꾼다). 대기 중 편집은 이제 다중 선택도 기억한다. 파일을 쓰는 값(에셋 Inspector 피벗)은 끌기를 받지 않는다(`noScrub`).
- **오버라이드 계산(13.6)**: Inspector를 만들 때와 기록을 확정할 때마다 `Scene.PropertyOverrides(root)` 한 번(타이핑 중에는 하지 않음). 행 표시는 이름·Transform·스크립트 필드 행, 내장 컴포넌트는 머리말 점. 막대(2px)와 점은 패널 안쪽 여백 8px 안에 음수 여백으로 그린다. 스크립트 bool 행은 막대를 담으려고 `Panel`로 감쌌다(체크 위치는 그대로 x+61). 행 메뉴는 그 행의 필드만 **Overrides…** 창과 같은 `EditPrefabProperty`로 처리한다.
- **에셋 Inspector 진입(13.16)**: Project 행을 누를 때가 아니라 끌지 않고 놓을 때 바꾼다. 누를 때 바꾸면 Project에서 Inspector 필드로 끄는 13.17 흐름이 대상 필드를 잃는다. 오브젝트 선택(`ShowSelection`)이 Inspector를 되돌린다. 잠근 Inspector는 바뀌지 않는다.
- **피벗**: MoNoon의 피벗은 오브젝트 Transform 값(회전·배율 중심)이라, PNG 피벗은 그 PNG로 만드는 오브젝트의 기본값으로 `.meta`에 둔다. 바꾸면 열린 씬에서 그 PNG를 그리며 이전 기본값을 그대로 쓰는 오브젝트가 따라간다(`.meta` 기록과 씬 변경이 Undo 한 번, `PrefabFileEdit`). 다른 씬 파일은 고치지 않는다.
- **필터는 설정으로 두지 않았다**: 런타임에 PNG별 샘플링 정보가 없다. 전체 PNG는 SDL 기본(Integer scale이면 Point, 아니면 Linear), 시트 셀은 Point다(`SdlCanvas`). PNG별 필터는 `Engine.Core`·`Engine.Desktop`과 내보내기 형식을 바꾸는 런타임 기능이라 "에디터 전용" 행 범위를 넘어서, 현재 실제 값을 읽기 전용으로 보여 주고 이유를 툴팁에 적었다.
- **아틀라스 그룹**: None·기존 `.atlas`·New atlas. 떠나는 아틀라스와 들어가는 아틀라스를 `AtlasPacker`로 다시 패킹하고, 두 파일을 `ProjectFileEdits`로 Undo 한 번에 묶는다. 마지막 PNG를 빼는 것은 거부한다(빈 아틀라스는 패킹할 수 없고 아틀라스 삭제는 Project에서 한다).
- **씬 스키마**: 바꾸지 않았다(Inspector와 `.meta`만).

## 바꾼 것

| 커밋 | 파일 | 내용 |
| --- | --- | --- |
| 13.5 | `Engine.Editor/Model/MultiEdits.cs`(새 파일) | JSON 경로 쓰기·읽기, 내장 컴포넌트 필드와 저장 키 찾기 |
| 13.5 | `Engine.Editor/Inspector/MainWindow.MultiInspector.cs` | Transform·내장·스크립트 필드를 입력으로, `—`, 확정마다 기록 1번 |
| 13.5 | `Engine.Editor/Hierarchy/MainWindow.ObjectEdits.cs` | 대기 중 편집이 다중 선택을 기억 |
| 13.6 | `Engine.Editor/Inspector/MainWindow.Overrides.cs`(새 파일), `ComponentSection.cs`, `ComponentRows.cs`, `Shell/MainWindow.axaml`, `PanelStyles.axaml`, `MainWindow.Inspector.cs`, `Project/MainWindow.Scripts.cs` | 머리말 점(`IsOverridden`), 행 막대·굵은 레이블, 행 메뉴 Revert/Apply to Source, 선택·확정 때 계산 |
| 13.10 | `Engine.Editor/Inspector/MainWindow.Scrub.cs`(새 파일), `Shell/MainWindow.axaml.cs`, `MainWindow.ObjectEdits.cs`, `Project/MainWindow.Scripts.cs` | 레이블 끌기, 슬라이더 끌기를 놓을 때 기록, 숫자 상자 → 슬라이더 |
| 13.16 | `Engine.Editor/Inspector/MainWindow.AssetInspector.cs`(새 파일), `Shell/MainWindow.axaml`, `Hierarchy/MainWindow.Selection.cs`, `Project/MainWindow.ProjectFiles.cs`, `Inspector/MainWindow.Sprites.cs`, `Model/PrefabFileEdit.cs`(`ProjectFileEdits`), `Engine.Core/ProjectYaml.cs`(`AssetMeta` 피벗) | 에셋 요약·패널 버튼, PNG 가져오기 설정, Scene에 놓을 때 피벗 |
| 테스트 | `engine/tests/Engine.Checks/InspectorEditChecks.cs`(새 파일, `--only inspector-edit`), `HistoryChecks.cs`, `Program.cs`, `Engine.Checks.csproj` | 아래 검증 |
| 하네스 | `engine/tests/KeyRoom.AssetFields/InspectEdit.cs`(새 파일), `StartupHook.cs`, `verify.py`, `README.md` | `inspect` 단계, `Scripts/QaWall.cs` 준비 |
| 문서 | `docs/manual/editor.md` | 여러 오브젝트 편집, 오버라이드 표시, 레이블 끌기, 에셋 Inspector |

## 검증

| 검사 | Scene 편집 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- | --- |
| 13.5 WallWorkshop 벽 10개: Width 76 공통, Strength·X `—`, Is Trigger 반쯤 찬 체크. Width 80·Is Trigger·Strength 7이 각각 기록 1번, width·isTrigger가 벽마다 오버라이드. Undo 3번으로 씬 텍스트·벽별 오버라이드 목록 복원, Redo 3번 | 통과 | — | — | — |
| 13.5 정렬: 1280/1600/1920px에서 다중 선택 값 열 왼쪽 끝 1개(1130/1450/1770) | 통과 | — | — | — |
| 13.6 두 오크: Courtyard oak만 Size·Name 행 막대(x 1064, 폭 2, 내용 왼쪽 1070보다 왼쪽)·굵은 레이블, Transform 머리말 점. 표시 유무로 값 열 위치가 1280/1600/1920px 모두 같음 | 통과 | — | — | — |
| 13.6 Size 행 Revert: 102 × 98, 기록 1, 이름 오버라이드 남음, 막대 사라짐, Undo 1번 복원. Name 행 Apply to Source: `Oak.prefab` 기록 1, Undo 1번으로 파일 바이트·씬 복원 | 통과 | — | — | — |
| 13.10 MoveSpeed 레이블 60px → 240→300, 슬라이더 300, 기록 1, Undo/Redo. 400px → `[Range]` 480에서 멈춤. Door Position X 축 레이블 −15px → X−15, 기록 1 | 통과 | — | — | — |
| 13.16 Door.png 클릭: 24 × 94 px, 피벗 0·0, 값 열 왼쪽 끝 1개(세 너비). clip(Frames 4)·data(Fields 1)·prefab(Instances 2)·particles(max 256)·긴 이름 PNG(8 × 8)의 요약과 버튼, Open Clip Editor, 오브젝트 선택으로 복귀 | 통과 | — | — | — |
| 13.16 Import pivot 0.5/0.5: `.meta`와 Door(20° 회전) 피벗, 각 기록 1, Undo가 `.meta`의 `pivotY`와 씬을 함께 되돌림, Redo | 통과 | — | — | — |
| 13.16 Atlas → New atlas: `Door Atlas.atlas` 생성 기록 1, Undo로 `.atlas`·`.meta` 삭제, Redo | 통과 | — | — | — |
| 13.16 저장한 결과가 게임에 반영: 30·80프레임 캡처가 세 경로에서 바이트 동일(`1c8e8015…`, `507f8c1c…`), 피벗을 바꾸기 전 씬(`inspect-base.scene`)의 Release 캡처와는 둘 다 다름. Release·Native AOT에 `.monoon-atlas/606C3E0A….png` 포함 | — | 통과 | 통과 | 통과 |
| 배포본 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) | — | — | 0개 | 0개 |
| `Engine.Checks --only inspector-edit`: 필드 키 찾기, 벽 10개 JSON 경로 편집 3기록·Undo/Redo·오버라이드, Courtyard oak width만 Revert와 Undo, `.meta` 피벗이 재임포트 스캔을 지나도 남음, 두 파일 편집 Undo/Redo | 통과 | | | |
| `Engine.Checks --only history`: 무작위 편집에 다중 선택 필드 확정(JSON 경로), 행 Revert, 끌기 1기록을 더해 KeyRoom 90단계·Field10000 30단계 Undo/Redo 바이트 동일 | 통과 | | | |
| `./monoon test`(`d9d6dca` 병합 뒤 다시 실행: YAML 기본값 132종, 결정성 API 검사 222파일, 빌드 크기 osx-arm64 배포 6개 +1% 이내, HistoryChecks·InspectorEditChecks 포함) | 통과 (`All checks passed.`) | | | |
| 기존 에디터 하네스(`editor.py`, 아래 표) 36개 + AssetFields `edit`·`field`·`curves` + InspectorComponents Scene Play | 42/42 통과 | 통과 | | |

### 기존 에디터 하네스

`f48fc00` 병합 뒤의 에디터 패키지로 GUI 잠금 아래에서 하나씩 실행했다. `d9d6dca` 병합(Audio·Animator·Tileset 패널 변경) 뒤에는 `inspect` 단계와 KeyRoom.InspectorComponents `editor.py`를 다시 돌려 통과했다. 실패와 재시도는 없었다. 실행 시작과 끝에 QEMU 게스트는 없었다.

| 하네스 | 결과 | 비고 |
| --- | --- | --- |
| AudioLab.Vorbis, BarrageLab.Lines, CameraLab.DebugDraw·Framing, CollisionLab.FrameProfiler·SplitLoading, DisplayLab.IntegerScale, InventoryLab.RichText | 통과 | |
| CollisionLab.EditorPerf·InspectorEdits | 통과 | 후보 에디터 5회만 실행(기준 에디터와의 시간 비교는 하지 않음). InspectorEdits는 Field10000 저장 바이트가 모든 실행에서 같음 |
| KeyRoom.ComponentKeys·CopyPaste·EditorBuild·EditorDocking·EditorExtensions·EditorPanelsDetail·EditorTheme·FieldRename·InputInteractions·PrefabStructure·Rebinding·ScriptReload·ScriptWaits·Tweens | 통과 | |
| KeyRoom.InspectorComponents(`verify.py editor` Scene Play 기록, `editor.py` UI) | 통과 | Range 슬라이더 240→300 Undo 1회 포함(슬라이더 기록이 놓을 때로 바뀐 뒤) |
| KeyRoom.AssetFields `edit`·`field`·`curves` | 통과 | |
| ParticleLab.Editor·Reload, PlatformerLab.Colliders·Joints·Parallax·Paths·TileCollision, Samples.GameText(KeyRoom·PlatformerLab·SpriteLab), SeedLab.GridNavigation·TileTerrain, SpriteLab.Aseprite | 통과 | |

캡처: [다중 선택(혼합)](i5-multi-mixed.png) · [다중 선택(편집 후)](i5-multi-edited.png) · [Oak tree(표시 없음)](i6-oak-plain.png) · [Courtyard oak(Name·Size 표시)](i6-oak-courtyard.png) · [Size Revert 뒤](i6-oak-reverted.png) · [Door.png 에셋 Inspector](i16-door-png.png) · [피벗 0.5](i16-door-pivot.png) · [아틀라스](i16-door-atlas.png) · [긴 이름 PNG](i16-long-png.png) · [Scene Play 80프레임](scene-play-0080.png). 결과 JSON: [inspect.json](inspect.json).

참고: Scene 탭 내용은 네이티브 NSView라 창 캡처에 비어 보인다. 캡처는 Inspector 패널만 그렸다.

## 재현

저장소 루트에서. 모델 검사는 `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only inspector-edit`. 에디터와 빌드 단계는 [하네스 README](../../KeyRoom.AssetFields/README.md#실행)의 `inspect` 블록(준비 → `inspect` → `trace` → 기준 씬 복사본 → Release·Native AOT 빌드 → play → compare → differs)을 그대로 실행한다.

## 확인하지 않은 것

- Windows·Linux x64: 에디터 작업이고 Linux 에디터는 지원하지 않으며 Windows는 보류(coordinator 몫).
- 실제 마우스 입력: 프로브는 레이블·Project 행에 포인터 이벤트를 직접 보낸다(운영체제 입력 장치를 거치지 않음). 레이블 끌기의 Shift(빠르게)·Alt(느리게) 배율은 만들지 않았다.
- 레이블 끌기 중 커서 모양은 프로브로 확인하지 않았다(첫 이동 때 레이블에 `SizeWestEast`를 붙인다).
- 오버라이드 행 표시는 이름·Transform·스크립트 필드 행이다. 내장 컴포넌트의 개별 행은 머리말 점만 있다(행 메뉴는 **Overrides…** 창을 쓴다). 에셋 피벗을 따라가는 범위는 열린 씬이다.
- 키보드로 Project 선택을 옮길 때는 에셋 Inspector로 바뀌지 않는다(클릭만).
- PNG별 필터(위 결정), 아틀라스 그룹을 바꿀 때 다른 씬·클립에서의 시각 비교(패킹은 기존 Repack 경로).
