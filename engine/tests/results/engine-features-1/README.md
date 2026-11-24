# 엔진 기능 1차: 정수 배율·패럴랙스·스크립트 트윈 (16.8 · 16.2 · 16.3)

2026-11-24 · macOS arm64(Apple Silicon) · Scene Play(패키지 에디터 `editor/MoNoon.app`), Release, Native AOT(배포용 Distribution 프로필) · 기준 `wip/next1` fdc1eb3 병합 후 · 측정 중 `qemu-system`은 실행 중이 아니었다(`pgrep` 0건). 병합 전후 세 하네스의 기록 파일이 바이트 단위로 같았다.

## 배포본 영향

osx-arm64 배포용 빌드(`./monoon test size`). 기존 기준은 이 작업 전 `baseline.tsv`이다. "이 작업분"은 병합 전(ce49a2a + 세 커밋)에 같은 검사로 잰 값이고, 나머지는 함께 병합한 wip/next1(13.x Inspector·활성 상태 등)의 증가분으로 추정한다.

| 빌드 | 기존 기준 | 병합 후 | 증가(전체) | 이 작업분(병합 전 측정) |
| --- | ---: | ---: | ---: | ---: |
| KeyRoom Release 앱 | 32,824,540 | 32,920,339 | +95,799 (+0.29%) | +62,007 |
| KeyRoom Native AOT 앱 / 실행 파일 | 15,675,408 / 8,537,616 | 15,758,391 / 8,620,544 | +82,983 / +82,928 (+0.97%) | +66,439 / +66,384 |
| PlatformerLab Release 앱 | 30,074,021 | 30,165,883 | +91,862 (+0.31%) | — |
| PlatformerLab Native AOT 앱 / 실행 파일 | 12,896,911 / 8,222,256 | 12,985,669 / 8,305,168 | +88,758 / +82,912 (+1.01%) | — |
| InventoryLab Release 앱 | 83,514,645 | 83,600,661 | +86,016 (+0.10%) | — |
| InventoryLab Native AOT 앱 / 실행 파일 | 66,314,911 / 8,222,160 | 66,397,807 / 8,305,056 | +82,896 / +82,896 (+1.01%) | — |

PlatformerLab·InventoryLab의 AOT 실행 파일이 1% 기준을 넘어 `./monoon test size --update`로 기준을 갱신했다(`engine/tests/results/build-size/baseline.tsv`). AOT 실행 파일 증가 약 83 KB 중 약 66 KB가 이 작업분이다: 새 `[YamlObject]`(`Parallax2DComponent`)과 `SpriteDrawMode`의 VYaml 읽기 포매터, 패럴랙스·타일 조각 경로, 트윈 구조체 풀과 단계 적용 코드, SdlCanvas의 SDL 타일 호출이다. PlatformerLab 앱의 나머지 약 6 KB는 새 Parallax 씬과 PNG 4장이다. VYaml 쓰기 코드는 들어오지 않았다(`Native AOT kit has no VYaml generated Serialize code`).

- 늘어난 코드는 Core의 `Parallax2D.cs`(패럴랙스·타일 조각 경로), `ScriptTweens.cs`(트윈 구조체 풀), SdlCanvas의 SDL 타일 호출, `GameSettings.IntegerScale`이다. 새 의존성은 없다. 시작 시간은 기록만 하는 값이고 차이는 측정 잡음 범위다.
- 프레임 비용: 패럴랙스·Tiled 레이어는 레이어당 SDL 호출 1회, 정수 배율은 SDL 표시 모드만 바꾼다. 트윈과 패럴랙스는 프레임마다 할당하지 않는다(아래 검사). 덤으로 `TryGetLocalTransform`·`SetLocalTransform`이 호출마다 만들던 `SceneObject` 복사본과, 충돌체 없는 오브젝트의 충돌 형상 재계산(컴포넌트 열거 할당)을 없앴다. 9.6 클립 포즈도 같은 경로라 함께 가벼워진다.
- BRUTAL 사용: 정수 배율은 `SdlRendererLogicalPresentation.IntegerScale`과 `SetDefaultTextureScaleMode(Nearest)`, 타일·9-slice는 `SDL_RenderTextureTiled`·`SDL_RenderTexture9Grid`·`SDL_RenderTexture9GridTiled`(Brutal.Sdl 2026.10.3)를 쓴다. SDL 함수는 회전·반전·X/Y 배율이 다른 행렬을 받지 못하므로 그런 스프라이트만 같은 조각을 기존 스프라이트 배치(`DrawAffine`)로 그린다. 에디터 Avalonia 미리보기와 테스트 캔버스도 이 공용 경로를 쓴다. 트윈은 BRUTAL에 해당 기능이 없다.

## 바꾼 것

| 행 | 파일 | 내용 |
| --- | --- | --- |
| 16.8 | `Engine.Core/GameSettings.cs` | `IntegerScale`(`bool?`, null=끔이라 기존 파일은 저장해도 바이트가 같다) |
| 16.8 | `Engine.Core/SceneCameras.cs` | 정수 배율이면 카메라 시야 왼쪽 위를 게임 픽셀로 반올림, `SceneGame.IntegerScale` |
| 16.8 | `Engine.Desktop/DesktopHost.cs`, `SdlText.cs` | SDL 정수 배율 표시와 nearest 기본 샘플링, 글자 텍스처는 linear 유지 |
| 16.8 | `Engine.Editor/Scene/SceneViewport.Pointer.cs`, `Shell/MainWindow.GameSettings.cs` | Game 패널 Fit을 장치 픽셀 정수 배로 내림, Game Resolution 창의 **Pixel art: integer scale** |
| 16.8 | `samples/DisplayLab` | 12×12 `PixelBadge.png`와 UI 이미지(옵션은 하네스 복사본에서만 켬, 8.7 기준 유지) |
| 16.2 | `Engine.Core/Parallax2D.cs` | `SpriteDrawMode`, `Parallax2DComponent`, 공용 타일·9-slice 조각(`SpriteGrid`), `TryGetParallaxOffset`, 패럴랙스 그리기 |
| 16.2 | `SceneComponent.cs`, `SpriteAssets.cs`, `IGame2D.cs`, `InputActions.cs`, `RenderGrid.cs`, `SceneDocument.cs`, `SceneGame.cs`, `SceneCameras.cs` | Sprite Renderer `drawMode`·`slice`(null 기본), `ICanvas2D.SpriteSize`·`DrawGrid`, `IGameCameras.TryGetParallaxOffset`, 렌더 격자 항상 후보, 씬 스키마 33(병합 후 32는 13.2의 활성 상태), Body의 `Parallax`·`CameraHome`, DrawCamera 한 줄 |
| 16.2 | `Engine.Desktop/SdlCanvas.cs`, `Engine.Editor/Scene/MacSceneHost.cs`, `SceneViewport.Render.cs` | SDL 타일 호출, 에디터 캔버스 전달 |
| 16.2 | `Engine.Editor/Inspector/MainWindow.Presentation.cs` | **Parallax 2D** case 한 묶음(Scroll X/Y, Repeat X·Y)만 추가 |
| 16.2 | `samples/PlatformerLab` | `Parallax.scene`(하늘 0·언덕 0.25·나무 0.6·땅, 9-slice 표지판)과 PNG 4장, `includedScenes`에 추가 |
| 16.3 | `Engine.Core/ScriptTweens.cs`, `ScriptRoutines.cs`, `RuntimeObjects.cs` | `ScriptTween`·`TweenClock`, `IGameObjects.Tween`, 루틴과 같은 위치에서 진행 |
| 16.3 | `ScreenUi.Layout.cs`, `ScreenUi.cs` | `SetBoxOffset`·`BoxOffset`, `IGameUi.Bounds` |
| 16.3 | `RuntimeTransforms.cs`, `SceneGame.cs` | 로컬 Transform API의 복사본 재사용, 충돌체 없는 오브젝트의 형상 재계산 생략 |
| 16.3 | `samples/KeyRoom` | `SatchelPop.cs`(코인 획득 시 가방 팝), `ToggleControls` 버튼 눌림(UI 시간) |
| 공통 | `engine/tests/Engine.Checks/EngineFeatures1Checks.cs`, `Program.cs` | 새 검사 묶음과 `--only engine-features-1`(Program.cs는 두 줄) |

Inspector는 다른 작업이 고치는 중이라 새 컴포넌트 case만 넣었다. Sprite Renderer의 **Draw mode**·**Slice** 행은 넣지 않았다(XAML 템플릿과 `SpriteFields` 수정 필요). 지금은 씬 YAML로 지정하고, Inspector 작업이 병합된 뒤 Flip/Align 행 아래에 같은 열(56,*)로 ComboBox와 4칸 숫자 행을 두면 된다.

## 검증

| 검사 | Scene Play | Release | Native AOT | 비고 |
| --- | --- | --- | --- | --- |
| 16.8 창 3종(800×450·720×540·1300×760 pt)이 정수 배(2·1·3배), 배지 텍셀이 단색 블록 | Game 패널 1280×800·1600×1000·1920×1080 창에서 장치 픽셀 2·2·3배(Fit 2.195·2.995·3.795) | 통과 | 통과 | Release·AOT 콘텐츠 픽셀 동일 |
| 16.8 재실행 후 같은 크기·같은 픽셀 | — | 통과 | 통과 | |
| 16.8 대조: 옵션 없는 DisplayLab | — | 배지 경계에 보간 색이 생겨 실패(의도) | — | [letterbox-control-2x.png](osx-arm64/integer-scale/letterbox-control-2x.png) |
| 16.2 120틱 동안 레이어 오프셋 = (1−Scroll)×카메라 이동(320) | 통과 | 통과 | 통과 | 세 기록 바이트 동일 |
| 16.2 0·30·60·119프레임 캡처 | — | 통과 | 통과 | Release·AOT PNG 바이트 동일 |
| 16.3 가방 0·3·6·9·15틱(스케일 1→1.3→1.4→1.35556→1, 금색 플래시) | 통과 | 통과 | 통과 | 세 기록 바이트 동일 |
| 16.3 버튼 0·1·3·5·8프레임(안쪽 0→1.111→2→1.653→0 px, UI 시간) | 통과 | 통과 | 통과 | [다섯 시점 값](osx-arm64/tweens/points.json) |
| `EngineFeatures1Checks` | 카메라 반올림, 타일·9-slice 조각, 레이어당 그리기 1회, 패럴랙스 그리기 할당 0, 트윈 순서·지연·UI 시간·종료·할당 0(트윈 없는 같은 틱과 바이트 수 동일) | | | `./monoon test` |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) | — | 0개 | 0개 | 세 하네스 빌드 |
| `./monoon test` | 810 PASS, 크기 기준 갱신 후 통과 | | | 13.2 `ComponentStateChecks`의 "샘플 스키마 < 32" 조건을 "≠ 32"로 고쳤다(스키마 33은 활성 상태와 무관) |

캡처: [1배](osx-arm64/integer-scale/release-small-1x.png) · [3배](osx-arm64/integer-scale/release-large-3x.png) · [패럴랙스 119프레임](osx-arm64/parallax/release-frame-119.png). 기록: [정수 배율](osx-arm64/integer-scale/) · [패럴랙스](osx-arm64/parallax/) · [트윈](osx-arm64/tweens/).

## 재현

하네스별 명령은 각 README에 있다: [DisplayLab.IntegerScale](../../DisplayLab.IntegerScale/README.md) · [PlatformerLab.Parallax](../../PlatformerLab.Parallax/README.md) · [KeyRoom.Tweens](../../KeyRoom.Tweens/README.md). 단위 검사만은 `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only engine-features-1`.

## 확인하지 않은 것

- x64(Windows·Linux) 게스트: coordinator 몫. SDL 정수 배율은 창이 논리 해상도보다 작을 때(배율 < 1) 동작을 이 Mac에서 보지 못했다(Retina에서는 720×540 pt도 1배가 된다). 에디터 Fit은 최소 1배로 둔다.
- 정수 배율 GUI 검사는 키 입력·창 크기 조절 권한 없이 돌도록 `display.settings`로 창 크기를 정했다. 실제 사용자의 창 드래그 크기 변경 중 표시는 보지 않았다.
- 에디터 Scene Play의 PNG 스프라이트는 정수 배율이어도 에디터 렌더러의 기존 샘플링을 쓴다(배율 계산만 같다). Scene 편집 화면은 패럴랙스 없이 배치 위치에 그린다.
- 같은 오브젝트에서 컴포넌트 키 클립이 Tint·Opacity를 재생하면 트윈 색을 덮어쓴다(문서화).
