# 배포 용량 2차 묶음 (T4·B5·B4)

2026-11-24 · macOS 27.0.1 · Apple Silicon arm64 · .NET SDK 10.0.101. 기준 커밋 `8eb15b2`(wip/next1, [1차 묶음](../size-wave1/README.md)·에셋 빌드 보고서·P.0 포함)에서 로드맵 15.11(T4), 15.7(B5), 15.6(B4)를 차례로 적용했다. 항목마다 커밋 하나다. 측정 중 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음).

- 크기: `./monoon build samples/<샘플> <release|native-aot> --target osx-arm64 --profile distribution`의 `.app`에서 심볼릭 링크를 0바이트로 센 일반 파일 크기 합이다. 실행 파일은 `Contents/MacOS/MoNoonPlayer`다.
- 시작 시간: 창 없이(`SDL_VIDEO_DRIVER=monoon-no-video`, `SDL_AUDIO_DRIVER=monoon-no-audio`) 1프레임 `.inputreplay`를 `MONOON_REPLAY_SNAPSHOTS=<폴더>;0`으로 재생해 0번 프레임을 PNG로 그리고 끝날 때까지의 벽시계 시간이다(조사 2와 같은 방법). `session_ready`는 `MONOON_STARTUP_TRACE` 값이다. 빌드 직후 첫 실행은 버리고, 비교 대상을 번갈아 실행했다.
- Windows·Linux: 이 Mac에서 만든 x64 배포본은 파일만 확인했다. 실행 확인은 다음 x64 회귀에서 한다.

## T4: 쓰지 않는 엔진 기능의 네이티브 라이브러리 제외 (15.11)

### 바꾼 것

- [EngineFeatures.cs](../../../src/Engine.Core/EngineFeatures.cs): `EngineFeature`(`Text`·`Audio`·`Physics`)와 `[EngineFeature]` 속성. 라이브러리가 필요한 컴포넌트와 스크립트 API에 붙였다.
  - text: `UiTextComponent`, `UiControlComponent`(버튼·토글·슬라이더·드롭다운·텍스트 필드), `HudComponent`, `CutsceneDirectorComponent`, `SessionState.LoadSceneAsync`(기본 전환 문구 "Loading…"을 그린다).
  - audio: `AudioSourceComponent`, `IGameAudio`, `AudioMixer`.
  - physics: `Collider2DComponent`(상속 포함), `Rigidbody2DComponent`, `Joint2DComponent`, `CharacterMotor2DComponent`, `IPhysics2D`, `Shape2D`(다각형 생성이 Box2D를 부른다).
  - `IPhysics2D` 질의나 내비게이션은 충돌체가 없으면 Box2D를 부르지 않는다(코드 확인). 충돌체 없는 씬에서 `CollisionGeometry.ForObject`는 `default`를 돌려준다.
- [BuildFeatures.cs](../../../src/Engine.Authoring/BuildFeatures.cs), [GameBuild.cs](../../../src/Engine.Authoring/GameBuild.cs): 기존 의존성 탐색에서 컴포넌트 형식의 속성을 리플렉션으로 읽고, 충돌 모양이 있는 타일셋, 배포하는 WAV·Ogg·글꼴, `GameSettings.localization`, 스크립트 사용을 모아 기능마다 처음 찾은 이유를 남긴다. 쓰지 않는 기능은 Release에서는 템플릿 파일 목록 복사에서, Native AOT에서는 publish 출력에서 `libSDL3_ttf`·`libSDL3_mixer`·`libbox2d`(`.dylib`·`.so`·`.dll`)와 `Licenses/BRUTAL`의 해당 고지 파일을 뺀다. 관리 바인딩(`Brutal.Sdl.Ttf.dll` 등)은 남긴다(BRUTAL 바인딩 유지). 영수증 `MoNoonBuild`에 `features`(포함 기능, 쉼표 구분)를 쓴다.
- [ProjectScripts.Features.cs](../../../src/Engine.Authoring/ProjectScripts.Features.cs): 프로젝트 스크립트 컴파일의 모든 이름(`SimpleNameSyntax`)을 의미 모델로 해석해, 기호·포함 형식·(형식이면) 기반 형식에 `[EngineFeature]`가 있으면 그 기능을 쓴다고 본다. 호출·멤버·형식 참조, `GetComponent<Rigidbody2DComponent>()`의 형식 인수, `typeof`·`nameof`가 모두 잡힌다. 조사 문서가 제안한 IOperation 호출 검사보다 넓게(보수적으로) 잡는다. 결과는 게임 빌드가 요청할 때만 계산한다(`Lazy`). 붙이지 않은 스크립트도 배포 어셈블리에 들어가므로 센다.
- `GameSettings.features`(문자열 목록, `Text`·`Audio`·`Physics`, 대소문자 무시)로 강제 포함한다. 빌더만 읽고 배포하는 `Game/GameSettings`에서는 지운다. 열거형 배열로 두면 VYaml 열거형 포매터가 모든 AOT 플레이어에 들어가 문자열로 했다(아래 크기 참고).
- 런타임: 기능이 처음 라이브러리를 읽는 곳(`AudioFile` SDL_mixer 초기화, `TtfTextRasterizer` SDL_ttf 초기화, `ScenePhysicsWorld` 월드 생성, `CollisionGeometry.Hull`)에서 `DllNotFoundException`과 Box2D 바인딩의 형식 초기화 실패(`TypeInitializationException`)를 `This build excluded physics (Box2D): no shipped scene, prefab or script used it. Add Physics to GameSettings features and build again to include it.`로 바꾼다. 정상 경로에는 비용이 없다(예외 필터).
- 보고서: `assets.yaml`에 `features`(기능·포함 여부·이유), CLI·Console 요약에 기능 세 줄.

샘플 판정(osx-arm64 배포용 Release):

| 샘플 | text | audio | physics |
| --- | --- | --- | --- |
| SpriteLab | 제외 | 제외 | 제외 |
| KeyRoom | `Scenes/KeyRoom.scene > Key label > UiText` | `Scenes/KeyRoom.scene > Door > AudioSource` | `Scenes/KeyRoom.scene > Player > BoxCollider2D` |
| PlatformerLab | `Scenes/Platform.scene > Far landing shot > CutsceneDirector` | 제외 | `Scenes/Platform.scene > Runner > BoxCollider2D` |
| InventoryLab | `Scenes/Inventory.scene > Title > UiText` | `Scenes/Inventory.scene > Market ambience > AudioSource` | 제외 |
| AudioLab | `Scenes/AudioLab.scene > Title > UiText` | `Scenes/AudioLab.scene > Tide > AudioSource` | 제외 |

InventoryLab·AudioLab은 Box2D가 빠져도 아래 리플레이·PCM 기준값이 같다(물리를 쓰지 않는다는 판정의 실행 확인).

### 크기 (osx-arm64, Distribution, 바이트)

| 산출물 | 기준 `8eb15b2` | T4 | 차이 |
| --- | ---: | ---: | ---: |
| SpriteLab Release `.app` | 87,312,277 | 84,894,023 | −2,418,254 |
| SpriteLab Native AOT `.app` | 14,873,212 | 12,468,414 | −2,404,798 |
| SpriteLab Native AOT 실행 파일 | 9,938,416 | 9,954,944 | +16,528 |

SpriteLab에서 빠진 파일: `libSDL3_ttf.dylib` 1,800,096, `libSDL3_mixer.dylib` 326,128, `libbox2d.dylib` 258,208(B7 정리 후 크기)과 고지 세 개 36,909, 합계 2,421,341바이트. AOT 실행 파일 +16.5 KB는 `[EngineFeature]` 속성 메타데이터와 오류 경로다. 열거형 배열 `features`와 `Enum.ToString`을 쓴 첫 구현은 +66 KB였다. 조사 추정(−3.2 MB, AOT .app 17→14 MB)과 차이는 B7이 dylib 로컬 심볼을 이미 지웠기 때문이다. 다른 샘플은 B5 표의 T4 열에 있다.

### 검사

| 검사 | Release | Native AOT |
| --- | --- | --- |
| SpriteLab `--smoke-test`(세 라이브러리 없음) | PASS | PASS |
| SpriteLab 창 실행(`gui-lock.sh`, Metal, 120프레임 표시) | PASS(first_present 229 ms) | PASS(149 ms) |
| SpriteLab 헤드리스 0번 프레임 PNG, 기준 빌드와 비교 | 같음 `44afce2f…` | 같음 `44afce2f…` |
| KeyRoom 월드 체크포인트(`ci.py osx-arm64`) | PASS | PASS |
| InventoryLab 리플레이 저장 `EED41DEF…`(Box2D 없음) | PASS | PASS |
| InventoryLab.Crashes `QuestJournal.cs:35` | PASS | PASS |
| PlatformerLab Joints trace `2d523cd0…` | PASS | PASS |
| AudioLab 첫 네 바퀴 PCM `ced6c800…`(Box2D 없음, 헤드리스) | PASS | PASS |
| 금지 파일 | 없음 | 없음 |
| `./monoon test`(새 `FeatureChecks`, KeyRoom 세 기능 유지 단언 포함) | PASS | |

- [FeatureChecks](../../Engine.Checks/FeatureChecks.cs)(`--features-only`): SpriteLab 복사본을 Release로 내보내 (1) 세 기능 모두 제외·`--smoke-test` 통과, (2) `tick.Objects.Audio.Play`만 부르는 붙이지 않은 스크립트가 audio로 판정(`Scripts/Chime.cs:4: IGameAudio.Play(Guid, float?)`), (3) `features: [physics]` 강제 포함과 배포 `GameSettings`에서 삭제, (4) `Shape2D.Box`를 부르는 스크립트의 빌드에서 `libbox2d.dylib`를 지우면 플레이어가 종료 코드 1과 위 안내 문구로 멈추는지 확인한다.
- AudioLab 기존 [ci.py](../../AudioLab.Vorbis/ci.py)는 창에 F 키를 보내는 `mac-input`이 이 세션에서 `No keyboard event permission`(손쉬운 사용 권한 없음)으로 실패해, F 이후 페이드 아웃은 확인하지 못했다. 대신 같은 환경 변수(SDL disk 드라이버, `MONOON_AUDIO_CAPTURE`)로 420프레임 빈 입력을 60 Hz 오프스크린 재생해 첫 네 바퀴(230,400프레임) SHA-256을 기준값과 비교했다. 창 실행의 캡처도 같은 값이었다.

## B5: Release 게임별 Trim + ReadyToRun (15.7)

### 바꾼 것

- [GameBuild.cs](../../../src/Engine.Authoring/GameBuild.cs), [NativeAotBuild.cs](../../../src/Engine.Authoring/NativeAotBuild.cs): Release도 Native AOT 키트(`player-templates/native-aot`)를 게임마다 `dotnet publish`한다. 같은 스크립트 스냅샷(`MoNoonGameReferences`)을 정적으로 연결하고 `PublishTrimmed=true`, `PublishReadyToRun=true`, `PublishReadyToRunEmitSymbols=false`(R2R 심볼 파일이 배포본에 남지 않게)를 준다. 배포용은 publish 출력에서 디버거·대체 GC 네이티브를 지운다(T1 규칙). 키트가 템플릿 옆에 없으면 이전처럼 공용 템플릿을 복사한다.
- ILLink는 기본적으로 다시 쓴 어셈블리의 디버그 디렉터리에 임시 작업 폴더의 PDB 경로를 적는다. 그래서 같은 설정의 에디터·CLI Release가 앱 어셈블리 16개에서 달랐다(KeyRoom.EditorBuild 첫 실행에서 발견). `--preserve-symbol-paths`(`_ExtraTrimmerArgs`)로 컴파일러가 `PathMap`으로 고정한 경로(`/_/monoon-aot/…`)를 남겨 두 번 빌드한 결과가 바이트 단위로 같다.
- ReadyToRun 컴파일러는 승인된 `Microsoft.NETCore.App.Crossgen2.osx-arm64` 10.0.1 하나만 내려받았다(NuGet 캐시 36 MB). 이 호스트 컴파일러가 win-x64·linux-x64도 컴파일한다. 다른 RID용 crossgen2 팩은 필요 없었고 받지 않았다. 세 RID의 `Engine.Core.dll` PE Machine 값이 각각 `0xEC20`(arm64·macOS), `0x8664`(x64·Windows), `0xFD1D`(x64·Linux)인 R2R 이미지임을 확인했다. 템플릿의 IL 전용 DLL은 `0x14C`다.
- [CrashDiagnostics.cs](../../../src/Engine.Player/CrashDiagnostics.cs): `#if MONOON_COMPILED_SCRIPTS`를 `RuntimeFeature.IsDynamicCodeSupported`로 바꿨다. Native AOT는 이미지 오프셋, JIT(Release)는 모듈 ID·메서드 토큰·IL 오프셋을 남긴다. AOT에서는 ILC가 JIT 분기를 지워 실행 파일 크기가 바이트 단위로 같다(KeyRoom 10,585,344).
- [BuildSymbols.cs](../../../src/Engine.Authoring/BuildSymbols.cs) `ScriptAssembly`: 스크립트 DLL이 플레이어 옆(`MoNoon.GameScripts.dll`)에 있으면 그 PDB를, 없으면(템플릿 복사) `Game/.monoon-compiled/`를 영수증에 쓴다. [CrashSymbols.cs](../../../src/Engine.Cli/CrashSymbols.cs)는 고정 목록 대신 영수증의 `pdbId/*` 항목을 따라가므로 이전 빌드의 보고서도 그대로 해석한다. MSBuild로 컴파일한 스크립트 PDB의 문서 이름 `/_/Scripts/…`도 프로젝트 상대 경로로 바꾼다.
- [PlayerDistribution.targets](../../../src/Engine.Player/PlayerDistribution.targets): 게임별 publish에서는 `MoNoonPlayer.files`를 쓰지 않는다.
- 검사: [Program.cs](../../Engine.Checks/Program.cs)의 세 RID KeyRoom 내보내기는 스크립트 DLL이 플레이어 옆에 하나 있고 `Game/.monoon-compiled`·`MoNoonPlayer.files`가 없는지 본다. 같은 검사의 프로세스 안 KeyRoom 진행은 Scene Play처럼 스크립트를 읽으므로 trim하지 않은 컴파일을 쓴다(trim한 스크립트 DLL은 쓰지 않는 `IEnumerator.Reset` 등이 빠져 일반 런타임에서는 형식 로드에 실패한다). `BuildProfileChecks`·`InventoryBuildChecks`·`KeyRoom.Desktop/verify.py`·`retest.py`도 새 위치를 읽는다.
- composite R2R(`PublishReadyToRunComposite`)는 측정만 했다. KeyRoom 첫 프레임이 115 → 78 ms로 빠르지만 `.app`이 33.2 → 41.5 MB, footprint가 33.0 → 37.8 MiB로 커진다. 용량 우선 원칙과 완료 기준(첫 프레임 260 ms 이하)을 이미 만족해 쓰지 않는다.

### 크기 (Distribution, 바이트)

| 산출물 | T4(템플릿 복사) | B5(게임별 publish) | 차이 |
| --- | ---: | ---: | ---: |
| SpriteLab Release `.app` | 84,894,023 | 27,931,073 | −56,962,950 (−67.1%) |
| KeyRoom Release `.app` | 89,548,041 | 33,168,409 | −56,379,632 (−63.0%) |
| PlatformerLab Release `.app` | 87,073,040 | 30,416,778 | −56,656,262 (−65.1%) |
| InventoryLab Release `.app` | 140,507,608 | 83,859,538 | −56,648,070 (−40.3%) |
| AudioLab Release `.app` | 87,096,552 | 30,332,770 | −56,763,782 (−65.2%) |
| KeyRoom Release composite R2R(측정만) | | 41,505,305 | |
| KeyRoom win-x64 Release 폴더 | 87,161,196 | 35,154,492 | −52,006,704 (−59.7%) |
| KeyRoom linux-x64 Release 폴더 | 89,760,170 | 38,828,629 | −50,931,541 (−56.7%) |
| Native AOT 다섯 샘플 | T4와 같음 | 같음 | 0 |

x64 열의 T4 값은 같은 소스에서 키트를 숨겨 템플릿 복사로 만든 결과다. 1차 묶음 기준(`8eb15b2`) KeyRoom Release는 89,544,936이다. InventoryLab은 의도된 대용량 번들(51.3 MB)이 비율을 낮춘다. 조사 추정(30–35 MiB)과 맞는다. 내보내기 시간은 Release 한 게임에 2–3 s → 19–20 s(세 RID 같음)다.

KeyRoom osx-arm64 B5의 큰 파일: `System.Private.CoreLib.dll` 7.4 MB, `libcoreclr.dylib` 4.7 MB, `Engine.Core.dll` 3.1 MB(R2R 코드 포함, IL만은 1.3 MB), `libclrjit.dylib` 2.6 MB, `libSDL3.0.dylib` 2.5 MB. 프레임워크·Microsoft.Extensions 관리 DLL은 185개 → 35개다.

### 시작 시간 (osx-arm64, 헤드리스, 5회 교대 실행 중앙값(범위))

이 측정 중에는 다른 작업의 QEMU 게스트가 실행 중이었다(`qemu_running=True`). 세 빌드를 번갈아 실행해 부하가 같게 걸리도록 했다. 모든 비교에서 중앙값 차이가 두 범위의 합보다 크다. 0번 프레임 PNG는 T4·B5·AOT가 같다.

| 샘플 | T4 Release 첫 프레임 ms | B5 Release | T4 Native AOT | T4 → B5 `session_ready` ms | T4 → B5 footprint MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| KeyRoom | 262 (259–292) | **124** (123–127) | 28 (27–29) | 144.9 → 57.9 | 58.3 → 33.1 |
| PlatformerLab | 288 (287–291) | **145** (143–150) | 34 (33–35) | 153.8 → 68.0 | 62.3 → 36.8 |
| InventoryLab(번들 캐시 미스) | 594 (590–627) | **486** (481–504) | 392 (385–393) | 473.5 → 415.8 | 62.0 → 38.1 |
| SpriteLab | 178 (176–183) | **91** (90–96) | 20 (20–21) | 86.3 → 29.7 | 54.5 → 29.1 |

composite 비교(QEMU 없음, KeyRoom 5회): T4 244, R2R 115, composite 78, AOT 25 ms. 완료 기준(KeyRoom 첫 프레임 260 ms 이하)을 만족한다. footprint는 조사 추정(−22 MiB)대로 약 −25 MiB다.

### Trim 경고

KeyRoom Release publish의 trim 경고는 네 곳(각각 컴파일 분석기와 ILLink가 한 번씩, 7줄)이고 모두 Native AOT에 이미 있던 경고다. 0건으로 만들지 않고 아래 이유로 둔다.

| 위치 | 경고 | 이유 |
| --- | --- | --- |
| `ScriptLibrary.cs:25` `LoadFromStream` | IL2026 | 스크립트 DLL을 실행 중 읽는 Scene Play·템플릿 복사 경로. 게임별 publish는 정적 팩토리(`CompiledGameScripts.Create`)를 넘기므로 이 경로를 타지 않는다 |
| `ScriptRuntime.cs:18` `Assembly.GetType` | IL2026 | 같은 동적 경로(이름으로 형식 찾기) |
| `ScriptRuntime.cs:19` `Type.GetMethod` | IL2075 | 같은 동적 경로 |
| `ScriptDefinition.cs:46` `JsonElement.Deserialize(Type)` | IL2026(AOT는 IL3050도) | 스크립트 필드 값은 기본 형식·`Guid`(기본 변환기)다. KeyRoom 체크포인트·InventoryLab 리플레이가 Release·AOT에서 이 경로를 쓰고 기준값과 같다 |

새로 생긴 경고는 없다. `CrashDiagnostics`의 `StackFrame.GetMethod`(IL2026)는 실행된 메서드만 묻으므로 `UnconditionalSuppressMessage`에 이유를 적었다(trim된 메서드는 `(unknown)`).

### 검사

| 검사 | Release(게임별 publish) | Native AOT |
| --- | --- | --- |
| KeyRoom 월드 체크포인트(`ci.py osx-arm64`) | PASS | PASS |
| InventoryLab 리플레이 저장 `EED41DEF…` | PASS | PASS |
| InventoryLab.Crashes `Scripts/Quests/QuestJournal.cs:35`, 다른 보관본 거부 | PASS | PASS |
| PlatformerLab Joints trace `2d523cd0…` | PASS | PASS |
| AudioLab 첫 네 바퀴 PCM `ced6c800…`(헤드리스) | PASS | PASS |
| KeyRoom·SpriteLab `--smoke-test`와 창 실행(120프레임) | PASS | (T4에서 확인) |
| 금지 파일(12개 산출물) | 없음 | 없음 |
| `./monoon test`(세 RID KeyRoom 게임별 publish 포함, 202 s) | PASS | |
| KeyRoom.EditorBuild(에디터 **Build Game…**와 CLI 결과 파일별 SHA-256, 패키지 포함) | PASS(허용 항목 외 차이 없음, `--preserve-symbol-paths` 뒤) | PASS |

## B4: 크기·시작 시간 회귀 기준 (15.6)

기준값을 고정하기 전에 wip/next1 끝(`e99c626`: B6/T8 VYaml 쓰기 코드 제거, 파티클 P.1–P.3, `--only` 검사 그룹)을 병합했다. 병합 뒤 게임별 Release publish에도 B6의 `MoNoon.ExportedGameOnly` 기능 스위치를 켰다(이전에는 `PublishAot`에서만). 게임별 Release도 내보낸 게임만 실행하므로 loose 프로젝트 경로를 trim할 수 있다. 공용 템플릿(키트가 없을 때의 Release)은 그대로 끈다.

### 바꾼 것

- [BuildSizeChecks.cs](../../Engine.Checks/BuildSizeChecks.cs), [기준값](../build-size/baseline.tsv)·[설명](../build-size/README.md): osx-arm64에서 세 샘플의 배포용 Release·Native AOT를 만들고 `.app` 합계와 실행 파일이 기준보다 1% 넘게 크면 실패한다. 시작 시간(첫 프레임, `session_ready`)은 세 번 중앙값을 출력·기록만 한다. 그 전에 AOT 키트 map에서 VYaml 생성 `Serialize` 메서드를 세어 0이 아니면 실패한다(코디네이터 요청: 참조형 `ProjectYaml.Encode<T>` 한 줄이 2.3 MB를 되살리는 회귀).
- `./monoon test`의 마지막 단계, `./monoon test size [--update]`(`--only build-size`), `./monoon ci`(macOS)에 넣었다. 조사 문서는 +2%였지만 브리프대로 1%로 잡았다. 같은 소스를 다시 빌드하면 Release `.app`이 수백 바이트(영수증·서명) 움직이므로 1%는 충분히 넓다.

### 기준값 (osx-arm64, Distribution, 바이트, 병합 후)

| 샘플 | Release `.app` | Native AOT `.app` | AOT 실행 파일 | 참고: B5 시점 Release / AOT `.app` |
| --- | ---: | ---: | ---: | ---: |
| KeyRoom | 32,676,572 | 15,526,016 | 8,388,224 | 33,168,409 / 17,723,136 |
| PlatformerLab | 29,927,077 | 12,747,551 | 8,072,896 | 30,416,778 / 14,944,055 |
| InventoryLab | 83,321,621 | 66,182,047 | 8,089,296 | 83,859,538 / 68,362,623 |

AOT 차이는 병합한 B6(쓰기 코드 −2.3 MB)와 파티클(+248 KB)이다. Release 차이(−0.5 MB)는 병합과 Release 기능 스위치를 합친 값이다(따로 재지 않았다). 기록한 시작 시간(QEMU 없음)은 KeyRoom Release 131 ms / AOT 38 ms, PlatformerLab 164 / 46 ms, InventoryLab 474 / 405 ms다. 판정에 쓰지 않는다. 같은 소스로 처음 기록했을 때(QEMU 실행 중)는 InventoryLab Release가 583 ms였다. 시작 시간을 판정에 쓰지 않는 이유다.

### B5 회귀 수정: 플레이어 로그 시작 경합

병합 뒤 게이트에서 InventoryLab.Crashes가 한 번 실패했다. Release 플레이어가 종료 코드 1 대신 −6(abort)으로 끝났다. 첫 예외는 스레드 풀의 `TypeInitializationException: The type initializer for 'Started'` → `ArgumentNullException (Parameter 'key')`(`LoggerFactory.CreateLogger` ← `Brutal.Logging.LogSystem.Initialize` ← `GameLog.Started`)였고, 그 앞에 `Log not initialized. Call Log.Initialize before using the logging system.`이 찍혔다. [GameLog](../../../src/Engine.Desktop/GameLog.cs)는 JIT 비용(약 20 ms)을 피하려고 로그 시스템을 스레드 풀에서 시작했는데, 시작 직후 실패하는 빠른 플레이어는 그 전에 BRUTAL 쪽 로그를 거치고, 이후 `Initialize`가 덜 등록된 범주에서 예외를 던졌다(BRUTAL 내부 원인은 소스가 없어 확인하지 못했다). 시작 즉시 실패하는 실행(잘못된 모드·SDL 초기화 실패)을 40회 반복하면 T4 템플릿 Release 0/20, B5 Release 2/20, 병합 후 4/20, 종료 전 대기만 넣은 시도 7/40이 비정상 종료였다. 로그 시스템을 `Attach`에서 바로 시작하도록 바꾼 뒤 0/40이고 `Log not initialized`도 사라졌다. 비용: KeyRoom Release `session_ready` 49.9 → 57.0 ms, 첫 프레임 110 → 120 ms(병합 차이 포함, 5회 교대), InventoryLab Release·AOT는 범위 안(455 → 462 ms, 362 → 357 ms). Native AOT는 1 ms 미만이다.

### 실패 확인

| 변경(임시) | 결과 |
| --- | --- |
| `DesktopHost.Run`에 `ProjectYaml.Encode<MixerLevels>(new())` 추가 후 키트 갱신 | `FAIL Native AOT player has 208 VYaml generated Serialize methods (1,991,524 B)` |
| `baseline.tsv`의 KeyRoom Release를 32,000,000으로 낮춤 | `FAIL build size baseline: KeyRoom release app 32,677,596 B > baseline 32,000,000 B + 1%`(로그 수정 전 측정) |
| 되돌림 | 두 검사 PASS |

### 검사 (병합·로그 수정 후, 최종)

| 검사 | Release(게임별 publish) | Native AOT |
| --- | --- | --- |
| KeyRoom 월드 체크포인트 | PASS | PASS |
| InventoryLab 리플레이 저장 `EED41DEF…` | PASS | PASS |
| InventoryLab.Crashes `QuestJournal.cs:35`(게이트 1회 + 연속 5회) | PASS 6/6 | PASS 6/6 |
| PlatformerLab Joints trace `2d523cd0…` | PASS | PASS |
| AudioLab 첫 네 바퀴 PCM `ced6c800…`(헤드리스) | PASS | PASS |
| 금지 파일(12개 산출물) | 없음 | 없음 |
| `./monoon test size`(VYaml 쓰기 코드 0, 6개 빌드 +1% 이내) | PASS | PASS |
| `./monoon test`(마지막 단계에 크기 기준 포함) | PASS | |

## 확인하지 못한 것

- Windows·Linux 실행: 이 Mac에서 만든 win-x64·linux-x64 게임별 Release(R2R x64 이미지)와 T4로 라이브러리가 빠진 배포본은 파일과 PE 헤더만 확인했다. 실행·리플레이·크래시 해석은 다음 x64 회귀(Linux 게스트, Windows는 장비 준비 후)에서 한다. 로드맵 15.7의 "Linux x64 게스트 Release에서 InventoryLab 리플레이·KeyRoom 체크포인트·Joints가 같을 것"과 15.11의 "InventoryLab 리플레이가 Linux 게스트 Release·Native AOT까지 같으면"은 그 회귀가 끝나야 완료다.
- AudioLab의 F 키 페이드 아웃: 이 세션에 손쉬운 사용(키 이벤트) 권한이 없어 창 입력을 보낼 수 없었다. 첫 네 바퀴 PCM만 비교했다.
- KeyRoom.EditorBuild는 B5에서 실행했다(PASS). 병합 뒤와 로그 수정 뒤에는 다시 실행하지 않았다(빌더 출력 경로는 바뀌지 않았다).
- 실제 콜드 스타트(`sudo purge`)와 창·GPU 렌더링 시간, 기능별 빠진 라이브러리를 부르는 경우의 창 실행 오류 화면은 재지 않았다.
- Release 기능 스위치(`MoNoon.ExportedGameOnly`)만의 크기 효과는 따로 재지 않았다.
