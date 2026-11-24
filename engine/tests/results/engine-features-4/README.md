# 엔진 기능 4: 월드 공간 텍스트, UI 그리드와 드래그 앤 드롭 (16.6 · 16.13)

2026-11-24, macOS 27 (Apple Silicon arm64), .NET 10. 기준 커밋 `f48fc00`(wip/next1), 16.6 커밋 뒤 wip/next1 `fe85d00`(16.9·16.5)을 병합하고 16.13을 만들었다. 16.13 커밋 뒤 wip/next1을 세 번 더 병합했다: `419690e`(16.10, Inspector 13.5–13.16), `77decf3`(16.7 조명, schemaVersion 42), `1c34b81`(16.5·16.9 후속). 두 하네스는 `77decf3` 병합까지 세 경로 모두, `1c34b81` 병합 뒤에는 Release·Native AOT를 다시 실행해 모두 같은 해시를 얻었고(에디터 Scene Play는 `77decf3` 병합 때가 마지막), 전체 `./monoon test` 945개가 크기 게이트를 포함해 통과했다. 실행 대상은 에디터 Scene Play, Release, Native AOT(모두 배포용 프로필)이고 빌드는 이 Mac에서 `./monoon build`로 만들었다(osx-arm64). 16.6 측정 중에는 다른 작업의 `qemu-system`이 실행 중이었고 16.13 측정 중에는 없었다. 판정은 결정성 기준(캡처·트레이스 바이트 일치, 할당 바이트 0)이고, 시간 값은 참고용이다. x64(Linux 게스트)는 coordinator 몫이고 Windows는 보류다.

## 16.6 월드 공간 텍스트 (Text 2D)

### 배포본 영향

- **코드 크기**(같은 샘플·같은 방법으로 `f48fc00` 플레이어 템플릿과 비교, [sizes.tsv](sizes.tsv)): KeyRoom Native AOT 실행 파일 8,653,728 → 8,703,424 B(+49,696 B, `__managedcode` +19,352 B), 앱 +49,696 B. PlatformerLab Native AOT 실행 파일 +33,184 B(`__managedcode` +19,320 B). Release 앱은 KeyRoom +20,992 B, PlatformerLab +21,504 B(트리밍한 `Engine.Core.dll`의 ReadyToRun 코드). 대부분은 새 컴포넌트 하나가 늘리는 YAML·JSON 다형 직렬화 코드(`text2D` 판별자, `TextAlign2D`)이고 그리기 코드는 작다. Text 2D를 쓰지 않는 게임도 이 코드는 포함한다(컴포넌트 목록이 고정 합집합이라 트리밍되지 않음).
- InventoryLab 콘텐츠 +3,635 B(씬 1,936 B, 프리팹 283 B, 스크립트 2개 1,416 B) + 번역 테이블 한 줄.
- 크기 게이트(`./monoon test size`, 기준선 `f397874`)는 InventoryLab Native AOT 실행 파일 8,404,496 B > 8,305,056 B + 1%로 실패했다. 기준선 뒤에 병합된 16.4·16.11·16.12(약 +50 KB)와 이번 +50 KB가 겹친 것이다. 최종 병합 뒤의 판정은 아래 [크기 게이트(최종)](#크기-게이트최종)에 있다.
- **프레임당 비용**: Text 2D마다 그리기 1회(같은 문자열은 SDL_ttf 문구 텍스처 1장 공유, `SdlText` 캐시 128개). 문구가 그대로면 그리기 할당 0 B. 서식 텍스트 배치는 문구가 바뀔 때만 다시 만든다. 번역 키는 언어가 바뀔 때만 다시 찾는다(`ko-KR`처럼 지역 태그면 매 프레임 부분 문자열을 잘라내는 비용을 피하려고).
- 데미지 숫자 100개(서로 다른 문자열, Position Tween 2D로 상승, Y 정렬)를 플레이어와 같은 `SdlCanvas`(SDL 소프트웨어 렌더러, SDL_ttf)로 600프레임 단계 + 그리기 + 표시: **그리기 할당 0 B**, 프레임 중앙값 248–279 µs, p95 275–314 µs(두 번 실행, QEMU 부하 중). 그리기 제출 100회/프레임. `f48fc00`에서는 단계(`FixedUpdate`)가 600프레임에 5,299,200 B를 할당했는데(Position Tween 2D 100개의 기존 이동 경로), 16.10의 물리 할당 제거를 병합한 뒤에는 단계도 0 B다(중앙값 278 µs, p95 303 µs). [검사 출력](world-text-check.txt)
- BRUTAL: 글자는 11.13의 `Brutal.Sdl.Ttf` 경로(`TtfTextRasterizer` → `SdlText`)를 그대로 쓴다. SDL_ttf의 `TTF_CreateRendererTextEngine`(글리프 아틀라스)은 쓰지 않았다. 화면 텍스트와 다른 래스터 경로가 생겨 같은 글자가 UI와 월드에서 다르게 보이고, 11.13이 우회한 대체 글꼴 밑선 문제를 다시 다뤄야 하기 때문이다. 대신 화면 텍스트의 문구 캐시를 공유한다.
- 기능 제외(15.11): `Text2DComponent`에 `[EngineFeature(Text)]`를 붙여, 배포 씬·프리팹에 Text 2D가 있으면 SDL_ttf를 포함한다(`BuildFeatures.Of` 검사).

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `engine/src/Engine.Core/WorldText2D.cs` | `Text2DComponent`(Text, Text key, Size, Color, Align, Rich text, Layer, Order, Use foot Y, 검증), `WorldTextLabel`(번역·서식 배치 캐시, 카메라·배율을 따른 위치와 크기, 화면 밖 생략), `SceneGame.SetWorldText` |
| `engine/src/Engine.Core/SceneComponent.cs` | `text2D` 판별자 |
| `engine/src/Engine.Core/SceneDocument.cs` | Text 2D가 있으면 schemaVersion 40(36–39는 다른 작업 예약), 검증 연결 |
| `engine/src/Engine.Core/SceneGame.cs` | `Body.Text` 한 줄, `DrawCamera`의 그리기 한 줄 |
| `engine/src/Engine.Core/WorldDrawOrder.cs`, `RenderGrid.cs` | 정렬 레이어·Order·Y 정렬 참여, 렌더 격자에서는 오브젝트 사각형 밖으로 나갈 수 있는 항목으로 처리 |
| `engine/src/Engine.Core/RuntimeObjects.cs` | `IGameObjects.SetWorldText`(기본 구현 false) |
| `engine/src/Engine.Editor/Inspector/MainWindow.Presentation.cs` | 기존 `Text`·`Number`·`Color`·`Choice`·`Sorting` 행으로 Text 2D 섹션 |
| `engine/src/Engine.Editor/Scene/SceneViewport.cs`, `SceneViewport.Render.cs` | 편집 화면 그리기와 정렬 입력 |
| `engine/tests/Engine.Checks/WorldTextChecks.cs`, `Program.cs` | 단위 검사, `--only world-text` |
| `engine/tests/InventoryLab.WorldText/` | 세 경로 하네스 |
| `samples/InventoryLab` | `Scenes/Labels.scene`(포함 씬에 추가), `Assets/Labels/DamageNumber.prefab`, `Scripts/Labels/DamageBurst.cs`·`RisingNumber.cs`, `Herbalist.table`의 `label.herbalist` |
| `docs/manual/game-ui.md`, `sprites.md` | [월드에 붙는 이름표와 데미지 숫자](../../../../docs/manual/game-ui.md#월드에-붙는-이름표와-데미지-숫자) |

### 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| 캡처 6장(10·30·79·100·120·230) 바이트 일치 | 통과 | 통과 | 통과 |
| 한글: 번역 키 이름표 ‘약초상’, 서식 간판, 제목 | 통과(캡처) | 통과 | 통과 |
| 풀 재사용: 30 = 120, 숫자가 없는 10 = 100 | 통과 | 통과 | 통과 |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) | 해당 없음 | 통과(InventoryLab 앱에서 0개) | 통과(0개) |
| Inspector 1280·1600·1920 px: Text 2D 7행의 레이블 x·입력 x·폭이 같음 | 통과 | 해당 없음 | 해당 없음 |

[캡처 비교](world-text-comparison.tsv) · [Scene Play 30](world-text-scene-play-0030.png) · [Release 30](world-text-release-0030.png) · [Native AOT 79](world-text-native-aot-0079.png) · [Inspector 위치](world-text-inspector.txt) · [1280 px Inspector](world-text-inspector-1280.png)

`WorldTextChecks`: YAML 왕복과 schemaVersion 40, Text 2D 없는 씬은 이전 버전 유지, 크기 0·잘못된 색·정의되지 않은 Align 저장 거부, Text 기능 표시, 줌 2 카메라에서의 위치·크기, Layer 1 이름표가 Layer 0 스프라이트 위, 서식 태그(굵게 2회·색 구간)와 없는 번역 키의 대체 문구, `SetWorldText`의 대상 제한과 다음 프레임 반영, 데미지 숫자 100개 할당. 전체 `./monoon test`는 크기 게이트를 빼고 모두 통과했다(831개).

재현: [하네스 README](../../InventoryLab.WorldText/README.md)

### 확인하지 않은 것

- Scene 편집 화면(Play 전)의 Text 2D 그림은 캡처로 확인하지 않았다(같은 `WorldTextLabel.Draw`를 쓴다).
- 회전한 오브젝트(회전은 적용하지 않는다고 문서화), 알파가 있는 글자색, `<icon>` 태그.
- Windows·Linux(coordinator, Windows는 보류).

## 16.13 UI 그리드와 드래그 앤 드롭

### 배포본 영향

- **코드 크기**(병합 커밋 `874213f`의 플레이어 템플릿과 같은 방법으로 비교, [sizes.tsv](sizes.tsv)): KeyRoom·PlatformerLab Native AOT 실행 파일 각각 +16,592 B(16 KB 페이지 하나, `__managedcode` +8,724 / +8,788 B), Release 앱 각각 +24,064 B. 레이아웃 분기, 끌어 놓기 상태와 입력 처리, `UiDrop`·`OnUiDrop`, YAML 키 두 개(`columns`, `draggable`)의 읽기 코드다.
- InventoryLab 콘텐츠 +8,715 B(씬 7,807 B, 스크립트 908 B).
- 크기 게이트: 첫 병합 뒤에는 `f397874` 기준선을 넘어 기준선을 갱신했지만, 두 번째 병합에서 coordinator가 16.4–16.12로 다시 잡은 기준선(`f3d4bc3`)을 받아들였다. 아래 최종 판정 참고.
- **프레임당 비용**: Grid·Wrap은 기존 레이아웃과 같은 네 번의 트리 순회 안에서 자식 수에 비례하는 계산만 더한다(할당 없음). 끌어 놓기는 입력이 있을 때만 상태를 바꾼다. 놓기 대상 찾기(`LastOrDefault`)는 놓는 순간 한 번 람다를 할당한다. 놓기 전달 대리자는 `SceneGame`에 한 번 캐시한다.
- 결정성: 칸 위치는 덧셈·곱셈뿐이고, 판정은 입력 이벤트 순서만 따른다. 세 경로 트레이스가 바이트 단위로 같다.
- BRUTAL에는 UI 레이아웃·끌어 놓기 기능이 없다(SDL 입력 이벤트까지만). 기존 MoNoon `ScreenUi`를 확장했다.

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `engine/src/Engine.Core/UiAccessibility.cs` | `UiLayoutComponent.Columns`(null이면 쓰지 않음) |
| `engine/src/Engine.Core/ScreenUi.Layout.cs` | Grid·Wrap 축: 칸 수·칸 폭(`Cells`), 행 높이와 내용 길이, 행 우선 배치, 세로 스크롤 |
| `engine/src/Engine.Core/UiDragDrop.cs` | 끌기 시작(4 px), 놓기, 활성화로 집기·놓기, 취소, 놓기 큐, 집은 칸 테두리와 끄는 사본 그리기, `SceneGame.NotifyDrop`(대상 → 스크립트가 있는 가장 가까운 부모) |
| `engine/src/Engine.Core/ScreenUi.cs`, `ScreenUi.Input.cs`, `ScreenUi.Controls.cs`, `ScreenUi.Paint.cs` | `UiButtonComponent.Draggable`(true일 때만 YAML·JSON에 씀), `UiDrop`, 입력·그리기 연결 |
| `engine/src/Engine.Core/GameBehaviour.cs`, `SceneGame.cs` | `OnUiDrop`, `Dispatch` 호출에 놓기 대리자 |
| `engine/src/Engine.Core/SceneDocument.cs` | Grid·Wrap 또는 끌어 놓기 칸이 있으면 schemaVersion 41, 축 이름에 Grid·Wrap 허용, Columns 1–256 |
| `engine/src/Engine.Editor/Inspector/MainWindow.Presentation.cs` | Axis 선택지 두 개, Columns 행(Grid일 때만 켜짐, 0은 값 없음), UI Button의 Drag and drop slot 체크 |
| `engine/tests/Engine.Checks/UiGridChecks.cs`, `Program.cs` | 단위 검사, `--only ui-grid` |
| `engine/tests/InventoryLab.BagGrid/` | 세 경로 하네스 |
| `samples/InventoryLab` | `Scenes/BagGrid.scene`(포함 씬에 추가), `Scripts/Bag/BagGrid.cs` |
| `docs/manual/game-ui.md` | [자동 배치와 스크롤](../../../../docs/manual/game-ui.md#자동-배치와-스크롤)의 Grid·Wrap, [가방 칸 끌어 놓기](../../../../docs/manual/game-ui.md#가방-칸-끌어-놓기) |

### 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| 가방 트레이스(23줄) 바이트 일치 | 기준 | 통과 | 통과 |
| 캡처 9장 바이트 일치(끄는 사본, 집은 칸 테두리 포함) | 통과 | 통과 | 통과 |
| 마우스 끌기 0 → 5, 클릭 집기·놓기 2 → 3 | 통과 | 통과 | 통과 |
| 키보드 ←, ↓, Enter, →, ↓, Enter로 빵·등불 교환 | 통과 | 통과 | 통과 |
| 패드 South 집기, East 취소(놓기 없음), D-pad 이동 뒤 South 놓기 | 통과 | 통과 | 통과 |
| 금지 파일 | 해당 없음 | 통과(0개) | 통과(0개) |
| Inspector 1280·1600·1920 px: Axis·Columns·Gap·Padding 행의 레이블 x·입력 x·폭이 같음 | 통과 | 해당 없음 | 해당 없음 |

[캡처 비교](bag-comparison.tsv) · [트레이스](bag-scene-play.trace) · [Scene Play 15(끄는 중)](bag-scene-play-0015.png) · [Release 50(집은 칸)](bag-release-0050.png) · [Native AOT 225](bag-native-aot-0225.png) · [Inspector 위치](bag-inspector.txt) · [1280 px Inspector](bag-inspector-1280.png)

[`UiGridChecks`](ui-grid-check.txt): Grid 4의 20칸 위치(82.5 × 58 칸, 간격 6), 숨긴 칸은 자리를 차지하지 않음, Wrap의 줄당 칸 수와 폭이 줄 때 다시 배치, 끌어 놓기(클릭 없음), 4 px 이하 이동은 클릭으로 집기, 두 번째 클릭으로 놓기, 일반 버튼 위나 빈 곳에서 놓으면 아무 일 없음, 일반 버튼 클릭 유지, Enter·방향키·Escape, 패드 South·D-pad·East, 집은 칸을 다시 활성화하면 내려놓기, schemaVersion 41 왕복과 쓰지 않으면 키를 쓰지 않음, Columns 0 거부. `419690e` 병합 뒤 전체 `./monoon test` 895개, `77decf3` 병합 뒤 909개가 크기 게이트를 포함해 모두 통과했다. `1c34b81` 병합 뒤에는 945개가 모두 통과했다. `./monoon ci`(기존 InventoryLab 장면의 Release·Native AOT 재생): `419690e` 병합 뒤 47개 통과, 체크포인트 3개와 입력 재생 저장(171 B, SHA-256 `EED41DEF…`)이 두 배포본에서 바이트 단위로 같고 크기 게이트도 통과했다. 기존 x64 재생 기준 저장은 x64 게스트에서 다시 확인하지 않았다(coordinator).

재현: [하네스 README](../../InventoryLab.BagGrid/README.md)

### 확인하지 않은 것

- 데이터 목록(`BindList`)의 풀 행을 끌어 놓기 칸으로 쓰는 경우. 놓기 이벤트는 `ClickTarget`으로 목록 ID에 연결되지만 검사하지 않았다.
- 스크롤 뷰 안의 그리드에서 집은 칸 테두리는 스크롤 영역으로 잘리지 않는다. 끄는 동안 자동 스크롤은 없다.
- 끌어 놓기 칸의 `OnUiClick`은 일부러 보내지 않는다(문서화). 칸을 집지 않고 "사용"하려면 별도 버튼이나 액션이 필요하다.
- Windows·Linux(coordinator, Windows는 보류).

## 크기 게이트(최종)

`77decf3`(16.7 조명) 병합 뒤 `./monoon test` 909개가 크기 게이트를 포함해 모두 통과했다(6개 배포 빌드가 coordinator의 `f3d4bc3` 기준선 +1% 이내). 기준선에는 16.6·16.13·16.7이 없으므로 아래 차이는 세 항목의 합이다(InventoryLab은 새 씬 2개·프리팹·스크립트 3개 포함). **InventoryLab Native AOT 실행 파일은 한도 8,471,815 B에 919 B 남았다(`1c34b81` 병합 뒤 8,470,880 B, 935 B).** 다음 엔진 변경은 게이트를 넘을 가능성이 크므로 coordinator가 병합 후 기준선을 다시 잡아야 한다. 이 작업에서는 기준선을 바꾸지 않았다.

| 샘플 | Release 앱 | Native AOT 앱 | Native AOT 실행 파일 |
| --- | --- | --- | --- |
| KeyRoom | 33,044,250 → 33,139,994 B (+95,744) | 15,858,263 → 15,908,119 B (+49,856) | 8,719,936 → 8,769,792 B (+49,856, +0.57%) |
| PlatformerLab | 30,303,066 → 30,427,118 B (+124,052) | 13,060,557 → 13,155,265 B (+94,708) | 8,371,424 → 8,437,824 B (+66,400, +0.79%) |
| InventoryLab | 83,724,764 → 83,867,453 B (+142,689) | 66,480,847 → 66,582,592 B (+101,745) | 8,387,936 → 8,470,896 B (+82,960, +0.99%) |

`419690e` 병합 뒤(16.7 이전) 같은 측정은 KeyRoom·PlatformerLab Native AOT 실행 파일 +33,264 / +33,248 B, InventoryLab +66,336 B, Release 앱 +47,616 B(KeyRoom·PlatformerLab)로, 이것이 16.6 + 16.13만의 합이다.

항목별 A/B(같은 방법, [sizes.tsv](sizes.tsv)): 16.6은 Native AOT 실행 파일 +33,184–49,696 B(`__managedcode` 약 +19.3 KB), Release 앱 +21 KB. 16.13은 실행 파일 +16,592 B(`__managedcode` 약 +8.8 KB), Release 앱 +24 KB. 실행 파일은 16 KB 페이지 단위로 늘어서 항목별 값을 더해도 합계와 맞지 않는다. 시작 시간은 게이트가 판정하지 않는다(부하에 따라 움직임).
