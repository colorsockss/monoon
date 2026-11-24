# Native AOT 플레이어 용량 2차 (분석·축소·15.12 판단)

2026-11-24 · macOS 27.0.1 · Apple Silicon arm64 · .NET SDK 10.0.101(ILCompiler 10.0.1). 대상은 osx-arm64 배포용(Distribution) Native AOT 게임의 `Contents/MacOS/MoNoonPlayer`이고 Release(ReadyToRun) 경로는 측정만 했다. 기준 커밋 `d45fc0c`(wip/next1, 13–16장 완료). 측정 중 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음). VM은 시작하지 않았다. Linux x64·Windows 실행 확인은 coordinator의 x64 회귀 몫이다.

크기 기준([build-size/baseline.tsv](../build-size/baseline.tsv))을 처음 기록한 `d91d491` 뒤로 KeyRoom AOT 실행 파일은 8,388,224 → 8,786,304 B(+398,080 B)가 됐다. 늘어난 묶음은 기준 파일 기록대로 파티클 P.4/P.8/P.9 +149 KB, engine-features-1 +83 KB, 16.4–16.12 +99 KB, 16.6·16.13 +66 KB다. 이번 작업으로 그보다 많이 줄였다.

## 결과 요약

| 실행 파일 | `d45fc0c` | R1 VYaml 기본 리졸버 제거 | R2 JSON 값 직접 쓰기 | R3 참조 필드 직접 읽기 | 합계 |
| --- | ---: | ---: | ---: | ---: | ---: |
| SpriteLab Native AOT | 8,106,096 | 7,674,864 (−431,232) | 7,359,440 (−315,424) | 7,359,440 (0) | **−746,656 B (−9.2%)** |
| KeyRoom Native AOT | 8,786,304 | 8,338,512 (−447,792) | 8,338,528 (+16) | 8,321,968 (−16,560) | **−464,336 B (−5.3%)** |

- 세 변경 모두 동작을 바꾸지 않는다. 읽고 쓰는 바이트, 오류 종류가 같은지 JIT에서 옛 경로와 나란히 비교하는 검사를 넣었고, Release·Native AOT 결정성 기준값이 모두 같다([검증](#검증)).
- `PlatformerLab`·`InventoryLab` 크기와 갱신한 기준값은 [크기 기준 갱신](#크기-기준-갱신)에 있다.
- Release 템플릿은 공용 `VYaml.dll`·`System.Text.Json.dll`을 그대로 싣기 때문에 Release 배포본 크기는 바뀌지 않는다(트리밍한 게임별 Release는 ILLink가 같은 코드를 덜어 낼 수 있으나 따로 재지 않았다).
- 시작 시간·프레임 비용: 바뀐 코드는 문서 읽기(`ProjectYaml.Decode`)와 값 쓰기 경로다. `Decode`는 VYaml과 같은 단계를 밟고 파서 풀을 그대로 돌려주며, 컨텍스트는 스레드마다 하나를 다시 쓴다(VYaml의 스레드 로컬 풀과 같은 할당). 프레임 경로는 바뀌지 않았다. 크기 검사의 첫 프레임 시간은 [크기 기준 갱신](#크기-기준-갱신)에 기록만 했다.

## 1. 분석: 무엇이 실행 파일을 차지하는가

ILC map(`IlcGenerateMapFile`)·mstat·DGML(`IlcGenerateDgmlFile`)을 실제 게임 빌드(`./monoon build … native-aot --profile distribution`)에서 환경 변수로 켜서 만들었다([tools/mapbuild.sh](tools/mapbuild.sh), [tools/dgmlbuild.sh](tools/dgmlbuild.sh)). map 생성은 실행 파일 바이트를 바꾸지 않는다(기준 KeyRoom 8,786,304 B가 기준값과 같음). 노드 이름의 어셈블리 접두어로 나눈 값이다. 제네릭 인스턴스화는 바깥 형식의 어셈블리에 들어간다(예: `List<SceneObject>`는 CoreLib). "교차 집계"는 이름 어디에든 해당 이름공간이 들어간 노드의 합이라 서로 겹친다. map 노드 합계는 실행 파일보다 10% 남짓 크다(메타데이터·dehydrated 형식 데이터가 일부 겹쳐 잡힌다).

"전"은 `d45fc0c`, "후"는 R3(`c7acc0c`)이다. 단위는 바이트.

| 항목 | SpriteLab 전 | SpriteLab 후 | KeyRoom 전 | KeyRoom 후 |
| --- | ---: | ---: | ---: | ---: |
| map 노드 합계 | 9,093,568 | 8,172,413 | 9,911,346 | 9,367,250 |
| CoreLib | 2,999,417 | 2,743,121 | 3,102,418 | 2,918,347 |
| Engine.Core | 1,230,191 | 1,229,601 | 1,311,357 | 1,325,848 |
| Engine.Desktop | 95,080 | 95,080 | 95,080 | 95,080 |
| MoNoonPlayer | 15,406 | 15,406 | 15,408 | 15,408 |
| GameScripts | 68 | 68 | 32,011 | 32,006 |
| VYaml | 540,625 | 430,871 | 540,625 | 429,555 |
| STJ | 231,441 | 71,943 | 588,239 | 580,067 |
| LINQ | 308,331 | 308,223 | 315,515 | 315,407 |
| Collections.Immutable | 80,408 | 80,408 | 80,619 | 80,619 |
| Collections.Concurrent | 44,530 | 42,972 | 56,904 | 56,784 |
| Collections | 27,787 | 27,475 | 34,228 | 33,916 |
| IO.Compression | 119,065 | 119,065 | 119,065 | 119,065 |
| M.E.* | 77,198 | 77,198 | 77,198 | 77,198 |
| Console | 47,693 | 47,693 | 47,693 | 47,693 |
| TypeLoader | 135,945 | 135,945 | 135,945 | 135,945 |
| Reflection | 25,891 | 25,713 | 25,891 | 25,840 |
| Box2D | 13,653 | 13,653 | 13,653 | 13,653 |
| Brutal.Mixer | 8,285 | 8,285 | 8,285 | 8,285 |
| Brutal.Ttf | 6,171 | 6,171 | 6,171 | 6,171 |
| Brutal other | 48,190 | 48,190 | 48,190 | 48,190 |
| (other) | 2,942,079 | 2,578,623 | 3,153,783 | 2,926,375 |

| 교차 집계(인스턴스화 포함) | SpriteLab 전 | SpriteLab 후 | KeyRoom 전 | KeyRoom 후 |
| --- | ---: | ---: | ---: | ---: |
| VYaml | 614,488 | 495,182 | 613,611 | 492,989 |
| STJ | 314,828 | 109,652 | 720,480 | 711,947 |
| LINQ | 321,717 | 321,609 | 329,402 | 329,294 |
| Immutable | 128,934 | 128,934 | 129,151 | 129,151 |
| System.Drawing | 219,175 | 219,175 | 219,175 | 219,175 |
| IO.Compression | 174,676 | 174,676 | 174,676 | 174,676 |
| M.E. | 178,704 | 178,704 | 178,704 | 178,704 |
| Uri | 56,673 | 0 | 56,752 | 79 |
| VYaml 생성 포매터(Deserialize) | 312,123 | 312,123 | 312,123 | 312,123 |
| record 합성 멤버 코드(Engine.Core) | 178,152 | 176,132 | 184,112 | 184,112 |

| 노드 종류 | SpriteLab 전 | SpriteLab 후 | KeyRoom 전 | KeyRoom 후 |
| --- | ---: | ---: | ---: | ---: |
| MethodCode | 4,421,608 | 3,994,368 | 4,838,632 | 4,564,124 |
| GCInfo | 426,350 | 389,104 | 473,322 | 450,533 |
| ConstructedEEType | 1,286,840 | 1,143,996 | 1,417,920 | 1,348,676 |
| DehydratedData | 1,034,706 | 919,668 | 1,127,559 | 1,069,830 |
| Metadata | 448,883 | 404,044 | 477,812 | 454,620 |
| ArrayOfFrozenObjects | 380,408 | 340,120 | 407,848 | 387,160 |

### 형식·기능별로 본 것

| 묶음 | SpriteLab(전) | 내용 |
| --- | ---: | --- |
| VYaml `BuiltinResolver` | 약 336 KB(map, DGML 가정 제거) | `ProjectYaml`이 `StandardResolver`를 거치면서 `BuiltinResolver`의 정적 생성자가 포매터 약 70개(Uri, DateTime·TimeSpan·DateTimeOffset, BigInteger·Complex, Version, Type, BitArray, 다차원 배열, 튜플, 컬렉션 인터페이스 …)를 만들었다. ILC는 그 Deserialize·Serialize와 뒤의 BCL 코드(System.Private.Uri 57 KB, 날짜 파서, `TypeNameParser`, IDN 매핑 …)를 모두 남겼다. 엔진 문서는 이 중 스칼라 15개만 쓴다 → **R1** |
| System.Text.Json 직렬화기 계층 | 약 138 KB(map, SpriteLab) / KeyRoom 약 456 KB | SpriteLab에서는 `GameData.Set(bool/double)`의 `JsonSerializer.SerializeToElement` 한 곳이 `JsonTypeInfo`·변환기·`WriteStack` 계층 전체를 끌어왔다 → **R2**. KeyRoom은 스크립트 필드 읽기(`ReadOverride`)와 `WorldCheckpoints`가 계속 쓴다 → R3, 후보 C1 |
| VYaml 생성 포매터 | 312,123(Deserialize) | 컴포넌트·문서 형식마다 2.5–9 KB. `SceneComponent` 합집합 포매터가 모든 컴포넌트를 참조하므로 게임이 쓰지 않는 컴포넌트도 남는다(15.12) |
| VYaml enum 포매터 | 형식마다 약 7.2 KB, 17개 | `EnumAsStringFormatter<T>`가 enum마다 사전 두 개와 정적 생성자를 인스턴스화한다 → 후보 C3 |
| record 합성 멤버 | 176–184 KB | 엔진 record 218개의 `PrintMembers`·`ToString`·`GetHashCode`·`Equals`. 형식이 생성되면 가상 슬롯 때문에 남는다 → 후보 C2 |
| LINQ | 308–315 KB | 대부분 공유(`__Canon`) 반복자와 `Enumerable` 본체. 값 형식 인스턴스화는 `KeyValuePair<Color,…>`·`(SpriteFilter, Color)`·`KeyValuePair<…, Guid>` 등 흩어져 각 10–15 KB |
| System.Drawing(Color·Point) | 219 KB(교차) | 엔진 색·좌표 형식. 값 형식 제네릭 인스턴스화가 많다. 바꾸려면 공개 API가 바뀐다 |
| System.IO.Compression | 175 KB(교차) | 콘텐츠 묶음·업데이트(ZIP). 시작 경로에서 닿는 플레이어 기능 |
| Microsoft.Extensions.* | 179 KB(교차) | `Brutal.Logging`이 `LoggerFactory`·DI를 쓴다. BRUTAL 우선 규칙대로 유지 |
| System.Collections.Immutable | 129 KB(교차) | `Tilemap2DComponent`의 `ImmutableDictionary<Point, ImmutableSortedDictionary<int,int>>`·`ImmutableDictionary<int,int>`, `ObjectGroups`의 `ImmutableList<Guid>`, `SceneObject.Components`(`ImmutableArray`) |
| Console | 48 KB | `Program.Main`의 `Console.WriteLine`(스모크·리플레이 출력) |

기능 묶음(엔진 코드 + 그 코드만 쓰는 인스턴스화)은 [15.12 판단](#3-1512-판단-aot-게임별-엔진-기능-조건부-컴파일)의 표에 있다.

## 2. 적용한 축소

### R1: Native AOT 플레이어에서 VYaml `BuiltinResolver` 제거 (`e5739c6`)

| 파일 | 내용 |
| --- | --- |
| [ProjectYaml.cs](../../../src/Engine.Core/ProjectYaml.cs) | 리졸버 사슬을 `[기존 포매터 목록] → ScalarYamlResolver → GeneratedResolver`로 바꿨다. `ScalarYamlResolver`는 엔진 문서가 쓰는 스칼라 15종(string, bool, int, uint, long, ulong, float, double, Guid와 그 nullable)에 `BuiltinResolver`가 주던 **같은 인스턴스**를 준다. 배열·리스트·사전·nullable·enum 포매터는 VYaml 소스 생성기가 이미 `GeneratedResolver`에 등록한다. 옵션은 생성자 대신 `GetUninitializedObject`로 만들고 생성자가 넣는 필드(EmitOptions, MaxDepth 64)를 직접 넣는다. `Decode`는 `YamlSerializer.DeserializeMultipleDocuments`의 단계를 그대로 밟는다(VYaml 쪽은 컨텍스트 풀의 팩터리가 `YamlSerializerOptions.Standard`를 읽는다). 파서 풀은 `UnsafeAccessor`로 VYaml의 `ReturnPool`에 돌려준다 |
| [ProjectScripts.cs](../../../src/Engine.Authoring/ProjectScripts.cs) | 에디터·빌드 도구는 기존 모듈 초기화에서 `ProjectYaml.ToolFormatters = BuiltinResolver.Instance`를 넣는다. 도구 쪽 해석 순서와 결과는 전과 같다(예: `Dictionary<string, ContentBuildEntry>`) |
| [VYaml.Substitutions.xml](../../../src/Engine.Player/VYaml.Substitutions.xml), [Engine.Player.csproj](../../../src/Engine.Player/Engine.Player.csproj) | `PublishAot=true`일 때만 ILC `--substitution`으로 `YamlSerializerOptions..ctor()`와 `get_Standard()`의 본문을 없앤다(`body="remove"`, 부르면 예외). 생성자는 `GetUninitializedObject`의 `DynamicallyAccessedMembers` 때문에 계속 컴파일되므로, 본문을 없애야 `StandardResolver`가 빠진다. Release·Scene Play는 해당 없음 |
| [ProjectYamlResolverChecks.cs](../../Engine.Checks/ProjectYamlResolverChecks.cs)(새, `PlayerYamlChecks`에서 실행) | ① Engine.Core 생성 포매터 전부의 문서·멤버 형식 202개에 대해 플레이어 사슬(도구 대체 없이)이 `StandardResolver`와 **같은 포매터 형식**을 준다 ② 옵션의 모든 필드가 생성자로 만든 것과 같다 ③ `Decode`가 VYaml의 `DeserializeMultipleDocuments`와 10개 입력(빈 문서, 두 문서, 잘못된 YAML, 없는 앵커, 모르는 키 …)에서 같은 값·같은 예외(7가지 결과)를 낸다. 일부러 `uint` 한 줄을 빼면 `YAML formatter for System.UInt32: player none, StandardResolver …UInt32Formatter`로 실패하는 것을 확인했다 |
| [BuildSizeChecks.cs](../../Engine.Checks/BuildSizeChecks.cs) | 키트 map 검사에 두 줄: `BuiltinResolver`가 다시 들어오면 실패, 플레이어 코드가 VYaml의 `YamlSerializer` 정적 API(AOT에서 예외)를 부르면 실패 |

처음에는 리졸버만 바꿨지만 크기가 오히려 33 KB 늘었다. DGML로 보니 `YamlSerializerOptions` 생성자가 `StandardResolver.Instance`를 저장하고 있었다. 생성자를 피하고(`GetUninitializedObject`) `Decode`를 다시 써도 `DynamicallyAccessedMembers`가 생성자 본문을 남겨서 16 KB만 줄었다. 대체 파일로 본문을 지우자 431 KB가 줄었다. 대체 파일만 쓰고 `Decode`를 VYaml 것으로 두면 스모크 테스트가 `The feature associated with this method was removed`로 멈췄다(VYaml 컨텍스트 풀이 `Standard`를 읽는다). 그래서 둘 다 필요하다.

### R2: JSON 값을 `JsonSerializer` 없이 쓰기 (`46a7766`)

| 파일 | 내용 |
| --- | --- |
| [GameJson.cs](../../../src/Engine.Core/GameJson.cs) | `JsonElements.Of(bool/double/string/Guid/Guid[]/JsonNode)`. `JsonSerializer.SerializeToElement(value, GameJson.Default.X)`가 GameJson 옵션에서 만드는 쓰기 옵션(들여쓰기, 깊이 64, 검증 생략)으로 `Utf8JsonWriter`에 직접 쓰고 같은 방식으로 요소를 만든다. `JsonNode`는 같은 `GameJson.Default.Options`로 `WriteTo`한다 |
| [GameData.cs](../../../src/Engine.Core/GameData.cs), [GameBehaviour.cs](../../../src/Engine.Core/GameBehaviour.cs), [PrefabMerge.cs](../../../src/Engine.Core/PrefabMerge.cs), [PrefabStructure.cs](../../../src/Engine.Core/PrefabStructure.cs), [ScriptDefinition.cs](../../../src/Engine.Core/ScriptDefinition.cs) | `SerializeToElement` 호출 9곳을 `JsonElements.Of`로. `WorldCheckpoints`의 한 곳은 그대로(아래 C1) |
| [GameJsonWriterChecks.cs](../../Engine.Checks/GameJsonWriterChecks.cs)(새) | 39개 값(−0.0, 1e21, double.MaxValue/Epsilon, NaN·무한대(같은 예외), 이스케이프·한글·이모지·제어 문자, null, 빈·여러 GUID 배열, 중첩·직접 만든 `JsonNode`, 깊이 64)의 `ValueKind`와 원문 텍스트가 `SerializeToElement`와 같다. 깊이 65 노드는 둘 다 거부한다(직렬화기 `JsonException`, 작성기 `InvalidOperationException`). 파싱한 노드는 `JsonDocument` 한도 때문에 64를 넘을 수 없어 프리팹 경로에서는 생기지 않는다 |

SpriteLab처럼 스크립트 필드가 없는 게임은 직렬화기 계층이 통째로 빠져 315 KB가 줄었다. KeyRoom은 스크립트 필드 읽기와 월드 체크포인트가 같은 계층을 쓰므로 그대로다.

### R3: 스크립트 에셋 참조 필드를 직접 읽기 (`c7acc0c`)

| 파일 | 내용 |
| --- | --- |
| [GameBehaviour.cs](../../../src/Engine.Core/GameBehaviour.cs) | `ReadOverride<T>`가 `SceneReference`·`SpriteReference`·`PrefabReference`·`DataReference`·`TableReference`를 `JsonElements.ReadReference`로 읽고, 기본 형식은 `GameJson.Field<T>()`로 해당 `JsonTypeInfo`만 고른다. `GameJson.Default.GetTypeInfo(Type)`는 목록의 모든 형식 변환기를 남겼다 |
| [GameJson.cs](../../../src/Engine.Core/GameJson.cs) | `ReadReference`: 객체가 아니면 실패, 속성 이름은 대소문자 무시, 마지막 `Id`가 이김, 다른 속성은 건너뜀, `Id`가 없으면 `Guid.Empty`(웹 기본값 직렬화기와 같음). `Field<T>`: 여섯 기본 형식, 그 밖은 null(전처럼 `Deserialize`가 `ArgumentNullException`) |
| [GameJsonWriterChecks.cs](../../Engine.Checks/GameJsonWriterChecks.cs) | 참조 다섯 형식 × 20개 입력(null, 숫자, 배열, `{}`, 대소문자 변형, 중복 `Id`, 잘못된·N 형식·중괄호·대문자·앞 공백 GUID, 이스케이프한 이름 …) 100개가 `JsonSerializer` 결과(`JsonException`은 "유지")와 같다. `Field<T>`가 `GetTypeInfo`와 같은 인스턴스다 |

KeyRoom은 다섯 참조 형식을 모두 쓰고, 객체 변환기 공통 코드가 `WorldCheckpoints`에 남아 있어 16.5 KB만 줄었다. C1과 함께하면 객체 변환기 계층이 빠진다(아래 추정).

## 남긴 후보 (측정만)

| 후보 | 추정 절감(map 바이트, DGML 가정 제거) | 하지 않은 이유 |
| --- | ---: | --- |
| C1 `WorldCheckpoints`를 손으로 읽고 쓰기 | KeyRoom 약 −0.34 MB(R3와 합치면 −0.46 MB). 체크포인트를 쓰는 게임만 | 웹 기본값 읽기 규칙(대소문자 무시, 따옴표 숫자 허용, 필수 생성자 인자, 숫자 enum, Guid 키 사전)을 그대로 옮겨야 한다. 바이트 비교 검사와 함께 별도 작업으로 하는 편이 맞다 |
| C1+ `ReadOverride`의 기본 형식까지 직접 읽기 | C1에 더해 약 −0.17 MB(직렬화기 전체 −0.62 MB) | `AllowReadingFromString` 숫자 해석(따옴표 안 정수·실수, 이름 있는 부동소수 리터럴 거부)을 옮겨야 한다 |
| C2 record `ToString`/`PrintMembers` 막기 | 약 −0.13 MB(모든 게임) | 기반 record에 `sealed override ToString`을 두면 파생 record의 합성 `ToString`이 사라진다. 출력 문자열이 바뀌므로 동작 중립이 아니다 |
| C3 enum 포매터 공유 | 약 −0.1 MB(모든 게임) | VYaml 생성 코드가 모듈 초기화에서 `EnumAsStringFormatter<T>`를 등록하므로, 같은 인터페이스 슬롯을 쓰는 한 원래 포매터가 남는다. 등록 자체를 바꿔야 한다 |
| C4 Tilemap의 Immutable 컬렉션 | 약 −0.02–0.08 MB | `with` 복사가 구조를 공유하는 것에 기대고 있어 단순 교체는 동작이 바뀔 수 있다 |

## 3. 15.12 판단: AOT 게임별 엔진 기능 조건부 컴파일

결정 6: 싼 축소를 한 뒤에도 최소 게임에서 1 MB 이상 남을 때만 한다.

방법: R2 뒤 SpriteLab(카메라·스프라이트·애니메이터만 쓰는 장면)의 codegen DGML에서, 기능 이름에 맞는 엔진 노드(Engine.Core·Desktop·Player·BRUTAL의 메서드·형식·정적 필드, `SceneGame`의 해당 단계 메서드 포함)를 지웠다고 가정하고 루트에서 더는 닿지 않는 노드의 map 바이트를 더했다([tools/features_cut.py](tools/features_cut.py)). 조건부 노드 `(A, B)`는 두 입력이 모두 닿아야 산다. 정규식이 넓으면 많게, 공유 코드가 다른 경로로 남으면 적게 잡히는 추정이다. map 바이트와 실행 파일의 비는 약 0.90이다.

| 기능 묶음 (SpriteLab에서 안 씀) | 사라지는 map 바이트 | 실행 파일 추정 |
| --- | ---: | ---: |
| UI·텍스트·대화(`ScreenUi`, `Ui*`, RichText, SDL_ttf 바인딩, 현지화) | 473,747 | ~0.43 MB |
| 물리(Box2D, 충돌체, 접촉, 캐릭터 모터, 타일 충돌) | 455,057 | ~0.41 MB |
| 타일맵(타일 월드·페이지, 오토타일, 지형) | 276,184 | ~0.25 MB |
| 파티클 | 145,735 | ~0.13 MB |
| 오디오(SDL_mixer 바인딩, 버스·믹서, 리듬) | 98,517 | ~0.09 MB |
| 스켈레톤·리그 | 64,547 | ~0.06 MB |
| 트윈·컷신·경로 | 54,336 | ~0.05 MB |
| 조명 | 39,974 | ~0.04 MB |
| 내비게이션, 선·트레일, 인벤토리·퀘스트·덱·턴, 프리팹 풀 | 34,427 | ~0.03 MB |
| **모두**(겹침 제외) | **1,405,278** | **~1.26 MB (SpriteLab 7.36 MB의 17%)** |
| 15.11 기능 집합만(텍스트·오디오·물리) | 1,006,386 | ~0.91 MB |

- 합집합 포매터만 기능 스위치로 막는 경우(씬 YAML에서 안 쓰는 컴포넌트 태그를 등록하지 않음)는 232,470 B(~0.21 MB)뿐이다. `SceneGame`이 시스템 코드를 직접 부르므로 컴포넌트 형식이 다른 경로로 계속 살아 있다. 이득의 대부분은 `SceneGame`·`RuntimeObjects`의 단계·그리기·적재 호출을 기능별로 막아야 나온다.
- KeyRoom(UI·텍스트·오디오·충돌체 사용)에서 안 쓰는 아홉 묶음(타일맵, 파티클, 스켈레톤, 트윈, 조명, 내비게이션, 선, 인벤토리 등, 프리팹 풀)은 612,875 B(~0.55 MB)다.

**판단: 조건 충족(최소 게임 약 1.26 MB, 15.11 세 기능만 막으면 약 0.9 MB). 이 작업 안에서는 구현하지 않았다.** 이득을 내려면 `SceneGame.cs`·`RuntimeObjects.cs`(여러 묶음이 함께 고치는 핫스팟, 이 브리프의 소유가 아님)의 단계 호출을 기능 스위치로 감싸야 하고, 행 자체가 L 규모다. 아래 행 문안으로 별도 묶음을 제안한다.

### 로드맵 15.12 행 문안 제안 (coordinator용, `docs/roadmap.md`는 고치지 않음)

> | 15.12 | AOT 게임별 엔진 기능 조건부 컴파일 | 예정. aot-size-2 측정에서 조건(결정 6)을 넘었다: VYaml 기본 리졸버·JSON 직렬화기 계층을 뺀 뒤에도 SpriteLab AOT 7.36 MB 중 안 쓰는 기능 코드가 약 1.26 MB(DGML 가정 제거, 12개 묶음), 15.11 기능(텍스트·오디오·물리)만 약 0.9 MB, KeyRoom 약 0.55 MB. `#if` 대신 기능 스위치(`[FeatureSwitchDefinition]` + 키트 publish의 `RuntimeHostConfigurationOption Trim="true"`, 기존 `MoNoon.ExportedGameOnly`와 같은 방식)를 쓴다. 15.11 `BuildFeatures`가 기능 집합을 정하고 `NativeAotBuild`가 스위치를 넘긴다. ① `SceneComponent` 합집합을 `ProjectYaml`의 사용자 포매터 목록에 둔 엔진 쪽 포매터로 읽고, 꺼진 기능의 태그는 등록하지 않는다(약 0.21 MB) ② `SceneGame`·`RuntimeObjects`의 단계·그리기·적재 호출과 스크립트 API 진입점을 `if (EngineFeatures.X)`로 감싼다(나머지) ③ 꺼진 기능의 컴포넌트가 씬·프리팹에 있으면 빌드가 실패한다. Release·Scene Play는 스위치가 켜진 채라 코드가 같다(트리밍 Release에도 같은 스위치를 줄 수 있다). 완료: SpriteLab·KeyRoom·PlatformerLab·AudioLab AOT 크기, 각 AOT 리플레이·스모크가 Release와 같음, 기능 끈 조합 4종이 빌드·실행됨, 크기 기준 갱신. [측정](../engine/tests/results/aot-size-2/README.md#3-1512-판단-aot-게임별-엔진-기능-조건부-컴파일) |

### coordinator에게: 핫스팟 문서에 옮길 내용

`docs/manual/build.md`(핫스팟)의 크기 기준 단락 뒤에 다음을 제안한다.

> 런타임 코드는 VYaml의 `YamlSerializer` 정적 API나 `StandardResolver`를 쓰지 않는다. 읽기는 `ProjectYaml.Decode`, 쓰기는 `ProjectYamlWriters`의 오버로드를 쓴다. Native AOT 플레이어는 `Engine.Player/VYaml.Substitutions.xml`로 `YamlSerializerOptions`의 생성자를 없애므로 그 API는 예외를 낸다. 크기 검사 앞의 키트 map 검사가 둘 다 잡는다. JSON 값 하나를 만들 때는 `JsonSerializer.SerializeToElement` 대신 `JsonElements.Of`를 쓴다. [측정](../../engine/tests/results/aot-size-2/README.md)

## 검증

osx-arm64, 배포용(Distribution) 빌드, 창 없는 재생. R1 커밋(`e5739c6`)과 최종 커밋(`c7acc0c`)을 각각 `git archive`로 스크래치 폴더에 풀고 그 안에서 템플릿을 다시 만든 뒤 [gates.sh](gates.sh)로 돌렸다. 출력: [gates-r1.txt](gates-r1.txt), [gates-r3.txt](gates-r3.txt). 첫 예외는 없다.

| 검사 | Release (R1 · 최종) | Native AOT (R1 · 최종) | 값 |
| --- | --- | --- | --- |
| PlatformerLab.Joints 300틱 | 같음 · 같음 | 같음 · 같음 | `2d523cd0…` |
| PlatformerLab.TileCollision | 같음 · 같음 | 같음 · 같음 | `f30ed686…` |
| PlatformerLab.Paths 400틱 | 같음 · 같음 | 같음 · 같음 | `e37de6c0…` |
| PlatformerLab.Colliders(Slopes) 450행 | 같음 · 같음 | 같음 · 같음 | `635f9029…` |
| ParticleLab 7개 장면(Modules, Sorting, Stress, Curves, Rain, Fireworks, Presets) | 같음 · 같음 | 같음 · 같음 | 커밋된 `results/particles/osx-arm64/*-trace.tsv`와 같음 |
| AudioLab.Buses 첫 네 바퀴 PCM(default) · 덕킹 | 같음 · 같음 | 같음 · 같음 | `ced6c800…` · `f6c485bb…` |
| InventoryLab 리플레이(`./monoon test replay`) | 같음 · 같음 | 같음 · 같음 | 171 B, `EED41DEF…E70F795` |
| KeyRoom.WorldCheckpoints(`ci.py osx-arm64`) | 통과 · 통과 | 통과 · 통과 | CarryCoin 833 B `93dd4173…` 등 22개 PASS |
| SpriteLab·KeyRoom `--smoke-test`(최종 빌드) | – | 통과 | |
| `./monoon test`(최종, 이 워크트리) | – | – | 통과. PASS 951줄. 새 항목: 리졸버 동등성(202형식, `Decode` 10입력), JSON 작성기 39값, 참조 읽기 100값, 키트 map의 BuiltinResolver·YamlSerializer 검사. YamlDefaultChecks, DeterminismScan(235파일), PlayerYaml(22문서), 크기 기준 포함 |
| 금지 파일 | – | – | ParticleLab `verify.py all`이 Release·AOT `.app` 14개를 검사(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring 없음) |

### 크기 기준 갱신

`./monoon test size --update`로 [baseline.tsv](../build-size/baseline.tsv)를 갱신했다(시간 열은 기록만, 판정하지 않음).

| 빌드 | 실행 파일 전 → 후 | `.app` 전 → 후 |
| --- | ---: | ---: |
| KeyRoom Native AOT | 8,786,304 → 8,321,968 (−464,336) | 15,924,631 → 15,460,295 (−464,336) |
| PlatformerLab Native AOT | 8,437,808 → 7,923,552 (−514,256) | 13,155,249 → 12,640,993 (−514,256) |
| InventoryLab Native AOT | 8,470,880 → 7,973,216 (−497,664) | 66,582,576 → 66,084,912 (−497,664) |
| KeyRoom Release | 124,712 → 124,712 | 33,151,258 → 33,162,522 (+11,264) |
| PlatformerLab Release | 124,712 → 124,712 | 30,438,894 → 30,448,622 (+9,728) |
| InventoryLab Release | 124,712 → 124,712 | 83,879,741 → 83,907,901 (+28,160) |

Release `.app`이 10–28 KB 늘었다. 공용 `VYaml.dll`·`System.Text.Json.dll`은 그대로이고, 새 코드(`ScalarYamlResolver`, `Decode`, `JsonElements`)가 들어간 `Engine.Core.dll`의 ReadyToRun 코드로 보인다(파일별로 나눠 재지 않음, 1% 허용 안).

## 재현

```sh
export AOT_SIZE_OUT=$(mktemp -d)
sh engine/tests/results/aot-size-2/tools/mapbuild.sh "$PWD" SpriteLab after     # map + mstat
sh engine/tests/results/aot-size-2/tools/dgmlbuild.sh "$PWD" SpriteLab afterd   # + DGML (약 3분)
python3 engine/tests/results/aot-size-2/tools/analyze.py "$AOT_SIZE_OUT/after/SpriteLab/native/MoNoonPlayer.map.xml"
python3 engine/tests/results/aot-size-2/tools/why.py "$AOT_SIZE_OUT/afterd/SpriteLab/native/MoNoonPlayer.codegen.dgml.xml" '^S_P_Uri_System_Uri___ctor$'
python3 engine/tests/results/aot-size-2/tools/cut.py <codegen.dgml.xml> <map.xml> '<지울 노드 정규식>'   # why.py를 먼저 한 번 실행해 그래프 캐시를 만든다
python3 engine/tests/results/aot-size-2/tools/features_cut.py <codegen.dgml.xml> <map.xml> [묶음,묶음]
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only player-yaml   # 리졸버·JSON 동등성
sh engine/tests/results/aot-size-2/gates.sh "$(mktemp -d)"                               # 결정성 기준값(Release·AOT)
./monoon test
```

## 확인하지 않은 것

- Scene Play: 에디터는 `ToolFormatters`로 `BuiltinResolver`를 되살려 해석 순서·결과가 전과 같고, 대체 파일은 Native AOT에만 걸린다. 그래서 결정성 기준값은 Release·Native AOT로만 다시 돌렸다(에디터 패키지를 새로 만들지 않음).
- Linux x64·Windows Native AOT 크기와 실행(coordinator x64 회귀). ILC 대체 파일은 RID와 무관하지만 win-x64·linux-x64 게스트에서 스모크를 봐야 한다.
- 15.12 추정은 DGML 가정 제거 값이다. 실제 기능 스위치로 한 기능도 구현해 비교하지 않았다.
- 트리밍한 게임별 Release(ILLink)의 크기 변화.
