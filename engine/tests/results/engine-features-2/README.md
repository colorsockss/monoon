# 엔진 기능 2: 스크립트 디버그 그리기, 카메라 프레이밍, 입력 상호작용 (16.14 · 16.11 · 16.12)

2026-11-24, macOS 27 (Apple Silicon arm64), .NET 10. 기준 커밋 `fdc1eb3`(wip/next1). 실행 대상은 에디터 Scene Play, Release, Native AOT이며 빌드는 모두 이 Mac에서 `./monoon build`로 만들었다(osx-arm64). 측정하는 동안 다른 작업의 `qemu-system`이 실행 중이었다. 이 문서의 판정은 모두 결정성 기준(바이트·개수 일치)이고 시간 측정은 하지 않았다. x64(Linux 게스트)는 coordinator 몫이다.

## 16.14 스크립트 디버그 그리기

### 배포본 영향

- **배포용 빌드에 추가되는 코드는 0바이트다.** 기준 커밋 엔진으로 만든 CameraLab 배포 빌드와, 16.14 엔진에 `tick.Debug` 호출을 넣은 CameraLab 배포 빌드를 비교했다. Release 앱(29,196 KB)과 Native AOT 앱(12,268 KB)의 모든 파일 크기가 같고, 다른 것은 새 `DebugDraw` 액션 한 줄이 들어간 `Game/GameSettings`(+42 B)뿐이다. Native AOT 실행 파일(8,139,200 B)의 `__managedcode`는 4,506,408 B로 같다. `__const`·`hydrated`의 +80 B는 `CallerFilePath` 문자열 두 개에 들어간 키트 폴더 경로 길이 차이(공유 캐시 경로 → 워크트리 `player-templates` 경로)이고 엔진 코드와 무관하다. [크기 비교](debug-draw-sizes.txt)
- 같은 배포 빌드를 두 번 만들면 Release는 빌드 기록 파일 외에 바이트 단위로 같고, Native AOT는 같은 크기에 UUID·애드혹 서명 47바이트만 다르다. 그래서 비교는 파일 크기와 섹션 크기로 했다.
- 두 단계로 없앤다. (1) `ScriptDebug.Line/Circle/Text`는 `[Conditional("MONOON_DEBUG_DRAW")]`이라 기호 없이 컴파일한 스크립트에서는 C# 컴파일러가 호출과 인수 계산을 지운다. Scene Play(`ProjectScripts.ForScenePlay`)와 개발용 게시의 `GameScripts.csproj`만 기호를 넣는다. (2) 엔진의 그리기 경로는 `ScriptDebug.IsSupported` 기능 스위치 뒤에 있고, 배포용 게시는 생성한 `Scripts.props`에 `MoNoon.ScriptDebugDraw=false`(Trim)를 넣어 ILLink·ILC가 분기를 지운다. 기존 `MoNoon.ExportedGameOnly`와 같은 방식이다.
- 크기 게이트 `./monoon test size`는 통과했다(6개 배포 빌드가 기준선 +1% 이내). 기준선(`6077d17`에 기록)과 비교하면 KeyRoom Native AOT 실행 파일이 8,537,616 → 8,554,160 B(+16,544 B)지만, 위의 같은 샘플 A/B 비교에서 16.14의 코드 증가가 0이므로 이 차이는 기준선 기록 뒤 `fdc1eb3`까지 병합된 다른 변경 몫으로 본다(기준 커밋으로 KeyRoom을 다시 빌드해 확인하지는 않았다). 기준선은 갱신하지 않았다.
- 개발용은 의도대로 커진다(개발용 Native AOT 실행 파일 8,321,552 B, PDB·dSYM 포함 앱 46,248 KB). 배포 프로필과 비교할 대상이 아니다.
- BRUTAL: 그리기는 기존 `ICanvas2D.FillStrip`·`DrawText`(플레이어는 BRUTAL SDL3 렌더러)를 쓴다. BRUTAL에 스크립트 디버그 그리기 기능은 없다.

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `engine/src/Engine.Core/ScriptDebug.cs` | `ScriptDebug`(조건부 `Line`·`Circle`·`Text`), 기능 스위치, 정적 도형 목록 `DebugShapes`(틱·UI 프레임별 수명, 4,096개 상한, 용량 재사용, 원 32분할 띠) |
| `engine/src/Engine.Core/GameBehaviour.cs` | `ScriptTick.Debug`, `UiTick.Debug` |
| `engine/src/Engine.Core/SceneGame.cs` | `DrawCamera`의 진단선 다음 한 줄: 미니맵이 아니면 `DebugShapes.Draw` |
| `engine/src/Engine.Authoring/ProjectScripts.cs` | Scene Play 파싱에 `MONOON_DEBUG_DRAW` 기호 |
| `engine/src/Engine.Authoring/NativeAotBuild.cs` | 개발용 `DefineConstants`, 배포용 `RuntimeHostConfigurationOption` |
| `samples/CameraLab` | `DebugDraw` 액션(G), `FieldPlayer`의 `#if MONOON_DEBUG_DRAW` 기즈모 |
| `engine/tests/Engine.Checks/ScriptDebugChecks.cs`, `Program.cs` | 단위 검사, `--only script-debug` |
| `engine/tests/CameraLab.DebugDraw/` | 세 경로 하네스 |
| `docs/manual/scripting.md` | [디버그 선·원·텍스트 그리기](../../../../docs/manual/scripting.md#디버그-선원텍스트-그리기) |

### 검증

| 검사 | Scene Play | 개발용 Release | 개발용 Native AOT | 배포용 Release | 배포용 Native AOT |
| --- | --- | --- | --- | --- | --- |
| G 전 디버그 도형 없음 | 통과(0개) | 통과 | 통과 | 통과 | 통과 |
| G 뒤 원·선·텍스트 | 통과(화면 2개 × 3) | 통과(금색 446·하늘색 562 px) | 통과(같은 값) | 해당 없음 | 해당 없음 |
| G 뒤 프레임 = G 없는 프레임 | 해당 없음 | 해당 없음 | 해당 없음 | 통과(바이트 일치) | 통과(바이트 일치) |
| 코드 크기 = 기준 커밋 | 해당 없음 | 해당 없음 | 해당 없음 | 통과(모든 파일 크기 같음) | 통과(`__managedcode` 같음) |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) | 해당 없음 | 통과 | 통과 | 통과 | 통과 |

[Scene Play 기록](debug-draw-scene-play.txt) · [플레이어 기록](debug-draw-players.txt) · [개발용 Native AOT 프레임 30](debug-draw-development-aot.png) · [배포용 Native AOT 프레임 30](debug-draw-distribution-aot.png)

`ScriptDebugChecks`: Scene Play 컴파일에 호출이 남고 기호 없는 컴파일에는 `ScriptDebug` 참조가 없다(메타데이터 검사). 게임 도형은 한 틱, UI 도형은 한 UI 프레임 보이고 일시정지 중에는 남는다. 인수는 Scene Play에서만 계산된다. 목록이 자란 뒤 100프레임 × 20개 그리기의 할당은 0바이트다. 비유한 좌표는 버리고 4,096개에서 멈춘다.

재현: [하네스 README](../../CameraLab.DebugDraw/README.md)

### 확인하지 않은 것

- 키트 없이 공유 Release 템플릿을 복사하는 예전 경로(`native-aot` 키트가 없을 때)는 개발용이어도 기호를 넣지 않아 그리지 않는다. 키트는 `build-players.sh`가 항상 만든다.
- `./monoon test project`(WithTests) 컴파일은 기호를 넣지 않는다.
- Windows·Linux 플레이어(coordinator, Windows는 보류).

## 16.11 카메라 데드존·앞보기·다중 대상

### 배포본 영향

- `./monoon test size`의 같은 세 샘플을 16.14 커밋과 비교했다(카메라 기능을 쓰지 않는 기존 샘플, 엔진 코드만 차이). Native AOT 실행 파일 +16 B(KeyRoom 8,554,160 → 8,554,176, PlatformerLab·InventoryLab도 +16 B), Native AOT 앱 +176–496 B. Release 앱은 +10,912–11,232 B로, 트리밍한 `Engine.Core.dll`에 ReadyToRun으로 미리 컴파일된 카메라 단계 코드다. 게이트는 통과했다(+1% 이내). [16.14 뒤](size-after-16.14.txt) · [16.11 뒤](size-after-16.11.txt)
- 프레임당 비용: 카메라마다 고정 틱에 상수 연산(앞보기 곱셈, 데드존 비교 두 번)이 더해진다. Frame 그룹은 멤버 수에 비례해 변환 사각형을 합친다(목록 조회는 기존 `FindByGroup`, 할당 없음). 시간은 측정하지 않았다.
- 결정성: 위치·줌 보간은 기존 `Affine2D.Lerp`와 덧셈·곱셈만 쓴다. 결정성 API 검사가 통과했다.
- BRUTAL에 카메라 추적 기능은 없다.

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `engine/src/Engine.Core/Camera2D.cs` | `DeadZoneWidth`·`DeadZoneHeight`·`LookAhead`·`FrameGroup`·`FramePadding`(기본값 0·null, YAML 기본값 규칙 그대로), 검증, `Port`, 줌을 받는 `View` |
| `engine/src/Engine.Core/SceneCameras.cs` | 앞보기 → 데드존 → 반감기 순서의 목표 계산, 그룹 경계·줌(`MinZoom`–`Zoom`, Bounds가 하한), 대상이 사라졌다 돌아올 때 속도 초기화, 렌더 보간에 줌 포함 |
| `engine/src/Engine.Core/SceneGame.cs` | `Body`의 카메라 상태 필드 한 줄(`CameraTarget`, `CameraZoomStart/End`, `CameraTracked`) |
| `engine/src/Engine.Core/SceneDocument.cs` | 이 설정을 쓴 씬은 schemaVersion 33(이전 플레이어는 거부). **다른 묶음이 33을 쓰면 coordinator가 번호를 조정해야 한다.** |
| `engine/src/Engine.Editor/Inspector/MainWindow.Cameras.cs` | Half-life 아래 행 4개: Deadzone(W·H 한 줄), Lead time, Frame, Padding. 기존 `Field`와 Bounds의 두 칸 그리드를 그대로 썼다. Inspector에서 고친 파일은 이것 하나다. |
| `engine/tests/Engine.Checks/CameraFramingChecks.cs`, `Program.cs` | 단위 검사, `--only camera-framing` |
| `engine/tests/CameraLab.Framing/` | 세 경로 하네스(복사본에만 설정을 넣어 기존 CameraLab 기록·화면 효과 기준은 그대로) |
| `docs/manual/cameras.md` | [데드존, 앞보기, 여러 대상 담기](../../../../docs/manual/cameras.md#데드존-앞보기-여러-대상-담기) |

### 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| 520틱 카메라 트레이스 바이트 일치 | 기준 | 통과 | 통과 |
| 데드존: 멈춘 뒤 왼쪽으로 걸어도 30틱 정지 | 통과 | 통과 | 통과 |
| 앞보기: 플레이어가 중심에서 9.1 px일 때(데드존 안) 카메라 이동 시작(215틱) | 통과 | 통과 | 통과 |
| 그룹 프레이밍: 줌 0.446–1.000, 엇갈림(48틱) 뒤 56틱에 0.893, 40틱 뒤 0.494 | 통과 | 통과 | 통과 |
| 금지 파일 | 해당 없음 | 통과 | 통과 |
| Inspector 1280·1600·1920px: 새 행의 레이블 x·입력 x·너비·높이가 Half-life·X / Y 행과 같음 | 통과 | 해당 없음 | 해당 없음 |

[판정 출력](framing-check.txt) · 트레이스: [Scene Play](framing-scene-play.trace) · [Release](framing-release.trace) · [Native AOT](framing-native-aot.trace) · [Inspector 위치](framing-inspector.txt) · [1280px Inspector](framing-inspector-1280.png) · [1920px Inspector](framing-inspector-1920.png)

`CameraFramingChecks`: 데드존 안 정지·가장자리 추종·되돌아올 때 정지, 0.5초 앞보기의 정확한 앞섬과 멈춤 시 소거, 비활성 뒤 재활성 시 속도 튀지 않음, 그룹 중심과 `800/(1010+2×20)` 줌, 모이면 Zoom까지 복귀(초과 없음), 멤버 없음 시 유지, 반감기 줌 완화, Bounds 하한, schemaVersion 33, YAML 왕복, 잘못된 값 거부. 기존 `CameraChecks`(CameraLab 추적 수치 포함)와 전체 `./monoon test`가 통과했다.

Inspector 레이블은 기본 패널 폭에서 잘리지 않도록 짧게 정했다(처음 쓴 "Dead zone W / H"·"Look-ahead"는 1280px에서 말줄임으로 표시됐다). 긴 설명은 툴팁에 있다.

재현: [하네스 README](../../CameraLab.Framing/README.md)

### 확인하지 않은 것

- 런타임에 스크립트가 컴포넌트 값을 바꾸는 경우(API 없음). 미니맵 카메라의 프레이밍은 동작하지만 하네스에서 보지 않았다.
- Windows·Linux(coordinator).

## 16.12 입력 상호작용과 버퍼

### 배포본 영향

- 기능을 쓰지 않는 CameraLab 배포 Native AOT를 16.14 엔진과 비교하면 16.11+16.12를 합쳐 `__managedcode` +10,656 B, `__const` +7,024 B, 실행 파일 8,139,200 → 8,155,760 B(+16,560 B, 16 KB 페이지 단위로 늘어남)다. 16.11만으로는 `./monoon test size`의 실행 파일이 +16 B(같은 페이지 안)였으므로 대부분은 16.12 몫이다: 새 YAML 타입(`ActionVector`, 액션의 세 키)의 VYaml 읽기 코드, 판정 규칙, `GameInput`의 새 멤버. 첫 구현의 LINQ(`Where/Select/ToArray`)를 반복문으로 바꿔 `__const` 3.8 KB와 실행 파일 한 페이지를 줄였다. [섹션 비교](size-16.11-16.12-sections.txt)
- `./monoon test size`(최종 커밋, 전체 811개 검사 통과): KeyRoom Native AOT 실행 파일 8,554,176 → 8,570,720 B(페이지 하나), 16.11 대비 Release 앱 +약 25 KB. 게이트(+1%)는 통과했고 기준선은 갱신하지 않았다. [16.12 뒤](size-after-16.12.txt)
- 프레임당 비용: 규칙이 있는 액션만 게임 틱마다 판정한다(`HashSet` 조회 2번과 비교 몇 번). 규칙이 없는 프로젝트는 빈 배열을 돈다. 벡터는 입력이 바뀐 틱에만 계산해 기존 샘플 사전과 같이 그때만 할당하고, 누르고 있는 동안에는 1,000틱에 0바이트다(`InputInteractionChecks`).
- 판정은 타임스탬프가 아니라 `InputActions`의 게임 틱 수로 세므로 실행 경로·CPU와 무관하다. 기존 `GameInput.Move`는 첫 벡터(없으면 기존 네 이름)와 같은 식으로 계산한다. 아날로그 값이 한 틱 안에서 올라갔다 내려간 경우 다음 틱에 이전 최고값 벡터가 남지 않도록 `Peaks`가 있으면 샘플을 다시 만든다(그 틱 한 번만 할당).
- BRUTAL에는 입력 액션 계층이 없다(SDL 이벤트까지만). 기존 MoNoon 입력 맵을 확장했다.

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `engine/src/Engine.Core/InputActions.cs` | `ActionBinding.Interaction/Ticks/Buffer`, `ActionVector`, `InputBindings.Vectors`, 검증, 틱 판정(`Rule`), `GameInput.Triggered/Consume/Vector`, 첫 벡터를 `Move`로 |
| `engine/src/Engine.Editor/Panels/InputMapWindow.cs` | 저장할 때 이름이 같은 액션의 규칙과 `vectors`를 유지(패널에 새 칸은 없음). Inspector 파일은 고치지 않았다. |
| `engine/tests/Engine.Checks/InputInteractionChecks.cs`, `Program.cs` | 단위 검사, `--only input-interactions` |
| `engine/tests/KeyRoom.InputInteractions/` | 세 경로 하네스(복사본의 `GameSettings`만 바꿈) |
| `docs/manual/input-actions.md` | [홀드·탭·더블탭과 입력 버퍼](../../../../docs/manual/input-actions.md#홀드탭더블탭과-입력-버퍼), [2D 벡터 액션](../../../../docs/manual/input-actions.md#2d-벡터-액션) |

### 검증

헤드리스 입력 재생(Release·Native AOT는 `--replay-input`, Scene Play는 같은 `.inputreplay`를 `InputReplay.Play`로) 300프레임.

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| 판정 트레이스 바이트 일치(38줄) | 기준 | 통과 | 통과 |
| Hold 30: 40틱 누름의 40틱째(누름 11틱)에 한 번, 10틱 누름은 없음 | 통과 | 통과 | 통과 |
| Tap 12: 4틱 누름을 놓은 85틱에만 | 통과 | 통과 | 통과 |
| DoubleTap 15: 131·181틱(10틱 간격), 20틱 간격은 없음 | 통과 | 통과 | 통과 |
| Buffer 8: 착지 5틱 전 입력을 200틱에 사용, 15틱 전 입력은 소멸, 사용 뒤 `Triggered` 거짓 | 통과 | 통과 | 통과 |
| 2D 벡터: Walk (1, 0), Aim (−0.70710677, −0.70710677), Move = Walk | 통과 | 통과 | 통과 |
| 금지 파일 | 해당 없음 | 통과 | 통과 |

[판정 출력](input-check.txt) · 트레이스: [Scene Play](input-scene-play.trace) · [Release](input-release.trace) · [Native AOT](input-native-aot.trace)

`InputInteractionChecks`: 1틱 단위 경계(Hold 3틱, Tap 3틱 미만, DoubleTap 4틱 이내), 버퍼 2틱의 시작·끝, `Consume` 뒤 같은 틱 다른 독자와 다음 틱, 이전 틱 `GameInput`은 판정을 보지도 쓰지도 않음, UI 입력은 `Pressed`와 같음, 재바인딩 뒤 새 키로 판정, 벡터 길이 1 제한·이름 대소문자 무시, 기존 네 이름의 `Move`, KeyRoom `GameSettings`가 그대로 읽힘, 새 키가 없는 액션은 YAML에 새 키를 쓰지 않음, 잘못된 규칙·벡터 거부. 기존 입력 맵·재바인딩·입력 기록 검사와 전체 `./monoon test`가 통과했다.

재현: [하네스 README](../../KeyRoom.InputInteractions/README.md)

### 확인하지 않은 것

- Linux x64 게스트의 같은 재생(로드맵 완료 조건의 일부, coordinator 몫). Windows는 보류.
- Input Actions 패널에서 규칙·벡터를 편집하는 UI(현재는 `GameSettings` YAML). 플레이어 재바인딩 저장(`inputBindings`)은 컨트롤만 저장하고 규칙은 프로젝트 값을 따른다.
- `./monoon ci`(InventoryLab Release·Native AOT 재생)는 이번에 실행하지 않았다. 입력 기록 단위 검사(`InputReplayChecks.CheckRecording`)는 `./monoon test`에서 통과했다.
