# BRUTAL 프레임워크 기능 정리

[BRUTAL](https://github.com/ahwootech/BRUTAL)은 Ahwoo(RocketWerkz)가 공개한 C# 게임 프레임워크다. 네이티브 API를 C#에서 직접 호출하는 인터롭 계층과 메모리·문자열·컬렉션·수학·작업 스케줄러, 그리고 SDL3·Vulkan·Box2D 같은 네이티브 라이브러리 바인딩을 제공한다. 에디터, 씬 그래프, 렌더러는 범위 밖이라 제공하지 않는다. MoNoon은 그 위에 씬·에디터·렌더링을 직접 구현한다.

- 기준: BRUTAL `main` 커밋 `c4c62f5` (2026-11-12). 폴더 하나가 NuGet 패키지 하나(`Ahwoo.BRUTAL.<이름>`)이고 필요한 것만 참조한다.
- 요구 사항: .NET 10. 패키지에 Windows x64, Linux x64, macOS arm64용 네이티브 라이브러리가 들어 있다.
- MoNoon 사용 현황: [Engine.Desktop](../engine/src/Engine.Desktop/Engine.Desktop.csproj)이 `Ahwoo.BRUTAL.Sdl` 2026.10.3을 참조하고 `Brutal.SdlApi`로 창·입력·렌더링·오디오를 처리한다. 2026.10.3부터 SDL3 네이티브 라이브러리(SDL 3.4.12)는 `Ahwoo.BRUTAL.Sdl.Native` 패키지에 들어 있다. Windows에서는 [Directory.Build.targets](../engine/Directory.Build.targets)가 이 패키지의 `SDL3.dll`(VCRUNTIME140 필요)을 공식 SDL 3.4.12 빌드로 교체하고, 교체 대상을 찾지 못하면 win-x64 게시를 실패시킨다. 게임 텍스트는 `Ahwoo.BRUTAL.Sdl.Ttf` 2026.10.3(SDL_ttf 3.2.2, FreeType·HarfBuzz 내장)으로 배포한 TTF·OTF에서 실행 중에 그린다. 빌드 때 굽던 글리프 시트와 StbTrueTypeSharp는 없앴다([게임 텍스트](manual/game-ui.md#공용-번역-테이블과-게임-폰트) · [전환 기록](../engine/tests/results/text-ttf/README.md)). 게임 오디오는 [Engine.Core](../engine/src/Engine.Core/Engine.Core.csproj)가 참조하는 `Ahwoo.BRUTAL.Sdl.Mixer` 2026.10.3(SDL_mixer 3.2.4, WAV·stb_vorbis 디코더 내장)으로 디코딩·리샘플링·루프·팬·합산한다(아래 [오디오](#monoon에서-사용할-때)). [Engine.Core](../engine/src/Engine.Core/Engine.Core.csproj)는 `Ahwoo.BRUTAL.Box2D` 2026.10.2(box2d 3.1.1)로 강체·조인트를 계산하고 충돌 판정에도 Box2D 함수를 쓴다([플랫포머 물리](manual/platformer-physics.md)).
- 패키지별 최소 예제는 BRUTAL 저장소의 [samples](https://github.com/ahwootech/BRUTAL/blob/main/samples/README.md)에 있다.
- 로그: 독립 플레이어는 스크립트 로그(`Session.Log`), 씬 로드 실패, 콘텐츠 업데이트 경고를 [GameLog](../engine/src/Engine.Desktop/GameLog.cs)를 거쳐 `Brutal.Logging`으로 보낸다. `Ahwoo.BRUTAL.Sdl`이 이미 의존하므로 Release 배포 파일은 늘지 않지만, Native AOT 실행 파일은 Microsoft.Extensions.Logging 초기화 코드만큼 약 519 KiB 커진다. 출력이 같아도 BRUTAL 우선 원칙에 따라 유지하기로 사용자가 정했다(2026-11-23). 출력 줄은 이전과 바이트 단위로 같다. CLI의 `PASS`·사용법·리플레이 요약 줄과 크래시 보고서 경로는 콘솔에 직접 쓴다. [로그·작업 스케줄러 검증 기록](../engine/tests/results/brutal-logging-jobs/README.md)

## 기반 라이브러리

게임 런타임에서 할당을 줄이고 네이티브 메모리를 다룰 때 쓴다.

| 패키지 | 기능 | 주요 타입 |
| --- | --- | --- |
| `Brutal.Core` | 기본 타입, 플랫폼 판별, 포인터 헬퍼, 열거형·비트마스크 확장, Roslyn 분석기 | `Platform`, `Ptr<T>`, `Bool8`/`Bool32`, `BitMask`, `EnumMask`, `ByteSize` 확장 |
| `Brutal.Interop` | C#에서 네이티브 API를 호출하는 마샬링 계층 | `IInteropHandle`, `InteropHandleMarshaller`, `InteropStringMarshaller` |
| `Brutal.Memory` | 비관리 메모리 할당·해제, 포인터 소유권 | `Mem`, `PtrOwner<T>` |
| `Brutal.Collections` | 비관리 메모리 기반 무할당 컬렉션, 인덱스 풀 | `NativeList<T>`, `NativeStrideList`, `ScratchSpan`, `FreeListIndexPool` |
| `Brutal.Strings` | UTF-8 문자열과 무할당 문자열 빌더, 경로 | `String8`, `RefString8`, `StringBuilder<T>`, `ValueStringBuilder`, `StringBuilderPool`, `Utf8Path` |
| `Brutal.Numerics` | 벡터·행렬·쿼터니언·평면·사각형·색상, 패킹 | `float2`~`float4`, `int2`, `float3x2`, `float4x4`, `double*`, `Color`, `VectorPack`/`QuaternionPack` |
| `Brutal.Maths` | `Random` 확장: 범위, 도형 안의 점, 임의 방향 | `RandomExtensions` |
| `Brutal.Concurrency` | 작업 시스템과 워커 스레드, 실행 지표 | `JobScheduler`, `IJob`, `JobRunner`, `JobRunnerMetrics` |
| `Brutal.Logging` | 레벨·카테고리별 로그 | `LogSystem`, `LogCategory`, `LogString` |
| `Brutal.Localization` | 이름 있는 변수를 쓰는 번역 문자열, ResX 읽기·쓰기 | `LString`, `LStrings`, `LocalizedStrings`, `ResXReader`/`ResXWriter` |

## 창·입력·2D 출력 (SDL3)

| 패키지 | 기능 | 주요 타입 |
| --- | --- | --- |
| `Brutal.Sdl` | SDL3 전체 바인딩: 창, 입력(키보드·마우스·게임패드), 2D 렌더러와 텍스처, 오디오 장치, 이벤트 | `Sdl`, `SdlException` |
| `Brutal.Sdl.Image` | PNG 등 이미지 로드·저장, 애니메이션 이미지 디코드·인코드 | `SdlImage`, `ImgAnimationDecoder`, `ImgAnimationEncoder` |
| `Brutal.Sdl.Mixer` | 믹서·트랙·그룹, 디코더, 스테레오 게인, 3D 위치 음향 | `SdlMixer`, `MixMixer`, `MixTrack`, `MixGroup`, `MixPoint3D` |
| `Brutal.Sdl.Ttf` | TTF 글꼴 로드, 텍스트를 서피스·텍스처로 렌더, 텍스트 엔진·GPU 아틀라스 | `SdlTtf`, `TtfFont`, `TtfText`, `TtfTextEngine` |
| `Brutal.Glfw` | GLFW 창과 입력. Vulkan·OpenGL과 함께 쓴다 | `Glfw`, `GlfwWindow` |

## GPU 렌더링과 셰이더

| 패키지 | 기능 |
| --- | --- |
| `Brutal.Vulkan`, `Brutal.Vulkan.Abstractions`, `Brutal.Vulkan.Vma` | Vulkan 1.3 바인딩, 장치·버퍼·이미지·디스크립터·동기화·스테이징 헬퍼(`VulkanHelpers`, `StagingPool`, `FrameResources`, `VertexInput`), Vulkan Memory Allocator |
| `Brutal.OpenGl` | OpenGL 바인딩(GLAD 로더) |
| `Brutal.ShaderC`, `Brutal.Slang` | GLSL·Slang을 SPIR-V로 컴파일 |
| `Brutal.Render.Mesh` | 메시 저장 구조와 절차적 메시(구 등) 생성(`Mesh`, `ProcGenMesh`) |

## 텍스처·에셋

| 패키지 | 기능 |
| --- | --- |
| `Brutal.Stb` | stb_image 기반 8·16비트·float 이미지 읽기·쓰기 |
| `Brutal.Texture`, `Brutal.Texture.Abstractions` | 텍스처 형식 기술, PNG·JPEG·KTX2·DDS 로드·저장(`TextureLoader`, `TextureWriter`), CPU 텍스처(`GenericTexture`)와 GPU 업로드 영역 계산 |
| `Brutal.Ktx`, `Brutal.Gli` | KTX2·압축 텍스처, 밉 체인과 DDS |
| `Brutal.Gltf`, `Brutal.MeshOptimizer` | glTF 파싱·저장, 정점 캐시·오버드로·메시렛 최적화 |
| `Brutal.FreeType` | 글꼴 래스터화(네이티브 라이브러리 패키지) |
| `Brutal.ZLib` | 압축(네이티브 라이브러리 패키지) |

## 물리·오디오·네트워크

| 패키지 | 기능 |
| --- | --- |
| `Brutal.Box2D` | Box2D 2D 물리: 바디·도형(원, 캡슐, 다각형, 체인), 조인트(거리, 회전, 프리즘, 모터, 마우스 등), 접촉·센서 이벤트, 레이캐스트·도형 캐스트, 폭발, 디버그 드로우 |
| `Brutal.Jolt` | Jolt 3D 물리 |
| `Brutal.OpenAl` | OpenAL 오디오 |
| `Brutal.Fmod` | FMOD 바인딩. FMOD 네이티브 라이브러리는 포함하지 않으며 별도 라이선스가 필요하다 |
| `Brutal.RakNet` | RakNet 네트워크(서버·클라이언트 연결, 메시지 송수신) |

## 디버그 UI·도구·진단

에디터, 개발 빌드, 진단 도구용이다. 배포 게임에 넣을 때는 아래 [MoNoon에서 사용할 때](#monoon에서-사용할-때)의 경계 규칙을 따른다.

| 패키지 | 기능 |
| --- | --- |
| `Brutal.ImGui`, `Brutal.ImGui.Backends`, `Brutal.ImGui.FreeType`, `Brutal.ImPlot` | Dear ImGui(docking 브랜치)와 백엔드, FreeType 글꼴 아틀라스, ImPlot 그래프 |
| `Brutal.ImGui.Abstractions`, `Brutal.ImGui.Extensions` | `[TerminalAction]` 명령을 등록하는 게임 내 콘솔(`ConsoleWindow`, `TerminalInterface`), C# 열거형에 묶인 위젯(`ImGuiEx`) |
| `Brutal.NativeFileDialog` | OS 파일·폴더 열기·저장 대화상자(Linux는 GTK 3 필요) |
| `Brutal.NotCurses` | 터미널 UI |
| `Brutal.Monitor.Common`, `Brutal.Monitor.Host` | 감시 프로세스, 크래시 직전 로그(`CrashTail`), 덤프 수집, 파일 로깅 |
| `Brutal.RenderDoc`, `Brutal.RenderDoc.ImGui` | RenderDoc 프레임 캡처 API와 ImGui 제어판(Windows·Linux) |

## 유지 결정

BRUTAL에 겹치는 패키지가 있지만 MoNoon이 기존 구현을 유지하는 항목이다. 2026.10.x 패키지의 공개 API와 파일을 직접 확인했다(2026-11-23).

| 영역 | BRUTAL 패키지 | 유지하는 구현 | 이유 |
| --- | --- | --- | --- |
| 게임 이미지 | `Brutal.Sdl.Image` | SDL 3.4 코어의 `SDL_LoadPNG`(`Brutal.Sdl`) | 게임이 읽는 이미지는 PNG뿐이고 SDL 3.4.12 코어가 PNG를 읽는다. SDL_image를 쓰면 관리 어셈블리와 네이티브 라이브러리(macOS `libSDL3_image` 약 246 KiB)가 배포본에 더해진다. |
| 도구 이미지 | `Brutal.Stb`, `Brutal.Texture` | 에디터·빌드 도구의 SkiaSharp | 셰이더 시트는 SkSL 런타임 효과(`SKRuntimeEffect`)로 미리 굽는다. `Brutal.ShaderC`·`Brutal.Slang`은 SPIR-V 컴파일러라 CPU에서 SkSL을 실행하지 못한다. Skia가 남으므로 이미지 입출력만 옮기면 도구의 이미지 라이브러리가 둘이 된다. 게임 배포본에는 둘 다 들어가지 않는다. |
| 현지화 | `Brutal.Localization` | [GameLocalization](../engine/src/Engine.Core/GameLocalization.cs) | `LString`은 키마다 번역 문자열 하나를 공급자 델리게이트로 받고, API에 언어 태그 인자가 없다. MoNoon은 `ko-KR → ko → 기준 언어 → 리터럴 → 키` 순서로 찾고 언어별 대체 글꼴을 고른다. BRUTAL의 이름 있는 변수(`{name}`) 치환은 `Regex`로 처리한다. 이름 있는 변수가 필요해지면 따로 검토한다. |
| 크래시·로그 감시 | `Brutal.Monitor.Host`, `Brutal.Monitor.Common` | 플레이어 안에서 쓰는 `Crashes/*.crash` 보고서 | Monitor는 플랫폼별 감시 실행 파일 `Brutal.Monitor.Subprocess`(약 2.5–2.6 MiB)를 두 번째 프로세스로 띄워 `CrashTail`, 덤프, 파일 로그를 남긴다. MoNoon 보고서는 C# 예외 스택을 YAML로 남기고 `monoon crash explain`으로 해석한다. 네이티브 신호 오류를 잡는 QA용 선택 기능으로 나중에 검토한다. |
| 압축 | `Brutal.ZLib` | .NET `System.IO.Compression` | `Brutal.ZLib.dll`(5 KB)에는 공개 형식이 없고 zlib 네이티브 라이브러리만 들어 있다. 콘텐츠 번들과 배포 패키지는 런타임에 포함된 `ZipArchive`·`DeflateStream`으로 처리한다. |
| 벡터·행렬 | `Brutal.Numerics` | `System.Numerics` | 게임 스크립트 공개 API(`Vector2`, `Matrix3x2`)가 `System.Numerics`다. `float2` 등에는 `System.Numerics` 형식과의 변환이 없어 바꾸면 모든 스크립트가 깨진다. 시뮬레이션 계산에도 쓰지 않는다. 2026.10.2의 `float4x4.Invert`, `floatQuat` 곱, `float4x4.Rows.Transform`, `float2/3/4.MultiplyAddEstimate`는 CPU에 FMA가 있을 때만 곱셈·덧셈을 합쳐 x64 JIT과 Native AOT의 결과가 갈리고, `float2/3/4.FusedMultiplyAdd`(`Single.FusedMultiplyAdd`)는 FMA 없는 x64 AOT에서 소프트웨어로 돈다. 엔진은 곱셈과 덧셈을 따로 하는 `Affine2D` 함수를 쓴다. [x64 결정론 기록](../engine/tests/results/x64-determinism/README.md) |
| 컬렉션·문자열 | `Brutal.Collections`, `Brutal.Strings` | .NET 컬렉션과 `string` | `NativeList<T>`, `StringBuilder<TChar>`, `ValueStringBuilder<TChar>`는 `unmanaged` 제약이 있어 씬 오브젝트 같은 관리 객체를 담지 못한다. `String8`은 UTF-8 문자열이라 스크립트·UI의 `string` API와 맞지 않는다. 네이티브 버퍼가 필요한 경로가 생기면 그때 쓴다. |
| 파일 대화상자 | `Brutal.NativeFileDialog` | Avalonia `StorageProvider`, macOS [ScriptEditorPicker.m](../engine/native/macos/ScriptEditorPicker.m) | 에디터는 이미 Avalonia 대화상자를 쓴다. NFD는 동기 호출이고 별도 네이티브 라이브러리가 필요하며 Linux에서는 GTK 3도 필요하다. 외부 스크립트 에디터(`.app` 번들) 선택만 NFD로 바꿀 수 있는지 검토할 수 있다. |
| YAML | 없음 | VYaml | BRUTAL에는 YAML 패키지가 없다. |
| 문자열 풀 의존성 | `Brutal.Strings`가 `Microsoft.Extensions.ObjectPool` 11.0.0-preview.5를 요구 | 그대로 배포 | 미리보기 패키지지만 Release 배포본에 이미 들어 있고 `Brutal.Logging`의 문자열 풀이 사용한다. 안정판 10.0.x로 고정하면 BRUTAL이 요구하는 버전보다 낮아진다. BRUTAL이 안정판으로 바꾸면 따라간다. |
| 백그라운드 로딩 | `Brutal.Concurrency`(`JobScheduler`) | `SceneResources.Prefetch`의 `Parallel.ForEach`, `SceneStreaming`의 `Task.Run` | `JobScheduler`는 한 번에 한 묶음만 실행하고(실행 중 `Queue`는 `InvalidOperationException`), 완료 알림 없이 `IsExecuting` 확인이나 바쁜 대기 `Wait()`만 있다. `IJob.Run()`은 결과를 돌려주지 않고 예외를 잡지 않아 프로세스가 종료된다(종료 코드 134). 시작 전 작업만 건너뛰는 취소(`AbortJobs`)는 현재 방식과 같지만, 취소 뒤 진행 중인 디코딩이 끝나면 버퍼를 해제하는 비차단 완료, 씬 읽기의 결과·예외 전달, 이전 로딩이 끝나기 전 새 로딩 시작을 지원하지 않는다. Native AOT에서는 동작했다. |

## MoNoon에서 사용할 때

- **BRUTAL 우선.** 엔진 기능에 필요한 것이 위 표에 있으면 다른 NuGet 패키지나 직접 구현보다 BRUTAL 패키지를 먼저 쓴다. BRUTAL로 부족할 때만 다른 방법을 쓰고 그 이유를 남긴다.
- **런타임 경계.** 게임 런타임에는 실행에 필요한 패키지만 참조한다. ImGui, NativeFileDialog, RenderDoc, NotCurses, ShaderC·Slang 같은 편집·빌드·진단용 패키지는 에디터나 빌드 도구에서만 참조하고 Release·Native AOT 배포본에 넣지 않는다.
- **배포 렌더링.** 배포 게임은 `Brutal.Sdl`의 SDL3 렌더러로 그린다. SkiaSharp는 에디터와 빌드 도구에서만 쓴다([AGENTS.md](../AGENTS.md#배포-게임-렌더링)).
- **네이티브 라이브러리.** 패키지를 추가하면 세 플랫폼의 Release·Native AOT 출력에 해당 네이티브 라이브러리가 복사되는지 확인한다. Windows `SDL3.dll`처럼 교체가 필요한 경우는 [Directory.Build.targets](../engine/Directory.Build.targets)에 둔다. BRUTAL의 Windows `SDL3_mixer.dll`·`SDL3_ttf.dll`(2026.10.3)도 VCRUNTIME140을, `SDL3_ttf.dll`은 VCRUNTIME140_1도 가져온다. `SDL3_mixer.dll`은 오디오 전환부터 BRUTAL 빌드 그대로 win-x64 배포본에 들어가며, `SDL3_ttf.dll`도 BRUTAL 빌드를 그대로 배포한다. VC 런타임이 없는 Windows에서의 동작은 Windows 장비 검증 때 확인한다. macOS·Linux에서는 SDL_mixer가 읽는 `libSDL3_mixer.dylib`·`libSDL3_mixer.so`만 넣고, 바이트가 같은 버전 이름 사본은 [Directory.Build.targets](../engine/Directory.Build.targets)에서 뺀다. `Ahwoo.BRUTAL.Sdl.Ttf.Native`도 같은 파일이 세 벌(버전 없는 이름, `.0`, `.0.2.2`) 들어 있어 P/Invoke가 읽는 이름 하나만 배포한다(약 4 MB 절약). [SDL 2026.10.3 전환 기록](../engine/tests/results/brutal-sdl-upgrade/README.md)
- **오디오.** [AudioMixer](../engine/src/Engine.Core/AudioMixer.cs)는 메모리 믹서(`MIX_CreateMixer`, 48 kHz 스테레오 float32)에 소스마다 트랙 하나를 만들고 파일을 트랙별 `MIX_SetTrackIOStream`으로 스트리밍한다. 에디터 Scene Play·Release·Native AOT·오프라인 검사가 모두 512프레임 블록의 `MIX_Generate`로 같은 PCM을 만들고, SDL 출력 큐로 보내는 [SdlSounds](../engine/src/Engine.Desktop/SdlSounds.cs)와 리듬 판정용 출력 시계(`AudioOutputClock`, macOS 지연 보정)는 그대로다. `MIX_CreateMixerDevice`는 쓰지 않는다. 장치가 직접 당겨 가면 출력 캡처·장치 큐 관찰 시점이 바뀌어 기존 리듬 보정과 검증 기준을 다시 맞춰야 하기 때문이다. SDL_mixer와 동작이 다른 부분만 MoNoon이 맡는다: 출력 프레임 단위의 정확한 선형 페이드와 공간 음원의 모노 합치기(트랙 cooked 콜백 — SDL_mixer의 페이드는 리샘플링 전 원본 프레임에 걸려 정지 시점이 블록 크기에 따라 달라진다), 240프레임 버스 램프(그룹 post-mix 콜백), 등전력 팬과 선형 거리 감쇠(`MIX_SetTrackStereo` 게인), 96개 보이스 제한과 교체 순서, 출력 ±1 제한. 48 kHz가 아닌 새 음원이 있는 블록은 1프레임을 먼저 요청한다. SDL_mixer는 리샘플링하는 트랙의 첫 요청을 리샘플러 지연(24 kHz에서 14프레임)만큼 덜 채워 그 끝에 빈 구간을 남기는데, 첫 요청을 1프레임으로 하면 빈 구간 대신 1프레임 시작 지연만 남는다. 이미 재생 중인 트랙은 블록을 나눠도 같은 샘플을 내므로 48 kHz 음원의 PCM은 바뀌지 않는다. [SDL_mixer 전환 기록](../engine/tests/results/audio-mixer/README.md)
- **라이선스.** BRUTAL로 만든 게임은 크레디트에 BRUTAL 저작권 고지, 게임 시작 전 BRUTAL 로고, `NOTICE.txt` 동봉이 필요하다. 패키지별 제3자 고지는 각 패키지의 `LICENSES` 폴더에 있다. [BRUTAL 라이선스](https://github.com/ahwootech/BRUTAL/blob/main/LICENSE.txt) · [빌드와 배포](manual/build.md)
