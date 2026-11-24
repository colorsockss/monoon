# 렌더링·애니메이션·효과 구현 기록

범위: 스프라이트·아틀라스·정렬, 클립·애니메이터, 카메라·화면 효과, 셰이더·조명, 트윈·스켈레톤·선, 파티클, 타일 지형·오토타일.
사용법은 [매뉴얼](../manual/README.md)에, 측정 원본은 각 결과 기록에 있다. 이 문서는 어떻게 만들었는지와 기록에 남은 이유만 적는다. 대괄호는 2026-11-25에 로드맵에서 뺀 예전 [로드맵](../roadmap.md) 번호다.

- [공통 원칙](#공통-원칙)
- [스프라이트 그리기와 정렬](#스프라이트-그리기와-정렬) [2.1, 6.6, 9.3, 16.2, 16.8]
- [스프라이트 클립과 애니메이터](#스프라이트-클립과-애니메이터) [2.2, 9.6, 11.8, 16.5]
- [카메라와 화면 효과](#카메라와-화면-효과) [2.4, 11.4, 16.11]
- [셰이더 시트, 점광원, 2D 조명](#셰이더-시트-점광원-2d-조명) [4.2, 7.7, 16.7]
- [트윈, 스켈레톤, 선·궤적·경로](#트윈-스켈레톤-선궤적경로) [4.2, 11.7]
- [파티클](#파티클) [14.1–14.11, 16.1]
- [타일맵 지형과 오토타일](#타일맵-지형과-오토타일) [10.6, 16.9]

## 공통 원칙

- **그리기 경로는 하나.** 배포 게임(Release·Native AOT)과 Mac Scene Play·Scene 편집은 BRUTAL `Brutal.Sdl`의 SDL3 렌더러(`Engine.Desktop/SdlCanvas.cs`, `SdlSpriteBatch.cs`의 `SDL_RenderGeometry`)로 그린다. Windows 에디터의 Avalonia 미리보기는 같은 `ICanvas2D`(`Engine.Core/IGame2D.cs`)를 구현한 근사 경로다. 이유: AGENTS.md의 배포 렌더링 규칙(게임에 SkiaSharp 없음).
- **시뮬레이션은 Engine.Core, 제출은 Engine.Desktop, 편집 UI는 Engine.Editor.** 결과 기록마다 배포본에 Engine.Editor·Engine.Authoring·SkiaSharp·Avalonia 파일이 없는지 검사했다.
- **상태는 60 Hz 고정 틱에서만 바뀐다.** 렌더 프레임은 직전 두 틱을 보간한다. 그래서 같은 입력을 재생하면 프레임 속도와 관계없이 같은 기록이 나온다. 같은 OS·아키텍처의 Scene Play·Release·Native AOT 기록이 바이트 단위로 같아야 완료로 본다.
- **변환은 `Affine2D`.** 곱셈·덧셈을 따로 계산하는 고정 식이다. FMA로 계산하던 것을 사용자 결정으로 바꿨다(arm64에서 약 39% 빠름). [결정 기록](../plans/next-work/README.md) · [x64 결정성](../../engine/tests/results/x64-determinism/README.md)
- **프레임당 관리 할당 0을 목표로 한다.** 버퍼는 생성 때 한 번 잡는다. 런타임이 커지는 변경은 Native AOT 크기를 재서 남긴다([크기 게이트](../../engine/tests/results/build-size/README.md): 기준선 +1%).
- **BRUTAL 먼저.** BRUTAL에 없는 기능(파티클, 곡선, 타일맵, 애니메이터, 카메라 추적, 선 두께)은 직접 만들었고 결과 기록에 그 판단을 적었다.

## 스프라이트 그리기와 정렬

**스프라이트·아틀라스 [2.1].** Sprite Renderer가 PNG GUID를 참조하고 반전·색·마스크·피벗·픽셀 정렬은 그릴 때만 적용한다(충돌·이동 값은 그대로). 아틀라스는 `.atlas` YAML(`Engine.Core/SpriteAtlas.cs`)과 내용 해시가 이름인 시트 PNG(`.monoon-atlas/`)로 나뉘고, 패킹은 에디터 전용 `Engine.Authoring/AtlasPacker.cs`가 한다. 씬과 클립은 계속 **원본 PNG의 ID**를 가리키고 엔진이 그 ID로 현재 셀을 찾는다. 재패킹으로 셀 좌표가 바뀌어도 씬 파일을 다시 쓰지 않기 위해서다. 투명 테두리 트림과 가장자리 1픽셀 복제는 이웃 셀 번짐을 막는다. 빌드는 참조된 세대만 넣고, 쓰는 셀이 70%보다 적으면 배포용 시트만 다시 패킹한다(15.13). [SpriteLab 검증](../../engine/tests/results/sprites/README.md)

**발 Y 정렬과 정렬 그룹 [6.6].** `Engine.Core/WorldDrawOrder.cs`가 Layer → Order → (고정 항목 먼저) 발의 월드 Y → 씬 순서로 정렬한다. Sorting Group 2D는 그룹 전체를 한 항목으로 정렬하고 안쪽은 따로 비교한다. 씬 편집, 클릭 선택, 월드 이름표가 같은 순서를 쓴다. 쓰는 씬은 스키마 22. KeyRoom 첫 방의 나무와 4장짜리 플레이어로 세 경로의 겹친 픽셀을 대조했다. [기록](../../engine/tests/results/y-sorting/README.md)

**카메라별 그리기 후보 [9.3].** `SceneGame.RenderGrid`(`Engine.Core/RenderGrid.cs`)가 오브젝트 사각형을 공간 격자(`SpatialGrid`)에 넣고, 카메라마다 시야와 겹치는 칸만 조회한 뒤 6.6의 순서로 되돌려 그린다. 움직인 오브젝트만 격자를 갱신한다. 타일맵·스켈레톤·스프라이트 풀·선·궤적·입자·패럴랙스·Text 2D는 소유 사각형 밖에도 그리므로 항상 후보다. CollisionLab 1만 개 필드에서 후보 10,010 → 270(기본)·520(미니맵), 화면 차이 0픽셀, macOS CPU p95 23.9–29.1% 감소. [기록](../../engine/tests/results/render-candidates/README.md)

**타일·9-slice와 패럴랙스 [16.2].** `Engine.Core/Parallax2D.cs`에 `SpriteDrawMode`(Simple·Tiled·Sliced)와 공용 조각 계산(`SpriteGrid`)을 두었다. 배포 게임은 BRUTAL의 `SDL_RenderTextureTiled`·`SDL_RenderTexture9Grid`·`SDL_RenderTexture9GridTiled`를 레이어당 한 번 부른다. 이 SDL 함수는 회전·반전·X/Y 배율이 다른 행렬을 받지 못하므로 그런 스프라이트만 같은 조각을 기존 스프라이트 배치로 그린다. Parallax 2D는 카메라 첫 프레임을 기준으로 오프셋을 더해 Play 시작 화면이 편집 배치와 같다. 씬 스키마 33. Inspector의 Draw·Slice 행은 Inspector 작업과 충돌을 피하려고 나중 커밋으로 넣었다. AOT 실행 파일 증가 약 66 KB(16.3·16.8 포함). [기록](../../engine/tests/results/engine-features-1/README.md) · [사용법](../manual/sprites.md#반복-타일과-9-slice-스프라이트)

**정수 배율 표시 [16.8].** `GameSettings.IntegerScale`(`bool?`, null이 끔이라 기존 설정 파일은 저장해도 바이트가 같다)을 켜면 `DesktopHost`가 BRUTAL `SdlRendererLogicalPresentation.IntegerScale`과 `SetDefaultTextureScaleMode(Nearest)`를 쓴다. 글자 텍스처는 linear를 유지한다. `SceneCameras`는 시야 왼쪽 위를, `Parallax2D`는 오프셋을 게임 픽셀로 반올림한다. 에디터 Game 패널 Fit도 정수 배로 내린다. 배율 1 미만 창과 드래그 중 표시는 확인하지 않았다. [기록](../../engine/tests/results/engine-features-1/README.md) · [사용법](../manual/runtime.md)

## 스프라이트 클립과 애니메이터

**클립과 시트 셀.** `.clip`은 프레임마다 원본 PNG GUID, 선택적인 `source` 사각형·`canvas`·오프셋, 초 단위 시간을 저장한다. `Engine.Core/SpriteClipTimeline.cs`가 누적 초로 프레임을 고르고 미리보기·스크럽·게임이 같은 함수를 쓴다. 시트를 복제하지 않고 영역만 저장하는 이유는 원본 PNG 하나를 공유해 배포 용량을 늘리지 않기 위해서다. [설계](../design/sprite-animation-editor.md) · [타임라인 검증](../../engine/tests/results/clip-timeline/macos-arm64/README.md) · [시트 셀 검증](../../engine/tests/results/sprite-sheet/macos-arm64/README.md)

**애니메이터 [2.2, 16.5].** `.animator` 에셋과 `Engine.Core/AnimationController.cs`. 컨트롤러를 불러올 때 상태마다 나가는 규칙(Any State 포함, 목록 순서)을 인덱스 배열로, 조건을 `(파라미터 번호, 비교, 값)` 구조체 배열로 컴파일한다. 한 틱은 현재 상태의 규칙만 본다(O(조건), 2만 틱 동안 0 B). Exit time은 상태 진입 뒤 누적한 고정 틱 시간과 클립 길이를 비교한다. 프레임 이벤트는 같은 오브젝트 스크립트의 `OnAnimationEvent`로 시간순 전달한다. [2.2 검증](../../engine/tests/results/animator/README.md)

**4·8방향 클립 세트 [16.5].** 상태가 Vector2 파라미터로 방향 클립을 고른다. 방향은 정수 방위 벡터와의 내적 비교만 쓴다. 삼각함수를 쓰지 않아 CPU와 JIT·AOT에 관계없이 같은 클립을 고른다. 경계 깜빡임을 막으려고 30°(8방향)·60°(4방향) 히스테리시스를 두고, 방향이 바뀌어도 재생 위치를 이어 간다. 이 작업 중 방향 클립이 배포에서 빠지던 결함을 `GameBuild`·`ProjectCatalog`에서 고쳤다. 처음 구현은 KeyRoom AOT +99.6 KB로 크기 게이트를 넘었다. 값 튜플 제네릭 인스턴스를 줄여 16.5 몫을 약 +33 KB로 낮췄다. [기록](../../engine/tests/results/engine-features-3/README.md)

**컴포넌트 속성 키 [9.6].** 클립에 Rotation·ScaleX·ScaleY·Tint·Opacity 트랙을 둔다(`Engine.Core/ComponentAnimation.cs`). 속성마다 독립 시간, 키 사이는 선형 보간, 끝값 유지다. 프레임 없이 오브젝트의 기존 스프라이트를 쓰는 **Use object renderer** 프레임을 둔 것은 PNG 없이 뚜껑·코인 같은 오브젝트를 움직이기 위해서다. KeyRoom 두 클립의 다섯 시점을 macOS arm64·Windows x64·Linux x64 게스트의 미리보기·Scene Play·Release·Native AOT에서 비교했다. [기록](../../engine/tests/results/component-keys/README.md)

**Aseprite 임포트 [11.8].** 에디터 전용 `Engine.Authoring/AsepriteImporter.cs`가 `.aseprite`·`.ase`를 직접 읽어 시트 PNG와 클립을 만든다. BRUTAL `Brutal.Sdl.Image`(SDL_image 3.4.4)를 먼저 확인했지만 Aseprite 디코더가 없고 애니메이션 API도 태그·레이어를 주지 않아, 공개 파일 형식을 .NET 기본 라이브러리(`ZLibStream`)로 읽는다. 원본이 바뀌면 같은 시트 GUID로 다시 만들어 참조가 유지된다. 배포본에는 생성된 PNG·클립만 들어가고 원본과 임포터는 없다. Aseprite가 설치되지 않아 픽스처 파일로 검증했다. [기록](../../engine/tests/results/aseprite-import/README.md)

## 카메라와 화면 효과

**Camera 2D [2.4].** `Engine.Core/Camera2D.cs`(설정)와 `SceneCameras.cs`(계산). 추적은 고정 틱에서 Half-life 지수 감쇠로 하고 렌더 프레임 사이를 보간한다. Bounds는 화면 전체가 맵 안에 있도록 멈추고, 맵이 시야보다 작으면 그 뷰의 줌만 올린다. Output은 Primary·Extra·Minimap 세 종류이고 HUD는 모든 카메라 뒤에 한 번 그린다. [기록](../../engine/tests/results/cameras/README.md)

**데드존·앞보기·그룹 프레이밍 [16.11].** 목표는 앞보기 → 데드존 → Half-life 순서로 계산한다. 그룹 프레이밍은 기존 `FindByGroup` 결과의 변환 사각형을 합쳐 `MinZoom`–`Zoom` 사이에서 줌을 정한다(Bounds가 하한). 줌도 렌더 프레임 사이에 보간한다. 보간은 `Affine2D.Lerp`와 덧셈·곱셈만 쓴다. 이 설정을 쓰면 씬 스키마 35. 기능을 쓰지 않는 게임의 AOT 실행 파일 증가 +16 B. 런타임에 스크립트로 값을 바꾸는 API는 없다. [기록](../../engine/tests/results/engine-features-2/README.md#1611-카메라-데드존앞보기다중-대상)

**화면 흔들림과 전체 효과 [11.4].** `Engine.Core/ScreenEffects.cs`의 `SessionState.Screen`(세션 동안 유지)이 게임 시간으로 흔들림·줌 펀치·플래시·페이드·색 보정을 계산하고, 흔들림 위상은 고정 시드에서 정한다. 그리는 위치만 바꾸므로 `TryGetView`·충돌·추적 결과는 그대로다. 색 보정 중에는 월드를 창 픽셀 크기의 SDL3 렌더 타깃에 그려 `MOD`·`ADD` 블렌드로 보정한 뒤 합성한다. 보정이 꺼져 있으면 렌더 타깃을 거치지 않는다. 접근성 배율(`ShakeScale`·`FlashScale`)을 곱한다. 이 작업에서 `SdlCanvas.FillRectangle`이 반투명 색을 블렌드하지 않던 결함을 고쳤다. 모두 기존 `Brutal.SdlApi` 호출이다. macOS arm64 세 경로와 Linux x64 게스트 Release·Native AOT가 같다. Windows는 장비 준비 후. [기록](../../engine/tests/results/screen-effects/README.md) · [Linux 기록](../../engine/tests/results/chapter11-linux-x64/README.md)

## 셰이더 시트, 점광원, 2D 조명

**스프라이트 셰이더와 배포용 프레임 시트 [4.2 1차, 7.7].** `.shader` 에셋은 SkSL 함수 `shade(uv, t)`다. 에디터 Scene Play는 SkiaSharp(Mac은 오프스크린 GPU)로 시트를 만든다. 게임에는 SkiaSharp를 넣지 않으므로 빌드가 씬·프리팹에서 참조한 셰이더를 렌더링해 `<이름>.shader.frames.png`와 셀 정보만 남은 YAML로 바꾼다(`Engine.Authoring/ShaderSheetBuild.cs`, OS별 `MacShaderContext`·`WindowsShaderContext`·`LinuxShaderContext`, Linux는 오프스크린 Mesa). 플레이어는 준비 단계에서 PNG를 SDL 텍스처로 올리고 게임 시간으로 셀을 고른다(`Engine.Core/BakedShaderSheets.cs`, `Engine.Desktop/ShaderSheets.cs`). 내장 광원 감쇠·입자 이미지도 쓰는 빌드에서만 굽는다. PlatformerLab 횃불 32프레임의 33개 시점에서 세 경로의 밝기가 같았다. [7.7 기록](../../engine/tests/results/baked-shaders/README.md) · [4.2 1차 기록](../../engine/tests/results/effects/README.md)

**그리기 무할당화 [14.1].** `SdlCanvas.DrawShader`가 호출마다 `SpriteRegion`(클래스 record)을 만들어 입자 하나당 32 B를 할당했고, 시트 사전 키의 기본 해시가 SkSL 원문 전체를 해시했다. 시트를 만들 때 프레임별 셀 배열을 한 번 만들고, 크기 필드만 해시하는 비교자를 넣었다(값 비교는 그대로). 입자 10,240개 그리기 873 → 179 µs, 320 KB → 0 B/프레임. 직렬화 형식과 캡처는 바뀌지 않았다. [기록](../../engine/tests/results/particles-p0/README.md)

**2D 조명: 어둠과 그림자 [16.7].** 사용자 결정 14에 따라 셰이더 없이 만들었다. SDL GPU 셰이더와 노멀맵은 나중 후보다. Global Light 2D가 있으면 캔버스 크기의 라이트맵 렌더 타깃을 Ambient 색으로 지우고 광원을 더한 뒤 `SDL_BLENDMODE_MOD`로 월드에 곱한다. Unlit layer에 처음 닿는 그리기 지점에서 합성하므로 그 뒤 항목과 UI는 원래 밝기다. Global Light 2D가 없으면 기존 가산 경로라 조명 없는 씬의 캡처는 그대로다. 그림자(`Engine.Core/Lighting2D.cs`의 `LightShadows`)는 광원을 등진 콜라이더·단단한 타일 변을 모아 같은 직선 변을 합치고 반경 밖까지 늘인 다각형이고, 보조 타깃에 `RenderGeometry`로 칠한다(광원당 그리기 3회). 광원별로 캐시하고 광원 이동, 반경 안 콜라이더 자세·켜짐, 타일 셀 버전, 스트리밍 페이지가 바뀔 때만 다시 만든다. 측정: Metal에서 그림자 광원 8개 +0.27 ms(목표 +1 ms 이하), 조명이 더하는 할당 0 B, 라이트맵 1080p 8.29 MB, AOT +33 KB(record·LINQ를 구조체·배열로 바꿔 줄였다). 씬 스키마 42. 회전 콜라이더 그림자 재생성은 할당이 있고, D3D·Vulkan·OpenGL 렌더러는 확인하지 않았다. [기록](../../engine/tests/results/lighting/README.md)

## 트윈, 스켈레톤, 선·궤적·경로

**트윈·스켈레탈 애니메이션·발 IK [4.2 2차].** Position Tween 2D(`Engine.Core/Tween2D.cs`)는 부모 좌표계의 변위를 강체 변위 자리에 넣어 기존 이동 처리(키네마틱 운반 포함)를 재사용한다. `.rig`(`Skeleton2D.cs`)는 부모 기준 뼈 각도와 주기 키이고, Travel 시계는 이동 발판의 운반량을 뺀 로컬 이동 거리로 재생한다. Foot IK 2D(`SkeletonPose.cs`)는 발마다 레이캐스트한 접점·법선으로 두 뼈 IK를 풀고, 점프 중이거나 지면이 끊기면 원래 포즈로 돌아간다. 포즈는 충돌체 크기에 영향을 주지 않는다. [기록](../../engine/tests/results/rig-animation/README.md)

**Line 2D·Trail 2D·Path Follower 2D [11.7].** BRUTAL에 선 두께·그라데이션·곡선 경로 기능이 없어(Numerics는 벡터 타입, Render.Mesh는 3D) 리본 계산과 베지어를 `Engine.Core/Lines2D.cs`에 직접 썼다. `ICanvas2D.FillStrip` 하나를 더해 SDL은 꼭짓점 색으로, Avalonia는 사각형별 그라데이션으로 그린다. 마이터 계산이 Core에 있어 모든 경로가 같은 꼭짓점을 쓴다. 곡선은 구간마다 24개 직선과 누적 길이 표로 나눠 일정 속도로 따라간다. 기존 `MotionPath`(충돌용 한 틱 이동)는 개념이 달라 확장하지 않았고, Path Follower는 트윈과 같은 변위 자리에 값을 내 평행한 이동 체계를 만들지 않았다. 궤적은 게임 시간으로만 기록한다. 씬 스키마 30. Linux x64 게스트에서 Paths는 Native AOT만 최대 0.0002px 달랐다. 실제 마우스 경로 편집은 확인하지 않았다. [기록](../../engine/tests/results/lines-paths/README.md)

## 파티클

설계 근거는 [조사 4](../plans/next-work/4-particles.md)와 사용자 결정 9–11이다. 시뮬레이션은 `Engine.Core/Particles2D.cs`·`ParticleCollision2D.cs`, SDL 제출은 `Engine.Desktop`, 편집은 `Engine.Editor`에만 있다. 16.1(파티클 확장)은 14장으로 옮겨 여기서 끝냈다.

**런타임과 `.particles` 에셋 [14.2].** 방출기마다 `maxParticles`(1–65536) 크기의 SoA 배열(x·y·vx·vy·수명·크기·cos·sin·출생 시각·색)을 생성 때 한 번 잡는다. 만료는 swap-remove, 꽉 차면 버리고 센다. 위치는 출생 시각 기준 닫힌 식 `p0 + v·나이 + ½g·나이²`이고 방출·만료는 고정 틱에서만 한다. 설정을 처음부터 에셋에 둔 것은 결정 10이다(같은 효과를 여러 오브젝트·프리팹이 공유). 빠진 키는 `[YamlConstructor]` 기본값을 쓴다. VYaml이 init 속성 초기값을 무시하기 때문이다. `monoon migrate`가 이전 인라인 방출기를 내용 해시 ID의 에셋으로 옮겨 같은 설정은 한 에셋을 공유한다. 씬 스키마 31. Native AOT +248 KB.

**난수와 결정성 [14.2].** 형태 샘플링은 결정 11대로 `Ahwoo.BRUTAL.Maths`를 확인 후 채택했다: 호출당 난수 소비가 일정하고 FMA가 없으며 JIT·AOT 결과가 같았다. 방출기의 `SeededRandom`을 `Random` 하위 클래스로 감싸 넘긴다. 이 과정에서 **`MathF.SinCos`가 macOS arm64 JIT과 Native AOT에서 2.6% 입력의 마지막 비트가 다르다**는 것을 찾아 `Sin`·`Cos`를 따로 부른다(첫 구현의 Release·AOT trace 차이 원인).

**정렬·블렌드·텍스처 [14.3].** 입자는 소유 오브젝트의 정렬 칸에서 그린다(Line·Trail과 같은 규칙). 예전처럼 모든 스프라이트 위에 그리지 않는다. Multiply는 SDL `MUL`이 `src·dst + dst·(1 − srcA)`라서 알파를 미리 곱한 텍스처 사본으로 그린다.

**모듈·곡선·힘·서브 방출기 [14.4, 14.5, 14.9, 14.10].** 곡선·그라데이션은 방출기 생성 때 64칸 표(`ParticleLut`)로 굽고 입자는 이웃 두 칸만 보간한다: 키 수와 무관하게 +3.9 ns/입자, 0 B. `endSize`·`endAlpha`만 쓰는 에셋은 이전 식을 그대로 써서 기존 trace가 바뀌지 않았다. 속도 곡선·drag·노이즈·충돌 중 하나가 있을 때만 틱마다 반암시적 오일러로 적분하고, 없으면 닫힌 식을 유지한다. 노이즈는 정수 격자 해시 + smoothstep이라 Sin·Cos와 표를 쓰지 않는다. 타일 충돌은 입자가 든 칸만 본다(O(1)). 콜라이더 충돌은 틱당 질의 상한을 두고 기존 Box2D 레이캐스트를 써서 질의마다 할당이 있다. 서브 방출은 바로 아래 자식 World 방출기로 깊이 1이고, 스크립트 콜백 대신 방출기당 직전 틱 집계(`ReadParticleEvents`)를 둬 할당이 없다. 14.5·14.9·14.10 합계 AOT +149 KB(+1.9%).

**패널·편집 컨트롤·프리셋·다시 불러오기 [14.6, 14.7, 14.8].** Particle 패널의 미리보기(`Engine.Editor/Model/ParticlePreview.cs`)는 플레이어의 `ParticleSystem2D`를 같은 순서·시드로 0부터 진행해, 커밋된 Curves trace와 300/300틱 일치했다. 색 선택은 결정 9의 `Avalonia.Controls.ColorPicker` 11.3.23(에디터 전용, 에디터 패키지 +630 KB), 곡선·그라데이션 편집기는 Avalonia 자체 컨트롤로 만들었고 런타임 `ParticleLut.Bake` 결과를 그대로 그린다. 같은 컨트롤을 Inspector의 곡선 행(13.15)이 쓴다. 프리셋 6종은 에디터에만 있고 내장 텍스처를 써서 배포 PNG가 늘지 않는다. Play 중 다시 불러오기는 스크립트 다시 불러오기(11.2)의 감시 타이머에 붙였고, 입자 배열 구성이 같으면 살아 있는 입자를 유지한다. 런타임에 남은 것은 `ParticleSystem2D.Reload` 등 세 메서드다(AOT +16 B).

**ParticleLab과 성능 기준 [14.11].** 기존 PlatformerLab 먼지·횃불은 호환 회귀로만 두고, 새 장면은 새 ParticleLab에 만들었다(결정 10). `ParticleLab.Trace/verify.py all`이 일곱 씬을 세 경로로 돌려 커밋된 trace와 비교한다. `particle-budget` 검사가 10,240개 Step+Draw의 0 B/프레임과 중앙값 500 µs 이하를 본다(실측 127–257 µs). [측정 전체](../../engine/tests/results/particles/README.md) · [편집기 기록](../../engine/tests/results/particles-editor/README.md) · [사용법](../manual/effects.md)

## 타일맵 지형과 오토타일

**지형 비용과 실행 중 셀 바꾸기 [10.6].** 타일 정의에 `terrain`·`moveCost`를 두고(비우면 충돌 없음 1, 충돌 있음 0), `TileNavigation`이 영역의 비용을 `GridNavigation`으로 읽는다. `SetTile`·`ReplaceTiles`(`Engine.Core/RuntimeTiles.cs`)는 그림·이웃 오토타일·타일 충돌·그리드 비용을 같은 틱에 갱신하고, 비용이 실제로 바뀌면 `Revision`이 올라 걷는 NPC가 다시 경로를 찾는다. 변경은 실행 중에만 유지한다. SeedLab의 7개 경우가 macOS arm64와 Windows·Linux x64 게스트의 세 경로에서 같았다. [기록](../../engine/tests/results/tile-terrain/README.md)

**8방향 오토타일·무작위 타일·움직이는 타일 [16.9].** 씬에는 계속 브러시 ID만 저장한다(셀당 0바이트). 47칸 blob 모양은 이웃에서, 무작위 그림은 `(x, y, seed)` 정수 해시에서 정해지므로 저장할 것이 없다. 이전에는 스트리밍하지 않는 레이어가 그릴 때마다 이웃을 다시 읽었다. 이제 씬을 불러올 때 한 번 해석(`Tilemap2DComponent.Resolved`)하고 칠할 때 바뀐 칸과 여덟 이웃만 다시 계산해 그리기는 표 조회만 한다. 움직이는 타일은 타일셋마다 게임 시간 시계 하나라 셀별 상태가 없다. BRUTAL에 타일맵 기능이 없어 `Engine.Core/Tilemap2D.cs`에 만들었다. 16.9 몫 AOT 약 +16.6 KB. Scene 편집 화면은 움직이는 타일의 첫 장만 그린다. [기록](../../engine/tests/results/engine-features-3/README.md) · [설계](../design/tilemap-editor.md) · [사용법](../manual/tilemaps.md)
