# 2. 배포 빌드(Release·Native AOT) 최적화 조사

2026-11-23 · macOS arm64(Apple Silicon) · .NET SDK 10.0.101 / ILCompiler 10.0.1 · 기준 `4c9b090`(wip/phase1)을 내 워크트리에서 빌드했다. 창·오디오 장치는 열지 않았다(`SDL_VIDEO_DRIVER=monoon-no-video`, `SDL_AUDIO_DRIVER=monoon-no-audio`). 실험용 수정(`NativeAotBuild.cs`에 작업 폴더를 남기는 한 줄, AOT 키트 사본의 Regex 제거)은 측정 뒤 모두 되돌렸다. 커밋은 없다. Windows는 제외했다.

측정 도구는 스크래치패드 `research/t2-tools/`에 있다. `sizes.py`는 항목별 크기, `mapsum.py`는 ILC map 분석, `variant.sh`는 키트를 빌드 옵션별로 publish한다. `measure.py`는 정합성 검사와 시작·메모리 측정, `rtconfig.py`는 runtimeconfig 노브 적용, `closure.py`는 어셈블리 참조 폐포 계산을 맡는다. 원시 결과는 `research/t2-runs/*.tsv`에 있다.

## 1. 현재 상태

### 1.1 출력을 만드는 경로

| 단계 | 내용 | 근거 |
| --- | --- | --- |
| Release 템플릿 | `dotnet publish Engine.Player -c Release -r <rid> --self-contained true`로 만든다. Trim, ReadyToRun 옵션 지정, 피처 스위치가 모두 없다. 프레임워크 BCL 전체(172개 DLL)를 복사한다. | `engine/scripts/build-players.sh:12-14`, `engine/src/Engine.Player/Engine.Player.csproj` |
| Release 내보내기 | 템플릿의 `MoNoonPlayer.files` 목록을 그대로 복사한다. 게임 스크립트 DLL은 `Game/.monoon-compiled/`에 넣고, 실행 중 collectible ALC로 `LoadFromStream`한다. 그래서 템플릿은 Trim할 수 없다. 스크립트가 어떤 BCL API를 쓸지 템플릿을 만들 때 알 수 없기 때문이다. | `GameBuild.cs:351-357,369`, `ScriptRuntime.cs:8`, `ScriptLibrary.cs:25` |
| Native AOT | 게임마다 키트를 `PublishAot=true`로 publish하고 스크립트는 `MoNoonGameReferences`로 정적 연결한다. `OptimizationPreference=Size`는 osx-arm64 Distribution에만 쓴다. | `NativeAotBuild.cs:15-20` |
| 중복 네이티브 | SDL_mixer·SDL_ttf의 버전 사본은 제거한다. macOS에서는 `libSDL3.dylib`을 심볼릭 링크로 바꾼다. **Box2D의 버전 사본 2개는 그대로 배포한다.** | `engine/Directory.Build.targets:19-57`, `GameBuild.cs:364,464` |
| 심볼 | Distribution에서는 PDB와 dSYM을 `Symbols/`로 옮긴다. AOT는 ILC 기본값 `StripSymbols=true`(non-win)를 따른다. | `BuildSymbols.cs:57,96-117`, ILCompiler `Microsoft.NETCore.Native.targets:19` |
| AOT 기본값 | `EventSourceSupport=false`, `DebuggerSupport`가 꺼진 기능 스위치, Generic 본문 접기(generic folding)가 이미 적용돼 있다. `InvariantGlobalization`, `UseSystemResourceKeys`, `StackTraceSupport=false`는 지정하지 않았다. | `Microsoft.NETCore.Native.targets:38,227-229,304` |

### 1.2 크기 (Distribution 프로필, 바이트)

| 산출물 | 합계 | BCL 관리 | 런타임 네이티브 | 게임 네이티브 | 엔진 관리 DLL | 실행 파일 | 콘텐츠 | ZIP(`ditto -c -k`) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| KeyRoom Release `.app` | 99,847,104 (95.2 MiB) | 68,497,040 | 17,982,736 | 6,047,512 | 4,167,496 + M.E.* 663,640 | 124,712 | 2,239,866 | 37,654,621 |
| KeyRoom Native AOT `.app` | 20,736,615 (19.8 MiB) | – | – | 6,047,512 | – | **12,410,368** | 2,207,610 | 8,237,923 |
| PlatformerLab Release | 97,710,426 (93.2 MiB) | 68,497,040 | 17,982,736 | 6,047,512 | 4,831,136 | 124,712 | 103,170 | 36,920,785 |
| PlatformerLab Native AOT | 18,428,737 (17.6 MiB) | – | – | 6,047,512 | – | 12,227,904 | 82,178 | 7,437,334 |
| InventoryLab Release | 151,066,518 (144 MiB) | 68,497,040 | 17,982,736 | 6,047,512 | 4,831,136 | 124,712 | 2.1 MB + 번들 51.3 MB | 88,935,184 |
| InventoryLab Native AOT | 71,768,845 (68.4 MiB) | – | – | 6,047,512 | – | 12,227,792 | 2.1 MB + 번들 51.3 MB | 59,438,149 |

- **Release가 95 MiB인 이유**: BCL 전체가 65.3 MiB이고 그중 CoreLib가 17.1 MB다. 실제로 쓰지 않는 System.Private.Xml(9.0 MB), Linq.Expressions(4.7 MB), Data.Common(3.2 MB), Cryptography(2.5 MB), DataContractSerialization(2.4 MB), Net.Http(2.0 MB)도 함께 들어간다. 런타임 네이티브 17.15 MiB 중 게임 실행에 필요 없는 파일은 6,286,736 B다. 디버거용 `libmscordaccore` 2.41 MB와 `libmscordbi` 1.79 MB, `createdump` 0.21 MB, 대체 GC `libclrgc`·`libclrgcexp` 1.89 MB가 여기에 속한다.
- 게임 네이티브(모든 빌드 공통): libSDL3 2.84 MB, libSDL3_ttf 2.06 MB, libSDL3_mixer 0.34 MB, libbox2d 0.27 MB × **3개**다. `libbox2d.3.dylib`과 `libbox2d.3.1.1.dylib`은 바이트까지 같고, 링크하는 라이브러리가 없다(`otool -L`). 중복이 543,248 B이고 Linux `.so`도 같은 구성이다. 모든 라이브러리는 arm64 단일 아키텍처라 universal 바이너리 문제는 없다.
- KeyRoom 콘텐츠 2.24 MB 중 2.05 MB(92%)가 `NanumGothic-Regular.ttf`다. AOT 앱의 약 10%에 해당한다.
- InventoryLab은 `Market.bundle`(PCM WAV 67 MB를 deflate로 51.3 MB까지 압축)이 크기를 좌우한다. 의도된 대용량 시험 데이터다.

### 1.3 Native AOT 실행 파일 구성 (KeyRoom, `IlcGenerateMapFile` 노드 합 14.16 MB)

- 노드 종류: MethodCode 53.4%, ConstructedEEType 14.2%, DehydratedData 10.2%, GCInfo 4.7%, Metadata 4.1%, StackTraceMethodMapping 206 KB(1.5%).
- 소유 어셈블리(기호 접두사로 추정): CoreLib 26.9%, **Engine.Core 23.4%(3.31 MB)**, System.Text.Json 9.9%(1.41 MB), VYaml 5.3%, Linq 2.5%, Regex 2.4%, Collections 1.6%, IO.Compression 0.9%. Microsoft.Extensions.*는 직접 기호만 약 85 KB다. Brutal.Logging의 실제 비용은 기존 측정값인 +531,344 B다(`engine/tests/results/brutal-logging-jobs/README.md` 크기 표).
- **VYaml 생성 포매터가 2.25 MB를 차지한다.** 그중 Serialize 쪽은 1,898,616 B(코드)와 103,679 B(GCInfo)로 약 2.0 MB이고, Deserialize는 0.23 MB다. 102개 형식에서 형식당 Serialize가 약 18.6 KB로 Deserialize보다 9배 크다. 런타임은 `ProjectYaml.Encode<T>`로 세이브·설정·리플레이를 쓴다. `PrefabMerge.cs:12`와 `Prefabs.cs:90`는 SceneObject를 YAML로 왕복 복제하므로 대부분의 Serialize가 도달 가능하다.
- Regex는 0.34–0.51 MB다. 런타임 사용처는 `ProjectAssets.cs:13`과 `ScriptLibrary.cs:36` 두 줄의 예약 이름 검사뿐이다.
- Trim·AOT 경고는 5건이고 모두 알려진 것이다. 위치는 `ScriptDefinition.cs:46`(`JsonElement.Deserialize(Type)`), `ScriptLibrary.cs:25`(`LoadFromStream`), `ScriptRuntime.cs:18-19`(`GetType`/`GetMethod`)다. SDL·Box2D 상호 운용 경고는 없다.

### 1.4 시작 시간·메모리 (헤드리스, 따뜻한 상태 5회 중앙값)

측정 방법: 1프레임 `.inputreplay`에 `MONOON_REPLAY_SNAPSHOTS=<dir>;0`을 주면 SDL 소프트웨어 렌더러가 0번 프레임을 PNG로 그린다. 프로세스 시작부터 종료까지의 벽시계 시간을 "첫 프레임"으로 쓴다. `main→session_ready`는 `MONOON_STARTUP_TRACE` 값이다. 메모리는 `/usr/bin/time -l`로 쟀다. 정상 상태 메모리는 600프레임을 60 Hz로 맞춰 돌린 오프스크린 재생(`MONOON_REPLAY_OFFSCREEN=1`)의 최대 RSS와 peak footprint다. 진짜 콜드 스타트는 `sudo purge`가 필요해 재지 않았다. 빌드 직후 첫 실행은 275–1,085 ms가 나왔는데, 이는 macOS가 새 실행 파일을 검사하는 시간까지 포함한 값으로 보인다(추정).

| 샘플 | 빌드 | 첫 프레임 ms | main→session ms | 시작 RSS MiB | 600프레임 RSS / footprint MiB |
| --- | --- | ---: | ---: | ---: | ---: |
| KeyRoom | Release | 260 | 146.5 | 95.1 | 92.7 / 58.7 |
| KeyRoom | Native AOT | 28 | 7.2 | 36.7 | 32.6 / 11.6 |
| PlatformerLab | Release | 287 | 154.1 | 99.9 | 103.6 / 68.6 |
| PlatformerLab | Native AOT | 32 | 10.1 | 41.1 | 40.4 / 18.2 |
| InventoryLab(번들 캐시 미스) | Release | 608 | 483.5 | 100.1 | 98.9 / 62.9 |
| InventoryLab(번들 캐시 미스) | Native AOT | 384 | 358.8 | 41.8 | 37.3 / 14.8 |
| InventoryLab(캐시 적중) | Native AOT | – | 61.2 | – | – |

- 프레임당 비용: 렌더링이 빠진 헤드리스 업데이트만 쟀다. KeyRoom은 측정 잡음 수준(40 µs 미만)이었다. PlatformerLab은 Release 144 µs, AOT 39 µs/프레임이다(600프레임 − 1프레임 차분). GPU 렌더 비용은 창 없이 잴 수 없어 범위에서 뺐다. 기존 10.3·10.7 기록을 참고한다.
- InventoryLab에서 가장 큰 시작 비용은 번들 압축 해제(Market 240 ms)였다. 그다음은 **첫 번째 번들(877 B Dialogue)을 읽는 113.7 ms**였다. `InvariantGlobalization`을 켜면 이 값이 0.66 ms로 줄어든다(1.5절). `ZipArchive`가 항목 시각을 로컬 시간대로 바꾸면서 `TimeZoneInfo.Local`을 초기화할 때 ICU 표시 이름을 조회하는 것이 원인으로 보인다. 원인은 확인하지 않았고, 효과만 측정했다.

### 1.5 노브 실험 결과 (워크트리에서 실측, 모든 변형이 정합성 검사 통과)

정합성 기준은 세 가지다. KeyRoom 월드 체크포인트는 `KeyRoom.WorldCheckpoints/ci.py`의 `check_player`로 11개 검사를 했다. InventoryLab은 `Herbalist.inputreplay`의 저장 바이트가 `Herbalist.save`와 같은지 봤다. PlatformerLab은 Joints 재생 trace를 비교했다.

| 변형 | KeyRoom exe(B) | 차이 | 시작(첫 프레임 / main→session) | 결과 |
| --- | ---: | ---: | --- | --- |
| AOT Speed(현재 Development) | 12,576,224 | +1.3% | 28 / 7.2 ms | PASS |
| AOT Size(현재 Distribution, 기준) | 12,410,368 | 0 | 26 / 6.6 ms | PASS |
| + Regex 두 곳을 문자열 비교로 교체 | 11,680,896 | **−729,472 (−5.9%)** | 26 / 6.5 ms | PASS |
| + `InvariantGlobalization`(InventoryLab 기준) | 12,011,584 | −216,208 | InventoryLab 384→**267 ms**, 359→242 ms | PASS |
| + Invariant + `UseSystemResourceKeys` + `IlcFoldIdenticalMethodBodies` | 11,978,192 | −432,176 (−3.5%) | 23 / 3.5 ms | PASS. 예외 메시지가 `IO_PathNotFound_Path, …`로 바뀐다(관찰). |
| + `StackTraceSupport=false` | 11,449,680 | −960,688 (−7.7%) | 22 / 3.5 ms | PASS. 크래시 보고서의 메서드 이름이 `(unknown)`이 된다(`CrashDiagnostics.cs:30-31`). 이미지 오프셋은 남는다. |
| Release runtimeconfig `System.Globalization.Invariant=true` | – | 0 | KeyRoom 260→241 ms, InventoryLab 608→**486 ms** | PASS |
| + `TieredPGO=false`, `GC.Concurrent=false` | – | 0 | 잡음 범위(±10 ms) | PASS |
| Release Trim(`PublishTrimmed`, 정적 스크립트) | BCL 65.3→**4.66 MiB** | 전체 −61 MB | **317 ms**(+57), main→session 201 ms | PASS. 경고 7건(위 5건 + 중복 2건) |
| Release Trim + Invariant·ResourceKeys·EventSource·Debugger·Metrics 끔 | BCL 3.31 MiB | | 303 ms, RSS 63.7, footprint 35.5 MiB | PASS |
| Release 미트림 + 정적 스크립트(ALC 없음) | – | | 242 ms(−18) | PASS |
| 정적 어셈블리 참조 폐포만 남기기(IL 수정 없음) | 202개 중 122개 DLL 유지 | −7.2 MB만 | – | 계산만 함. 프레임워크 내부 참조가 넓어서 효과가 작다. |
| Box2D 버전 사본 2개, DAC·DBI·createdump·대체 GC 제거(Release와 AOT) | – | Release −6.83 MB, AOT −0.54 MB | – | Joints trace SHA `a00b9e4b…`가 Release·AOT·삭제본 4개에서 모두 같다. |
| dylib `strip -x` 후 `codesign -f -s -` 재서명 | – | −617 KB(SDL3 −346 KB, ttf −246 KB, mixer −13 KB, box2d −12 KB) | – | Joints trace가 같다. |

- **Trim한 Release가 더 느린 이유**: ILLink가 다시 쓴 프레임워크 어셈블리에서 ReadyToRun 코드가 사라져 모두 JIT로 컴파일되기 때문이다. 크기와 메모리(footprint −22 MiB)는 크게 줄지만 R2R(crossgen2)과 함께 써야 의미가 있다. crossgen2 패키지(`microsoft.netcore.app.crossgen2.*`)는 NuGet 캐시에 없다. 내려받기가 승인되지 않아 R2R·composite는 측정하지 못했다.
- 추정 결과(합산): Distribution Release를 Trim·Invariant·디버그 네이티브 제거·Box2D 중복 제거로 만들면 약 25.1 MB(24 MiB)이고 R2R을 더하면 약 30–35 MiB다(추정). AOT는 Regex 제거, Invariant, Box2D·strip을 적용하면 KeyRoom 기준 약 18.6 MB(17.7 MiB)이고, VYaml Serialize 축소까지 하면 약 17 MB다(추정).

### 1.6 다른 엔진과 비교 (목표 설정용)

| 엔진 | 빈 프로젝트·최소 2D 데스크톱 빌드 | 출처 |
| --- | --- | --- |
| Godot 4 | 기본 Windows 템플릿 약 93 MB. size·LTO·3D 끄기로 직접 빌드한 템플릿은 6.4 MB, 4.5에서는 5.37 MB(커뮤니티 보고) | https://forum.godotengine.org/t/how-to-minify-godots-build-size-93mb-6-4mb-exe/104965 · https://docs.godotengine.org/en/stable/engine_details/development/compiling/optimizing_for_size.html |
| Unity(구 버전) | macOS 빈 프로젝트 56.4 MB, IL2CPP + High stripping 37.7 MB. Windows 빈 프로젝트 약 66 MB | https://forum.unity.com/threads/mininum-build-size.675427/ · https://issuetracker.unity3d.com/issues/merely-adding-com-dot-unity-dot-rendering-dot-denoising-package-to-an-empty-project-doubles-the-build-size |
| Unity 6 | 2D 템플릿 Web 빌드 6.9 MB(데스크톱 수치 아님) | https://gist.github.com/aras-p/740c2d4f9977ce92b7de72b1394dd365 |
| MoNoon 현재 | AOT 17.6–19.8 MiB(ZIP 7.4–8.2 MB), Release 93–95 MiB(ZIP 36.9–37.7 MB) | 이 문서 1.2절 |

AOT는 이미 Unity 데스크톱의 절반 이하다. Release는 Unity 빈 프로젝트보다 크다. 제안 목표는 AOT 앱 15 MiB 이하·ZIP 6 MB 이하(Godot 맞춤 템플릿 수준), Release Distribution 30 MiB 이하, 첫 프레임 AOT 30 ms·Release 250 ms 이하다(헤드리스 기준).

## 2. 부족한 점 / 문제 (우선순위순)

1. **Release 배포본이 95 MiB이고 footprint가 59 MiB다.** 게임마다 publish하지 않고 Trim하지 않은 공용 템플릿을 복사하는 구조(`GameBuild.cs:351`) 때문이다. Mac에서 Windows·Linux로 배포할 수 있는 형식은 Release뿐이다. Native AOT는 같은 OS에서만 빌드된다(`GameBuild.cs:82-83`). 따라서 x64 배포 크기는 현재 이 수치다.
2. 시작 경로에서 ICU·시간대 초기화가 약 110 ms를 쓴다. 번들이 있는 게임에서 AOT·Release 모두 해당한다. 엔진은 `CultureInfo.InvariantCulture` 말고는 문화권 API를 쓰지 않는다(`grep` 결과 0건). 그런데도 `InvariantGlobalization`을 켜지 않았다.
3. 런타임에 Regex가 남아 AOT 실행 파일이 0.73 MB(5.9%) 커졌다. 사용처는 이름 검사 두 줄이다.
4. Distribution Release에 디버거·덤프·대체 GC 네이티브 6.29 MB가 들어 있다. 모든 빌드에 Box2D 중복 0.54 MB가 들어 있다.
5. VYaml Serialize 생성 코드가 AOT 실행 파일의 약 16%(2.0 MB)를 차지한다.
6. 배포 dylib에 로컬 심볼이 남아 있다(0.62 MB). 정리하려면 재서명이 필요하다.
7. 크기·시작 시간 회귀 기준이 없다. 지금까지 수치는 결과 README에 흩어져 있고, 커져도 실패하는 검사가 없다.
8. `OptimizationPreference=Size`를 osx Distribution에만 쓴다(`NativeAotBuild.cs:15`). Linux에도 적용할 수 있다. 차이는 1.3%로 작다.
9. 남은 큰 비용은 System.Text.Json 1.4 MB 이상, Microsoft.Extensions DI·Logging 531 KB, 폰트 2 MB다. 모두 구조나 정책을 정해야 하는 문제다.

## 3. 제안 작업 목록

| 순서 | 이름 | 내용 | 완료 기준(검증) | 규모 | 의존성 | 용량·성능 영향 | BRUTAL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| B1 | 배포 플레이어 Invariant 전역화 | `Engine.Player.csproj`에 `InvariantGlobalization=true`를 넣는다. Release 템플릿 runtimeconfig와 AOT 모두에 적용된다. 매뉴얼에 스크립트 문자열 비교·서식이 문화권과 무관해진다고 적는다. | `./monoon test`, InventoryLab 리플레이(Release·AOT 저장 바이트 동일), KeyRoom 월드 체크포인트, Samples.GameText의 한글 표시 기준. `MONOON_STARTUP_TRACE`로 InventoryLab 첫 번들 읽기가 1 ms 미만인지 확인 | S | 없음 | AOT −216 KB, footprint −4 MiB. InventoryLab 시작 −117 ms(AOT −30%, Release −20%), KeyRoom −3~−19 ms. 로캘이 달라도 서식이 같아 결정성이 좋아진다. | 해당 없음(.NET 설정) |
| B2 | 런타임 Regex 제거 | `ProjectAssets.ValidName`·`ScriptLibrary.ValidName`의 예약 이름 검사를 `OrdinalIgnoreCase` 비교로 바꾼다. Engine.Checks에 CON·com1·LPT9.txt·CONX 경계 검사를 추가한다. | Engine.Checks 통과, AOT 실행 파일 −0.7 MB 확인, InventoryLab·KeyRoom 리플레이 동일 | S | 없음 | AOT −729 KB(−5.9%). Release는 영향 없음 | 해당 없음 |
| B3 | 중복·비필수 네이티브 제거 | `Directory.Build.targets`에서 `Ahwoo.BRUTAL.Box2D.Native`의 버전 사본(`libbox2d.3*.dylib`, `libbox2d.so.3*`)을 뺀다. Distribution Release에서는 `libmscordaccore`, `libmscordbi`, `createdump`, `libclrgc*`도 뺀다. Development 프로필은 디버깅용으로 그대로 둔다. | PlatformerLab Joints trace가 Release·AOT 모두 기준과 같을 것(이번 실험에서 확인). KeyRoom 금지 파일 검사 갱신. Linux 게스트에서 Box2D 로딩 확인 | S | 없음 | Release −6.83 MB(−7%), AOT −0.54 MB(−2.6%). 각 플랫폼 템플릿 −6 MB 내외 | BRUTAL Box2D를 그대로 사용 |
| B4 | 크기·시작 시간 회귀 기준 | 기존 `--replay-input` + `MONOON_STARTUP_TRACE` + 1프레임 스냅샷으로 샘플 3개의 앱·실행 파일 크기, `session_ready`, footprint를 기록한다. 기준보다 +2% 넘게 커지면 실패시킨다. `engine/tests/results/build-size/`에 기준값을 둔다. | `./monoon test`에 포함하고 macOS에서 통과. 임계를 넘는 변경이면 실패하는지 확인 | S | B1–B3 뒤에 기준값 고정 | 앞으로 생길 용량 증가를 막는 장치 | 해당 없음 |
| B5 | Release를 게임별 Trim + ReadyToRun publish로 전환 | Release 내보내기도 AOT처럼 키트를 게임별로 publish한다(`PublishTrimmed=true`, `PublishReadyToRun=true`, 스크립트 정적 연결). crossgen2는 다른 OS 대상 컴파일을 지원하므로 Mac에서 win-x64·linux-x64도 만들 수 있다. `CrashDiagnostics.cs:29-47`의 `#if MONOON_COMPILED_SCRIPTS` 분기를 `RuntimeFeature.IsDynamicCodeSupported`로 바꿔 JIT 빌드에서 IL 오프셋을 유지한다. `BuildSymbols.cs:43-45`의 `.monoon-compiled` 전제도 고친다. | 3종 RID Release에서 InventoryLab 리플레이·KeyRoom 체크포인트·Joints trace가 기준과 같을 것. 크래시 진단 검사(InventoryLab.Crashes) 통과. 첫 프레임이 현재(260 ms)를 넘지 않을 것 | L | crossgen2 패키지 내려받기 승인. Release 빌드에도 .NET SDK가 필요해짐 | 95 MiB → 약 30–35 MiB(R2R 포함 추정), footprint 약 −22 MiB. 내보내기 시간 +10–15 s/게임(측정: Trim publish 14 s) | 해당 없음 |
| B6 | VYaml Serialize 코드 축소 | 형식당 18.6 KB인 원인을 찾는다(생성 코드에서 키 이름 변환과 emitter 인라인 확인). 플레이어가 쓰지 않는 Serialize 경로를 끊거나, 런타임 쓰기 형식을 몇 개로 모은다. `PrefabMerge`·`Prefabs`의 YAML 왕복 복제를 직접 복사로 바꾸는 방안도 포함한다(할당 감소). | ILC map에서 GeneratedFormatter Serialize 합이 1 MB 미만. 모든 리플레이와 저장 바이트가 같을 것 | M | B4(측정 기준) | AOT −1.0~−1.9 MB(추정). 프리팹 복제 할당 감소(미측정) | VYaml 유지 |
| B7 | 배포 dylib 심볼 정리 | Distribution에서 macOS는 `strip -x` 후 ad-hoc 또는 Developer ID로 재서명하고, Linux는 `strip --strip-unneeded`를 쓴다. 11.12 패키지 서명 단계보다 먼저 실행한다. | Joints trace 동일(이번 실험에서 확인), `codesign --verify --deep --strict` 통과 | S | 11.12 서명 단계 | Mac −617 KB(AOT 앱 −3%) | BRUTAL 네이티브 사용 |
| B8 | AOT Size 프로필을 Linux Distribution에도 적용 | `compactMac` 조건을 linux-x64까지 넓힌다. Windows는 Windows 검증을 재개할 때 다룬다. | Linux 게스트 AOT 리플레이 동일 | S | 없음 | 약 −1.3%(Mac 측정치 기준 추정) | 해당 없음 |
| B9 | 큰 PCM 오디오 경고 | 번들이나 Game에 수 MB 이상의 WAV가 있으면 빌드 보고서에 Ogg 전환 권고를 남긴다(7.8의 94% 감소 근거 인용). | InventoryLab 빌드 로그에 Market.wav 경고가 나올 것 | S | 없음 | InventoryLab을 Ogg로 바꾸면 −약 48 MB(추정) | BRUTAL SDL_mixer(Ogg) |

## 4. 결정이 필요한 것

1. **Release의 역할.** (a) Mac에서 x64로 배포하는 정식 형식으로 쓰고 B5로 줄인다. (b) 개발·테스트용으로 두고 배포는 각 OS의 AOT로만 한다. **권장은 (a)다.** Native AOT는 같은 OS에서만 빌드되므로 Mac 한 대로 3종을 내보내려면 Release가 필요하다.
2. **crossgen2 패키지(`Microsoft.NETCore.App.Crossgen2.osx-arm64` 10.0.1) 내려받기 승인.** Ahwoo.BRUTAL.*가 아니라서 받지 않았다. 없으면 Trim한 Release의 시작이 22% 느려진다. **권장: 승인.** 그 뒤 R2R·composite 크기와 시작 시간을 실측한다.
3. **`InvariantGlobalization` 기본 적용 여부.** 스크립트의 `OrderBy(string)`·`ToString("F1")`이 사용자 로캘을 따르지 않게 된다. **권장: 옵션 없이 항상 켠다.** 엔진은 이미 InvariantCulture만 쓰고, 리플레이 결정성에도 유리하다.
4. **`StackTraceSupport=false`(AOT −528 KB).** 크래시 보고서에 메서드 이름이 없어지지만, 이미지 오프셋과 dSYM으로 `monoon crash explain`은 계속 줄 번호를 해석한다(`CrashSymbols.cs:85`). **권장: 보류.** 먼저 InventoryLab.Crashes 검사로 해석 결과가 같은지 확인한다.
5. **`UseSystemResourceKeys`.** 예외 메시지가 키로 바뀌어 사용자 오류와 보고서를 읽기 어려워진다. **권장: 쓰지 않는다.**
6. **폰트 서브셋.** NanumGothic 2.05 MB가 KeyRoom AOT 앱의 10%다. 서브셋을 만들면 IME 입력이나 콘텐츠 업데이트로 들어온 새 글자가 표시되지 않는다. **권장: 폰트별 선택 옵션으로 두고 기본은 끈다.** BRUTAL에 서브셋 도구가 있는지는 확인하지 못했다.
7. **Brutal.Logging의 DI 비용 531 KB.** 이미 사용자가 유지하기로 결정했다(docs/brutal.md:9). **권장: 유지하되, DI 없는 `LogSystem` 초기화 경로를 BRUTAL에 요청한다.**

## 확인하지 않은 것

- ReadyToRun·composite R2R(패키지 없음), Windows 전부(보류), Linux·Windows의 크기 분해(Mac 템플릿만 빌드)
- 창·GPU 렌더링의 프레임 비용과 진짜 콜드 스타트(`purge` 필요)
- `InvariantGlobalization`이 시작을 줄이는 정확한 원인(TimeZoneInfo·ICU 가설)
- System.Text.Json을 없애는 범위와 효과(L 규모라 제안에서 뺐다)
