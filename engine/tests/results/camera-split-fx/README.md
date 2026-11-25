# 카메라별 화면 효과와 합쳐지고 갈라지는 분할 화면

2026-11-25 · macOS 27.0.1 arm64 · 기준 커밋 `7b6068d`(main). 실행 대상: Scene Play(패키지 에디터의 Mac SDL Scene 패널), Release, Native AOT(osx-arm64, 배포용 Distribution 프로필, 이 워크트리에서 다시 만든 플레이어 템플릿). 측정 중 `qemu-system`: 없음.

배포본 영향: 같은 워크트리에서 `./monoon test`의 `build-size`를 main 템플릿(공용 캐시, 기준 커밋 그대로)과 이 브랜치 템플릿(`engine/scripts/build-players.sh`)으로 한 번씩 돌린 값이다. osx-arm64 배포용.

| 게임 | Release 앱 | Native AOT 앱 | Native AOT 실행 파일 |
| --- | ---: | ---: | ---: |
| KeyRoom | 32,231,928 → 32,254,456 (+22,528) | 14,000,475 → 14,017,035 (+16,560) | 6,861,776 → 6,878,336 (+16,560, +0.24%) |
| PlatformerLab | 29,665,741 → 29,688,269 (+22,528) | 12,160,192 → 12,176,752 (+16,560) | 7,442,272 → 7,458,832 (+16,560) |
| InventoryLab | 83,150,901 → 83,172,917 (+22,016) | (main 템플릿 실행은 컴파일러 서버 오류로 측정 못 함) → 64,774,616 | 기준 6,646,112 → 6,662,672 (+16,560) |

- 늘어난 것은 `Engine.Core` 코드뿐이다(Release는 ReadyToRun `Engine.Core.dll`). 새 의존성·네이티브 라이브러리·에셋은 없다. 크기 검사(`+1%` 이내)는 통과했다. Native AOT 빌드의 `IL*`·`AOT*` trim 경고는 0개다.
- 프레임 비용: 분할 쌍이 없으면 카메라 단계에 빈 배열 순회 하나, 그리기에 카메라마다 사전 조회 한 번(`Session.Screen.For`를 한 번도 부르지 않았으면 null 검사)만 더해진다. 쌍과 카메라별 효과가 켜져 있어도 60틱+60프레임 할당은 0 B다([CameraSplitChecks](../../Engine.Checks/CameraSplitChecks.cs)). 카메라별 색 보정은 viewport에 채우기 1–2번을 더하고 렌더 타깃을 쓰지 않는다.
- 첫 프레임·session_ready는 한 번씩만 재서 번갈아 비교하지 않았다. 차이를 판정하지 않는다.

BRUTAL: 새 그리기는 기존 `Ahwoo.BRUTAL.Sdl`의 `Brutal.SdlApi` 채우기(`SDL_BLENDMODE_MOD`·`ADD`)만 쓴다. 새 패키지·내려받기 없음. 런타임 리플렉션 없음, YAML은 기존 VYaml 소스 생성기(`Camera2DComponent`의 새 속성)를 그대로 쓴다.

## 무엇을 넣었나

- `Session.Screen.For(cameraId)`: 카메라 하나의 `ScreenEffects`. Shake·ZoomPunch는 그 카메라 시야에, Flash·Fade·Adjust는 그 viewport 안에만(HUD 아래) 그린다. 화면 전체 `Session.Screen`과 합성된다. 흔들림 시드는 카메라 ID에서 나온다(`Guid.GetHashCode`, 플랫폼 무관).
- Camera 2D `splitWith`·`mergeDistance`·`splitDistance`·`blendSeconds`: Primary 카메라와 Extra 카메라를 한 쌍으로 묶는다. 두 viewport를 감싸는 영역을 합친 화면으로 쓰고, 대상 거리가 `mergeDistance`보다 가까우면 합쳐 중점을 따라가고 `splitDistance`보다 멀면 좌우로 가른다(그 사이는 히스테리시스). 전환은 `blendSeconds` 동안 smoothstep으로 viewport 폭과 중심을 함께 보간한다. 좌우는 갈라지는 순간 정하고, 새 쌍은 처음부터 거리에 맞는 상태로 시작한다. 짝 Extra 카메라를 끄면 Primary가 합친 영역 전체를 쓴다. schemaVersion 43.
- 런타임 API는 더하지 않았다. `IGameCameras`는 그대로이고, `TryGetView`의 `Viewport`가 바뀐 영역을 돌려준다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `engine/src/Engine.Core/ScreenEffects.cs` | `For(Guid)`, 카메라별 인스턴스와 시드, 내부 `Sample(Guid, t)` |
| `engine/src/Engine.Core/SceneGame.cs` | `DrawCamera`가 카메라 효과로 시야를 옮기고 viewport에 보정·페이드·섬광을 그린다. 합쳐진 짝 카메라와 그 미니맵 테두리를 건너뛴다 |
| `engine/src/Engine.Core/SceneCameras.cs` | `SplitPair`, 거리·히스테리시스·blend 단계, 쌍의 목표점·viewport, `TryGetView`의 합쳐진 시야 |
| `engine/src/Engine.Core/Camera2D.cs` | 새 필드 4개와 검증, `Relink`(복제 시 Follow·SplitWith 다시 잇기), viewport를 받는 `View`·`Port` |
| `engine/src/Engine.Core/SceneDocument.cs` | schemaVersion 43 |
| `engine/src/Engine.Core/{RuntimeObjects,AdditiveScenes,SceneDuplication,Prefabs}.cs`, `engine/src/Engine.Authoring/ProjectCatalog.cs` | 복사본의 SplitWith 연결, 프로젝트 참조 목록에 `Camera.SplitWith` |
| `engine/src/Engine.Core/IGame2D.cs`, `engine/src/Engine.Desktop/SdlCanvas.cs`, `engine/src/Engine.Editor/Scene/{MacSceneHost,SceneViewport.Render}.cs` | `ICanvas2D.AdjustRectangle`: 이미 그린 viewport에 곱하기·더하기 채우기(렌더 타깃 없음). Avalonia 미리보기는 같은 블렌드 채우기 |
| `engine/src/Engine.Editor/Inspector/MainWindow.Cameras.cs` | Primary 카메라에 Split with(Extra 카메라 목록)·Merge at·Split at·Blend 행. Output을 바꾸면 SplitWith를 지운다 |
| `engine/tests/Engine.Checks/CameraSplitChecks.cs` | `camera-split`(그리고 `screen-effects`) 검사 |
| `engine/tests/CameraLab.SplitScreen/`, `engine/tests/harnesses.json` | 세 실행 경로 하네스 |
| `docs/manual/cameras.md` | 분할 화면 쌍, 카메라별 효과 |

## 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| `--only camera-split`(시작 상태, 히스테리시스 구간, 전환 중 Bounds·폭·중심 변화, 중점, 좌우, 짝 비활성화, 할당 0 B/60프레임, schema 43, YAML, 검증, 복제, 카메라별 효과가 다른 viewport에 새지 않음, 접근성 배율, 결정성) | 통과(엔진 코어 단위 검사, 경로 무관) | | |
| `--only camera-framing`, `--only screen-effects` | 통과(단위 검사) | | |
| [CameraLab.SplitScreen](../../CameraLab.SplitScreen/README.md) 트레이스: 시작 분할, `split > blend > merged > blend > split`, 합쳐진 25틱(43–67), 폭 변화 최대 40px/틱, 엇갈린 뒤 좌우 바뀜, 카메라별 효과 값 | 통과 | 통과 | 통과 |
| 세 경로 트레이스 바이트 비교(공통 200틱) | 기준 | 같음 | 같음 |
| 22프레임 캡처: Player viewport 93.9% 검정(나머지는 HUD 이름표), Partner viewport 92.4% 빨강, HUD는 효과 밖 · 55프레임: 합친 화면에 효과 없음 | (캡처 없음) | 통과 [캡처](release-0022-effects.png) · [합침](release-0055-merged.png) | 통과(Release와 PNG SHA-256 같음) |
| Inspector: Half-life·Padding·Split with·Merge at·Split at·Blend 행이 창 1280·1600·1920px에서 같은 레이블 x·입력 x·입력 폭 | 통과 [캡처](editor-inspector-1280.png) · [측정](editor-inspector.txt) | | |
| Native AOT trim·AOT 경고(`IL*`/`AOT*`) | | | 0개 |
| `./monoon test`(전체, 이 브랜치 템플릿) | 통과(329 s, `build-size` +1% 이내 포함) | | |
| `./monoon ci`(InventoryLab Release·Native AOT 빌드와 재생) | | 통과 | 통과 |

### 회귀 `full` 계층

`python3 engine/scripts/run-harness.py --tier full --changed main`: diff가 `engine/src/**`에 닿아 하네스 67개를 모두 골랐다. 58개 통과, 9개 실패. 실패 9개는 모두 환경 때문이며 이 브랜치의 변경과 관계없다.

| 하네스 | 첫 오류 | 판정 |
| --- | --- | --- |
| CameraLab.DebugDraw, CameraLab.Framing, CameraLab.SplitScreen | (통과, 세 경로) | 카메라를 쓰는 기존·새 하네스 |
| CameraLab.ScreenEffects | Scene Play 커서 단계에서 두 번째 `7`(시스템 커서로 복귀)이 기록되지 않음: `IndexError: list index out of range`. 효과·접근성·진동 검사는 Scene Play에서 모두 통과, Release·AOT 기록은 완전함 | 기준 커밋 `7b6068d`에서 같은 단계로 같은 실패(main 재현). 회귀 아님 |
| KeyRoom.PlayInspector | `System.TimeoutException: Scene Play`(합성 OS 클릭 뒤 Play가 시작되지 않음) | main에서 같은 실패 |
| KeyRoom.UiLayout | `Editor exited or timed out at input 2` | main에서 같은 실패 |
| AudioLab.Vorbis | `mac-input focus` 종료 코드 2 | main에서 같은 실패 |
| AudioLab.Widgets | `controls/window focus` | 같은 합성 입력·창 포커스 실패(main 미실행) |
| Accessibility.Mac | 창 도구 `engine/.artifacts/clip-qa/window`, 이어서 `engine/.artifacts/tile-qa/access` 없음 | 새 워크트리에 없는 로컬 도구. 창 도구는 메인 체크아웃의 빌드본을 복사했고 `access`는 복사하지 않았다 |
| DisplayLab.Desktop | `ModuleNotFoundError: No module named 'PIL'` | Python 패키지 없음. 설치하지 않았다 |
| Input.Gamepad | `samples/KeyRoom/.monoon-compiled/MoNoon.GameScripts.dll` 없음 | 새 워크트리에 없는 컴파일 산출물 |
| KeyRoom.Desktop | XCTest `RegisterExecutionPolicyException` | Xcode UI 테스트 실행 정책(권한) |

결과 폴더: 스크래치 `regress-full/`, 재실행 `rerun-1/`, main 비교 `main-compare/`(세션 임시 폴더, 저장소에 넣지 않음).

## 재현

```sh
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only camera-split
engine/scripts/build-players.sh
python3 engine/scripts/run-harness.py CameraLab.SplitScreen --build-editor --out "$(mktemp -d)/h"
```

## 확인하지 않은 것

- Windows(사용자 보류), Linux x64 게스트 헤드리스 재생: 실행하지 않았다(VM은 coordinator 몫). 분할 계산은 `MathF.Sqrt`와 따로 쓴 곱셈·덧셈, `Affine2D.Lerp`만 쓰고 `determinism-scan`은 통과했지만 x64 바이트 비교는 하지 않았다.
- Windows 에디터의 Avalonia 미리보기에서 카메라별 색 보정(`AdjustRectangle` 블렌드 채우기)은 그려 보지 않았다. Mac Scene Play는 SDL 경로다.
- Scene Play의 픽셀 캡처: Scene Play는 트레이스와 Inspector만 확인했고, 화면 캡처는 Release·Native AOT의 SDL 소프트웨어 렌더러 스냅숏으로만 봤다. 물리 GPU 창에서의 카메라별 채우기는 캡처하지 않았다.
- 첫 프레임·session_ready·프레임 시간은 번갈아 반복 측정하지 않았다.
- 같은 Extra 카메라를 두 Primary 카메라가 함께 가리키는 경우(먼저 나온 쌍만 쓴다), Frame 그룹을 쓰는 카메라의 분할, 세로 분할은 검사하지 않았다.
- `docs/manual/agent-workflow.md`의 `--only` 이름 표에 `camera-split`을 더하지 않았다(소유 파일 밖). `--list`에는 나온다.
- schemaVersion 43은 다른 작업 묶음과 겹칠 수 있다. 병합할 때 번호를 확인해야 한다.
