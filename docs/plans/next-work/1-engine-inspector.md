# 1. 엔진 기능 개선 — Inspector 중심 조사

2026-11-23 · 기준: `.worktrees/phase1` (wip/phase1, `4c9b090`) 읽기 전용. 경로는 `engine/src/` 기준(예: `Editor/Inspector/…` = `engine/src/Engine.Editor/Inspector/…`).
측정: `git archive`로 Engine.Core·CollisionLab·KeyRoom을 스크래치패드에 풀어 Release 콘솔로 실행(macOS arm64 M4 Max, QEMU Debian 게스트가 동시에 실행 중이라 시간은 참고값, 21회 중앙값). GUI는 열지 않았다.

## 1. 현재 상태

### 1.1 Inspector 구조 요약

- 선택 1개: 이름·Transform·Tags는 `MainWindow.axaml:355-447`의 고정 XAML, 컴포넌트는 `AttachedComponents` ItemsControl(`axaml:434-442`)에 `AttachedComponent` 레코드(`Inspector/ComponentRows.cs:6-18`)를 바인딩한다. 선택이 바뀔 때마다 모든 컴포넌트 컨트롤을 새로 만든다(`Inspector/MainWindow.Inspector.cs:47-66`).
- 내장 컴포넌트 행: `PresentationFields`의 큰 switch(`Inspector/MainWindow.Presentation.cs:80-461`)에서 컴포넌트별 클로저로 직접 만든다. 행 설명자(레이블·기본값·직렬화 키)가 없어서 다중 편집·리셋·오버라이드 표시를 행 단위로 붙일 곳이 없다. 이것이 아래 부족한 점 대부분의 공통 원인이다.
- 스크립트 필드: `ScriptInspector`/`AddScriptField`(`Project/MainWindow.Scripts.cs:54-213`). 필드 목록은 Roslyn으로 뽑는다(`Engine.Authoring/ProjectScripts.cs:299-378`). 값은 `Values` JSON에 기본값과 다를 때만 저장한다(`Scripts.cs:95`). 런타임은 생성한 팩토리가 `ReadOverride/ReadEnum/ReadVector`로 읽는다(`ProjectScripts.cs:230-247`). 이 방식은 AOT에 안전하다.
- Undo: 편집마다 씬 전체를 YAML 스냅샷으로 남긴다(`Editor/Model/SceneHistory.cs:48-73`, `Core/SceneDocument.cs:41`). 같은 컨트롤의 연속 편집은 한 단계로 합치고, 포커스가 바뀌면 묶음을 끝낸다(`SceneHistory.cs:54-63`, `Shell/MainWindow.axaml.cs:82`). 보관 한도는 100단계다(`SceneHistory.cs:9`).
- 정렬 기준: 레이블 열 56px, 간격 4px(`EditorStyles.axaml:57-58`). 공용 행 함수는 `InspectorRow`(`Inspector/MainWindow.MultiInspector.cs:16-25`)와 `EditorContext.Row`(`Shell/EditorExtensions.cs:61-70`, 중복)이다. bool 행만 `Margin=61`을 하드코딩한다(`Scripts.cs:109`).

### 1.2 Unity 사용자 기대 항목별 현황

| 항목 | 상태 | 근거 | 비고 |
| --- | --- | --- | --- |
| 다중 오브젝트 편집·Mixed | 부분 | `MultiInspector.cs:34-57`(Transform은 읽기 전용 레이블), `:84-107`(값은 `ValueLabel`, 읽기 전용), `:87-97`(충돌 Mask만 편집) | 8.3의 범위가 충돌 마스크뿐이다. 숫자·bool·enum·스크립트 필드는 함께 바꿀 수 없다 |
| 필드 단위 Undo | 있음 | `SceneHistory.cs:54-63`, `axaml.cs:82` | 입력 중인 필드를 한 단계로 묶는다. Undo 메뉴에 무엇을 되돌리는지 이름은 없다 |
| 컴포넌트 값 복사·붙여넣기 | 없음 | 톱니 메뉴에 Edit Script·Remove만 있다(`Inspector.cs:131-157`) | 오브젝트 단위 복사는 있다(`Hierarchy/MainWindow.Clipboard.cs`) |
| 기본값으로 리셋 | 없음 | 같은 메뉴 | 스크립트는 값이 기본값과 같으면 키를 지우는 처리만 있다(`Scripts.cs:95`) |
| 컴포넌트 순서 변경 | 없음 | 같은 메뉴 | |
| 프리팹 오버라이드 행 표시와 필드별 되돌리기 | 부분 | 별도 창 목록에서 행마다 Apply/Revert(`Hierarchy/MainWindow.Prefabs.cs:88-166`) | Inspector 행에 굵은 글씨나 표시 막대가 없다. 키는 `PrefabProperty(Component, Group, Field)`(`Core/PrefabProperties.cs:7`)라 행과 바로 연결할 수 있다 |
| 배열·리스트 | 부분 | 오브젝트·에셋 참조 배열만 지원. 드래그로 정렬(`Inspector/MainWindow.ReferenceLists.cs:89-116`), Hierarchy·Project에서 끌어 추가 | `int[]`, `float[]`, `string[]`, `List<T>`는 Inspector에 나오지 않는다(`ProjectScripts.cs:320-325`) |
| 중첩 구조체·딕셔너리 | 없음 | `ProjectScripts.cs:343-349` 타입 목록 | 내장 UI Text의 번역·아이콘 목록은 `key=value` 여러 줄 텍스트로 편집한다(`Presentation.cs:380-411`) |
| enum | 있음 | 스크립트 `Scripts.cs:121-131`, 내장 `Presentation.cs:37-42` | |
| 플래그 enum | 없음 | 이름 하나만 저장하고 검사한다(`Core/GameBehaviour.cs:49-55`) | `A, B` 조합은 거부된다 |
| 에셋·오브젝트 참조 피커 | 부분 | ComboBox에 해당 확장자 전체 또는 씬 전체를 나열(`Scripts.cs:160-189`, `Presentation.cs:14-24`, 조인트·스트림 대상 `:200-202`, `:312-314`). 끊긴 참조는 "Missing…"으로 표시(`Scripts.cs:168`) | 검색과 썸네일이 없다. 단일 필드에는 드래그해 놓을 수 없다. PNG 파일을 Sprite에 놓는 기능만 있다(`Sprites.cs:110-127`). 참조 대상을 Hierarchy나 Project에서 강조하는 Ping과 Select가 없다 |
| 컴포넌트 검색·Add Component 검색 | 없음 | `Inspector.cs:99-129` | 내장 컴포넌트 37종이 분류 없이 하위 메뉴 하나에 있다 |
| Tooltip·Range·Header·Space 속성 | 없음 | 인식하는 속성은 `FormerlySerializedAs`뿐(`ProjectScripts.cs:314`). 툴팁은 필드 이름(`Scripts.cs:120`) | 공개 필드만 표시(`:308`). 상수가 아닌 초기화식이 있는 필드는 경고 없이 빠진다(`:366`) |
| 색·곡선·그라디언트 편집 | 부분/없음 | 색은 `#RRGGBB` 텍스트 상자(`Presentation.cs:43`, `Sprites.cs:129-148`). 견본은 Sprite만(`ComponentRows.cs:51`) | 색 피커가 없다. 곡선·그라디언트는 런타임 데이터에도 없다(파티클 색은 하나, `Core/Effects2D.cs:108`) |
| 검증 메시지 | 부분 | 스크립트 오류는 섹션 안에 빨간 글씨(`Scripts.cs:82-86`). 숫자가 틀리면 상태줄(`:204-209`, `Inspector.cs:166,188`) | 컴포넌트 수준 경고(예: Rigidbody만 있고 충돌체 없음, 필드가 숨은 이유)가 없다. 범위 검사는 저장할 때 Core `Validate`에서 예외로 난다(`Effects2D.cs:109-114`) |
| Play 중 편집과 유지·폐기 | 부분 | int·float·double 스크립트 필드와 로컬 Transform만 편집(`Inspector/MainWindow.PlayInspector.cs:124-160`). 체크한 값만 원본에 적용(`:41-65`). Stop하면 나머지는 폐기 | 내장 컴포넌트 필드는 Play 중 꺼진다(`ComponentRows.cs:11`). bool·enum·Vector2·참조는 비활성(`Scripts.cs:75`) |
| Inspector 잠금 | 없음 | — | Hierarchy 잠금은 Scene 클릭용이다(`Hierarchy.cs:124-132`) |
| 비공개 필드 디버그 보기 | 부분 | Play의 Runtime 섹션이 public·private 스칼라 필드 48개까지 읽기 전용으로 표시(`Debugging/MainWindow.Diagnostics.cs:136-157`) | 편집 모드에는 없다 |
| 오브젝트 활성·컴포넌트 켜기/끄기 체크 | 없음 | `SceneObject`에 저장되는 활성 상태가 없다(`Core/SceneDocument.cs:311-370`). `SetActive`는 실행 중에만 있다(`Core/RuntimeObjects.cs:41-42`) | Unity 사용자가 가장 먼저 찾는 체크박스다. "비활성으로 배치한 뒤 스크립트로 켜기"를 씬 데이터로 표현할 수 없다 |
| 에셋 Inspector | 없음 | Project 선택은 Project 패널의 미리보기만 갱신한다(`Project/MainWindow.ProjectPreview.cs:11-44`) | 데이터·클립·애니메이터는 별도 패널에서 편집한다 |
| 사용자 지정 Inspector | 부분 | `AddInspector`가 스크립트 하나의 행 전체를 대체(`EditorExtensions.cs:33-37`, `:74-87`) | 필드 타입별 drawer가 없다. 확장이 바꾼 값을 Undo에 기록하는 API가 없다(`EditorContext` `:48-71`에 RecordEdit 없음) |
| 컴포넌트가 많을 때의 성능 | 문제 | 아래 측정 | |

### 1.3 측정

| 항목 | KeyRoom(44 오브젝트) | CollisionLab Field10000(10,010 오브젝트) |
| --- | --- | --- |
| `CaptureObjects` 1회(모든 Inspector 편집이 `RecordEdit`에서 부름, `ObjectEdits.cs:53-57`) | 0.3 ms, 0.1 MB 할당 | 중앙값 14.4 ms [12.8–97.2], 32.5 MB 할당 |
| 스냅샷 크기 | 0.03 M자 | 5.27 M자 ≈ UTF-16 10.5 MB |
| Undo 100단계 보관 시 스냅샷 메모리(추정: 단계마다 새 문자열 하나) | ≈ 6 MB | ≈ 1.05 GB |
| `ProjectAssets.Scan` 1회(에셋 피커 하나마다 부름, `Presentation.cs:17`, `Scripts.cs:164`) | 2.4 ms | 1.7 ms |

- 텍스트 필드는 입력 한 글자마다 `MarkDirty`를 부른다(`Presentation.cs:28`, `Scripts.cs:198`). 1만 오브젝트 씬에서 이름이나 색을 10자 입력하면 약 144 ms를 쓰고 325 MB를 할당한다(측정값 ×10). 묶음은 Undo 단계 수만 줄이고 스냅샷 비용은 줄이지 않는다.
- Inspector 재구성 시간과 UI 할당은 GUI를 열지 않아 재지 않았다. Tilemap을 선택하면 에셋 피커 1개, 오브젝트 피커 1개(1만 항목), 타일 도구가 만들어진다.

### 1.4 그 밖의 엔진 기능 현황 (10–12장과 4.x 완료분 제외)

| 영역 | 있는 것 | 없는 것 | 근거 |
| --- | --- | --- | --- |
| 파티클 | 개수 ≤256, 수명, 속도, 중력, 크기, 단색. 트리거는 Manual·Landing | 연속 방출률, 방향·퍼짐, 수명에 따른 색·크기·알파, 스프라이트 입자, 로컬·월드 공간 | `Core/Effects2D.cs:93-115` |
| 2D 조명 | 가산 점광원(반경·색·깜박임) | 어둠(주변광)과 라이트맵, 그림자, 스폿 | `docs/manual/effects.md:45-49`. 노멀맵·그림자는 셰이더 확인 전 후보로 남김(`docs/roadmap.md:120`) |
| 배경 | — | 패럴랙스 레이어, 타일 반복 스프라이트 | `SpriteRendererComponent`에 그리기 모드가 없다(`Core/SceneComponent.cs:133-150`) |
| 충돌체 | Box·Circle·Polygon | Capsule, Edge/Chain | 컴포넌트 목록 `SceneComponent.cs:9-87`. BRUTAL.Box2D는 캡슐·체인을 지원한다(`docs/brutal.md:62`) |
| 애니메이터 | 상태·Bool/Float/Trigger, 전이당 조건 1개, 우선순위 | 여러 조건(AND), Any State, 클립 끝 전이, 4·8방향 클립 세트 | `Core/AnimationController.cs:7-16`, `docs/manual/sprites.md:78` |
| 트윈 | 위치 오프셋 컴포넌트 하나(Ease 4종, PingPong) | 스크립트 트윈 API(스케일·색·알파·UI·임의 값), 시퀀스 | `Core/Tween2D.cs:7-22` |
| 월드 텍스트 | 화면 UI 텍스트만 | 데미지 숫자, 이름표, 상호작용 안내(월드 좌표 텍스트) | ScreenUi에 월드 공간 캔버스가 없다(검색 결과 0건) |
| 타일 오토타일 | 4변(16칸)·6변(64칸) 브러시 | 8방향(47칸 blob) 연결, 무작위 변형, 애니메이션 타일 | `docs/manual/tilemaps.md:66-78` |
| 오디오 믹서 | Master·Music·Effects 고정 3버스 | 사용자 버스(Voice·UI·Ambience), 스냅샷 전환, 덕킹 | `Core/AudioMixer.cs:10-12`. SDL_mixer `MixGroup`(BRUTAL)을 이미 쓴다(`:141-142`) |
| 카메라 | 추적·반감기·경계·분할·미니맵·흔들림 | 데드존, 진행 방향 앞보기, 여러 대상 프레이밍 | `Core/Camera2D.cs:17-24` |
| 픽셀 아트 표시 | Letterbox 논리 해상도 | 정수 배율 | `Desktop/DesktopHost.cs:24`. BRUTAL.Sdl에 `IntegerScale`이 있다 |
| 입력 | 액션·재바인딩·패드·포인터 | 홀드·탭·더블탭, 입력 버퍼, 2D 벡터 액션(이동 이름이 고정) | `Core/InputActions.cs:250-251` |
| UI 배치 | 세로·가로 자동 배치·스크롤·목록 바인딩 | 그리드·줄바꿈 배치, UI 드래그 앤 드롭(슬롯) | `Core/ScreenUi.Layout.cs:45` |
| 길찾기 | 플랫폼 보행 A*, 그리드 A*·비용 | 격자가 아닌 탑다운 자유 이동(폴리곤 내비메시·흐름장), 국소 회피 | `docs/manual/navigation.md:5-42` |
| 스크립트 디버그 그리기 | 에디터 충돌체·레이 표시 | 스크립트용 Gizmos(선·원·텍스트) | `Core/PlayDiagnostics.cs:10,68`(내부 전용) |
| 완료로 제외 | 저장(1.6·7.4·8.5), 씬 관리(1.7), 프리팹 변형(3.1, `Prefabs.cs:190`), 현지화 `{0}` 서식(`docs/manual/game-ui.md:85`), 풀링(10.8) | | |

## 2. 부족한 점 / 문제 (우선순위순)

1. **편집 기록 비용이 씬 크기에 비례한다.** 1만 오브젝트에서 키 입력마다 14 ms·32.5 MB가 들고, 100단계 보관 시 약 1 GB까지 쓴다. 대형 씬 작업(8.8·9.3·10.7에서 다룬 규모)에서 Inspector 입력이 끊긴다.
2. **활성·켜짐 상태를 씬에 저장할 수 없다.** 모든 장르에서 쓰는 패턴인데 데이터 모델에 없다.
3. **스크립트 필드 표현력이 부족하다.** 속성, 툴팁, 기본 타입 배열, 색, 플래그, 구조체가 없다. 필드가 숨어도 경고가 없어 사용자가 이유를 모른다(`ProjectScripts.cs:366`).
4. **컴포넌트 단위 작업이 없다.** Reset, Copy/Paste Values, 순서 변경, Add Component 검색이 없다.
5. **다중 편집이 사실상 읽기 전용이다.** 충돌 마스크만 편집할 수 있다.
6. **프리팹 오버라이드를 Inspector에서 볼 수 없다.** 별도 창을 열어야 한다.
7. **참조 피커가 확장되지 않는다.** 전체 목록 ComboBox라 검색, 드래그 놓기, Ping이 없고, 만들 때마다 Scan을 다시 한다.
8. **색은 hex 텍스트로만 입력한다.** 9곳(Presentation 8·Sprite 1)이 그렇고 곡선·그라디언트는 없다.
9. **Play 편집 범위가 좁다.** 숫자와 Transform만 편집할 수 있다.
10. **잠금·편집 모드 디버그 보기, 확장 API의 Undo가 없다.**
11. 엔진 쪽 공백: 파티클 표현력, 패럴랙스·타일 반복, 스크립트 트윈, 캡슐·체인 충돌체, 애니메이터 조건, 월드 텍스트, 조명(어둠·그림자), 정수 배율, 8방향 오토타일, 오디오 버스.

## 3. 제안 작업 목록 (로드맵 행 형식, 우선순위순)

공통 규칙:
- 새 행은 모두 `InspectorRow`/`EditorTokens.LabelColumns(FieldLabelWidth)`를 쓰고 `ComponentSection`을 재사용한다. 머리말 열 구성(`axaml:35` "8,16,*")을 바꿀 때는 모든 섹션에 같은 열을 예약해 제목 기준선을 유지한다. 1280·1600·1920px과 긴 이름으로 캡처를 비교한다(12.8 방식).
- 프로브 제약: `Select`·`ShowSelection`·`AttachComponent`·`FinishObjectChange`·`RebuildHierarchy`·`EditValues` 등은 이름을 바꾸거나 오버로드하지 않는다. 형식 이름 `ComponentSection`과 클래스 `fieldLabel`·`HeaderToggle`을 유지한다. `MainWindow.axaml`은 나누지 않는다. 새 기능은 새 이름의 private 메서드로 넣는다.
- 게임 런타임에는 에디터 코드를 넣지 않는다. 스크립트 속성 형식은 Engine.Core에 두되 `[Conditional]`로 배포 메타데이터에서 뺀다(I-3).

### A. Inspector

| 순서 | 이름 | 내용 | 완료 기준(검증) | 규모 | 의존성 | 용량·성능 | BRUTAL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| I-1 | 편집 기록 비용 줄이기 | 텍스트·숫자 입력은 값만 즉시 반영하고 기록은 Enter·포커스 이탈 때 한 번 한다. Play 숫자 필드(`PlayInspector.cs:95-115`)와 같은 방식이다. 에셋 피커는 Inspector 재구성마다 `ProjectAssets.Scan`을 한 번만 하고 결과를 공유한다. 2차(측정 후 필요하면): 단일 오브젝트 편집은 그 오브젝트만 스냅샷하는 차분 기록 | CollisionLab.EditorPerf 훅으로 Field10000에서 이름 10자 입력 시 `CaptureObjects` 1회, UI 할당 ≤40 MB. KeyRoom 6.8 편집 회귀와 Undo/Redo 결과가 같다. 저장 파일이 바이트 단위로 같다 | S(1차)·M(2차) | 없음 | 에디터 전용. 1만 오브젝트 입력 비용 약 10분의 1(추정) | — |
| I-2 | 오브젝트 활성·컴포넌트 켜짐 체크 | `SceneObject.Active`, `SceneComponent.Enabled`(기본값 true면 YAML에 쓰지 않음)를 추가한다. Inspector 이름 행 왼쪽과 컴포넌트 머리말에 체크박스를 둔다(16px 열을 모든 섹션에 예약). Hierarchy에서 흐리게 표시(Play와 같은 0.55)하고, 런타임 시작 시 `SetActive(false)`·컴포넌트 비활성, 프리팹 오버라이드 키, 월드 저장(7.4)을 처리한다 | KeyRoom에서 비활성으로 둔 코인이 스크립트로 켜지는 것을 Scene Play·Release·Native AOT 기록으로 확인. 기존 씬 파일이 바뀌지 않는다(콘텐츠 해시 동일). 프리팹 Apply/Revert 행에 표시 | M | 없음. 콘텐츠 버전 검토 | 런타임 IL 수 KB(추정). 프레임 비용 없음(시작 시 1회) | — |
| I-3 | 스크립트 필드 속성과 툴팁 | Engine.Core에 Unity와 같은 이름의 `Tooltip`·`Range`·`Min`·`Header`·`Space`·`Multiline`·`HideInInspector`·`SerializeField`(private 노출)를 추가하고 `[Conditional("MONOON_EDITOR")]`를 붙인다. `///` 요약 주석을 툴팁으로 쓴다(Roslyn `GetDocumentationCommentXml`). 표시되지 않는 필드(상수가 아닌 초기화식 등)는 섹션 아래에 회색 안내 한 줄로 알린다 | KeyRoom `PlayerController`에 Range 슬라이더와 Header를 넣고 저장·재열기 값이 같다. Release·Native AOT 게임 dll에 속성 형식 참조가 0건(ildasm/`strings` 확인) | M | I-10(슬라이더 행) | 배포 0 B(Conditional로 적용부 제외. 원본 심볼에서 읽히는지 먼저 확인 필요) | — |
| I-4 | 컴포넌트 메뉴 확장 | Add Component에 검색 상자와 분류(Rendering·Physics·UI·Audio·Animation·Navigation·Scripts)를 둔다. 톱니 메뉴에 Reset, Copy Component, Paste Component Values, Paste as New, Move Up/Down을 추가한다. 구현은 기존 YAML·JSON 직렬화(`PrefabMerge.Snapshot`)를 쓴다: Reset은 `ComponentChoice.Create()`와 `Values.Clear()`로 한다. 각 동작은 Undo 한 번 | KeyRoom 문 Collider 값을 복사해 상자에 붙이고 Undo 1회로 복원. 검색 "col"로 충돌체 3종 표시. 메뉴 폭이 바뀌어도 행 정렬 유지 | M | 없음 | 에디터 전용 | — |
| I-5 | 다중 편집 확장 | Transform 숫자를 입력 가능 필드로 바꾼다(값이 다르면 "—" placeholder). 공통 bool·enum·숫자·스크립트 필드를 JSON 경로(`PrefabProperty`)로 한꺼번에 바꾸고 `RecordEdit(multiSelection)` 한 번으로 기록한다 | WallWorkshop 벽 10개 너비 76→80, Is Trigger, 스크립트 숫자를 함께 바꾸고 Undo 1회로 각 값과 프리팹 오버라이드가 복원(8.3 기록 확장) | M–L | I-1 | 에디터 전용 | — |
| I-6 | 프리팹 오버라이드 인라인 표시 | 오버라이드된 컴포넌트 머리말에 점, 스크립트 필드·Transform 행에는 굵은 레이블과 2px 막대를 표시한다. 막대는 패널 안쪽 여백 안에 음수 여백으로 그려 입력 열을 밀지 않는다. 행 우클릭으로 Apply to Source / Revert. 데이터는 `Scene.PropertyOverrides(root)`를 재사용한다 | 7.2·10.4의 나무 두 그루에서 width 행 표시·개별 Revert. 표시 유무로 행 위치가 1px도 바뀌지 않음(캡처 비교) | M | I-4(행 컨텍스트 메뉴 공유) | 에디터 전용. 선택마다 오버라이드 계산 1회 | — |
| I-7 | 참조 피커 개선 | 드롭다운에 검색 상자를 넣고 썸네일은 Project 썸네일 캐시를 재사용한다. 단일 참조 필드에 Hierarchy·Project 드래그를 놓을 수 있게 한다(`ReferenceDrop` 재사용). 필드 옆 아이콘으로 Ping(대상 행 강조 + `ScrollIntoView`)과 Select를 제공한다 | KeyRoom 문 스프라이트 필드에 Project PNG를 끌어 놓기. 피커 검색으로 Field10000의 오브젝트 하나를 고를 때 UI 멈춤 <100 ms | M | I-1(Scan 공유) | 에디터 전용 | — |
| I-8 | 스크립트 필드 타입 확장 | 색(`Color32` 또는 hex 문자열 + `[ColorUsage]`), 플래그 enum(다중 체크, `ReadEnum`이 쉼표 목록 허용), `int[]`·`float[]`·`string[]`·`bool[]`(참조 목록 UI 재사용), `long`·`uint`, 1단계 `[Serializable]` 구조체(접기 행). 팩토리 코드 생성과 `GameJson` 컨텍스트를 확장한다 | InventoryLab 스크립트에 각 타입 하나씩 추가해 저장·재열기, Scene Play·Release·Native AOT 값 비교(헤드리스 재생) | L | I-3 | 런타임 IL 수 KB, 리플렉션 없음(AOT 유지) | — |
| I-9 | 색 피커 | 내장 컴포넌트와 스크립트 색 필드 9곳에 견본과 HSV·알파 팝업을 붙이고 hex 입력은 유지한다 | Sprite·Line·Light 색을 피커로 바꾸고 Undo 1회. 1280px Inspector 최소 폭에서 정렬 유지 | S–M | 결정 3 | 에디터 전용. 패키지를 쓰면 에디터 배포본 증가(측정 후 기록) | — |
| I-10 | 숫자 레이블 끌기와 Range 슬라이더 | 숫자 행 레이블을 좌우로 끌어 값을 바꾼다(끄는 동안 한 묶음, 기록 1회). Range 필드는 슬라이더와 숫자를 함께 둔다 | KeyRoom Speed 레이블을 끌어 240→300, Undo 1회 | S | I-1 | 에디터 전용 | — |
| I-11 | Play 편집 확대 | Play 중 bool·enum·Vector2·string 스크립트 필드와 내장 컴포넌트의 단순 값(색·숫자·체크)을 편집한다. Apply selected와 "Apply all"을 지원한다 | 9.1 기록에 bool·enum 하나씩 추가해 Stop 후 복원과 선택 적용 확인 | M | I-5의 JSON 경로 쓰기 | 에디터 전용 | — |
| I-12 | Inspector 잠금·편집 모드 디버그 | 탭 머리줄에 자물쇠(선택이 바뀌어도 대상 유지, editor-state.json 저장)와 Debug 보기(편집 모드에서 모든 직렬화 키와 원시 JSON 읽기 전용)를 둔다 | 잠근 상태에서 다른 오브젝트를 선택해도 Inspector 유지. 레이아웃 재시작 복원 | S | 없음 | 에디터 전용 | — |
| I-13 | 확장 API 보강 | `EditorContext.RecordEdit()`·`SetField(component, name, value)`를 Undo와 함께 제공한다. 필드 타입별 `AddFieldDrawer`(속성 이름 기준)를 추가하고 행 함수 중복(`EditorExtensions.cs:61-70`)을 `InspectorRow`로 합친다 | KeyRoom `Editor/`에 drawer 하나 추가, Undo로 되돌리기 | S–M | I-3 | 에디터 전용 | — |
| I-14 | 컴포넌트 경고 줄 | 섹션 위에 노란 한 줄을 표시한다: 충돌체 없는 Rigidbody, 시작 카메라 없음, 누락 에셋, 범위를 벗어나 저장에 실패할 값(`Validate` 규칙을 편집 중에 미리 검사) | KeyRoom에서 충돌체를 지우면 경고, Undo하면 사라짐 | S | 없음 | 에디터 전용 | — |
| I-15 | 곡선·그라디언트 편집기 | E-1의 수명 곡선·색 그라디언트용 행 편집기(점 4–8개). 런타임 데이터가 생긴 뒤에만 만든다 | E-1 파티클 곡선을 편집해 세 실행 경로 프레임 캡처 비교 | M | E-1 | 에디터 전용 | — |
| I-16 | 에셋 Inspector | Project에서 PNG·clip·data·prefab을 선택하면 Inspector에 요약과 가져오기 설정(피벗·필터·아틀라스 그룹)을 표시하고, 기존 패널 열기 버튼을 둔다 | Door.png 선택 시 크기·피벗 표시, 변경 후 재임포트 반영(8.2 경로) | M | 없음 | 에디터 전용 | — |

### B. 엔진(여러 장르 공통)

장르 수는 대략 플랫포머, 탑다운 액션·RPG, 슈팅·탄막, 로그라이크, 퍼즐, 시뮬, 리듬, 카드, 비주얼노벨 9종 중 필요로 하는 수다(추정).

| 순서 | 이름 | 내용 | 완료 기준 | 규모 | 장르 | 용량·성능 | BRUTAL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E-1 | 파티클 확장 | 연속 방출률·버스트, 원뿔 방향·퍼짐, 수명에 따른 색·크기·알파(키 4개), 스프라이트·시트 입자, 로컬·월드 공간, 스크립트 `Emit(id, n)`. 고정 크기 풀, 틱당 할당 0 | PlatformerLab 횃불 연기·착지 먼지. BarrageLab 방식으로 입자 1만 개 프레임 시간·GC 0 확인, 세 실행 경로 캡처 비교 | M | 9/9 | 런타임 IL ≈10 KB(추정). `RenderGeometry` 배칭 1 드로우콜/텍스처 | `Brutal.Sdl` RenderGeometry |
| E-2 | 패럴랙스와 타일 반복 스프라이트 | Sprite Renderer에 Draw mode(Simple·Tiled·Sliced) 추가, `Parallax2D`(카메라 기준 계수 X/Y, 반복) | PlatformerLab 배경 3층. 카메라 이동 시 계수별 이동량 비교, Release·AOT 캡처 | S | 5/9 | IL 수 KB. 드로우콜은 레이어당 1 | `SDL_RenderTextureTiled`·`RenderTexture9GridTiled` |
| E-3 | 스크립트 트윈 API | `tick.Objects.Tween(id).Scale/Rotate/Color/Alpha/Move`, UI Rect·임의 float, 시퀀스·지연, 게임·UI 시간 선택. 9.6의 속성 적용 경로와 11.1 루틴 재사용, 구조체 풀로 할당 0 | KeyRoom 코인 획득 팝·UI 버튼 눌림 연출 다섯 시점 값 비교(9.6 방식) | S–M | 9/9 | IL 수 KB, 활성 트윈당 O(1) | — |
| E-4 | 캡슐·체인 충돌체 | Capsule Collider 2D, Edge/Chain Collider 2D. 자체 질의(겹침·레이·스윕)와 공간 격자(8.8)도 지원 | PlatformerLab 캐릭터를 캡슐로 바꿔 타일 이음새 걸림 0회, 체인 지형 경사 왕복. 1천·1만 질의 대조(8.8 방식) | M | 6/9 | IL 수 KB | `Brutal.Box2D` 캡슐·체인 |
| E-5 | 애니메이터 조건 확장 | 전이당 여러 조건(AND), Any State, 클립 끝 전이, 4·8방향 클립 세트(Vector2 파라미터로 클립 선택) | AnimatorLab 탑다운 8방향 걷기와 공격 후 복귀. 미리보기·Scene Play·AOT 프레임 비교 | M | 6/9 | IL 수 KB. 전이 평가는 상태당 O(조건) | — |
| E-6 | 월드 공간 텍스트 | 월드 좌표를 따라가는 텍스트 컴포넌트(정렬 레이어·Y 정렬 참여, 서식 있는 텍스트 재사용) | 데미지 숫자 100개 동시 표시 프레임·할당, 한글 캡처(11.13 경로에서 재확인) | M | 7/9 | 글리프 시트 공유, 추가 에셋 없음 | 11.13 이후 `Brutal.Sdl.Ttf` |
| E-7 | 2D 조명: 어둠과 그림자 | 주변광 색 + 라이트맵 렌더 타깃에 점·스폿 광원을 가산한 뒤 `BlendmodeMod`로 합성한다. 선택적으로 충돌체 다각형에서 그림자 지오메트리를 만든다. 사용자 셰이더는 쓰지 않는다 | PlatformerLab 동굴 씬 광원 8개·그림자 다각형 200개의 프레임 시간(목표 +1 ms 이하, 측정 후 확정), 세 실행 경로 캡처 | M–L | 5/9 | 렌더 타깃 1장(화면 해상도 RGBA, 1080p 약 8 MB VRAM 추정), 광원당 드로우콜 1–2 | `Brutal.Sdl` 렌더 타깃·`BlendmodeMod`·RenderGeometry |
| E-8 | 정수 배율 표시 | 게임 설정에 Pixel art: integer scale 옵션을 추가하고 카메라 위치를 픽셀에 맞춘다 | DisplayLab에서 창 크기 3종으로 흐림 없는 캡처, 재실행 복원(8.7 방식) | S | 4/9 | 0에 가까움 | `SdlRendererLogicalPresentation.IntegerScale` |
| E-9 | 8방향 오토타일과 타일 변형 | 47칸 blob 브러시, 같은 브러시의 무작위 변형(시드 고정), 프레임 애니메이션 타일(청크 렌더 유지) | SeedLab 탑다운 지형 칠하기·지우기·청크 경계 일치, 변형 시드 재현 | M | 5/9 | 타일 데이터 +1 byte/셀 이하(추정) | — |
| E-10 | 오디오 버스·스냅샷·덕킹 | 프로젝트별 버스 추가(Voice·UI·Ambience), 스냅샷 전환(시간 지정 페이드), 대사 재생 중 음악 덕킹 | AudioLab 대사 재생 시 음악 −8 dB 샘플 단위 비교(2.5·7.8 방식), 설정 저장 복원 | M | 6/9 | 버스당 SDL 그룹 1개 | `Brutal.Sdl.Mixer` `MixGroup` |
| E-11 | 카메라 데드존·앞보기·다중 대상 | Follow에 데드존 사각형, 진행 방향 앞보기, 여러 대상의 경계를 감싸는 프레이밍(줌 포함) | CameraLab 기록 비교(2.4 방식) | S–M | 5/9 | 무시할 수준 | — |
| E-12 | 입력 상호작용·버퍼 | 액션별 Hold·Tap·DoubleTap, 입력 버퍼(N 틱), 2D 벡터 액션(이동 이름 고정 제거) | 헤드리스 입력 재생(5.1)으로 판정 틱 일치 | S–M | 6/9 | 무시할 수준 | — |
| E-13 | UI 그리드·드래그 앤 드롭 | UI Layout에 Grid·Wrap 축, UI 요소 끌어 놓기 이벤트(슬롯 교환) | InventoryLab 가방 4×5 그리드 슬롯 교환·패드 탐색 | M | 5/9 | IL 수 KB | — |
| E-14 | 스크립트 디버그 그리기 | `tick.Debug.Line/Circle/Text` — 개발용 빌드와 Scene Play에서만 그리고, 배포 프로필에서는 호출을 빈 함수로 둔다 | 개발용·배포용 빌드 비교(5.1 프로필), 배포본 추가 코드 없음 확인 | S | 9/9(제작 도구) | 배포본 영향 0 목표 | — |
| E-15 | 탑다운 자유 이동 길찾기 | 충돌체에서 만든 폴리곤 내비메시 또는 흐름장(대량 유닛), 간단한 분리 회피 | SeedLab 유닛 200개가 장애물을 돌아 목표 도달, 틱당 할당 0 | L | 4/9 | 메시 데이터 KB 단위 | — |

## 4. 결정이 필요한 것

1. **Undo 개선 범위**: (A) 입력 확정 시에만 기록 + Scan 공유(S), (B) 오브젝트 단위 차분 기록까지(M). → **A부터.** 측정 후 1만 오브젝트에서 여전히 50 ms를 넘을 때만 B.
2. **스크립트 속성 이름**: (A) Unity와 같은 이름(`Tooltip`, `Range`, `SerializeField`, `HideInInspector`), (B) MoNoon 고유 이름. → **A.** Unity 코드를 옮기기 쉽고, `FormerlySerializedAs`(10.1)를 이미 같은 이름으로 쓴다.
3. **색 피커 구현**: (A) `Avalonia.Controls.ColorPicker` 11.3.x 패키지(에디터 전용, 새로 받아야 하고 에디터 배포본이 커짐 — 크기는 받은 뒤 측정), (B) 기존 컨트롤로 만든 작은 HSV 팝업. → **A.** 게임 배포본에는 영향이 없고 코드가 가장 적다. 에디터 패키지 증가가 1 MB를 넘으면 B.
4. **활성·켜짐 상태 형식**: 기본값 true는 쓰지 않아(I-2) 기존 파일을 그대로 두는 안 vs 콘텐츠 버전을 올리는 안 → **기본값 생략.** 콘텐츠 버전은 읽기 규칙이 바뀌는 경우에만 올린다.
5. **2D 조명 방식**: (A) 셰이더 없이 라이트맵 `Mod` 합성 + 지오메트리 그림자(E-7), (B) SDL GPU 사용자 셰이더 조사 후 노멀맵까지. → **A.** BRUTAL SDL 렌더러만으로 된다. B는 11장 후보로 계속 둔다.
6. **로드맵 배치**: Inspector 항목을 13장, 엔진 항목을 14장으로 새로 만들지, 11.13(Sdl.Ttf) 뒤에 이어 번호를 붙일지 → **13장(Inspector)·14장(엔진).** E-6 월드 텍스트는 11.13 뒤로 순서를 고정한다.
7. **필드 타입 확장 깊이(I-8)**: 구조체 1단계와 기본 타입 배열까지만 vs `List<T>`·딕셔너리까지 → **1단계·배열까지.** 딕셔너리는 데이터 테이블(3.2)로 안내한다.

참고(공개 문서): Unity Inspector 옵션 https://docs.unity3d.com/Manual/InspectorOptions.html · 속성 https://docs.unity3d.com/ScriptReference/TooltipAttribute.html, https://docs.unity3d.com/ScriptReference/RangeAttribute.html, https://docs.unity3d.com/ScriptReference/HeaderAttribute.html, https://docs.unity3d.com/ScriptReference/SerializeField.html · 프리팹 오버라이드 https://docs.unity3d.com/Manual/PrefabInstanceOverrides.html · Godot export https://docs.godotengine.org/en/stable/tutorials/scripting/gdscript/gdscript_exports.html (링크는 기억에 의존했고 이번에 열어 확인하지 않았다).
