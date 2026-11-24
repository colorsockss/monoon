# Inspector 편집 기록 비용 줄이기 (I-1)

2026-11-24 · macOS 27.0.1 arm64 · Apple M4 Max · .NET SDK 10.0.101 · 기준 커밋 `d523ec7`. 실행 대상은 패키징한 Mac 에디터(Release, `editor/MoNoon.app`)의 Scene 편집이다. 기준 에디터는 같은 커밋을 `git archive`로 풀어 같은 스크립트로 만들었다. 측정 시작과 끝에 `qemu-system` 프로세스는 없었다. 시간은 기준과 후보를 번갈아 5회씩 재고, 중앙값 차이가 두 범위의 합보다 클 때만 개선으로 적었다. 관리 할당과 캡처 횟수는 실행마다 같아 그대로 비교했다.

에디터만 바꿨다. Engine.Core·Desktop·Player와 게임 배포본은 건드리지 않았으므로 Release·Native AOT 용량과 프레임 비용은 그대로다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `Hierarchy/MainWindow.ObjectEdits.cs` | `RecordEdit`에 묶음 대상으로 `TextBox`·`NumericUpDown`이 오면 값은 이미 반영된 채로 `pendingEdit`만 남기고 씬을 캡처하지 않는다. 제목의 `*`, Undo 메뉴, 상태 줄 "Unsaved changes"는 바로 갱신한다. `CommitEdit`가 남은 편집을 한 번 기록한다. 다른 필드나 다른 종류의 편집이 오면 먼저 확정한다. |
| `Shell/MainWindow.axaml.cs` | 포커스 이동(GotFocus)과 Enter에서 확정한 뒤 묶음을 끝낸다. 저장(`Save`)과 타일 획 기록 전에도 확정한다. 제목의 `*`는 확정 전 편집도 포함한다. |
| `Shell/MainWindow.Menus.cs` | Undo/Redo(`RestoreEdit`)는 먼저 확정한다. 확정 전 편집이 있으면 Undo는 켜고 Redo는 끈다(이전처럼 첫 키 입력에서 Redo 목록이 사라지는 것과 같다). |
| `Inspector/MainWindow.Inspector.cs` | `CommitText`(RunAction, Play 시작, 창 닫기에서 부름)가 먼저 확정한다. XAML로 묶인 Sprite Layer·Order·Foot ΔY는 포커스가 있는 `NumericUpDown`을 묶음으로 넘긴다(이전에는 키 입력마다 별도 Undo 단계였다). 컴포넌트 목록을 다시 만들 때 에셋 피커가 `ProjectAssets.Scan`을 한 번만 하고 결과를 나눠 쓴다. |
| `Inspector/MainWindow.UiImages.cs` | 9분할 테두리 두 숫자 필드가 `Grid` 대신 입력 중인 필드를 묶음으로 넘긴다. |
| `Inspector/MainWindow.Presentation.cs`, `Project/MainWindow.Scripts.cs` | 에셋 피커가 공유 스캔(`InspectorAssets`)을 쓴다. |
| `Shell/MainWindow.Scenes.cs` | 씬을 바꿔 기록을 초기화할 때 남은 편집을 버린다(그 전에 `RunAction`→`CommitText`가 이미 확정한다). |

체크박스·선택 목록·색 hex 확정(Physics 마스크)처럼 한 번에 값이 정해지는 편집은 묶음 없이 기록되므로 이전처럼 바꿀 때마다 기록한다. 기존 Play 숫자 필드(`PlayInspector.cs`)와 같은 방식이지만, 편집 모드 필드는 값을 계속 바로 반영해 Scene 화면이 입력을 따라간다.

`FinishObjectChange`·`MarkDirty`·`RecordEdit`·`Select`·`ShowSelection`·`Save`·`RestoreEdit` 이름과 인자, `history` 형식은 그대로다. `SceneHistory`(Engine.Checks가 링크하는 파일)는 바꾸지 않았다. 새 UI 요소가 없어 행 정렬은 바뀌지 않는다.

### 함께 고친 결함

`AddHandler(GotFocusEvent, …, RoutingStrategies.Tunnel)`는 한 번도 불리지 않았다. Avalonia의 `GotFocus`는 Bubble로만 전달되기 때문이다. 그래서 이전 에디터는 포커스가 바뀌어도 묶음을 끝내지 않았고, 같은 필드를 떠났다가 돌아와 다시 입력하면 앞 편집과 한 Undo 단계로 합쳐졌다. Bubble(`handledEventsToo`)로 등록해 의도대로 포커스 이동이 편집의 끝이 되게 했다. 아래 측정에서 기준 에디터의 100단계 기록이 X·Y를 번갈아 입력하는 이유다.

## 측정(Field10000, 10,010개)

[하네스](../../CollisionLab.InspectorEdits/README.md) · [실행별 값](summary.json). 키 입력 시간은 입력 이벤트와 그 뒤 디스패처 작업(TextBox의 `TextChanged`가 여기서 실행된다)을 합한 값이다. 확정은 Scene 패널로 포커스를 옮기는 동작이다. 값은 5회 실행 중앙값 [최소–최대]이다.

| 항목 | 기준 `d523ec7` | 후보 | 판정 |
| --- | --- | --- | --- |
| Position X 키 입력 1회 | 18.47 ms [18.22–24.42], 34.72 MB | 7.64 ms [7.41–8.72], 2.24 MB | 개선 |
| Position X 확정 1회 | 0.26 ms [0.24–0.28], 0.02 MB | 15.15 ms [14.38–15.48], 32.47 MB | 캡처가 여기로 옮겨 옴 |
| Position X 5자리 입력+확정 | 94.93 ms [93.81–246.68], 173.65 MB | 60.67 ms [58.64–67.86], 43.65 MB | 할당 1/4. 시간은 기준 한 회(246.68 ms) 때문에 범위 조건 미달 |
| UI Text 키 입력 1회 | 17.35 ms [17.18–17.51], 34.72 MB | 4.79 ms [4.00–5.13], 2.23 MB | 개선 |
| UI Text 확정 1회 | 0.26 ms [0.24–0.29], 0.02 MB | 13.55 ms [12.99–13.66], 32.46 MB | 캡처가 여기로 옮겨 옴 |
| UI Text 10자 입력+확정 | 184.17 ms [181.27–185.75], 347.26 MB | 51.32 ms [49.21–90.57], 54.84 MB | 개선(시간 약 1/3.6, 할당 약 1/6.3) |
| 편집 한 번의 씬 캡처 횟수 | 키 입력마다(5회·10회) | 1회 | 목표 달성 |
| 100단계 기록: 걸린 시간 | 3,397 ms [3,328–3,594] | 3,472 ms [3,421–3,601] | 차이 없음 |
| 100단계 기록: 할당 | 3,981 MB [3,881–4,047] | 3,977 MB | 같음(한 자리 입력이라 캡처 수가 같다) |
| 100단계 기록: 보관 스냅샷 | 101개, 1,065.2 MB | 101개, 1,065.2 MB | 같음 |
| 100단계 기록: GC 후 힙 증가 | 1,049 MB [1,049–1,057] | 923 MB [679–923] | GC 시점 차이로 보고 판정하지 않음 |
| 저장한 Field10000 SHA-256(10회 모두) | `99189634ed49e12cbada8a90f93b0dd5a851916ec9eae00cfb87cedcceeb37f4` | 같음 | 바이트 동일 |

- 확정 한 번의 비용(13.5–15.2 ms, 32.5 MB)은 조사 때의 `CaptureObjects` 1회(14.4 ms, 32.5 MB)와 같다. 결정 1의 조건("1만 오브젝트에서 여전히 50 ms를 넘을 때만 B")에서 기록 비용은 50 ms 아래이므로 2차(오브젝트 단위 차분 기록)는 하지 않았다.
- 계획의 "10자 입력 UI 할당 ≤40 MB"는 54.84 MB로 **미달**이다. 32.5 MB는 확정 시 캡처 한 번이고, 나머지는 키 입력마다 약 2.2 MB다. 키 입력의 `RecordEdit`가 부르는 `Viewport.InvalidateUi()`가 보관한 편집 뷰 그리기 순서를 버려 1만 항목 순서를 다시 만드는 비용으로 본다(추정, [편집 뷰 그리기 순서](../perf-editor/README.md)). 이름·텍스트처럼 순서와 무관한 필드에서 무효화를 건너뛰는 것은 이번 범위 밖이다.
- **Undo 100단계 메모리는 줄지 않았다.** 확정한 편집마다 씬 전체 스냅샷(10.5 MB)을 하나씩 보관하므로 1만 오브젝트에서 약 1.07 GB다. 이를 줄이려면 2차(차분 기록)가 필요하다.
- 에셋 피커 스캔 공유: Field10000·CollisionLab의 측정 대상 오브젝트에는 에셋 피커가 없어 이 측정에 드러나지 않는다. 컴포넌트 목록을 다시 만들 때 피커 수만큼 하던 `ProjectAssets.Scan`(조사 측정 1회 1.7–2.4 ms)이 한 번으로 준다.

## 동작 검사

| 검사 | 실행 대상 | 결과 |
| --- | --- | --- |
| 프리팹 인스턴스 Sprite Order에 `12` 입력 후 이어서 `3` 입력: 즉시 123, 제목 `*`, 확정 전 Undo 기록 없음 → Enter 후 기록 정확히 1개 → Overrides에 `spriteRenderer.order` 추가 → Undo 한 번에 원래 값·오버라이드 목록 → Redo 한 번에 123·오버라이드 | 후보 Scene 편집, 5회 | 통과 |
| Position X 입력 후 포커스를 둔 채 저장: 기록 1개, 제목 `*` 없음, Undo·Redo 동작 | 후보 Scene 편집, 5회 | 통과 |
| Field10000 입력 묶음마다 씬 캡처 1회 | 후보, 5회 × 13묶음 | 통과 |
| 저장 확인 등 예기치 않은 창 없음(모달 감시) | 기준·후보 10회 | 통과 |
| 같은 입력의 Field10000 저장 바이트 동일 | 기준·후보 10회 | 통과 |

## 회귀

후보 에디터(Release 패키지, macOS arm64)로 실행했다. 에디터 하네스는 `editor.py`의 Scene Play·편집 부분만, GUI 잠금(`gui-lock.sh`) 아래에서 하나씩 실행했다. 게임 배포본(Release·Native AOT)은 바뀌지 않아 다시 만들지 않았다(`./monoon test`의 내보내기 검사는 실행).

| 검사 | 결과 |
| --- | --- |
| `./monoon test`(Engine.Checks 전체, 템플릿은 `build-players.sh`로 새로 만듦) | 통과 (`All checks passed.`) |
| `CollisionLab.EditorPerf` 1회(실행 취소·다시 실행 포함 그리기 순서 검사) | 통과 |
| `AudioLab.Vorbis/editor.py` | 통과 |
| `BarrageLab.Lines/editor.py` | 통과 |
| `CollisionLab.FrameProfiler/editor.py` | 통과 |
| `CollisionLab.SplitLoading/editor.py` | 통과 |
| `InventoryLab.RichText/editor.py` | 통과 |
| `KeyRoom.ComponentKeys/editor.py` | 통과 |
| `KeyRoom.CopyPaste/editor.py` | 통과 |
| `KeyRoom.EditorDocking/editor.py` | 통과 |
| `KeyRoom.EditorExtensions/editor.py` | 통과 |
| `KeyRoom.EditorPanelsDetail/editor.py` | 통과 |
| `KeyRoom.EditorTheme/editor.py` | 통과 |
| `KeyRoom.FieldRename/editor.py` | 통과 |
| `KeyRoom.PrefabStructure/editor.py` | 통과 |
| `KeyRoom.Rebinding/editor.py` | 통과 |
| `KeyRoom.ScriptReload/editor.py` | 통과 |
| `KeyRoom.ScriptWaits/editor.py` | 통과 |
| `PlatformerLab.Joints/editor.py` | 통과 |
| `PlatformerLab.Paths/editor.py` | 통과 |
| `PlatformerLab.TileCollision/editor.py` | 통과 |
| `Samples.GameText/editor.py` | 통과 |
| `SeedLab.GridNavigation/editor.py` | 통과 |
| `SeedLab.TileTerrain/editor.py` | 통과 |
| `SpriteLab.Aseprite/editor.py` | 통과 |

첫 예외: 없음. 재현 입력과 로그는 하네스 README의 명령으로 다시 만든다(결과 폴더의 `editor.log`, `result.json`).

## 확인하지 못한 것

- Windows·Linux 에디터는 실행하지 않았다(Windows 보류, Linux 에디터 없음). 바꾼 코드는 Avalonia 공통 경로라 OS별 분기가 없다.
- 실제 키보드 입력이 아니라 `TextInputEvent`·`KeyDownEvent`를 올려 보냈다. 메뉴 막대(macOS 네이티브 메뉴)로 Undo를 누르는 경로는 `RestoreEdit`의 확정으로 덮이지만 직접 누르지는 않았다.
- 비정상 종료 복구 체크포인트는 확정할 때 큐에 들어간다. 필드에 입력 중인 채 프로세스가 죽으면 그 입력은 복구본에 없다(이전에는 키 입력마다 큐에 들어갔다).
