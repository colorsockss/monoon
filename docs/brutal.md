# BRUTAL 프레임워크 기능 정리

[BRUTAL](https://github.com/ahwootech/BRUTAL)은 Ahwoo(RocketWerkz)가 공개한 C# 게임 프레임워크다. 네이티브 API를 C#에서 직접 호출하는 인터롭 계층과 메모리·문자열·컬렉션·수학·작업 스케줄러, 그리고 SDL3·Vulkan·Box2D 같은 네이티브 라이브러리 바인딩을 제공한다. 에디터, 씬 그래프, 렌더러는 범위 밖이라 제공하지 않는다. MoNoon은 그 위에 씬·에디터·렌더링을 직접 구현한다.

- 기준: BRUTAL `main` 커밋 `c4c62f5` (2026-11-12). 폴더 하나가 NuGet 패키지 하나(`Ahwoo.BRUTAL.<이름>`)이고 필요한 것만 참조한다.
- 요구 사항: .NET 10. 패키지에 Windows x64, Linux x64, macOS arm64용 네이티브 라이브러리가 들어 있다.
- MoNoon 사용 현황: [Engine.Desktop](../engine/src/Engine.Desktop/Engine.Desktop.csproj)이 `Ahwoo.BRUTAL.Sdl` 2026.10.2를 참조하고 `Brutal.SdlApi`로 창·입력·렌더링·오디오를 처리한다. Windows에서는 [Directory.Build.targets](../engine/Directory.Build.targets)가 패키지의 `SDL3.dll`을 교체한다.
- 패키지별 최소 예제는 BRUTAL 저장소의 [samples](https://github.com/ahwootech/BRUTAL/blob/main/samples/README.md)에 있다.

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

## MoNoon에서 사용할 때

- **BRUTAL 우선.** 엔진 기능에 필요한 것이 위 표에 있으면 다른 NuGet 패키지나 직접 구현보다 BRUTAL 패키지를 먼저 쓴다. BRUTAL로 부족할 때만 다른 방법을 쓰고 그 이유를 남긴다.
- **런타임 경계.** 게임 런타임에는 실행에 필요한 패키지만 참조한다. ImGui, NativeFileDialog, RenderDoc, NotCurses, ShaderC·Slang 같은 편집·빌드·진단용 패키지는 에디터나 빌드 도구에서만 참조하고 Release·Native AOT 배포본에 넣지 않는다.
- **배포 렌더링.** 배포 게임은 `Brutal.Sdl`의 SDL3 렌더러로 그린다. SkiaSharp는 에디터와 빌드 도구에서만 쓴다([AGENTS.md](../AGENTS.md#배포-게임-렌더링)).
- **네이티브 라이브러리.** 패키지를 추가하면 세 플랫폼의 Release·Native AOT 출력에 해당 네이티브 라이브러리가 복사되는지 확인한다. Windows `SDL3.dll`처럼 교체가 필요한 경우는 [Directory.Build.targets](../engine/Directory.Build.targets)에 둔다.
- **라이선스.** BRUTAL로 만든 게임은 크레디트에 BRUTAL 저작권 고지, 게임 시작 전 BRUTAL 로고, `NOTICE.txt` 동봉이 필요하다. 패키지별 제3자 고지는 각 패키지의 `LICENSES` 폴더에 있다. [BRUTAL 라이선스](https://github.com/ahwootech/BRUTAL/blob/main/LICENSE.txt) · [빌드와 배포](manual/build.md)
