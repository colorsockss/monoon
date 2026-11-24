# Native AOT 게임별 엔진 기능 제외 (C1 + 15.12)

2026-11-24 · macOS 27.0.1 · Apple Silicon arm64 · .NET SDK 10.0.101(ILCompiler 10.0.1). 대상은 osx-arm64 배포용(Distribution) Native AOT 게임의 `Contents/MacOS/MoNoonPlayer`이고, Release(JIT·ReadyToRun)는 동작이 바뀌지 않는지와 크기만 봤다. 기준 커밋 `05406a8`(wip/next1, AOT 2차 축소 뒤). 마지막 커밋 전에 wip/next1 `145a4b5`(W4 하네스 목록, W5 검사 등록)를 병합했다. 측정 중 QEMU 게스트는 실행되지 않았다(시작·끝 `pgrep -f qemu-system` 결과 없음). VM은 시작하지 않았다. Linux x64·Windows 확인은 coordinator의 x64 회귀 몫이다.

## 결과 요약

| 실행 파일 (B) | `05406a8` | C1 (`a0b8ef7`) | 15.12 최종 | 합계 |
| --- | ---: | ---: | ---: | ---: |
| SpriteLab Native AOT | 7,359,440 | 7,359,440 | 6,264,544 | **−1,094,896 (−14.9%)** |
| KeyRoom Native AOT | 8,321,968 | 7,575,072 | 6,861,776 | **−1,460,192 (−17.5%)** |
| PlatformerLab Native AOT | 7,923,552 | 7,475,584 | 7,442,272 | **−481,280 (−6.1%)** |
| InventoryLab Native AOT | 7,973,216 | 7,541,808 | 6,646,112 | **−1,327,104 (−16.6%)** |
| AudioLab Native AOT | 7,840,496 | – | 6,480,304 | −1,360,192 (−17.3%) |
| ParticleLab Native AOT | 7,807,440 | – | 7,226,736 | −580,704 (−7.4%) |

- 목표(SpriteLab ≥ 1 MB)를 넘었다: −1.04 MiB(−1.09 MB). 조사 추정 1.26 MB 중 UI 쪽(아래 [자르지 못한 것](#자르지-못한-것))을 빼고 거의 다 나왔다.
- C1은 동작 중립 변경이다. 월드 체크포인트와 스크립트 필드를 `JsonSerializer` 없이 읽고 써서 직렬화기 계층이 플레이어에서 사라졌다(KeyRoom −746,896 B, 조사 추정 −0.34~−0.62 MB보다 크다). 체크포인트를 쓰지 않아도 스크립트 기본 형식 필드를 읽는 모든 게임이 같이 줄었다.
- 15.12는 빌드의 기능 판정(15.11)이 뺀 기능의 엔진 코드를 Native AOT에서 기능 스위치로 잘라 낸다. 기능을 모두 쓰는 PlatformerLab(오디오만 없음)은 15.12 몫이 −33 KB뿐이다.
- Release: 기능 스위치는 Native AOT publish에만 들어가므로 Release 코드는 그대로다. C1만 Release에도 효과가 있다(게임별 트리밍 Release가 `JsonSerializer`를 덜어 냄): KeyRoom `.app` 33,162,522 → 32,219,268 B, PlatformerLab 30,448,622 → 29,653,486 B, InventoryLab 83,907,901 → 83,138,365 B.
- 시작 시간·프레임 비용: 꺼진 기능의 코드는 원래 실행되지 않던 분기다. 빌드는 모든 기능 스위치를 상수(빠진 기능 `false`, 나머지 `true`)로 넘겨 Native AOT 실행 파일에 스위치 조회가 남지 않는다(map에 `EngineFeatures` 속성·정적 생성자 없음). Release·에디터는 시작할 때 한 번 읽은 배열 값을 본다. 크기 검사의 첫 프레임 시간은 [크기 기준 갱신](#크기-기준-갱신)에 기록만 했다.

## 1. C1: 월드 체크포인트와 스크립트 필드를 직접 읽고 쓰기 (`a0b8ef7`)

| 파일 | 내용 |
| --- | --- |
| [WorldCheckpointJsonCodec.cs](../../../src/Engine.Core/WorldCheckpointJsonCodec.cs)(새) | `WorldCheckpoint`를 `Utf8JsonWriter`로 쓰고 `JsonElement`에서 읽는다. 직렬화기가 `WorldCheckpointJson`(웹 기본값, `RespectRequiredConstructorParameters`)으로 하던 규칙을 그대로 옮겼다: camelCase 이름, 이름 대소문자 무시, 같은 이름은 마지막 값, 모르는 속성 건너뜀, 기본값 없는 생성자 인자는 모두 필수(nullable 포함), 값 형식에 `null`이면 거부, 숫자는 따옴표 안도 허용, enum은 숫자만(`TryGetInt32`), `Guid` 사전 키는 36자 `D` 형식(Utf8Parser), `Reparented`·`Basis`는 null이면 쓰지 않음 |
| [GameJson.cs](../../../src/Engine.Core/GameJson.cs) | `JsonElements.ReadField<T>`와 `Int32`·`Single`·`Double`·`Boolean`·`Guid`·`Text`·`GuidName`. 따옴표 숫자는 직렬화기의 리더처럼 이스케이프를 푼 UTF-8을 `Utf8Parser`로 끝까지 읽고 길이 한도(정수 11, 실수 128)와 유한값을 본다. `"NaN"`·`"Infinity"`·`"-Infinity"`는 철자가 정확하면 받는다(비교 검사로 확인한 직렬화기 동작). 잘못된 서로게이트 이스케이프는 실패로 본다 |
| [GameBehaviour.cs](../../../src/Engine.Core/GameBehaviour.cs) | `ReadOverride<T>`의 기본 형식 6종이 `JsonElements.ReadField`를 쓴다. 다른 `T`는 전처럼 `ArgumentNullException` |
| [WorldCheckpoints.cs](../../../src/Engine.Core/WorldCheckpoints.cs) | `SaveWorld`·`LoadSavedScene`이 코덱을 부른다. `WorldCheckpointJson` 컨텍스트는 비교 기준으로만 남는다(AOT에서는 참조가 없어 빠진다) |
| [GameJsonWriterChecks.cs](../../Engine.Checks/GameJsonWriterChecks.cs) | ① 기본 형식 6종 × 54개 값(따옴표 정수·실수·부호·공백·`1e39`·`1e400`·명명 리터럴·이스케이프·잘못된 서로게이트·GUID 철자 …) 324개가 `Deserialize(GameJson.Field<T>())`와 같은 값 또는 같은 "유지"(JsonException) ② 체크포인트 4개(기본 포즈, 기저·재부모·고아, 프리팹 멤버, 빈 사전, NaN 포즈는 둘 다 `ArgumentException`)의 쓰기 원문이 같음 ③ 쓴 체크포인트 3개의 한 군데 편집 변형(값마다 54개 이상한 값, 속성 삭제·대문자·키 철자 변경·중복) 6,320개를 두 경로로 읽어 같은 값(직렬화기로 다시 쓴 원문 비교) 또는 둘 다 `JsonException`. 필수 속성 마스크를 일부러 하나 빼면 `World checkpoint read differs`로 실패하는 것을 확인했다 |
| [BuildSizeChecks.cs](../../Engine.Checks/BuildSizeChecks.cs) | 키트 map에 `JsonSerializer` 메서드가 생기면 실패한다 |

KeyRoom map에서 `JsonSerializer` 이름 노드가 489개 → 0개가 됐다(InventoryLab·AudioLab도 0).

## 2. 15.12: 게임별 엔진 기능 스위치

### 방법

- 기능 목록을 넓혔다: [EngineFeatures.cs](../../../src/Engine.Core/EngineFeatures.cs)의 `EngineFeature`에 `Tilemaps`·`Particles`·`Lighting`·`Skeletons`·`Tweens`를 더했다(기존 `Text`·`Audio`·`Physics` 포함 8종). 판정은 15.11 그대로다: 포함 씬·프리팹의 컴포넌트(`[EngineFeature]`, 기반 형식 포함), 충돌 타일셋, 배포 오디오·글꼴, localization, 스크립트 이름 해석(Roslyn). 새 기능의 표지는 컴포넌트와 스크립트 API(`IGameObjects`의 타일·입자·뼈대·`PlayTween` 멤버)에 달았다. 텍스트를 그리는데 표지가 없던 UI Text Field(text)와 Audio Listener 2D(audio)에도 달았다.
- 스위치: 기능마다 `[FeatureSwitchDefinition("MoNoon.Feature.<이름>")]` 속성. [NativeAotBuild.cs](../../../src/Engine.Authoring/NativeAotBuild.cs)가 Native AOT publish의 `Scripts.props`에 빠진 기능을 `RuntimeHostConfigurationOption Value="false" Trim="true"`로 적는다(`MoNoon.ExportedGameOnly`·`MoNoon.ScriptDebugDraw`와 같은 방식, `#if` 없음). Release(트리밍 Release 포함)와 에디터는 스위치를 받지 않는다.
- 코드: ILC는 생성되지 않은 형식의 인스턴스 메서드를 "잠정"으로 두고 컴파일하지 않으므로, 기능의 실행 객체를 만드는 곳만 막으면 그 객체의 메서드가 빠진다. 그래서 [SceneGame.cs](../../../src/Engine.Core/SceneGame.cs)의 `Body` 필드(입자 시스템, 타일맵, 조명, 뼈대, 트윈·경로, Text 2D, 오디오 소스, 충돌체·Rigidbody·Motor·조인트)를 `스위치 ? 만들기 : throw Disabled` 형태로 바꾸고, 객체 없이도 닿는 정적·장면 메서드 호출(`StepEffects`, `IndexParticles`, `UpdateTileWorlds`, 타일 그리기, `GlobalLight`·`DrawLighting`, 뼈대 단계, `StepRigidBodies`, `DispatchContacts`, `MoveGroup`, `SyncAudio`·`Play`·`Stop`·`Silence`, 믹서 설정·해제, 컷신 대화 그리기, UI 라벨·컨트롤·드롭다운·끌기 그리기, 줄 측정, 글자 나타내기)을 스위치로 감쌌다. 모든 감싼 곳은 기능이 없으면 원래도 아무 일을 하지 않던 분기다. 예: 충돌체가 없으면 `MoveGroup`은 `(0, 0, 0, target, 1)`(이동량 0이면 `(…, 0, 0)`)을 그대로 돌려주므로 그 값을 바로 돌려준다.
- 데스크톱: [SdlCanvas.cs](../../../src/Engine.Desktop/SdlCanvas.cs)는 text가 꺼지면 SDL_ttf 래스터라이저 대신 [ExcludedTextRasterizer](../../../src/Engine.Desktop/ExcludedTextRasterizer.cs)를 쓴다(빈 문자열·글꼴 목록은 같은 답, 그 밖은 15.11의 라이브러리 없음 오류). [SdlSounds.cs](../../../src/Engine.Desktop/SdlSounds.cs)는 audio가 꺼지면 사운드 준비·디코드에서 같은 오류를 낸다(소리가 없으니 불리지 않는다).
- YAML 포매터: VYaml 생성기의 모듈 초기화가 모든 `[YamlObject]` 형식·enum·배열 포매터를 등록해, 쓰지 않는 컴포넌트·에셋의 Deserialize 코드와 enum 포매터(enum 하나에 사전 인스턴스화 포함 약 14–18 KB)가 남았다. Native AOT에서는 그 초기화를 ILC 대체 파일([EngineFeatures.Substitutions.xml](../../../src/Engine.Player/EngineFeatures.Substitutions.xml))로 비우고 [PlayerYamlRegistration.cs](../../../src/Engine.Core/PlayerYamlRegistration.cs)가 같은 목록을 등록한다. 기능의 컴포넌트·에셋 문서에서만 닿는 형식(생성 코드의 참조 그래프로 계산, 194줄 중 83줄)은 그 기능의 스위치 블록 안에 있다. 콜라이더 꼭짓점 배열처럼 두 기능이 쓰는 줄은 `Physics || Tilemaps` 블록이다. JIT에서는 생성기 초기화가 그대로 돌고 이 파일은 바로 돌아간다.

### 바꾼 파일

| 파일 | 내용 |
| --- | --- |
| [EngineFeatures.cs](../../../src/Engine.Core/EngineFeatures.cs) | 기능 8종, 스위치 속성, `Name`, `Disabled(feature, owner)` 메시지 |
| [SceneGame.cs](../../../src/Engine.Core/SceneGame.cs), [Particles2D.cs](../../../src/Engine.Core/Particles2D.cs), [TileWorld.cs](../../../src/Engine.Core/TileWorld.cs), [ScenePhysics.cs](../../../src/Engine.Core/ScenePhysics.cs), [ScenePhysicsWorld.cs](../../../src/Engine.Core/ScenePhysicsWorld.cs), [SceneAudio.cs](../../../src/Engine.Core/SceneAudio.cs), [ScriptLifecycle.cs](../../../src/Engine.Core/ScriptLifecycle.cs), [GameSession.cs](../../../src/Engine.Core/GameSession.cs), [ScreenUi.Paint.cs](../../../src/Engine.Core/ScreenUi.Paint.cs), [ScreenUi.Wrap.cs](../../../src/Engine.Core/ScreenUi.Wrap.cs), [ScreenUi.Controls.cs](../../../src/Engine.Core/ScreenUi.Controls.cs), [UiDragDrop.cs](../../../src/Engine.Core/UiDragDrop.cs) | 위의 생성·호출 지점 감싸기 |
| [RuntimeObjects.cs](../../../src/Engine.Core/RuntimeObjects.cs), [Effects2D.cs](../../../src/Engine.Core/Effects2D.cs), [Particles2D.cs](../../../src/Engine.Core/Particles2D.cs), [Skeleton2D.cs](../../../src/Engine.Core/Skeleton2D.cs), [Tilemap2D.cs](../../../src/Engine.Core/Tilemap2D.cs), [Tween2D.cs](../../../src/Engine.Core/Tween2D.cs), [Lines2D.cs](../../../src/Engine.Core/Lines2D.cs), [UiAccessibility.cs](../../../src/Engine.Core/UiAccessibility.cs), [Presentation2D.cs](../../../src/Engine.Core/Presentation2D.cs) | `[EngineFeature]` 표지 |
| [PlayerYamlRegistration.cs](../../../src/Engine.Core/PlayerYamlRegistration.cs)(새), [EngineFeatures.Substitutions.xml](../../../src/Engine.Player/EngineFeatures.Substitutions.xml)(새), [Engine.Player.csproj](../../../src/Engine.Player/Engine.Player.csproj) | AOT 플레이어의 포매터 등록 |
| [Engine.Core.csproj](../../../src/Engine.Core/Engine.Core.csproj) | `EmitCompilerGeneratedFiles`(검사가 생성 목록을 읽는다, `obj/`에만 쓴다) |
| [ExcludedTextRasterizer.cs](../../../src/Engine.Desktop/ExcludedTextRasterizer.cs)(새), [SdlCanvas.cs](../../../src/Engine.Desktop/SdlCanvas.cs), [SdlSounds.cs](../../../src/Engine.Desktop/SdlSounds.cs) | 데스크톱 쪽 |
| [BuildFeatures.cs](../../../src/Engine.Authoring/BuildFeatures.cs), [GameBuild.cs](../../../src/Engine.Authoring/GameBuild.cs), [NativeAotBuild.cs](../../../src/Engine.Authoring/NativeAotBuild.cs) | 새 기능의 판정 이유, 코드 전용 기능은 빠지는 파일 없음, AOT publish에 스위치 전달, 알 수 없는 `features` 이름의 안내 |
| [FeatureSwitchChecks.cs](../../Engine.Checks/FeatureSwitchChecks.cs)(새, `--only feature-switches`), [FeatureChecks.cs](../../Engine.Checks/FeatureChecks.cs) | 아래 검증 |

### 단계별 SpriteLab 크기

| 단계 | 실행 파일 | 변화 |
| --- | ---: | ---: |
| `05406a8` | 7,359,440 | |
| 실행 객체·단계 호출 막기(tilemaps, particles, lighting, skeletons, physics) | 7,027,632 | −331,808 |
| 기능 전용 객체 포매터 등록 막기(형식별 대체) | 6,712,944 | −314,688 |
| text·audio 코드 막기(SDL_ttf 래스터라이저, 리치 텍스트, 믹서) | 6,596,624 | −116,320 |
| 등록을 `PlayerYamlRegistration`으로 옮겨 enum·배열 포매터도 막기, tweens 기능 | 6,297,760 | −298,864 |
| 조명 그리기 호출 막기, wip/next1 병합, 켜진 기능도 상수 스위치 | 6,264,544 | −33,216 |

map 노드를 기능 이름으로 나눈 값(SpriteLab, `05406a8` → 최종, 바이트): 입자 272,183 → 11,653, 물리 190,127 → 73,582, 타일맵 114,034 → 36,657, 뼈대 81,398 → 10,953, 트윈·컷신·경로 85,107 → 34,849, 오디오 104,485 → 70,704, UI·텍스트 260,485 → 154,994, 조명 20,320 → 3,496. 남은 바이트는 아래 표의 이유로 남는다. 도구: [afs 스크립트는 재현 절 참고](#재현).

### 자르지 못한 것

| 묶음 | SpriteLab에 남은 양(map) | 이유 |
| --- | ---: | --- |
| UI 전체(`ScreenUi` 레이아웃·입력·초점·입력 모드, UI 위젯 포매터) | 약 75 KB + 위젯 포매터 약 50 KB | `ScreenUi`는 모든 씬에서 만들어지고 `SceneGame`·`GameInputRouter`가 매 프레임 입력·레이아웃·그리기를 부른다. 위젯이 없을 때 각 메서드(`Key`·`PadKey`·`Pointer`·`Navigate`·`Rebuild`·`Arrange` …, 15개 이상)가 정확히 무엇을 돌려주는지 하나씩 증명해야 동작 중립이다. 스크립트가 있는 게임은 대부분 `IGameUi`를 쓰므로 이득도 작다. 텍스트 부분(SDL_ttf, 리치 텍스트, 라벨·컨트롤 그리기)만 잘랐다. 다음 후보 |
| 게임 설정에 딸린 오디오 형식(`AudioBus` enum, `MixerLevels`·`AudioSnapshot`·`AudioDucking` 포매터, `AudioMixer` 생성자) | 약 50 KB | `GameSettings`가 믹서 설정을 담고 모든 게임이 읽는다. 설정 읽기를 기능별로 나누면 저장 형식 처리가 둘로 갈라진다 |
| 두 기능이 같이 쓰는 형식(콜라이더 꼭짓점, `SpriteRegion`) | 약 20 KB | 둘 다 꺼진 경우만 빠진다(`Physics || Tilemaps`). SpriteLab에서는 빠졌다 |
| 스크립트 트윈(`Tween()`, 16.3), 선·트레일, 내비게이션, 프리팹 풀, 인벤토리·퀘스트 등 | 각 15 KB 미만 | `IGameObjects` 공통 API이거나 스위치를 둘 만큼 크지 않다 |
| record 합성 멤버(`PrintMembers` 등) | 기능 컴포넌트에 몇 KB | 형식을 다른 경로(복사 `with`, 기본값)가 만들면 남는다 |

KeyRoom(text·audio·physics 사용)은 타일맵·입자·조명·뼈대·트윈이, InventoryLab·AudioLab은 여기에 물리까지 빠진다. PlatformerLab은 audio만 빠진다.

### 런타임 계약

- 빌드가 본 콘텐츠에는 꺼진 기능의 컴포넌트가 없다(판정의 정의). 빌드가 보지 못한 콘텐츠(모드, 콘텐츠 업데이트)가 꺼진 기능의 컴포넌트를 쓰면, 포매터를 등록하지 않은 형식은 씬을 읽을 때 VYaml의 포매터 없음 오류로, 그 밖은 `<오브젝트>: this build excluded <기능>: … Add <기능> to GameSettings features and build again to include it.`로 멈춘다. 15.11과 같이 `GameSettings features`로 포함한다.
- 스위치는 다른 조건과 `&&`·`||`로 묶지 않고 따로 `if`로 둔다. aot-serializer에서 `ExportedGameOnly || exported`가 비단락 `or`로 컴파일돼 ILC가 접지 못한 일이 있었다.

### 프레임 비용

Native AOT: 스위치가 상수라 분기 자체가 컴파일되지 않거나(꺼짐) 조건 없이 남는다(켜짐). 처음 판에서는 켜진 기능에 스위치를 적지 않아 `AppContext.TryGetSwitch`가 오브젝트 그리기마다 불렸다. 모든 스위치를 적게 고쳤고 크기는 0–16 B 달라졌다. JIT(Release·에디터): `EngineFeatures`가 처음 쓰일 때 여덟 값을 배열에 한 번 읽는다. 검사만 `EngineFeatures.SetForChecks`로 값을 바꾼다.

## 로드맵 15.12 행 문안 제안 (coordinator용, `docs/roadmap.md`는 고치지 않음)

> | 15.12 | AOT 게임별 엔진 기능 조건부 컴파일 | 구현 완료. 15.11 판정을 코드 기능 8종(text, audio, physics, tilemaps, particles, lighting, skeletons, tweens)으로 넓히고, Native AOT publish에 기능 스위치 `MoNoon.Feature.<이름>`을 상수로 넘긴다(`MoNoon.ExportedGameOnly`와 같은 `[FeatureSwitchDefinition]` + `Trim="true"`, `#if` 없음). `SceneGame`의 실행 객체 생성과 단계·그리기 호출, SDL_ttf 래스터라이저·믹서를 스위치 뒤에 두고, 플레이어의 VYaml 포매터 등록을 기능별 블록으로 나눠(`PlayerYamlRegistration`, 생성기 목록과 같은지 검사) 쓰지 않는 컴포넌트·에셋 읽기 코드도 뺐다. 앞서 월드 체크포인트·스크립트 필드를 `JsonSerializer` 없이 읽고 써(같은 바이트, 한 군데 편집 변형 6,320개와 필드 값 324개가 직렬화기와 같은 결과) 직렬화기 계층을 플레이어에서 없앴다. Native AOT 실행 파일 SpriteLab 7.36 → 6.26 MB(−14.9%), KeyRoom 8.32 → 6.86 MB(−17.5%), InventoryLab 7.97 → 6.65 MB(−16.6%), PlatformerLab 7.92 → 7.44 MB(−6.1%). Release는 기능 코드가 그대로이고 트리밍 Release만 `JsonSerializer` 몫(KeyRoom `.app` −0.94 MB)이 줄었다. 결정성 게이트(Joints·TileCollision·Paths·Slopes·ParticleLab·AudioLab·InventoryLab 리플레이·KeyRoom 월드 체크포인트)가 Release·AOT에서 기준과 같고, 스크립트 없는 샘플 씬 5개가 JIT에서 안 쓰는 기능을 끈 채 450프레임 같은 그리기를 낸다. UI 전체(`ScreenUi` 레이아웃·입력, 약 0.12 MB)는 위젯 없을 때의 동등성 확인이 별도 작업이라 남겼다. 빌드가 보지 못한 모드·콘텐츠가 빠진 기능을 쓰면 `features`로 포함해야 한다. Linux·Windows는 다음 x64 회귀. [검증 기록](../engine/tests/results/aot-feature-switches/README.md) |

## 검증

osx-arm64, 배포용(Distribution) 빌드, 창 없는 재생. 커밋 전 이 워크트리에서 템플릿을 다시 만들고(`engine/scripts/build-players.sh`) [aot-size-2/gates.sh](../aot-size-2/gates.sh)로 돌렸다. C1 출력: [gates-c1.txt](gates-c1.txt), 최종: [gates-final.txt](gates-final.txt), [particles-rerun.txt](particles-rerun.txt). 첫 예외는 없다.

| 검사 | Release (C1 · 최종) | Native AOT (C1 · 최종) | 값 |
| --- | --- | --- | --- |
| PlatformerLab.Joints 300틱 | 같음 · 같음 | 같음 · 같음 | `2d523cd0…` |
| PlatformerLab.TileCollision | 같음 · 같음 | 같음 · 같음 | `f30ed686…` |
| PlatformerLab.Paths 400틱 | 같음 · 같음 | 같음 · 같음 | `e37de6c0…` |
| PlatformerLab.Colliders(Slopes) | 같음 · 같음 | 같음 · 같음 | `635f9029…` |
| ParticleLab 7개 장면 trace·캡처 | 같음 · 같음 | 같음 · 같음 | 커밋된 `results/particles/osx-arm64/*-trace.tsv`와 같음. 최종 실행의 첫 시도는 SDK의 Roslyn이 Rain 게임 스크립트를 컴파일하다 `MissingMethodException: CSharpSyntaxNodeCache.GetNodeFlags`로 죽었다(코드와 무관한 도구 오류, 앞 장면 4개는 통과). 같은 트리에서 다시 돌려 7개 모두 통과([particles-rerun.txt](particles-rerun.txt)) |
| AudioLab.Buses PCM · 덕킹 | 같음 · 같음 | 같음 · 같음 | `ced6c800…` · `f6c485bb…` |
| InventoryLab 리플레이 | 같음 · 같음 | 같음 · 같음 | 171 B, `EED41DEF…E70F795` |
| KeyRoom.WorldCheckpoints(`ci.py osx-arm64`) | 통과 · 통과 | 통과 · 통과 | 22개 PASS, CarryCoin 833 B `93dd4173…` |
| SpriteLab.Aseprite(모든 기능 꺼짐, 600프레임) | – · 같음 | – · 같음 | Release·AOT `trace.txt`·`result.json` 바이트 동일 `4a9a7053…` |
| `--smoke-test` | – | SpriteLab·KeyRoom 통과 · SpriteLab·KeyRoom·InventoryLab·AudioLab·PlatformerLab·ParticleLab 통과 | |
| `./monoon test` | C1 통과(PASS 953줄) · 최종 통과(PASS 956줄) | – | 새 항목: 필드 324값, 체크포인트 6,320 읽기, `feature-switches`(스위치 이름, 씬 5개 450프레임, 제외 메시지, 포매터 194줄·83줄 게이트·링크 54개), 키트 map의 `JsonSerializer` 검사. lighting·world-text·ui-grid·nav-free·autotile·animator 검사 포함 |
| 금지 파일 | – | – | ParticleLab `verify.py all`의 Release·AOT `.app` 14개, SpriteLab.Aseprite의 두 `.app`: SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring 없음 |


## 크기 기준 갱신

`./monoon test size --update`로 [baseline.tsv](../build-size/baseline.tsv)를 두 번 갱신했다(C1 커밋, 최종 커밋). 시간 열은 기록만 하고 판정하지 않는다.

| 빌드 | `05406a8` | C1 | 최종 |
| --- | ---: | ---: | ---: |
| KeyRoom Native AOT 실행 파일 · `.app` | 8,321,968 · 15,460,295 | 7,575,072 · 14,713,399 | 6,861,776 · 14,000,103 |
| PlatformerLab Native AOT 실행 파일 · `.app` | 7,923,552 · 12,640,993 | 7,475,584 · 12,193,025 | 7,442,272 · 12,159,758 |
| InventoryLab Native AOT 실행 파일 · `.app` | 7,973,216 · 66,084,912 | 7,541,808 · 65,653,504 | 6,646,112 · 64,757,808 |
| KeyRoom Release `.app` | 33,162,522 | 32,219,268 | 32,231,556 |
| PlatformerLab Release `.app` | 30,448,622 | 29,653,486 | 29,665,307 |
| InventoryLab Release `.app` | 83,907,901 | 83,138,365 | 83,150,653 |

Release `.app`이 15.12에서 12 KB 늘었다. Release는 스위치를 받지 않으므로 새 코드(`PlayerYamlRegistration`, 감싼 분기, `ExcludedTextRasterizer`)의 ReadyToRun 코드가 그대로 들어간다(1% 허용 안). 첫 프레임(헤드리스 중앙값): KeyRoom AOT 38 → 34–46 ms, Release 165 → 113–128 ms. 같은 날 다른 작업과 겹쳐 재어 판정하지 않는다.

## 재현

```sh
engine/scripts/build-players.sh
export AOT_SIZE_OUT=$(mktemp -d)
for s in SpriteLab KeyRoom PlatformerLab InventoryLab; do sh engine/tests/results/aot-size-2/tools/mapbuild.sh "$PWD" $s after; done
python3 engine/tests/results/aot-feature-switches/tools/featbytes.py "$AOT_SIZE_OUT"/after/*/native/MoNoonPlayer.map.xml   # 기능 이름별 map 바이트
python3 engine/tests/results/aot-feature-switches/tools/top.py "$AOT_SIZE_OUT/after/SpriteLab/native/MoNoonPlayer.map.xml" 'ScreenUi' 30
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only feature-switches   # 스위치 이름, 씬 동등성, 포매터 등록
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only player-yaml        # C1 비교 검사(앞 검사 포함)
sh engine/tests/results/aot-size-2/gates.sh "$(mktemp -d)"
./monoon test
```

[PlayerYamlRegistration.cs](../../../src/Engine.Core/PlayerYamlRegistration.cs)는 [tools/genreg.py](tools/genreg.py)로 만들었다(`EmitCompilerGeneratedFiles`가 쓴 생성 파일 폴더를 준다). `[YamlObject]` 형식이 바뀌면 다시 만들거나 손으로 같은 블록에 옮기고 `--only feature-switches`로 확인한다.

## 확인하지 않은 것

- Scene Play: 에디터는 스위치가 모두 켜진 JIT이고 바뀐 코드는 켜진 경우 원래 분기와 같다. 에디터 패키지를 새로 만들어 돌리지 않았다.
- Linux x64·Windows Native AOT(coordinator x64 회귀). ILC 대체 파일과 기능 스위치는 RID와 무관하지만 게스트에서 스모크·리플레이를 봐야 한다.
- 켜진 기능의 스위치 조회 비용 실측(위 [프레임 비용](#프레임-비용)).
- 빌드가 보지 못한 콘텐츠(모드)로 꺼진 기능을 실제 Native AOT에서 부르는 경로. JIT에서 스위치를 끄고 `Disabled` 메시지는 확인했다.
