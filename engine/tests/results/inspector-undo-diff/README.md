# 오브젝트 단위 차분 Undo 기록

2026-11-24 · macOS 27.0.1 arm64 · Apple M4 Max · .NET SDK 10.0.101 · 기준 커밋 `14080b0`(wip/next1, I-1 포함). 로드맵 13.1의 2차(결정 7의 "B", 사용자 결정)다. 실행 대상은 패키징한 Mac 에디터(Release, `editor/MoNoon.app`)의 Scene 편집이다. 기준 에디터는 `14080b0`을 `git archive`로 풀어 같은 스크립트로 만들었다. 측정 내내 `qemu-system` 프로세스는 없었지만 다른 에이전트의 빌드로 부하 평균이 3.4–9.5였다. 시간은 기준과 후보를 번갈아 5회씩 재고, 중앙값 차이가 두 범위의 합보다 클 때만 개선으로 적었다. 관리 할당과 기록 크기는 실행마다 같아 그대로 비교했다.

게임 런타임은 바꾸지 않았다. Engine.Core에는 에디터가 쓰는 내부 메서드 한 줄(`ProjectYaml.EncodeTo`)만 늘었다. Release 플레이어의 `Engine.Core.dll`은 1,313,792바이트로 기준과 같고(파일 정렬 여백 안), 플레이어 코드가 부르지 않으므로 Native AOT에서는 잘려 나간다(AOT 크기는 따로 재지 않았다).

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `Model/SceneHistory.cs` | 상태를 씬 YAML 한 문자열 대신 머리(`schemaVersion`, `objects:`), 오브젝트 항목(`- id: …` 블록) 배열, 꼬리(`defaultCameraId`)로 나눠 둔다. 항목 바이트 배열은 바꾸지 않고 현재 상태·저장 상태·Undo 기록이 참조로 함께 쓴다. 기록할 때 씬을 재사용 버퍼에 한 번 직렬화하고, 앞뒤에서 같은 항목을 맞춘 뒤 가운데는 항목 첫 줄(id)로 찾아 바이트가 같으면 이전 배열을 그대로 쓴다. 그래서 편집 한 번이 붙잡는 것은 바뀐 항목과 오브젝트당 참조 8바이트뿐이다. |
| 〃 Undo·Redo | 대상 상태와 현재 기록 상태가 같은 배열을 가진 항목은 지금의 `SceneObject`를 그대로 두고, 나머지 항목만 머리·꼬리와 함께 디코딩한다. 그 뒤는 `RestoreObjects`와 같다(`Validate`로 부모 연결·검증, 시작 카메라, `NormalizeScriptFields`). 복원 뒤 다시 캡처해 항목과 꼬리가 대상과 바이트 단위로 같은지 확인하고, 다르면(기록하지 않은 변경이 남은 오브젝트, 정규화로 바뀐 필드) 모든 항목을 디코딩해 이전과 똑같이 복원한다. |
| 〃 저장 상태·복구 | "Unsaved changes"는 현재 상태와 저장 상태의 항목을 비교한다(같은 배열이면 참조 비교). 에셋 이동의 저장 상태 경로 고치기는 전체 문자열을 만들어 이전과 같은 `RewriteSnapshot`을 거친 뒤 다시 나눈다. `CurrentSnapshot`은 문자열 대신 그 상태의 문자열을 만드는 함수를 돌려준다. |
| `Shell/MainWindow.Workspace.cs` | 비정상 종료 복구 큐는 확정 때 상태를 받아 두고 450 ms 뒤 쓸 때 문자열을 만든다. 상태는 바뀌지 않으므로 쓰는 내용은 이전과 같다(마지막으로 큐에 넣은 상태). 확정마다 10 MB 문자열을 만들지 않는다. |
| `Scene/SceneViewport.cs`, `Scene/SceneViewport.Gizmos.cs`, `Hierarchy/MainWindow.ObjectEdits.cs` | 입력 중인 텍스트·숫자 필드 편집(`RecordEdit`의 묶음 경로)은 선택한 오브젝트만 바꾼다. 그래서 `InvalidateUi` 대신 `InvalidateSelectionUi`를 부른다. 보관한 편집 뷰 그리기 순서는 선택 오브젝트의 그리기 순서 입력(부모, 크기, Sorting Group·Tilemap·Sprite/Skeleton/Line/Trail의 Layer·Order·Sort by Y·Foot Offset, Tilemap·Sorting Group 컴포넌트)이 순서를 만들 때(또는 순서를 보관한 채 선택이 바뀐 때)와 다를 때만 버린다. UI 미리보기는 선택 오브젝트가 Canvas나 UI 위젯일 때만 버린다. 다른 모든 편집 경로(추가·삭제·부모 변경·체크박스·실행 취소 등)는 이전처럼 `InvalidateUi`다. [그리기 순서 무효화 목록](../perf-editor/README.md#무효화가-닿는-편집-경로)의 Inspector 행 중 입력 필드로 바꾸는 값(Layer·Order·Foot Offset·크기)이 위 입력에 들어 있다. |
| `Engine.Core/ProjectYaml.cs` | 같은 VYaml 옵션으로 `IBufferWriter`에 쓰는 `EncodeTo`. |

`FinishObjectChange`·`MarkDirty`·`RecordEdit`·`Select`·`ShowSelection`·`Save`·`RestoreEdit`의 이름과 인자, `history` 필드와 `CanUndo`·`CanRedo`·`IsDirty`·`Reset`·`Record`·`RecordStroke`·`Restore`·`MarkSaved`·`EndGroup`은 그대로다. UI 요소는 바꾸지 않았다.

### 전체 스냅샷을 남긴 곳

- 없다. 바뀐 상태마다 오브젝트당 참조 배열(1만 오브젝트에서 80 KB)을 하나씩 둔다. 추가·삭제·순서 변경도 같은 방식이라 따로 처리하지 않는다. 구간 단위(splice) 기록으로 이 배열까지 줄일 수 있지만 100단계에 약 8 MB라 단순한 쪽을 택했다.
- 나누기 규칙에 맞지 않는 YAML(빈 씬의 `objects: []` 등)은 머리 하나에 전체 문자열을 둔다. 그 상태만 전체 스냅샷이 되며, 복원은 전체 디코딩이라 결과가 정확하다.
- 확정할 때 씬 전체를 한 번 직렬화하는 비용(약 15 ms)은 남는다. `SceneObject`에 변경 추적이 없어 무엇이 바뀌었는지 알려면 직렬화해 비교해야 하기 때문이다. 할당은 재사용 버퍼 덕분에 32.5 MB에서 0.8 MB가 됐다.

## 측정

[하네스](../../CollisionLab.InspectorEdits/README.md)(I-1 하네스를 넓혀 KeyRoom 모드, Undo·Redo 100회 시간, 상태 형식과 무관한 기록 크기를 추가했다) · 실행별 값 [field10000-summary.json](field10000-summary.json), [keyroom-summary.json](keyroom-summary.json). 값은 5회 실행 중앙값 [최소–최대]이다. 키 입력은 입력 이벤트와 그 뒤 디스패처 작업, 확정은 Scene 패널로 포커스를 옮기는 동작이다. Undo·Redo 100회는 `SceneHistory.Restore`만 잰다(`RestoreEdit`는 매번 Hierarchy를 다시 만들고, 이 비용은 이번 변경과 무관하다).

```sh
H=engine/.artifacts/bin/InspectorEditsProbe/release/InspectorEditsProbe.dll
python3 engine/tests/CollisionLab.InspectorEdits/editor.py samples/CollisionLab "$(mktemp -d)/field" 5 base=<기준>/MoNoon.app/Contents/MacOS/MoNoon,$H cand=editor/MoNoon.app/Contents/MacOS/MoNoon,$H
MONOON_I1_SCENE=KeyRoom python3 engine/tests/CollisionLab.InspectorEdits/editor.py samples/KeyRoom "$(mktemp -d)/keyroom" 5 base=…,$H cand=…,$H
```

### Field10000 (10,010개)

| 항목 | 기준 `14080b0` | 후보 | 판정 |
| --- | --- | --- | --- |
| **100단계 기록이 붙잡은 크기**(101개 상태) | 1,065.2 MB | 13.0 MB | **약 1/82** |
| 100단계 뒤 GC 후 힙 증가 | 914 MB [677–914] | 21.2 MB [21.1–21.6] | 개선 |
| 100단계 기록: 걸린 시간 | 3,790 ms [3,657–4,168] | 1,838 ms [1,815–1,891] | 개선 |
| 100단계 기록: 할당 | 3,981 MB | 560 MB | 약 1/7 |
| Undo 100회 | 7,028 ms [6,905–7,451], 5,630 MB | 2,083 ms [1,916–2,087], 1,262 MB | 개선 |
| Redo 100회 | 8,837 ms [7,661–9,936], 5,630 MB | 1,995 ms [1,894–2,026], 1,262 MB | 개선 |
| Position X 키 입력 1회 | 7.00 ms [3.64–9.89], 2.28 MB | 3.72 ms [2.84–4.57], **0.10 MB** | 할당 개선. 시간은 범위 조건 미달 |
| UI Text 키 입력 1회 | 6.72 ms [3.24–7.21], 2.28 MB | 3.69 ms [1.93–3.93], **0.99 MB** | 할당 개선. 시간은 범위 조건 미달 |
| Position X 확정 1회 | 14.77 ms [14.53–81.77], 32.47 MB | 14.77 ms [14.63–15.02], 0.79 MB | 할당 약 1/41, 시간 같음 |
| UI Text 확정 1회 | 14.57 ms [13.76–14.75], 32.46 MB | 14.82 ms [14.54–14.95], 0.78 MB | 할당 약 1/41, 시간 같음 |
| Position X 5자리 입력+확정 | 55.19 ms [36.35–133.53], 43.86 MB | 33.37 ms [29.67–38.15], 1.29 MB | 할당 약 1/34. 시간은 범위 조건 미달 |
| UI Text 10자 입력+확정 | 95.48 ms [53.01–119.26], 55.25 MB | 53.04 ms [36.23–55.22], 10.73 MB | 할당 약 1/5. 시간은 범위 조건 미달 |
| 편집 한 번의 씬 캡처 횟수 | 1회 | 1회 | 같음 |
| 저장한 Field10000 SHA-256(10회 모두) | `99189634ed49e12cbada8a90f93b0dd5a851916ec9eae00cfb87cedcceeb37f4` | 같음 | 바이트 동일(I-1과도 같다) |

- 13.0 MB는 현재 씬의 항목(한 벌)과 상태 101개의 참조 배열(101 × 10,010 × 8 B ≈ 8.1 MB)이다. 편집이 바꾼 항목은 한 단계에 수백 바이트다. 이와 별도로 직렬화 재사용 버퍼(씬 YAML 크기)를 에디터 창마다 하나 둔다. 씬을 처음 기록할 때 잡혀 위 힙 증가에는 들지 않는다.
- Position X 키 입력은 그리기 순서를 다시 만들지 않아 0.10 MB다. UI Text 키 입력은 선택 오브젝트가 UI라 UI 미리보기를 다시 만들어야 해서 약 1 MB가 남는다(`ScreenUi`가 씬 1만 오브젝트를 훑는다). 그리기 순서는 다시 만들지 않는다.
- Undo 한 번은 약 20 ms, 12.6 MB다. 남은 대부분은 복원 뒤 확인용 전체 직렬화 한 번(약 15 ms)과 `Validate`(부모 연결이 오브젝트마다 목록을 만든다, 약 5 ms)다. `Validate`는 이전 `RestoreObjects`와 같은 검증이라 그대로 두었다.

### KeyRoom (60개)

| 항목 | 기준 `14080b0` | 후보 | 판정 |
| --- | --- | --- | --- |
| 100단계 기록이 붙잡은 크기 | 5.2 MB | 0.1 MB | 개선 |
| 100단계 뒤 GC 후 힙 증가 | 15.6 MB [15.5–15.6] | 3.9 MB [3.8–4.1] | 개선 |
| 100단계 기록: 걸린 시간 | 483 ms [474–495], 45.3 MB | 399 ms [372–408], 11.3 MB | 개선 |
| Undo 100회 | 288 ms [268–304], 68.7 MB | 155 ms [151–172], 44.7 MB | 개선 |
| Redo 100회 | 274 ms [256–291], 68.7 MB | 147 ms [144–161], 44.7 MB | 개선 |
| Position X 키 입력 1회 | 2.40 ms [2.09–2.44], 0.22 MB | 1.30 ms [1.19–1.58], 0.02 MB | 개선 |
| UI Text 키 입력 1회 | 1.93 ms [1.83–2.17], 0.21 MB | 1.99 ms [1.84–2.19], 0.21 MB | 같음(UI 미리보기 재구성) |
| Position X 확정 1회 | 1.05 ms [0.93–1.28], 0.17 MB | 1.21 ms [1.03–1.48], 0.03 MB | 할당만 개선 |
| UI Text 확정 1회 | 1.03 ms [0.98–1.18], 0.17 MB | 1.20 ms [1.03–1.23], 0.03 MB | 할당만 개선 |
| 저장한 KeyRoom SHA-256(10회 모두) | `4286adef1fba24f72c6040ce960521edfb0981481ce3f9537c50366a04a8d9f4` | 같음 | 바이트 동일 |

## 정확성 검사

| 검사 | 실행 대상 | 결과 |
| --- | --- | --- |
| [HistoryChecks](../../Engine.Checks/HistoryChecks.cs)(`./monoon test`에 포함, `--history-only`로 따로 실행): KeyRoom 90단계·Field10000 30단계 무작위 편집. 종류는 이동, 이름, Sprite 필드, 추가, 삭제, 복제, 부모 변경, 순서 변경, 시작 카메라·카메라 추가, 프로젝트 설정, 같은 필드 묶음 입력(시작 값으로 돌아오면 기록 없음), 여러 개 선택 편집, 프리팹 배치·되돌리기·원본 적용, 타일맵 추가·타일 획, 변경 없는 기록, 저장 표시다. 매 단계 씬 문자열·설정·복구 문자열·"Unsaved changes"를 확인하고 Undo 한 번·Redo 한 번 결과를 앞뒤 상태와 바이트 단위로 비교한다. KeyRoom은 매 단계, Field10000은 10단계마다 전부 Undo·전부 Redo하며 각 상태를 비교한다. 끝에 기록하지 않은 변경 위의 Undo(이전처럼 버려진다), 전부 Undo 뒤 저장 파일이 처음 저장 파일과, 프리팹 원본이 처음과 바이트 동일, 전부 Redo 뒤 저장 파일·프리팹 원본이 편집 뒤와 바이트 동일한지 확인한다. | Engine.Checks(macOS arm64) | 통과(KeyRoom 기록 78개, Field10000 기록 25개) |
| 위 검사의 변조 확인: 위치만 맞으면 오브젝트를 재사용하게 바꾸면 실패, 복원 뒤 확인과 전체 디코딩 대비를 끄면 "기록하지 않은 변경" 단계에서 실패 | Engine.Checks | 둘 다 실패를 잡음(되돌림) |
| I-1 기록 시점 검사: 프리팹 인스턴스 Sprite Order `12`→`3` 입력, Enter 뒤 기록 정확히 1개, Undo 한 번에 원래 값·오버라이드 목록, Redo 한 번에 123·오버라이드, 포커스를 둔 채 저장 | 후보 Scene 편집 | 통과(아래 참고) |
| Undo 100회 뒤 Redo 100회가 100단계 직후 씬 문자열과 같음 | 기준·후보, Field10000·KeyRoom 각 5회 | 통과 |
| 입력 묶음마다 씬 캡처 1회 | 후보 5회 × 13묶음 | 통과 |
| 편집 뷰 그리기 순서([CollisionLab.EditorPerf](../../CollisionLab.EditorPerf/README.md) 7단계): 기존 단계에 입력 필드 편집을 더했다. 이름 입력은 보관한 순서 객체를 그대로 쓰고, Sprite Order 입력은 순서를 버리며 둘 다 새로 만든 `WorldItems()`와 같다. | 후보 | 통과 |

- Field10000 측정의 첫 시도에서 후보 2회차가 I-1 기록 시점 검사의 "no Sprite Order field"로 멈췄다. 프리팹 변경 뒤 Inspector 행을 100 ms만 기다리는 하네스 조건이 높은 부하에서 모자랐다. 같은 후보로 4회 다시 돌려 모두 통과했고, 하네스가 Order 행을 최대 20초 기다리게 고친 뒤 위 5회를 다시 측정했다.

## 회귀

후보 에디터(Release 패키지, macOS arm64)로 실행했다. 게임 배포본은 `build-players.sh`로 다시 만든 템플릿을 썼다.

| 검사 | 결과 |
| --- | --- |
| `./monoon test` | 통과 (`All checks passed.`, HistoryChecks 포함) |
| `CollisionLab.InspectorEdits/editor.py`(위 측정, Field10000·KeyRoom 각 기준·후보 5회) | 통과 |
| `CollisionLab.EditorPerf/editor.py` 1회(실행 취소·다시 실행·입력 필드 편집 포함 그리기 순서 검사) | 통과. 첫 실행은 새 "이름 입력은 순서를 그대로 쓴다" 검사에서 실패했다: 선택을 바꿔도 보관한 순서는 그대로라 비교 기준이 이전 선택의 값이었다. 선택이 바뀔 때 새 선택의 입력을 기준으로 잡게 고친 뒤 통과 |
| `AudioLab.Vorbis/editor.py` | 통과 |
| `BarrageLab.Lines/editor.py` | 통과 |
| `CollisionLab.FrameProfiler/editor.py` | 통과 |
| `CollisionLab.SplitLoading/editor.py` | 통과 |
| `InventoryLab.RichText/editor.py` | 통과 |
| `KeyRoom.ComponentKeys/editor.py` | 통과 |
| `KeyRoom.CopyPaste/editor.py` | 통과 |
| `KeyRoom.EditorBuild/editor.py` | 통과 |
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

wip/next1 `e99c626`(YAML 기본값 수정, 공용 probe 라이브러리 등)을 병합한 뒤 `./monoon test`(`All checks passed.`, HistoryChecks 포함), `CollisionLab.InspectorEdits/editor.py` 후보 1회씩(Field10000: 키 입력 0.10·0.99 MB, 100단계 기록 13.0 MB, 저장 SHA-256 위와 같음 / KeyRoom: 저장 SHA-256 위와 같음), `CollisionLab.EditorPerf/editor.py` 1회를 다시 실행해 모두 통과했다. 나머지 에디터 하네스는 병합 전 결과다.

첫 예외: 없음. 재현 입력과 로그는 각 하네스 README의 명령으로 다시 만든다.

## 확인하지 못한 것

- Windows 에디터는 실행하지 않았다(Windows 보류, Linux 에디터 없음). 바뀐 코드에 OS별 분기는 없다.
- 실제 키보드가 아니라 `TextInputEvent`·`KeyDownEvent`로 입력했다. 메뉴 막대의 Undo는 `RestoreEdit` 경로라 따로 누르지 않았다.
- Undo·Redo 시간은 `SceneHistory.Restore`만 쟀다. 에디터에서 누르는 Undo는 여기에 Hierarchy 재구성(1만 행에서 수 초, [성능 기록](../perf-editor/README.md))이 더해지며 이번에 바꾸지 않았다.
- 에셋 이동(`ProjectPathEdit`)이 섞인 Undo는 기존 [ProjectMoveChecks](../../Engine.Checks/ProjectMoveChecks.cs)로만 확인했고 무작위 검사에는 넣지 않았다.
