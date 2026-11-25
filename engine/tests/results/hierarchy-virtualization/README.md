# 에디터 Hierarchy 가상화와 Play Inspector 유지

2026-11-25 · macOS 27.0.1 arm64 · Apple M4 Max · .NET SDK 10.0.101 · 기준 커밋 `7b6068d`(main). 실행 대상은 패키징한 Mac 에디터(Release, `editor/MoNoon.app`)의 Scene 편집과 Scene Play다. 기준 에디터는 같은 커밋을 같은 스크립트(`engine/scripts/build-editor-mac.sh`)로 만들어 따로 보관했다. 측정 시작과 끝에 `qemu-system` 프로세스는 없었다. 시간은 기준과 후보를 번갈아 5회씩 재고, 중앙값 차이가 두 범위의 합보다 클 때만 개선으로 적었다.

에디터만 바꿨다. Engine.Core·Desktop·Player와 게임 배포본은 건드리지 않았으므로 Release·Native AOT 용량과 프레임 비용은 그대로다. 새 패키지·런타임 의존성은 없다(Avalonia의 `ListBox`와 Fluent 테마의 TreeView 화살표 테마를 그대로 쓴다).

## 원인

[나눠 읽기 기록](../split-loading/README.md#에디터-scene-play)에서 CollisionLab Scene Play 중 **4**로 1만 오브젝트 방을 붙이는 연결 프레임이 5.1–5.6초, UI 스레드 할당 6.35 GB였다. 이번에 같은 하네스로 다시 재니 약 7.0초, 6,648 MB였다. 임시 계측(커밋하지 않음)으로 나눈 내역은 다음과 같다.

| 부분 | 비용 | 이유 |
| --- | --- | --- |
| Hierarchy `TreeView` | 프레임 대부분(Editor 샘플 약 0.8초 + 샘플 밖 레이아웃 약 6초, 6.6 GB) | 오브젝트마다 `TreeViewItem`을 만들어 모두 논리 트리에 붙이고, 비가상화 패널이 1만 행을 전부 템플릿 적용·배치했다. [이전 실험](../perf-editor/README.md#clear생성추가-시간수정-전-계측)대로 문자열만 있는 행도 레이아웃에 3.4 GB가 든다. |
| Inspector 컴포넌트 목록 | 약 65 ms(목록 16 ms + 레이아웃 50 ms) | 구조가 바뀔 때마다 `Select(selected)`가 선택이 그대로여도 컴포넌트 목록을 새로 만들었다. 1만 개와 관계없이 Play 구조 변경마다 든다. |

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `Shell/MainWindow.axaml` | Hierarchy를 `TreeView`에서 가상화 `ListBox`로 바꿨다. 이름(`Hierarchy`), 자동화 이름, 툴팁, 여백은 그대로다. 행 상자는 TreeView 행과 같게 높이 22px, 투명 1px 테두리, 모서리 0, 글자색 `TreeViewItemForeground`로 맞췄다. 여러 선택(`groupSelected`)과 드롭 대상(`dropTarget`) 강조색도 같다. |
| `Hierarchy/MainWindow.Hierarchy.cs` | 트리는 `HierarchyNode`(오브젝트, 부모, 자식, 펼침, 검색 표시, 흐림)로 두고, 펼친 부모 아래 검색에 남은 행만 순서대로 목록에 넣는다(`ShowHierarchyRows`). 컨테이너는 화면에 보이는 행만 만들어지고, 준비될 때 `Tag`·흐림·자동화 이름·강조 클래스를 받는다. 행 안은 들여쓰기(단계마다 16px) + Fluent TreeView 화살표(13px 위치, 12px) + 기존 `HierarchyRow`이다. 화살표 클릭과 ←/→/+/−(접기·펼치기, 다음에 부모·첫 자식으로 이동)는 TreeView와 같게 동작한다. 접힘 상태 보존, 검색(일치 항목과 부모, 부모 펼치기, "No matching objects"), Shift 범위·Ctrl/⌘ 토글 선택, 끌어서 부모 바꾸기·프리팹 만들기·필드에 넣기, 우클릭 메뉴는 같은 코드가 노드와 컨테이너를 거쳐 한다. 선택한 행은 직접 스크롤해 보여 준다(펼치기·검색 때 다른 행으로 튀지 않게 자동 스크롤은 껐다). |
| `Hierarchy/MainWindow.Selection.cs` | 여러 선택 강조를 보이는 행에만 건다. 새로 보이는 행은 준비될 때 받는다. |
| `Shell/MainWindow.PlayState.cs`, `Inspector/MainWindow.Inspector.cs` | 같은 Play 월드의 구조 변경에서 선택한 오브젝트가 그대로이고(하나 또는 없음), 컴포넌트 배열이 같고, Inspector의 오브젝트 필드가 같은 대상을 보일 때는 컴포넌트 목록을 다시 만들지 않고 `RefreshInspector`로 값만 갱신한다. 하나라도 다르면 이전처럼 `Select`로 다시 만든다. Play Inspector에서 월드 구조에 따라 바뀌는 내용은 오브젝트 필드 이름표뿐이다(경고·오버라이드는 편집 모드 전용). |
| `Inspector/MainWindow.ReferenceFields.cs` | 오브젝트 필드가 "지금 다시 만들면 같은 이름표인가"를 검사하는 함수를 남긴다. 다른 패널에서 Hierarchy 행을 Ping할 때는 노드의 부모를 펼치고 패널 배치 뒤 스크롤·강조한다. |

`RebuildHierarchy(SceneObject?)`·`Select`·`ShowSelection`·`FinishObjectChange` 이름과 인자, `hierarchyRow` 클래스와 `Tag`는 그대로다. `treeItems`는 이제 `SceneObject → HierarchyNode`이다.

하네스 훅 8개를 새 구조에 맞췄다: `KeyRoom.EditorDocking`·`ObjectGroups`·`AssetFields`(컨트롤 형식만), `InspectorComponents`(행 흐림을 노드에서 읽음), `EditorTheme`(긴 이름 행을 `ListBoxItem`에서 찾음), `EditorPanelsDetail`(검색 결과를 목록 노드로 비교, 행은 스크롤해 실현한 뒤 찾음), `PlayInspector`(Player 행을 스크롤해 실현한 뒤 클릭), `CollisionLab.EditorPerf`(접기는 `SetExpanded`, 선택·접힘·흐림은 노드로 확인). 검사 내용은 바꾸지 않았다.

## 측정

### 나눠 읽기 연결 프레임(Field10000을 붙이는 프레임)

[CollisionLab.SplitLoading](../../CollisionLab.SplitLoading/README.md) `editor.py`를 실행마다 `runs=1`로 기준·후보 번갈아 5회씩 실행했다. 한 실행에 예산 0과 4 ms를 한 번씩 잰다. 값은 중앙값 [최소–최대], 할당은 그 프레임의 UI 스레드 관리 할당이다. 원본: [summary.json](summary.json)

| 예산 | 기준 | 후보 | 판정 |
| --- | --- | --- | --- |
| 0 | 7,397 ms [7,118–8,015], 6,648 MB, Gen0 515회 | **98.8 ms [97.4–100.2], 12.4 MB**, Gen0 1회 | 개선 |
| 4 ms | 7,251 ms [7,203–7,416], 6,648 MB | **95.5 ms [94.8–97.4], 12.5 MB** | 개선 |

- 후보 연결 프레임(예산 0, 1회차 97.4 ms)의 내역: Scene.Commit 25.4 ms, Simulate 26.2 ms(Physics.Contacts 대부분), Editor 24.1 ms, Render 3.8 ms, 샘플 밖(레이아웃·그리기) 약 18 ms. 기준은 Editor 896 ms, 샘플 밖 약 6.4초였다.
- 후보 Editor 24 ms는 Hierarchy 노드 10,026개 생성 약 8 ms, 목록 만들기 약 5 ms, 나머지 Inspector 값 갱신·진단이다. 그 뒤 화면의 약 20행을 다시 실현하는 레이아웃이 약 12 ms다(임시 계측).
- 같은 프레임을 게임(Release·Native AOT)은 22–30 ms에 처리한다. 남은 차이의 대부분은 에디터가 아닌 Scene.Commit과 Simulate가 에디터 프로세스에서 더 오래 걸리는 것이다(에디터는 `DOTNET_TieredCompilation=0`을 쓰지 않는다. 원인은 재지 않았다).
- 로딩 구간 최장 프레임은 같았다(예산 0: 기준 108.0, 후보 108.2 ms. 4 ms: 76.1, 73.2 ms). 20회 모두 취소 뒤 자원이 시작과 같아 `PASS`였다.

### 에디터 Hierarchy 비용(CollisionLab.EditorPerf, Field10000 10,010개)

[하네스](../../CollisionLab.EditorPerf/README.md) `editor.py`로 기준(기준 커밋의 훅)·후보(새 훅)를 번갈아 5회씩. 값은 실행별 중앙값의 중앙값 [최소–최대].

| 항목 | 기준 | 후보 | 판정 |
| --- | --- | --- | --- |
| 편집 모드 `RebuildHierarchy` 호출 | 3,496 ms [3,404–4,794], 2,632 MB | 58.6 ms [58.1–61.2], 6.9 MB | 개선 |
| 그 뒤 레이아웃 | 9,404 ms [9,130–10,947], 7,924 MB | 43.3 ms [42.2–46.2], 4.5 MB | 개선 |
| Play 진입(버튼 → Hierarchy 재구성) | 13,302 ms [12,319–14,418], 9,199 MB | 415 ms [394–429], 44 MB | 개선 |
| 활성 전환 1회 `SyncPlayInspector` | 11.7 ms [10.9–13.3], 1.43 MB | 0.25 ms [0.23–0.54], 0.01 MB | 개선(Inspector 유지) |
| 활성 전환 뒤 레이아웃 | 17.1 ms [12.9–22.6], 7.2 MB | 0.43 ms [0.17–0.48], 0.01 MB | 개선 |
| Play 전체 재구성 호출 | 782 ms [766–820], 221 MB | 11.9 ms [11.3–37.8], 4.4 MB | 개선 |
| 그 뒤 레이아웃 | 5,929 ms [5,631–6,334], 6,344 MB | 20.7 ms [18.2–56.3], 7.2 MB | 개선 |
| 편집 뷰 프레임(`DrawFrame`) | 5.06 ms [4.96–5.73] | 4.23 ms [2.76–4.97] | 차이 없음(같은 코드) |

활성 전환 6회마다 행 투명도·선택·접힘·다른 행 유지, 전체 재구성 뒤 선택·접힘 유지 검사가 10회 모두 통과했다. 1만 행 편집 모드 Hierarchy는 이제 씬을 열 때, 오브젝트를 추가·삭제·부모 변경할 때마다 약 0.1초가 든다(이전 약 13초).

## 정렬과 동작 비교

기준·후보 에디터에서 같은 임시 훅(커밋하지 않음)을 실행해 비교했다. KeyRoom, 창 1400×900.

- **행 좌표**: 편집 모드, 선택 뒤, Play에서 화면에 보이는 19행 모두 행 상자(0, 22k, 168×22), 화살표(x 13+16·단계, 12×20), 아이콘(x 40+16·단계), 이름 x·폭, 눈·자물쇠(x 136·150), 흐림, 글꼴이 같았다. 기준은 44행을 모두 만들고, 후보는 보이는 19행만 만든다.
- **캡처**: Hierarchy 패널 176×445 캡처 3장에서 다른 픽셀은 화면 아래에 잘린 마지막 줄의 20픽셀과, 나머지 약 400픽셀의 색 차이 1–2(안티에일리어싱)였다.
- **입력 25단계**: 클릭 선택, ⌘ 클릭 추가, Shift 범위 선택, 화살표 두 번, ←←→→↓↓↑←, Oak tree를 Chest로 끌기(드롭 강조 포함), 검색 `oak`·`zzz`·지우기, Play 진입·Play 중 클릭·Stop. 매 단계의 선택, 화면의 행 이름·위치·여러 선택 강조, 부모가 기준과 같았다. (처음에는 ←/→를 `ListBox`가 먼저 처리해 접히지 않았다. 터널 단계에서 받도록 고친 뒤 같아졌다.)

## 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| `engine/scripts/build-editor-mac.sh` | 통과 | — | — |
| `./monoon test` | `All checks passed.` 첫 실행은 KeyRoom 내보내기 검사가 공유 플레이어 캐시의 Native AOT 빌드 중 C# 컴파일러 프로세스 오류(`MissingMethodException … AttributeLocationExtensions`)로 실패했고, `dotnet build-server shutdown` 뒤 다시 실행해 통과했다 | — | — |
| CollisionLab.SplitLoading `editor.py`(번갈아 5회, 취소 뒤 자원 복귀) | 기준 10/10, 후보 10/10 | — | — |
| CollisionLab.EditorPerf(선택·접힘·행 흐림 유지) | 기준 5/5, 후보 5/5 | — | — |
| Scene Play 모드가 있는 하네스 56개(`run-harness.py … --mode scene-play`, 마지막 수정 뒤) | 51 통과, 아래 5개 | — | — |
| KeyRoom.ObjectGroups | 전체 실행에서 `Debug query 1 · Coin` 시간 초과, 단독 재실행 통과(첫 전체 실행에서도 통과) | — | — |
| KeyRoom.PlayInspector | 실패: `TimeoutException: Scene Play`. 기준 에디터(기준 훅)도 같은 곳에서 실패 | — | — |
| KeyRoom.UiLayout | 실패: `Editor exited or timed out at input 2`. 기준 에디터도 같은 실패 | — | — |
| Accessibility.Mac, SeedLab.NavigationFree | 실행 못 함: 창 캡처 도구 `engine/.artifacts/clip-qa/window`가 이 워크트리에 없다 | — | — |

- ObjectGroups의 RoleMatches 문구는 에디터 창이 활성일 때만 도는 Play 틱(`RuntimeTick` → `RefreshDiagnostics`)에서 갱신된다. 전체 실행 중 다른 앱으로 포커스가 넘어간 시점과 겹친 것으로 보며, 이번 변경은 이 경로를 바꾸지 않았다.
- PlayInspector·UiLayout은 `mac-input`(swiftc로 만든 실제 마우스 입력)으로 Play 버튼을 누르는 첫 입력에서 멈춘다. 캡처에는 Play가 눌리지 않은 편집 화면이 남았다. 기준 커밋 에디터로도 같은 곳에서 멈추므로 이 Mac의 입력 주입 환경 문제로 보고, 이번 변경과 구분했다. 로그: 각 하네스 `run/error.txt`, `run/failure.png`.
- 하네스 훅 수정 전 첫 전체 실행에서는 EditorPerf·UiLayout이 `TargetParameterCountException`으로 실패했다. `RebuildHierarchy`에 선택 인자를 더해 리플렉션 호출이 깨진 것이어서, 원래 서명을 두고 새 메서드(`RebuildHierarchyRows`)를 따로 만들었다.

## 재현

```sh
sh engine/scripts/bootstrap-worktree.sh
engine/scripts/build-editor-mac.sh
python3 engine/scripts/run-harness.py CollisionLab.SplitLoading CollisionLab.EditorPerf --mode scene-play --out /tmp/hierarchy-virtualization
```

연결 프레임은 각 `scene-play/*/result.json`의 `add.commit.max_ms`와 `add.commit.slowest.alloc_bytes`이다. 기준과 번갈아 재려면 기준 커밋으로 만든 에디터를 따로 두고 `engine/tests/CollisionLab.SplitLoading/editor.py <editor> <prepare한 CollisionLab> <SplitLoadingProbe.dll> <out> 1`을 번갈아 실행한다. EditorPerf는 `editor.py`가 `base=…,…`·`cand=…,…` 두 편집기를 번갈아 실행한다(기준에는 기준 커밋의 훅을 쓴다).

## 확인하지 않은 것

- Windows·Linux: Windows 검증은 보류 중이고 Linux에는 에디터가 없다. Windows 에디터의 Avalonia Scene 패널 경로는 재지 않았다.
- 실제 마우스·키보드로 Hierarchy를 조작하는 확인은 임시 훅의 합성 포인터·키 이벤트로 대신했다. 실제 입력 주입 하네스(PlayInspector·UiLayout)는 위 환경 문제로 기준·후보 모두 돌지 않았다.
- 접기 상태가 바뀌거나 구조가 바뀔 때 목록 전체를 다시 넣는다(Reset). 화면의 약 20행을 다시 만드는 데 약 12 ms가 든다. 바뀐 범위만 넣는 증분 갱신은 하지 않았다.
- 연결 프레임의 나머지 차이(Scene.Commit·Simulate가 게임보다 오래 걸림)는 에디터 UI 밖이라 재지 않았다.
- 같은 부모 아래 자식이 1만 개인 경우도 같은 목록이라 같은 비용일 것으로 보지만 따로 재지 않았다.
