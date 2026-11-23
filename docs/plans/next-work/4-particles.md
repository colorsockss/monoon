# 4. 파티클 에디터 조사 (MoNoon)

조사일 2026-11-23, 기준 `.worktrees/phase1` (wip/phase1, 4c9b090). 경로는 저장소 루트 기준. 측정은 macOS arm64(Apple M4 Max), scratchpad 복사본(`git archive`)에서 헤드리스로 했다. 창은 열지 않았다.

## 1. 현재 상태

### 1.1 런타임 (Engine.Core)

| 항목 | 현재 구현 | 근거 |
| --- | --- | --- |
| 컴포넌트 | `ParticleEmitter2DComponent`: Trigger(Manual·Landing), Count(1–256), Lifetime(0–60 s), Speed, Gravity, Size, Color(`#RRGGBB`). 이 7개 필드만 있다. | `engine/src/Engine.Core/Effects2D.cs:93-116` |
| 직렬화 | 씬·프리팹 YAML `type: particleEmitter2D`, JSON `particleEmitter2D` 다형 판별자. 컴포넌트가 있으면 스키마 19. 로드할 때 검증한다. | `SceneComponent.cs:22-23`, `SceneDocument.cs:220,257`, `samples/PlatformerLab/Scenes/Platform.scene:23-30` |
| 시뮬레이션 | `ParticleBurst`: `List<Particle>`(Origin, Velocity, Size, Birth: 32 B 구조체). 위치는 출생 시각 기준 닫힌 식 `p0 + v·age + ½g·age²`로 그릴 때 계산한다. 고정 틱에는 만료와 Landing 감지만 한다. | `Effects2D.cs:118-162`, `Effects2D.cs:155`, `Effects2D.cs:177-182` |
| 방출 형태 | 오브젝트 하단 중앙의 한 점(`EmitterOrigin`)에서 나온다. 방향은 좌·우를 번갈아 고르는 고정 부채꼴(x 0.35–1.0, y −0.25…−0.85)이고, 크기 ×0.65–1.35 외에는 랜덤 범위가 없다. 수명에 따라 크기 55→100%, 알파가 선형으로 준다. | `Effects2D.cs:141-143,156-159,168` |
| 한도 | 방출기당 동시 512개. 넘치면 오래된 것부터 지운다(`RemoveRange(0, n)`, O(n) 이동). 만료는 FIFO 앞에서부터 처리한다(모든 입자의 수명이 같다는 가정). | `Effects2D.cs:128-130,137-138` |
| 난수 | `new Random(owner.GetHashCode())`. Guid 해시는 .NET에서 결정적이지만 `SeededRandom` 규칙("GetHashCode() 대신 안정된 채널 ID")과 맞지 않는다. 시드를 사용자가 바꿀 수 없다. | `Effects2D.cs:122`, `SeededRandom.cs:32-34` |
| 스크립트 API | `IGameObjects.EmitParticles(Guid)` 하나다(개수·위치·방향 인자 없음). 비활성화와 풀 반환 때 Reset한다. | `RuntimeObjects.cs:22,350`, `PrefabPools.cs:148`, `Effects2D.cs:170-175` |
| 그리기 순서 | 모든 바디를 정렬 순서대로 그린 **뒤에** 방출기를 한꺼번에 그린다. 그래서 Sorting Layer/Order가 없고 입자는 항상 스프라이트 위(조명 아래)에 놓인다. Line·Trail 2D에는 SortingLayer/Order가 있다. | `SceneGame.cs:568-586`, `Lines2D.cs` (Line2D·Trail2D `SortingLayer`, `Order`) |
| 텍스처·블렌드 | 내장 SkSL `ShaderClip.Puff`(12×12, 1프레임) 하나만 쓰고 알파 블렌드만 지원한다. 배포본에는 빌드 때 `Builtins/Puff.png`로 구워진다(SkiaSharp는 빌드 도구에만 있다). 사용자 스프라이트, 시트 애니메이션, 가산 블렌드, 회전은 없다. | `Effects2D.cs:48-53`, `SceneGame.cs:199,233-237`, `BakedShaderSheets.cs:7`, `Engine.Authoring/ShaderSheetBuild.cs:19,59-63` |
| SDL3 경로 | 입자마다 `canvas.DrawShader` 호출 → `SdlCanvas.DrawShader` → `ShaderSheets.Get((ShaderClip, Color, bool))` 사전 조회 → `SdlSpriteBatch.Add`. 배치는 4,096 꼭짓점(1,024 사각형)마다, 또는 텍스처가 바뀔 때 제출한다. | `SdlCanvas.cs:27-59`, `ShaderSheets.cs:8-15`, `SdlSpriteBatch.cs:10-31,59-60` |
| 프로파일러 | Step 비용은 `FrameProfiler.Effects` 구간에 들어간다. Draw 비용은 렌더 구간에 섞인다. | `SceneGame.cs:501`, `FrameProfiler.cs:13` |

### 1.2 측정: 현재 경로의 프레임당 비용과 할당

`ParticleBurst`를 그대로 쓰는 벤치를 만들었다(`tools/particles/Program.cs`, 어셈블리 이름 `Engine.Checks`로 internal 접근). 캔버스에는 `SdlCanvas.DrawShader`가 하는 일 가운데 SDL 제출만 빼고 나머지를 그대로 옮겼다. 즉 `ShaderSheets.Get` 조회, `shader.Region(frame)`, `SdlSpriteBatch.Add`의 꼭짓점 계산을 한다. Release JIT로 2회 반복했고 2회차 값이다. 512개는 상한(방출기 1개), 10,240개는 방출기 20개다.

| 경우 | 1×512 | 20×512 = 10,240 |
| --- | ---: | ---: |
| Step+Draw, 사전 조회 없음 | 15.8 µs (31 ns/입자) | 236 µs (23 ns/입자) |
| Step+Draw, `ShaderSheets.Get` 포함 (= SdlCanvas와 같음) | 98.5 µs (**192 ns/입자**) | 1,100 µs (**107 ns/입자**) |
| 관리 할당 | **16,384 B/프레임** | **327,680 B/프레임** |
| 참고: SoA 배열에서 적분하고 꼭짓점 4개를 쓰는 기준 커널 | – | 71–135 µs (6.9–13 ns/입자), 0 B |

원인 두 가지(코드로 확인):
- **입자 하나마다 32 B를 할당한다.** `SpriteRegion`이 `sealed record` 클래스(`SpriteRegion.cs:26`)라서 `shader.Region(frame)`(`Effects2D.cs:17`)이 그릴 때마다 힙 객체를 만든다. `SdlCanvas.cs:58`과 에디터 `SceneViewport.Render.cs:143`에서 매 호출 실행된다. 10k 입자면 프레임당 320 KB가 쌓여 수 프레임마다 Gen0 GC가 난다. BarrageLab 풀의 "0 B" 기준(`engine/tests/results/sprite-pools/README.md`)과 대비된다.
- **사전 키 해시가 비싸다.** `ShaderSheets`의 키 `(ShaderClip, Color, bool)`에서 `ShaderClip`이 record다. 그래서 조회할 때마다 SkSL `Source` 문자열 전체를 포함한 8개 필드를 해시한다(`ShaderSheets.cs:11`). 입자당 약 75–160 ns(측정 차이)로, 시뮬레이션보다 3–6배 비싸다. 같은 경로를 쓰는 횃불 셰이더와 점광원에도 적용되지만, 그쪽은 개수가 적다.
- 측정 범위: SDL 제출과 GPU 비용, Native AOT, x64는 측정하지 않았다. 기존 결과(`engine/tests/results/effects/README.md`)에는 화면의 먼지 픽셀 수와 방출 수(22개, 0.62 s 뒤 소멸)만 있고 비용·할당 측정은 없다.

### 1.3 에디터

| 항목 | 현재 | 근거 |
| --- | --- | --- |
| Inspector | 7행: Trigger 콤보, Count/Life/Speed/Gravity/Size `NumericUpDown`, Color 텍스트(hex). 색 견본이 없다. Sprite Renderer의 Color 행에는 16px 견본이 있다. 그리드는 레이블 56px 열과 `EditorFieldGap`. | `Inspector/MainWindow.Presentation.cs:153-162,25-36,44`, `Shell/MainWindow.axaml:108-112` |
| Scene 편집 미리보기 | **없다.** `DrawSceneSprite`는 스프라이트·셰이더·리그만 그리고 방출기는 그리지 않는다. 결과를 보려면 Play를 눌러 착지시켜야 한다. | `Scene/SceneViewport.cs:61-88` |
| Scene Play | Mac은 SDL3 NSView(`SdlCanvas`, 배포본과 같은 경로)로 그린다. Win·Linux는 Avalonia `PreviewCanvas`가 입자마다 Push 3회와 `DrawImage`를 한다. 입자가 많으면 Avalonia 경로가 병목이 된다(추정). | `Scene/MacSceneHost.cs:151,232-233`, `Scene/SceneViewport.Render.cs:140-149` |
| 곡선·그라데이션·색 선택 컨트롤 | **없다.** `Engine.Editor.csproj:16-17`은 `Avalonia.Desktop`·`Avalonia.Themes.Fluent`만 참조한다(ColorPicker 패키지 없음). 에디터의 곡선 편집은 경로 베지어 핸들(`SceneViewport.Paths.cs:106`)뿐이다. | grep `Curve/Gradient/ColorPicker` |
| 재사용할 수 있는 패턴 | Animator Preview 탭(`SceneViewport(spritePreview:true)`, `DispatcherTimer` FixedStep, Restart), 클립 타임라인 Scrub과 Play/Pause, 도킹 패널(12.4, `DockHost`), 프로젝트 에디터 확장의 도킹 패널(11.10) | `Panels/AnimatorWindow.cs:30-37,243-279`, `Panels/ClipTimelineTrack.cs:29,121-131`, `Panels/ClipEditorWindow.axaml.cs:54,83,287-304` |
| 검사 | `EffectsChecks`: Landing에서 22개를 한 번 방출, 만료, 수동 방출, 비활성화 시 삭제, 저장 왕복을 본다. 세 경로 화면 캡처의 먼지 픽셀(Scene Play 59, Release 78, AOT 151)은 캡처 타이밍 차이로 값이 다르다. 결정론 트레이스 비교는 없다. | `engine/tests/Engine.Checks/EffectsChecks.cs:77-84`, `engine/tests/results/effects/README.md` |

### 1.4 바로 쓸 수 있는 기반

- **`SpritePool2D`** (`SpritePool2D.cs:13-99`): 고정 용량 슬롯, 활성·빈 칸 인덱스 배열, Previous/Position 보간(`Affine2D.Lerp`), `DrawSpriteInstances(span)`로 SDL 배치에 한 번에 넘긴다. 그리기 순서도 소유 바디를 따른다(`SceneGame.cs:574`). BarrageLab에서 예열 뒤 0 B와 2,700개당 제출 10회가 측정됐다. 입자 런타임의 골격으로 쓸 수 있다. 없는 것: 입자별 회전, 입자별 시트 셀, 수명.
- **`SeededRandom`** (SplitMix64, `SeededRandom.cs`)과 `Fork(channel)`: 방출기별 결정적 난수열에 쓴다.
- **결정론 규칙** (`docs/manual/transform-2d.md:17`, `Transform2D.cs:76-85`): 브리프의 "unfused math rule"과 반대로, 현재 규칙은 **항상 합쳐 계산(FMA)하는 `Affine2D`를 쓰고 `Vector2.Transform`·`Lerp`(MultiplyAddEstimate)를 쓰지 않는 것**이다. 일반 스칼라 `a*b+c`는 JIT·AOT 모두 합치지 않아 안전하다.
- **트레이스 하네스 패턴**: `engine/tests/PlatformerLab.Paths/`(StartupHook + `PathTrace` + `verify.py`)는 Scene Play·Release·AOT의 기록을 바이트 단위로 비교한다(`engine/tests/results/lines-paths/README.md`).

### 1.5 BRUTAL 확인 (docs/brutal.md)

- BRUTAL에는 파티클 시스템, 곡선, 그라데이션 기능이 없다(`docs/brutal.md` 표 전체. 렌더러·씬은 범위 밖으로 명시).
- `Brutal.Maths` 2026.10.3(8,192 B, NuGet에서 내려받아 확인)의 `RandomExtensions`는 `System.Random` 확장으로 `NextSingle(min,max)`, `NextSinglePointInCircle/Rectangle/Square`, `NextSingleUnit2`(반환 `float2`)를 제공한다. 방출 형태 샘플링과 겹친다. 의존하는 `Brutal.Numerics`는 Box2D를 통해 이미 배포본에 있어 추가되는 것은 8 KB다. 맞지 않는 점: 결정적 스트림인 `SeededRandom`이 `System.Random`이 아니고, 스크립트 API가 `System.Numerics`다(유지 결정). 문자열 확인으로는 `Sqrt`만 쓰고 `Sin`·`Cos`가 없어 거부 표본추출로 보이지만, 호출당 소비하는 난수 수는 확인하지 않았다.
- 그리기는 기존처럼 `Brutal.Sdl`의 `SDL_RenderGeometry`(SdlSpriteBatch)를 쓴다. 새 패키지는 필요 없다.

## 2. 부족한 점 / 문제 (우선순위순)

1. **할당과 조회 비용이 입자 수에 비례한다**(측정): 입자당 32 B, `ShaderSheets` 조회 ~100–190 ns. 10k 입자면 ~1.1 ms/프레임과 320 KB/프레임이다. 기능을 늘리기 전에 고쳐야 할 기반 결함이다.
2. **그리기 정렬이 불가능하다**: 입자를 항상 모든 스프라이트 위에 그린다(`SceneGame.cs:586`). 횃불 뒤 불씨, 캐릭터 뒤 먼지 같은 기본 연출을 만들 수 없다.
3. **표현력이 Unity 이전 수준이다**: 연속 방출(rate)·버스트·반복·지속시간이 없고, 형태는 한 점, 시작값 랜덤 범위·회전·수명 곡선·그라데이션·텍스처·시트·가산 블렌드·로컬 공간·Prewarm이 없다. 트리거도 Landing과 Manual뿐이다.
4. **편집 중 미리보기가 없다**: Scene에서 보이지 않고 Play해서 조건(착지)을 만들어야 확인할 수 있다. 재생·일시정지·되감기, 고립 미리보기, 곡선·그라데이션·색 편집기도 없다.
5. **자료 구조가 확장에 막혀 있다**: FIFO 만료는 수명이 하나라는 가정이라 랜덤 수명에서 깨진다. 닫힌 식 위치는 힘·노이즈·충돌을 넣으면 쓸 수 없다. 상한 512는 하드코딩이다.
6. **결정론을 검증하지 않는다**: 시드 규칙이 어긋나 있고(GetHashCode), 세 경로 트레이스 비교와 비용 기준값이 없다.
7. **Windows 에디터 미리보기 비용**: Avalonia 경로가 입자마다 Push 3회와 DrawImage를 한다(측정 안 함, 대량 입자 미리보기 때 확인 필요).

## 3. 제안 작업 목록 (로드맵 행 형식, 우선순위순)

공통: 런타임 코드는 `Engine.Core`(시뮬레이션·자료)와 `Engine.Desktop`(SDL 제출)에만 두고, 편집 UI는 `Engine.Editor`에만 둔다. 에디터 미리보기는 같은 `Engine.Core` 시뮬레이션을 에디터 캔버스로 그린다. 배포본에는 SkiaSharp를 넣지 않는다(내장 입자 텍스처는 기존처럼 빌드 때 PNG로 굽는다). Windows 검증은 장비 준비 후(사용자 결정). Linux x64는 기존 게스트에서 Release·AOT 트레이스만 확인한다.

| 순서 | 이름 | 내용 | 완료 기준(검증) | 규모 | 의존성 | 용량·성능 영향 | BRUTAL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| P.0 | 셰이더 그리기 무할당화 | `SdlCanvas`·`PreviewCanvas`의 `ShaderSheets` 조회를 `ShaderClip` 참조 기준(`ReferenceEqualityComparer` 또는 마지막 항목 캐시)으로 바꾸고, `ShaderClip`이 셀 영역을 한 번 만들어 재사용하게 한다(`Region()`이 매번 `new` 하지 않도록). 동작은 바꾸지 않는다. | `EffectsChecks`에 예열 뒤 Draw 1,000회 = 0 B 단언을 추가. 위 벤치 기준 10,240개 Step+Draw ≤ 300 µs(현재 1,100 µs, M4 Max). PlatformerLab 착지·횃불 캡처가 기존 결과와 같음(Scene Play·Release·AOT, macOS arm64) | S | 없음 | 용량 변화 없음. 입자·셰이더·조명의 CPU 비용이 3–6배 감소(측정 기반 추정) | Brutal.Sdl 그대로 |
| P.1 | 입자 런타임 재작성(SoA 고정 풀) | `ParticleBurst`를 `SpritePool2D`와 같은 구조로 바꾼다. 용량은 방출기 설정값(최대 4,096, 생성 때 한 번 할당). x·y·vx·vy·prevX·prevY·age·life·size·rot·seed를 `float[]`로 둔다. 만료는 swap-remove. 고정 틱(1/60)마다 적분하고 그릴 때 prev→cur 보간. 꼭짓점은 새 `ICanvas2D.DrawQuads(texture, ReadOnlySpan<Quad>)`(회전·셀·색 포함, SDL은 `SdlSpriteBatch`, 기본 구현은 개별 그리기)로 한 번에 넘긴다. `Random`을 `SeededRandom(scene seed ⊕ 방출기 seed).Fork(채널)`로 교체. `Vector2.Lerp`·`Transform`은 쓰지 않는다. 기존 7필드와 Landing·Manual 동작을 유지한다(기존 씬은 그대로 열림). | 예열 뒤 0 B/프레임. 10,240개 Step+Draw ≤ 150 µs(M4 Max, SoA 기준 커널 71–135 µs 근거). 10,240개 제출 ≤ 11회. 기존 `EffectsChecks` 통과. PlatformerLab 착지 먼지 트레이스(틱별 활성 수 + 위치 비트 해시)가 Scene Play·Release·AOT에서 바이트 단위로 같음(macOS arm64), Linux x64 게스트 Release·AOT 동일 | M | P.0 | Engine.Core IL +15–30 KB(추정: 현재 1,660,416 B / 15,332줄 ≈ 108 B/줄). 상한 512 → 설정값 | 없음(BRUTAL에 입자 없음, 이유 기록) |
| P.2 | 정렬·블렌드·텍스처 | 방출기에 SortingLayer/Order를 추가해 바디 정렬 순서 안에서 그린다(입자 AABB로 컬링해 방출기 오브젝트가 화면 밖이어도 입자 유지). Blend(Alpha·Additive), 사용자 스프라이트(PNG·아틀라스 셀)와 내장 Puff, 텍스처 시트 애니메이션(기존 sprite sheet 셀, 수명 비율·FPS). 스키마 버전을 올린다. | PlatformerLab 횃불에 가산 불씨를 추가하고 횃불 스프라이트 **뒤**에 그려지는지 캡처로 확인. 같은 텍스처·블렌드의 입자는 1–2회 제출. 세 경로 트레이스·캡처 동일 | M | P.1 | 시트를 쓰는 경우에만 PNG가 추가된다. 블렌드 그룹마다 제출이 1회 늘어난다 | Brutal.Sdl `SetTextureBlendMode` |
| P.3 | Shuriken 핵심 모듈(MVP 데이터) | Main: Duration, Looping, Prewarm, Start delay, Start life/speed/size/rotation/color(상수·두 값 사이 랜덤), Gravity, Simulation space(World·Local), Max particles, Seed. Emission: Rate over time, Rate over distance, Bursts(시각·개수·반복). Shape: Point, Circle(반경·두께·호), Rectangle, Edge(선분), Cone(2D 각도). 기존 Trigger(Landing)는 "Burst on event"로 유지. 스크립트 API `Play/Stop/Clear/Emit(id, count)`. | 각 모듈의 단위 검사(랜덤 범위 경계, burst 시각, rate 누적의 프레임 속도 무관성: 30·60·144 Hz 렌더에서 같은 틱 트레이스). Prewarm은 첫 프레임에 같은 시드로 N틱을 시뮬레이션한 결과와 동일 | M | P.1 | IL +20–40 KB(추정). 비용은 활성 입자 수에 비례 | Brutal.Maths 형태 샘플러 검토(결정 4.3) |
| P.4 | 수명 곡선·그라데이션 | Size/Color/Alpha/Rotation speed/Velocity(선형·궤도) over lifetime. 곡선은 키(시간, 값, 접선), 그라데이션은 색 8개·알파 8개 키. 로드할 때 64칸 LUT(`float[]`, 색은 `uint[]`)로 구워 런타임은 인덱스 + 선형 보간만 한다. "두 곡선 사이 랜덤"은 LUT 2개와 입자별 난수로. | LUT 평가에 할당 없음. 곡선 YAML 왕복이 바이트 단위로 같음. 끝점·0길이 구간 검사. P.1 비용 기준 +≤5 ns/입자 | S | P.3 | 방출기당 LUT 최대 ~2 KB | 없음 |
| P.5 | 파티클 패널과 미리보기(에디터) | 도킹 패널 "Particle"(12.4 패턴). 선택한 방출기의 모듈을 접이식 섹션으로 보여 준다(Inspector는 요약 행과 "Open in Particle panel"). 상단 도구줄: Play·Pause·Restart·Step·Scrub 슬라이더·Speed(0.1–4×)·Seed·입자 수/µs 표시. **Scene 편집 화면에서** 선택한 방출기만 시뮬레이션해 그린다(편집 시간 기준, 씬 데이터는 바꾸지 않음). 패널 안 고립 미리보기는 `SceneViewport(spritePreview:true)`를 재사용. Scrub은 같은 시드로 0부터 다시 시뮬레이션한다(고정 틱이라 결정적, 최대 Duration×60틱). 색 행에 견본을 추가(Sprite 행과 같은 56·16·* 열). | 1280·1600·1920px과 긴 이름에서 레이블·필드 열 정렬이 유지되는 캡처(macOS arm64 패키지 에디터, Linux 게스트). Scrub t의 패널 미리보기 트레이스 = Play에서 t초 뒤 트레이스. 편집 중 미리보기가 씬 파일을 바꾸지 않음(바이트 비교). Release·AOT 배포본에 Engine.Editor 타입이 없음(기존 배포 검사) | L | P.3 | 에디터만 커진다. 배포본 0 B | 없음 |
| P.6 | 곡선·그라데이션 편집 컨트롤 | Avalonia 자체 컨트롤 2개: 곡선(키 추가·삭제·끌기, 접선, 0–1 정규화, 프리셋: 상수·선형·ease-in/out), 그라데이션(키 막대 + hex 입력 + 견본). 색 선택은 우선 hex + 견본 + HSV 슬라이더 3개(직접 구현). 같은 행 높이와 기준선, 끌기 강조가 주변을 밀지 않게(AGENTS.md UI 규칙). Undo는 기존 씬 스냅숏 방식. | 키 끌기 한 번이 Undo 한 단계. 창을 880×660으로 줄여도 정렬 유지(캡처). 곡선 값이 LUT와 같음(편집기 표시 = 런타임 평가) | M | P.4, P.5 | 에디터만. `Avalonia.Controls.ColorPicker`를 쓰지 않으면 +0 패키지(결정 4.4) | 없음(ImGui는 에디터가 Avalonia라 쓰지 않음) |
| P.7 | 파티클 에셋과 프리셋 | `.particles` YAML 에셋(GUID). 컴포넌트는 에셋 참조 + 인스턴스별 Seed·Play on start·Sorting만 둔다. 인라인 설정은 "Extract to asset"으로 옮긴다. 프리셋 6종(먼지, 불씨, 연기, 스파크, 피격, 비)은 템플릿·패키지로 배포한다. 에셋을 저장하면 Scene Play 중에도 다시 불러온다(11.2와 같은 방식). | 에셋을 쓰는 프리팹 2개가 같은 효과를 공유. 에셋 변경 → 두 인스턴스에 반영. 누락 참조 검사(3.2)에 표시. 콘텐츠 번들에 들어감 | M | P.3 (P.5와 병행 가능) | 에셋당 1–3 KB YAML | VYaml 유지(BRUTAL에 YAML 없음) |
| P.8 | 힘·노이즈·충돌 | Force over lifetime, Limit velocity(drag), 2D 값 노이즈(정수 해시 + smoothstep, `Sin`·`Cos` 없음, 강도·주파수·스크롤). 타일맵 충돌: 셀 직접 조회로 O(1)/입자, Bounce·Dampen·Lifetime loss·Kill. 콜라이더 충돌은 opt-in으로, 방출기당 최대 N개만 기존 공간 질의 raycast를 쓴다. | PlatformerLab 비가 경사·발판 타일에서 튐. 10,240개 + 타일 충돌 ≤ 300 µs(M4 Max). 세 경로 트레이스 동일. 노이즈 결과가 arm64·x64 게스트에서 같음 | M | P.1, P.3 | 타일 충돌은 입자당 ~5–10 ns(추정). 콜라이더 충돌은 상한으로 제한 | Box2D 질의는 콜라이더 모드에만(이미 배포됨) |
| P.9 | 서브 방출기와 이벤트 | Birth·Death·Collision 시 다른 방출기에 방출(같은 오브젝트 하위, 깊이 1). 대상 풀이 꽉 차면 버리고 dropped로 집계. 스크립트 콜백은 프레임당 집계 1회(입자마다 호출하지 않음). | 불꽃놀이 예제: 폭발 서브 방출 수 = 기대값. 할당 0 B. 트레이스 동일 | S | P.8 | 작음 | 없음 |
| P.10 | ParticleLab 회귀와 성능 기준 | 샘플 씬(결정 4.2): 프리셋 6종을 격자로 배치, 입력 1키로 버스트, 2키로 10k 스트레스. 하네스: StartupHook 트레이스(틱별 방출기마다 활성 수·FNV 해시)와 `verify.py`로 Scene Play·Release·AOT 바이트 비교. CSV로 프레임당 Effects µs·렌더 µs·관리 할당·제출 수를 남긴다(BarrageLab 방식). `./monoon test`에 비용 상한 검사 추가. | macOS arm64 세 경로 트레이스 동일, Linux x64 게스트 Release·AOT 동일. 10k 입자: 프레임당 0 B, 제출 ≤ 11 + 블렌드 그룹 수, Effects+입자 렌더 CPU ≤ 0.5 ms(M4 Max). 결과는 `engine/tests/results/particles/README.md` | M | P.1–P.9를 단계마다 확장 | 샘플만, 배포 엔진 변화 없음 | – |

단계 묶음: **MVP = P.0 → P.1 → P.2 → P.3 → P.5(재생 도구줄과 Scene 미리보기만)**. 착지 먼지·횃불 불씨·연속 연기까지 만들 수 있다. **Full = P.4, P.6, P.7, P.8, P.9, P.10 확장.**

### 설계 메모 (항목 공통)

- **Unity 대응** ([Shuriken 모듈](https://docs.unity3d.com/Manual/ParticleSystemModules.html), [Main](https://docs.unity3d.com/Manual/PartSysMainModule.html)): 2D 게임에서 많이 쓰는 Main, Emission, Shape, Color/Size/Rotation over Lifetime, Velocity·Force, Noise, Texture Sheet, Collision(2D), Sub Emitters, Renderer(Sorting·Blend)만 둔다. Trails, Lights, External Forces, 3D 형태, Custom Data는 범위 밖이다(이미 Trail 2D와 Point Light 2D가 있다). Unity의 `useAutoRandomSeed`(기본 켜짐)와 달리 MoNoon은 **시드 고정이 기본**이다(리플레이 결정론). [Godot CPUParticles2D](https://docs.godotengine.org/en/stable/classes/class_cpuparticles2d.html)도 비교했다. GPU 파티클은 SDL3 2D 렌더러에 컴퓨트 경로가 없어 고려하지 않았다.
- **결정론**: 상태는 고정 틱에서만 바꾸고, 보간은 그리기에서만 한다. 그리기 결과는 트레이스에 넣지 않는다. 방출기 시드 = `SeededRandom(sceneSeed).Fork(stable emitterKey)`, 모듈별 채널 고정(방출 1, 형태 2, 시작값 3, 노이즈 4). 각도는 2D 단위 벡터 거부 표본추출이나 미리 구운 상수 표로 만든다. `MathF.Sin/Cos`는 OS libm에 따라 마지막 자리가 달라질 수 있어 상태 경로에서는 쓰지 않는다(추정. arm64·x64 비교는 P.8 기준). 입자는 게임 로직에 영향을 주지 않는다(스크립트는 이벤트 수만 받음).
- **무할당**: 생성할 때 용량을 정해 모두 할당하고, 이후 `ArrayPool`·LINQ·클로저·문자열 파싱을 쓰지 않는다(`TryColor`는 로드할 때 한 번). 캔버스에는 span으로 넘긴다.
- **배포 용량**: 런타임 증가는 IL +40–80 KB(전 단계 합, 추정)다. Native AOT 증가는 직접 측정해 각 단계 결과에 남긴다. 에디터 기능은 배포본 0 B이고, 프리셋 텍스처는 쓴 경우에만 들어간다.

## 4. 결정이 필요한 것

1. **설정 저장 위치**: (A) 컴포넌트 인라인 확장 / (B) `.particles` 에셋 + 컴포넌트는 참조와 인스턴스 값만. **권장 B**(P.7). 프리팹 여러 개가 공유하고, 패널에서 고립 편집하고, 프리셋을 배포하고, Play 중 다시 불러올 수 있다. MVP(P.1–P.3)는 인라인으로 시작하고, P.7에서 기존 인라인 값을 에셋으로 옮기는 마이그레이션을 둔다.
2. **샘플 위치**: (A) 새 ParticleLab / (B) PlatformerLab(먼지·불씨·비)과 BarrageLab(10k 스트레스)에 씬 추가. **권장 B**. 착지·타일 충돌·정렬은 PlatformerLab에 이미 필요한 요소가 있고, BarrageLab에는 GC·제출 CSV 하네스가 있다. 프리셋 갤러리가 필요해지면 그때 ParticleLab을 만든다.
3. **형태 샘플링에 `Brutal.Maths` 사용 여부**: (A) 채택(`SeededRandom`을 감싼 `System.Random` 하위 클래스 + `float2`→`Vector2` 변환, +8 KB) / (B) 직접 구현(2D 몇 줄). **권장: 먼저 A의 호출당 난수 소비 수와 결정성을 확인하고, 문제가 없으면 A**(BRUTAL 우선). 래퍼 비용이 크거나 소비 수가 달라지면 B로 하고 이유를 `docs/brutal.md` 유지 결정에 기록한다.
4. **색 선택 컨트롤**: (A) `Avalonia.Controls.ColorPicker` 11.3.x 추가(에디터만 커짐, 정확한 크기는 측정 필요) / (B) hex + 견본 + HSV 슬라이더 직접 구현. **권장 B**. 기존 Sprite·Line·Light 색 행과 열 정렬을 그대로 맞출 수 있고, 패키지 테마가 에디터 토큰과 따로 놀지 않는다.
5. **Scene 편집 화면 미리보기 범위**: (A) 선택한 방출기만 / (B) 씬 전체 방출기 상시. **권장 A**. 편집 화면의 프레임 비용을 늘리지 않는다. 토글로 "모두 재생"을 둔다.
6. **x64 결정론 범위**: 입자 트레이스의 arm64 ↔ x64 동일성을 합격 기준에 넣을지. **권장: 같은 OS·아키텍처 안에서 Scene Play·Release·AOT 동일만 필수로 하고, 교차 아키텍처는 기록만**. 입자는 게임 상태에 영향이 없고, 11.3·11.7에도 x64 AOT 미세 차이 선례가 있다.
7. **P.0을 4.2 연출 보완으로 먼저 진행할지**: 횃불 셰이더·점광원에도 적용되는 결함이다. **권장: 바로 진행**(S, 동작 변화 없음).

---
측정 재현: `tools/particles/Program.cs` (`dotnet build -c Release` 후 `.artifacts/bin/Engine.Checks/release/Engine.Checks.dll` 실행).
