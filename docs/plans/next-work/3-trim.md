# 3. 코드·에셋 트리밍 조사

기준: `wip/phase1` 4c9b090(내 워크트리에서 동일 커밋), macOS arm64. 템플릿은 `.worktrees/phase1/player-templates`(osx-arm64, native-aot)를 스크래치로 복사해 `MONOON_PLAYERS_DIR`로 지정. 창을 여는 실행은 하지 않았고 헤드리스 `--smoke-test`만 돌렸다. 소스 변경 없음(워크트리 clean).
실험 산출물: `tools/trim/ (실험 산출물 out/·aotmap/은 보존하지 않음, 도구만 이 폴더에 복사) ` (`out/`, `tools/mapsum.py`, `tools/features.py`, `tools/subset.py`, `aotmap/`).

## 1. 현재 상태

### 1.1 에셋: 씬 도달성 기반 수집이 이미 있다

- `GameBuild.ExportCore`는 `settings.IncludedScenes`만 읽고(GameBuild.cs:263-279) 씬 → 오브젝트 → 컴포넌트를 따라 `dependencies`(경로 집합)를 채운다(GameBuild.cs:137-262). 따라가는 참조: SpriteRenderer PNG/마스크(154-160), 프리팹 재귀(120-136, 161), 컷신 시퀀스(162-166), 리그(167-171), 셰이더(172-173), 타일셋+팔레트 이미지(107-118), 오디오(177-183), UI 테마·이미지·텍스트 아이콘(184-197), 애니메이터 컨트롤러·클립·프레임(198-213), 스크립트 에셋 필드 PNG/프리팹/씬/테이블/데이터(225-247). 테마·번역 테이블·글꼴·`.license`는 GameSettings에서 추가(101-102, GameLocalization.cs:60-78).
- 복사는 `dependencies`만(GameBuild.cs:383-400), 매니페스트도 `dependencies`에 든 항목만(402-411). 번들은 이 매니페스트로만 묶는다(412-413, build.md:232 "내보낼 에셋이 없는 폴더를 지정하면 빌드가 실패").
- 스크립트가 경로·이름으로 에셋을 읽는 API는 없다. 참조는 `SpriteReference`/`PrefabReference`/`SceneReference`/`DataReference`/`TableReference`(scripting.md:88)뿐이고 Inspector에 저장된 값을 빌더가 따라간다(ScriptDefinition.cs:19-31). 런타임 `AddComponent`류도 없다. → 닫힌 세계(closed world)라 도달성 분석이 정확하다. Resources 폴더 같은 "항상 포함" 개념은 없다.
- 스크립트: `Tests/`, `Editor/`는 게임 어셈블리에서 제외(ProjectScripts.cs:34-37, 90). 루트 `Program.cs`·`.csproj`는 수집 대상 아님.

**실측 (KeyRoom 복사본 + 실험 파일, Release)**

| 넣은 것 | 결과 |
| --- | --- |
| 참조 없는 `Assets/Unused/BigUnused.png` 4,196,671 B, `BigUnused.wav` 5,292,044 B | 미포함 |
| 빌드에서 뺀 `WallWorkshop.scene`만 참조하는 `WallSegment.prefab`, `PrefabWorkshop.scene`만 참조하는 `Chest.prefab`, 두 씬 자체 | 미포함 |
| `.meta`, `Tests/*.cs`, `Tests/TakeKey.inputreplay`, `Editor/KeyRoomTools.cs`, `KeyRoom.csproj`, `Program.cs` | 미포함 |
| 어느 씬에도 붙지 않은 `Scripts/UnusedProbe.cs` | **포함** (`MoNoon.GameScripts.dll` 32,256 B 안에 `UnusedProbe` 존재) |
| 결과 `Game/` 2.2 MB 중 `NanumGothic-Regular.ttf` 2,054,744 B (93%) | 대체 글꼴 전체가 그대로 들어감 |

SpriteLab(Release): `ArrowCycle.aseprite`·원본 `Arrow.png`·`Dot.png` 미포함, 패킹된 스프라이트는 아틀라스 이미지(`.monoon-atlas/<hash>.png`)로 대체. 단 씬이 쓰지 않는 `Dot`(6a3bc207)이 들어 있는 **아틀라스 이미지 전체가 포함**(GameBuild.cs:149-153 `Add(packed.Atlas); Add(packed.Sheet)`). 샘플은 171 B라 영향 없음, 큰 아틀라스에서는 미사용 영역만큼 낭비.

기타 확인:
- 동일 내용 다른 경로의 에셋은 경로별로 따로 복사된다(`dependencies`는 경로 `HashSet`, GameBuild.cs:99, 399). 해시 중복 제거 없음.
- 오디오 변환 없음: InventoryLab `Assets/Market/MarketMorning.wav` 67,108,908 B가 그대로 배포(의도된 대형 번들 시험용이지만 경고도 없다). Ogg는 7.8에서 WAV 대비 94.0% 감소 확인됨.
- 빌드 결과 보고는 생성물 캐시 결정(`build`/`reuse` + 이유)만 있다(ContentBuildCache.cs:20-21, 82; Cli Program.cs:81-82). "어떤 에셋이 왜, 몇 바이트로 들어갔는지 / 무엇이 미참조인지" 보고는 없다. KeyRoom의 `report.yaml`은 `{}`.
- 회귀 검사: Engine.Checks에 "미참조 에셋 미포함"을 직접 단언하는 검사는 없다. 있는 것은 Avalonia DLL·`Game/Scripts` 부재(Program.cs:283), 번들 에셋이 loose로 남지 않음(ContentBundleChecks.cs:19), 패킹 원본 PNG 부재(SpriteChecks.cs:83) 정도.

### 1.2 코드: 엔진 기능은 게임과 무관하게 전부 들어간다

**스크립트 바인딩.** Roslyn으로 프로젝트 스크립트 전체를 한 어셈블리로 컴파일하고, 모든 `GameBehaviour`에 대해 `switch(component.Script) { case "...": new T() ... }` 팩토리를 생성한다(ProjectScripts.cs:201, 229). Release는 이 DLL을 로드해 `GetType(FactoryName)`·`GetMethod("Create")` 리플렉션 한 번으로 바인딩(ScriptRuntime.cs:18-19), Native AOT는 같은 소스를 `extern alias`로 정적 링크해 `CompiledGameScripts.Create` 직접 호출(NativeAotBuild.cs:71-103, Program.cs:57-58) → AOT에 리플렉션 루트 없음. kit를 `--no-incremental`로 publish했을 때 **IL2xxx/IL3xxx 경고 0건**. 대신 팩토리가 모든 스크립트를 루트로 잡아 미사용 스크립트도 남는다(규모는 KB 단위).

**Release.** 템플릿은 트리밍하지 않은 self-contained 런타임(osx-arm64 97 MB, `Microsoft.VisualBasic.dll` 등 DLL 202개). 리포 어디에도 `PublishTrimmed`/`IsTrimmable`/`IsAotCompatible` 설정이 없다(engine/**/*.csproj·props·targets 검색). 게임 스크립트 DLL은 내보내기 때 컴파일되므로 템플릿을 미리 트리밍하면 스크립트가 쓰는 BCL API가 잘릴 수 있다 → Release 트리밍은 내보내기 시점 ILLink가 필요(일반 용량 담당 에이전트 영역, 여기서는 제약만 기록).

**Native AOT 실측 (osx-arm64, Distribution = `OptimizationPreference=Size`)**

| 빌드 | `MoNoonPlayer` | .app | 빌드 시간(warm) |
| --- | --- | --- | --- |
| kit만 publish(스크립트 없음) | 11,697,392 B | – | 13 s |
| SpriteLab (카메라·스프라이트·애니메이터만, 스크립트·오디오·텍스트·물리 없음) | **11,697,392 B** (위와 바이트 수 동일) | 17 MB | 18.8 s |
| KeyRoom (스크립트 26개, UI·오디오·충돌·타일 등) | 12,410,368 B | 20 MB | 19.7 s |

→ 최소 게임과 KeyRoom의 차이는 0.71 MB(대부분 스크립트)뿐이고, 엔진 11.7 MB는 기능 사용과 무관하게 고정이다.

**왜 안 잘리나 (ILC map, `IlcGenerateMapFile`/`IlcGenerateMstatFile`)** — MethodCode 7.11 MB, EEType 1.83 MB, 메타데이터 0.56 MB.

| 항목 | 메서드 코드 |
| --- | --- |
| Engine.Core 전체 | 2,928,144 B |
| └ VYaml 생성 포매터(Engine.Core 형식 499개 메서드) | **2,112,508 B** |
| 　└ 그중 직렬화 쪽(`SerializeMembers` 1,533,824 + `Serialize` 364,792) | **1,898,616 B** |
| └ STJ 소스 생성 `GameJson`(`PropInit` 155,480 포함) | 218,840 B |
| System.Text.Json | 400,908 B |
| System.Text.RegularExpressions | **245,460 B** |
| VYaml 라이브러리 | 592,720 B |
| System.Private.CoreLib | 1,904,212 B |

- 컴포넌트 하나당 약 24 KB. 예: `AudioSourceComponent` 68개 메서드, `SerializeMembers`만 16,712 B. `SceneComponent`의 `[YamlObjectUnion]` 40개(SceneComponent.cs:7-) + `[JsonDerivedType]`이 모든 컴포넌트 형식을 정적으로 참조하므로 씬에 없는 컴포넌트도 전부 루트가 된다.
- 직렬화 쪽 1.9 MB가 남는 이유(가설, 미확인): 플레이어도 `ProjectYaml.Encode<T>`를 GameSettings·저장·리플레이·크래시 보고 등에 쓰고(GameSettings.cs:43, PlayerCrashReport.cs:29, InputReplay.cs:125, GameData.cs:53 등) 참조형 제네릭이 `__Canon` 공유 코드라, 생성된 모든 포매터의 `IYamlFormatter<T>.Serialize` 구현이 유지되는 것으로 보인다. 플레이어가 `SceneDocument`를 쓰는 경로(SceneDocument.cs:132, Prefabs.cs:70, PrefabProperties.cs:108)는 에디터용이다.
- Regex 245 KB는 윈도 예약 이름 검사 두 곳 때문(ProjectAssets.cs:13, ScriptLibrary.cs:36-37).
- `GameJson`에는 레거시 JSON 변환용 `SceneDocument`/`GameSettings`/`SpriteClip`/`AssetEntry[]`가 들어 있고(GameJson.cs:8-11), 사용처는 Engine.Authoring `LegacyProject`뿐이다(LegacyProject.cs:21, 47, 103, 141). 그런데 `GameJson.Default.GetTypeInfo(typeof(T))`(GameBehaviour.cs:77)가 컨텍스트 전체를 루트로 잡아 플레이어에 남는다.

기능별 엔진 코드(형식 이름 패턴으로 분류, Engine.*·Brutal.* 메서드 코드 3,063,580 B 중; `tools/features.py`):

| 기능 | KiB | | 기능 | KiB |
| --- | --- | --- | --- | --- |
| UI·텍스트 | 408 | | 스켈레톤 | 102 |
| 물리·충돌 | 302 | | 리플레이·크래시·프로파일 | 101 |
| 타일맵 | 154 | | 컷신·경로·트윈 | 81 |
| 효과(입자·광원·선·셰이더) | 154 | | 오디오 | 62 |
| 내비게이션 | 49 | | 기타 공통 | 1,557 |

→ SpriteLab 같은 최소 게임이 쓰지 않는 기능 코드는 메서드 코드만 약 1.3 MB(형식 데이터 포함 추정 1.5 MB 안팎, 실행 파일의 ~13%).

**네이티브 라이브러리.** 게임 내용과 무관하게 항상 배포된다.

| 파일 (macOS) | 크기 | 필요 조건 |
| --- | --- | --- |
| libSDL3.0.dylib (+ libSDL3.dylib 심볼릭 링크, GameBuild.cs:464-477) | 2,835,280 | 항상 |
| libSDL3_ttf.dylib | 2,056,184 | 텍스트를 그릴 때. 초기화는 지연(TtfTextRasterizer.cs:52-56) |
| libSDL3_mixer.dylib | 341,176 | 소리를 재생할 때. `AudioMixer.Open()`/`SdlSounds.Open()` 지연(AudioMixer.cs:132-138, SdlSounds.cs:29-38) |
| libbox2d.dylib / .3.dylib / .3.1.1.dylib | 271,624 × **3** (SHA-1 동일) | 충돌체·강체·조인트. 충돌 판정도 Box2D 사용(CollisionGeometry.cs:27-29, 128) |

- mixer·ttf의 버전명 사본은 이미 빼고 있지만(Directory.Build.targets `DropVersionedMixerCopies`, `DropVersionedSdlTtf*`) **box2d 사본 3개는 그대로**다(macOS 0.54 MB 낭비). phase1 Linux 템플릿은 `libSDL3.so`/`.so.0`/`.so.0.4.12` 4,211,016 B × 3(**8.4 MB 낭비**), `libbox2d.so` 376,200 B × 3(0.75 MB 낭비). `ShareIdenticalSdl`은 macOS 전용(GameBuild.cs:363-364). Windows는 단일 파일.
- **제거 실험**: SpriteLab Native AOT와 Release에서 `libSDL3_ttf`·`libSDL3_mixer`·`libbox2d*`를 지우고 `--smoke-test` → 둘 다 `PASS`(Release는 `Brutal.Sdl.Ttf.dll`까지 지워도 PASS). AOT .app 17 MB → 14 MB(−3.5 MB, −20%). 렌더링 경로는 창을 열 수 없어 확인하지 못했다(텍스트·소리·충돌을 쓰지 않는 장면이라 해당 라이브러리는 호출되지 않는다).

**글꼴 부분 집합 실측** (에디터가 이미 갖고 있는 `libHarfBuzzSharp.dylib` 8.3.1.1의 `hb_subset_or_fail`, 새 의존성 없음 — `tools/subset.py`):

| 글꼴 | 원본 | 부분 집합 |
| --- | --- | --- |
| NanumGothic (KeyRoom 테이블·씬·스크립트 문자 + ASCII = 225자) | 2,054,744 B | **83,724 B (−95.9%)** |
| GowunDodum (이미 부분 글꼴) | 21,124 B | 20,016 B |
| NanumGothic (한글 음절 11,172자 전부) | 2,054,744 B | 1,906,616 B (−7%) |

## 2. 부족한 점 / 문제 (영향 순)

1. **중복 네이티브 라이브러리**: Linux Release `libSDL3.so` 3벌 8.4 MB + box2d 3벌(Linux 0.75 MB, macOS 0.54 MB, AOT 포함). 기능과 무관한 순수 낭비.
2. **엔진 기능별 제외 없음**: 텍스트·소리·물리를 안 쓰는 게임도 SDL_ttf·SDL_mixer·Box2D(macOS 3.2–3.5 MB, Linux 약 3.1 MB)와 해당 관리 코드(AOT ~1.3–1.5 MB)를 싣는다. 최소 AOT 게임의 약 20–30%.
3. **글꼴 전체 배포**: 텍스트가 있는 게임은 CJK 대체 글꼴 하나로 2 MB. KeyRoom 콘텐츠의 93%.
4. **AOT 플레이어의 죽은 직렬화 코드**: VYaml 직렬화 쪽 1.9 MB(16%, 원인 가설 미확인), Regex 245 KB, 레거시 JSON 메타데이터 ~0.2 MB, STJ+VYaml 이중 직렬화기(STJ 401 KB).
5. **포함 이유·크기 보고 없음 / 미참조 경고 없음**: 제작자가 무엇이 용량을 차지하는지 모른다(예: 64 MB WAV, 2 MB 글꼴). 미참조 에셋·미사용 스크립트·부분 사용 아틀라스 경고 없음.
6. **아틀라스 부분 사용**: 쓰는 스프라이트가 하나여도 아틀라스 전체 PNG가 들어간다.
7. **회귀 검사 공백**: 도달성 제외(미참조·제외 씬 전용·`.meta`·`Tests/`·`Editor/`·`.aseprite`)를 단언하는 검사가 없다.
8. 동일 내용 중복 에셋, 미사용 스크립트(KB 단위) — 보고만 하면 충분.

## 3. 제안 작업 목록 (우선순위 순, 로드맵 행 형식)

| # | 이름 | 내용 | 완료 기준(검증) | 규모 | 의존성 | 용량·성능 영향 | BRUTAL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T1 | 동일 네이티브 라이브러리 한 벌만 배포 | Directory.Build.targets에 box2d 버전명 사본 제거(P/Invoke 이름 `box2d` → `libbox2d.dylib/.so`, `box2d.dll`만 유지). Linux `libSDL3.so.0.4.12` 제거, `libSDL3.so`는 `libSDL3.so.0`(ttf·mixer의 DT_NEEDED) 심볼릭 링크로(macOS `ShareIdenticalSdl`을 Linux에 확장, tar.gz가 링크를 보존하는지 `DistributionPackage` 확인). | 세 플랫폼 템플릿·kit 재생성 후 KeyRoom·InventoryLab Release/AOT `--smoke-test` PASS, `monoon test replay` 저장 일치, Linux 게스트 Release 실행(시작 오류 시 SDL3부터 확인). 배포 폴더에 같은 SHA 파일 0개를 Engine.Checks에서 단언. | S | 없음 | Linux −9.2 MB, macOS −0.54 MB(Release·AOT 각각). 시작 시간 영향 없음 | BRUTAL 패키지 파일 정리만 |
| T2 | 에셋 빌드 보고서와 미참조 경고 | `dependencies`를 `Dictionary<string, IncludeReason>`(첫 경로: 씬 → 오브젝트 → 컴포넌트/필드 또는 GameSettings 항목)으로 바꾸고, 내보낸 뒤 출력 폴더(제품 밖, `.monoon-build-cache/.../assets.yaml`)에 경로·배포 바이트·이유·번들을 기록. CLI·Console에 크기 상위 10개와 경고: 미참조 에셋(Assets 아래 매니페스트 미포함), 미사용 스크립트(팩토리에 있으나 어느 씬·프리팹에도 없음), 아틀라스 사용률, 1 MiB 초과 WAV(Ogg 권장), 동일 해시 중복, 1 MiB 초과 글꼴. | KeyRoom에 미참조 PNG·WAV·제외 씬 전용 프리팹·미사용 스크립트를 넣은 픽스처로 Engine.Checks가 (a) 제품에 없음 (b) 보고서에 경고 (c) 포함 항목의 이유 체인 확인. 에디터 Console 표시는 Mac에서 Build 1회. | M | 없음 | 배포본 0 B. 빌드 시간 +수 ms(이미 해시 계산함) | 해당 없음 |
| T3 | 도달성 제외 회귀 검사 | T2 픽스처에 `.meta`, `Tests/`, `Editor/`, `.aseprite`, 부분 사용 아틀라스(원본 PNG 부재)까지 단언. Release·AOT 둘 다. | `./monoon test` 통과, 고의로 `dependencies.Add`를 넓히면 실패 | S | T2와 함께 | 0 | 해당 없음 |
| T4 | 미사용 엔진 기능의 네이티브 라이브러리 제외 | 빌더가 기능 집합 계산: **text** = UiText/UiControl 라벨/드롭다운/테마 글꼴/번역 테이블 또는 스크립트의 텍스트 API 사용, **audio** = `audioAssets.Count>0` 또는 오디오 API 사용, **physics** = 충돌체·강체·조인트·CharacterMotor·타일 충돌 또는 물리 질의 API 사용. 스크립트 사용은 Engine.Core 공개 API에 `[EngineFeature(Feature.X)]`를 달고 ProjectScripts의 Roslyn 컴파일에서 IOperation으로 호출 대상을 검사(이미 `Microsoft.CodeAnalysis.Operations` 사용). Release는 `MoNoonPlayer.files` 복사에서, AOT는 publish 출력에서 해당 dylib/so/dll과 라이선스를 제외. 기능 집합을 `MoNoonBuild` 영수증에 기록하고, 런타임 진입점(TtfTextRasterizer.Font, AudioFile.Init, Box2D 첫 사용)에서 `DllNotFoundException`을 "이 빌드는 X를 제외했다" 오류로 바꾼다. `GameSettings.features`로 강제 포함 가능. | SpriteLab AOT·Release에서 세 라이브러리 부재 + `--smoke-test` PASS + 화면 캡처 비교(코디네이터 GUI 회귀), KeyRoom·AudioLab·PlatformerLab은 각각 필요한 라이브러리 유지 확인, InventoryLab replay 저장 일치. 스크립트에서만 소리를 내는 픽스처가 audio로 판정되는지 Engine.Checks. | M | T2(보고서에 기능 판정 이유 표시) | 최소 게임 macOS −3.2 MB(AOT .app 17→14 MB 실측), Linux 약 −3.1 MB. 시작 시 dlopen 감소 | BRUTAL 바인딩 유지, 배포 파일만 선택 |
| T5 | 빌드 시 글꼴 부분 집합 | 번역 테이블 전 언어, 씬·프리팹 UiText, 테마, 스크립트 문자열 리터럴(Roslyn) + 항상 포함(ASCII 0x20-0x7E, 숫자·기호) + `LocalizationSettings.extraCharacters`(범위 지정, 예: 이름 입력용 KS X 1001 2,350자)를 모아 `hb_subset_or_fail`로 글꼴별 부분 집합 생성. Engine.Authoring에서 libHarfBuzzSharp를 P/Invoke(에디터에 이미 있음, 런타임 의존성 0). ContentBuildCache로 재사용, 글꼴별 "전체 유지" 옵션, `.license` 유지(OFL은 부분 집합 허용, 이름 변경 조항은 확인 필요). | KeyRoom Release·AOT에서 언어 전환 후 가방·의뢰 문구의 '휴'·'황'이 대체 글꼴로 그려지는지 캡처 비교(LocalizationChecks의 글리프 픽셀 검사 확장), 포함 안 된 글자는 보고서 경고. Windows·Linux 게스트 HUD 텍스트. | M | T2 | KeyRoom `Game/` 2.2 MB → 약 0.25 MB(NanumGothic −1.97 MB 실측). TTF_OpenFont 파일 읽기도 감소 | SDL_ttf(BRUTAL) 그대로 사용, 부분 집합은 빌드 도구(HarfBuzz)에서 |
| T6 | AOT 플레이어 Regex 제거 | ProjectAssets.cs:13, ScriptLibrary.cs:36-37의 예약 이름 검사를 문자열 비교로 교체. | 기존 이름 검사(ProjectMoveChecks 등) 통과, AOT map에서 `System_Text_RegularExpressions` 0 | S | 없음 | AOT 약 −0.25–0.3 MB(메서드 코드 245 KB + 형식) | 해당 없음 |
| T7 | 레거시 JSON 메타데이터를 Authoring으로 이동 | `GameJson`을 플레이어용(스크립트 필드 형식, JsonNode, Guid 등)과 `LegacyJson`(SceneDocument/GameSettings/SpriteClip/AssetEntry[], Engine.Authoring)으로 분리. | 레거시 프로젝트 변환 검사 통과, ScriptComponent ReadOverride 검사 통과, AOT map에서 `GameJson__*PropInit` 감소 | S | 없음 | AOT 약 −0.15–0.25 MB(추정) | 해당 없음 |
| T8 | AOT 직렬화 코드 원인 확정과 축소 | `IlcGenerateDgmlFile`로 컴포넌트 포매터 `Serialize*`의 루트를 확인. 플레이어가 쓰는 형식(GameSettings, 저장, 리플레이, 크래시 보고, 업데이트 카탈로그)만 쓰기 포매터가 필요하면 씬 형식은 읽기 전용 경로로 분리(예: 플레이어 빌드에서 씬 형식에 MoNoon 소스 생성기의 역직렬화 전용 포매터 사용). | 원인 보고 + 시제품에서 SpriteLab AOT 크기 측정, KeyRoom·InventoryLab Release/AOT replay 저장 일치 | L(조사 S) | T6·T7 뒤 | 최대 −1.9 MB(실행 파일 16%) | 해당 없음(VYaml) |
| T9 | AOT 게임별 엔진 기능 조건부 컴파일 | AOT kit는 이미 게임마다 Engine.Core 소스를 다시 컴파일한다(NativeAotBuild.cs:16-21). T4 기능 집합으로 `-p:MoNoonFeatures=...` → `MONOON_NO_PHYSICS` 등 상수, `[YamlObjectUnion]`/`[JsonDerivedType]`·시스템 등록·Box2D/Mixer/Ttf 호출을 `#if`로 감싼다. 빌더가 씬에 없는 컴포넌트를 이미 알기 때문에 제외 기능의 컴포넌트가 나타나면 빌드에서 실패시킨다. Release는 템플릿이 고정이라 해당 없음. | SpriteLab·KeyRoom·PlatformerLab·AudioLab AOT 크기 측정, 각 샘플 AOT replay/smoke가 Release와 같은 결과, 기능 제외 조합 4종 컴파일 검사 | L | T4, T8 | 최소 게임 AOT −1.0–1.5 MB 추정(기능 코드 1.3 MB) | 해당 없음 |
| T10 | 빌드용 아틀라스 재패킹 | 아틀라스 사용률이 임계값(예: 70%) 미만이면 빌드 때 쓰인 셀만 새 아틀라스로 패킹(AtlasPacker 재사용, SkiaSharp은 빌드 도구라 허용), 캐시로 재사용. | 큰 아틀라스 픽스처에서 미사용 셀 제외·Release/AOT 스프라이트 캡처 동일 | M | T2(사용률 보고 먼저) | 아틀라스 크기·VRAM에 비례. 샘플은 무시 가능 | 해당 없음 |

순서 제안: T1 → T2+T3 → T6·T7(작고 확실) → T4 → T5 → T8 조사 → T9·T10.

## 4. 결정이 필요한 것

1. **기능 라이브러리 제외(T4)를 자동으로 할지** — 추천: 자동 판정 + 보고서에 판정 이유 표시 + `GameSettings.features`로 강제 포함. 대안은 Build 창 체크박스(실수로 빼면 런타임 오류).
2. **글꼴 부분 집합(T5) 기본값** — 추천: 번역 테이블이 있는 프로젝트는 기본 켜기, 글꼴별 "전체 유지"와 `extraCharacters` 제공. 이름 입력 같은 동적 텍스트가 있으면 제작자가 범위를 추가한다. 대안: 기본 끄기(안전하지만 2 MB 유지).
3. **AOT 전용 조건부 컴파일(T9)을 받아들일지** — Release·Scene Play와 AOT의 엔진 코드가 달라져 검증 부담이 생긴다. 추천: T4·T8 측정 후 남은 이득이 1 MB 이상일 때만 진행.
4. **미참조 에셋 경고 범위(T2)** — 추천: 보고서 파일에는 전부, Console에는 크기 상위 10개와 총합. `Assets/` 밖(예: 샘플 전용 폴더)은 경고 제외.
5. **큰 WAV 처리** — Vorbis 인코더는 BRUTAL에 없다(SDL_mixer는 디코딩만). 추천: 자동 변환 없이 경고만(T2), 변환은 제작자가 Ogg로 임포트.
6. **STJ 제거(플레이어 직렬화기 일원화)** — ScriptComponent 값(JsonElement)·프리팹 병합(JsonNode)을 YAML 노드로 바꾸면 STJ 약 0.4 MB+를 뺄 수 있지만 범위가 크다. 추천: 이번 계획에서는 T7까지만, 일원화는 T8 결과를 보고 결정.

검증하지 못한 것: 라이브러리 제거 후 실제 창 렌더링(창 금지), Windows·Linux에서의 제거·심볼릭 링크 동작, VYaml 직렬화 코드 유지 원인(가설), T9 이득(map 분류 기반 추정).
