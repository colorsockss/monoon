# Native AOT에서 쓰지 않는 VYaml 쓰기 코드 제거 (B6 = T8)

2026-11-24 · macOS 27.0.1 · Apple Silicon arm64 · .NET SDK 10.0.101 · 기준 `8eb15b2`(wip/next1). 측정 중 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음). 시간은 재지 않았고 크기와 결정성만 확인했다.

## 원인

AOT 키트(게임 스크립트 없음, `OptimizationPreference=Size`)를 `IlcGenerateMapFile`·`IlcGenerateDgmlFile`로 publish했다. 그 결과 생성 포매터 102개 형식의 `Serialize`·`SerializeMembers`가 398개 메서드에 1,898,616 B(코드)와 103,679 B(GCInfo)를 차지했다. `Deserialize`는 213,892 B다.

1. **모든 포매터가 생성된다.** VYaml 소스 생성기가 만든 `VYamlGeneratedRegistration.g.cs`의 `[ModuleInitializer]`가 102개 포매터를 모두 `new`로 등록한다. 그래서 포매터 형식은 항상 "constructed" 상태다.
2. **참조형 Serialize 사용 하나가 전체를 남긴다.** ILC는 `IYamlFormatter<string[]>.Serialize`처럼 참조형 T에 대한 인터페이스 호출을 `IYamlFormatter<__Canon>.Serialize` 사용으로도 기록한다(DGML 이유 `Canonical method`). 그러면 생성된 모든 참조형 `IYamlFormatter<T>` 인터페이스 슬롯에 `Canonically equivalent virtual method use`가 붙는다. 결과적으로 씬 컴포넌트를 포함한 102개 형식의 쓰기 코드가 모두 남는다. 생성 코드 안의 `context.Serialize<string>` 호출도 같은 효과를 낸다. 따라서 일부 형식만 남기는 방식은 통하지 않는다. 플레이어에서 참조형 Serialize 호출이 **하나도 없어야** 줄어든다.
3. **형식당 크기가 큰 이유.** `SerializeMembers`는 멤버마다 `NamingConvention` 분기, `NamingConventionMutator.MutateToThreadStaticBufferUtf8`, 인라인된 `Utf8YamlEmitter.WriteScalar`를 포함한다. 멤버가 3개인 `AnimationParameter`도 `SerializeMembers`가 16,040 B다.
4. **플레이어에서 참조형 쓰기를 호출하는 곳**(키트 DGML의 `ProjectYaml.Encode<T>`/`Write<T>` 인스턴스와 호출 경로):

| 형식 | 호출 위치 | 실행 시점 |
| --- | --- | --- |
| `GameData` | `PlayerStorage.SaveSlot`·`Write`(저장 슬롯, `player.settings`), `GameMods.AddOverrides` | 내보낸 게임 |
| `string[]` | `PlayerStorage.Recovery`(`.sha256`), `GameMods.SetEnabled`(`Mods/Enabled`) | 내보낸 게임 |
| `InputReplay` | `InputRecorder.Save` | 내보낸 게임 |
| `PlayerCrashReport` | `PlayerCrashReport.Save` | 내보낸 게임 |
| `GameTable` | `GameMods.AddOverrides`(모드 교체 파일) | 내보낸 게임 |
| `AssetEntry[]` | `ContentBundle.WriteOverrides`(업데이트·모드 매니페스트) | 내보낸 게임 |
| `GridReplay` | `PlayerStorage.SaveReplay`(SeedLab 스크립트) | 내보낸 게임 |
| `AssetMeta`, `AssetEntry[]` | `ProjectAssets.Scan`의 `.meta`·매니페스트 기록 | 프로젝트 폴더 실행만(`.monoon-player` 없음) |
| `TilePage` | `TileWorld.Bake`(스트리밍 타일맵 청크) | 프로젝트 폴더 실행만(내보내기는 `ChunkFiles`를 미리 굽는다) |
| `SceneObject` | `PrefabMerge.Snapshot` ← `SceneDocument.Materialize` ← 프리팹 다시 맞추기(`SceneDocument.Load`, `SceneGame.ReadPrefab`의 `ReadResolvedPrefab`) | 프로젝트 폴더 실행만 |

`GameSettings`는 플레이어에서 쓰지 않는다. 런타임 프리팹 생성(`Instantiate`)은 [RuntimeObjects.cs](../../../src/Engine.Core/RuntimeObjects.cs)의 템플릿 캐시에서 직접 복사하므로 YAML 왕복이 없다. `Prefabs.cs:90`의 왕복(`RemapLink`)은 에디터 경로라서 키트 그래프에 없다. 따라서 복제 경로는 바꾸지 않았다.

실험: `Encode<T>`가 참조형이면 예외를 던지도록 임시로 바꾸자 키트 실행 파일이 9,938,416 → 7,559,520 B(−2,378,896)가 됐다. 생성 Serialize 코드는 0이 됐고 VYaml의 emitter 쪽 코드도 함께 빠졌다.

## 바꾼 것

- [ProjectYamlWriters.cs](../../../src/Engine.Core/ProjectYamlWriters.cs): 위 표에서 "내보낸 게임" 형식 7개에 `ProjectYaml.Encode`/`Write` 비제네릭 오버로드를 추가했다. C# 오버로드 규칙에 따라 기존 호출(`ProjectYaml.Encode(data.Validate())` 등)이 코드 변경 없이 이 오버로드로 연결된다. 각 기록기는 생성 포매터와 같은 순서로 같은 `Utf8YamlEmitter` 호출을 한다. 이 순서는 VYaml 1.6.1 IL에서 확인했다. 필수 멤버는 null이어도 쓰고, 선택 멤버는 null이면 뺀다(`WhenWritingNull`). 문자열은 `WriteString`, Guid는 `D` 형식 `WriteScalar`, enum은 lowerCamel `WriteScalar`, `JsonElement`는 기존 `ScriptValueFormatter`를 쓴다. 참조형 `IYamlFormatter<T>` 호출은 없다. 에디터와 Release도 같은 기록기를 쓰므로 세 실행 경로의 바이트가 같다.
- 기능 스위치 `MoNoon.ExportedGameOnly`(`ProjectAssets.ExportedGameOnly`, `[FeatureSwitchDefinition]`): [Engine.Player.csproj](../../../src/Engine.Player/Engine.Player.csproj)가 `PublishAot=true`일 때만 `RuntimeHostConfigurationOption Trim="true"`로 켠다. Native AOT는 내보내기에서만 만들어지므로(`.monoon-player`가 항상 있음) 동작은 바뀌지 않는다. 이전의 `.monoon-player` 검사 4곳(`ProjectAssets.Scan`, `SceneDocument.Load`, `SceneGame.ReadPrefab`, `TileWorld`)이 이 스위치를 먼저 보도록 바꿨고, ILC가 프로젝트 폴더 전용 분기를 버린다. Release(JIT)와 에디터는 스위치가 꺼져 있어 기존과 같다.
  - `ReadPrefab`에서 `ExportedGameOnly || exported` 조건은 Roslyn이 단락 평가가 아닌 `or`로 컴파일해 ILC가 접지 못했다. 이 때문에 SceneObject 경로 하나가 남아 101개 형식의 쓰기 코드 1.88 MB가 유지됐다(실행 파일 9,888,240 B). 그래서 스위치를 직접 분기하는 형태로 바꿨다.
  - 내보낸 스트리밍 타일맵에 `ChunkFiles`가 없으면 AOT 플레이어는 굽는 대신 `InvalidDataException`을 던진다. 내보내기는 항상 청크를 굽기 때문에(`GameBuild.cs`) 정상 배포본에서는 일어나지 않는다.
- [PlayerYamlChecks.cs](../../Engine.Checks/PlayerYamlChecks.cs)(`./monoon test`, `--player-yaml-only`): 7개 기록기의 결과를 생성 포매터(`Encode<T>`)와 바이트 단위로 비교한다. 비교 대상은 문서 22개다. 문자열 37개(null 포함, `true`, `null`, `a: b`, `#`, 줄바꿈, 한글·이모지, 빈 문자열 등), null 필수·선택 멤버, null 배열 원소, 빈 배열·사전, 모든 enum 값, `float.MaxValue`·`ulong.MaxValue`, 중첩 `JsonElement`를 포함한다. 형식에 멤버를 추가하면 생성자 인자가 바뀌어 이 검사가 컴파일되지 않으므로 기록기를 함께 고치게 된다.
- `GameBuild`·`DesktopBuild`·`NativeAotBuild`·`Directory.Build.targets`는 건드리지 않았다. BRUTAL에는 YAML 직렬화 기능이 없어 해당하지 않는다.

## 크기 (osx-arm64, Distribution, 바이트)

기준값은 같은 기준 커밋에서 잰 [배포 용량 1차 묶음](../size-wave1/README.md)의 B7 값이다. 이번에 다시 publish한 기준 키트 실행 파일(9,938,416)이 그 README의 T7 키트 값과 바이트까지 같았다.

| 산출물 | 기준 | 변경 후 | 차이 |
| --- | ---: | ---: | ---: |
| AOT 키트 실행 파일(스크립트 없음) | 9,938,416 | 7,591,936 | −2,346,480 (−23.6%) |
| KeyRoom Native AOT 실행 파일 | 10,552,304 | 8,255,568 | −2,296,736 (−21.8%) |
| PlatformerLab Native AOT 실행 파일 | 10,253,472 | 7,940,240 | −2,313,232 (−22.6%) |
| InventoryLab Native AOT 실행 파일 | 10,253,344 | 7,940,128 | −2,313,216 (−22.6%) |
| KeyRoom Native AOT `.app` | 17,690,063 | 15,393,327 | −2,296,736 (−13.0%) |
| PlatformerLab Native AOT `.app` | 15,265,817 | 12,952,585 | −2,313,232 (−15.2%) |
| InventoryLab Native AOT `.app` | 68,605,909 | 66,292,693 | −2,313,216 (−3.4%) |
| KeyRoom Release `.app` | 89,543,912 | 89,558,760 | +14,848 |
| PlatformerLab Release `.app` | 87,407,234 | 87,422,082 | +14,848 |
| InventoryLab Release `.app` | 140,763,326 | 140,778,174 | +14,848 |

Release 증가는 기록기 코드로 커진 `Engine.Core.dll`(1,313,792 → 1,327,616, +13,824)과 나머지 1,024 B다. Release는 공용 템플릿이라 VYaml 쓰기 코드가 처음부터 IL로만 있고 trim하지 않는다.

키트 map(`MoNoonPlayer.map.xml`) 비교:

| 항목 | 기준 | 변경 후 |
| --- | ---: | ---: |
| 생성 포매터 Serialize·SerializeMembers 코드 | 1,898,616 (398개, 102형식) | **0** |
| 생성 포매터 Serialize GCInfo | 103,679 | 0 |
| 생성 포매터 Deserialize 코드 | 213,892 | 213,892 |
| Engine.Core 소유 노드(접두사 추정) | 2,961,348 | 1,071,700 |
| VYaml 소유 노드 | 752,952 | 477,908 |
| System.Text.Json 소유 노드 | 276,737 | 235,833 |

완료 기준인 "ILC map에서 GeneratedFormatter Serialize 합이 1 MB 미만"을 충족한다(0 B). 게임 스크립트가 `ProjectAssets.Write<T>`·`GameTable.Save`처럼 참조형 `Encode<T>`를 쓰는 공개 API를 호출하면 그 게임에서는 쓰기 코드가 다시 들어간다. 그래도 동작은 올바르다(크기만 늘어남). 세 샘플은 해당하지 않는다(실행 파일 크기로 확인).

## 할당

프리팹 복제 경로(`Instantiate`, 프리팹 풀)는 바꾸지 않아 5회 교대 측정은 하지 않았다. BarrageLab 판정 실행 1회의 참고값은 Release 풀 53,872 B/프레임, AOT 풀 71,769 B/프레임으로, 시간 값과 함께 판정에 쓰지 않는다. 저장·설정·리플레이 기록은 Guid마다 36바이트 배열을 하나 할당하고, enum마다 이름 문자열을 하나 할당한다. VYaml은 컨텍스트 버퍼를 재사용했다. 둘 다 저장 시점에만 생기며 프레임 경로에는 없다.

## 검사 (macOS arm64)

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| InventoryLab 리플레이 저장(`./monoon test replay`, 171 B `EED41DEF…`) | – | PASS | PASS |
| KeyRoom 월드 체크포인트(`ci.py osx-arm64`, 각 11개, 획득 후 612 B `0becae59…`, Scene Play 슬롯 1,129 B 왕복) | – | PASS | PASS |
| KeyRoom.Rebinding(`player.settings` 12개 항목, rebind `05485eb7…`, restart·restored `622b960d…`) | PASS(같은 해시) | PASS | PASS |
| PlatformerLab Joints trace `2d523cd0…`(세 trace 80,853 B 일치) | PASS | PASS | PASS |
| BarrageLab.PrefabPools 명중 2,338, 재사용 6,721/7,200 | – | PASS | PASS |
| InventoryLab.Crashes(모드 켜기·`.crash` 기록·`QuestJournal.cs:35` 해석·모드 끈 뒤 저장 일치) | – | PASS | PASS |
| KeyRoom.PrefabStructure(에디터 프리팹 병합, `PrefabMerge` 사용) | PASS | – | – |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·HarfBuzzSharp) | – | 없음 | 없음 |
| `./monoon test`(731개 PASS, 새 기록기 비교 22개 포함) | PASS | | |

첫 KeyRoom 월드 체크포인트 실행에서 AOT 키트를 컴파일하던 Roslyn이 `MissingMethodException`(`CSharpSyntaxNodeCache.GetNodeFlags`)으로 중단됐다. 코드를 바꾸지 않고 다시 실행하자 통과했다. 같은 시각 다른 worktree의 빌드와 컴파일러 서버가 겹친 일시적 오류로 보고 원인은 확인하지 않았다.

Windows·Linux는 실행하지 않았다(Windows 보류, x64는 코디네이터가 나중에 실행). 기능 스위치와 기록기는 플랫폼 공통 코드라서 x64 AOT에서도 같은 크기 감소가 예상되지만 측정하지 않았다.

## System.Text.Json (결정 6, 구현하지 않음)

변경 후 키트에 남은 System.Text.Json 소유 노드는 235,833 B(접두사 추정)이고, `GameJson` 소스 생성 메타데이터가 12,084 B 더 있다. 두 부분으로 나뉜다.

- **값 표현(약 80 KB: `Utf8JsonReader`·`Utf8JsonWriter`·`JsonDocument`·`JsonElement`).** `GameData.Values`, `ScriptComponent.Values`, `TableRow.Cells`가 공개 API에서 `JsonElement`를 쓰고, `ScriptValueFormatter`는 YAML을 `Utf8JsonWriter` → `JsonDocument`로 읽는다. 이 부분을 없애려면 MoNoon 고유 값 형식으로 바꾸고, 스크립트 API(`GameData`·스크립트 필드), 저장 형식 검사, 에디터 프리팹 병합(`JsonNode`)을 함께 고쳐야 한다(L, 공개 API 변경).
- **직렬화기 계층(약 110–150 KB: `JsonTypeInfo`·`JsonPropertyInfo`·변환기·`JsonSerializer`).** 런타임에서 이 계층을 쓰는 곳은 `GameData.Set`(bool·string·double), `GameBehaviour` 필드 쓰기·`ReadOverride<T>`(`GameJson.GetTypeInfo`), `WorldCheckpoints`의 `SerializeToElement`뿐이다. 이 호출을 `Utf8JsonWriter`/`JsonElement` 직접 읽기로 바꾸면 `JsonElement`는 유지하면서 이 계층을 뺄 수 있다(S–M, 추정 −0.1 MB 안팎, 미측정).

판단: 전체 제거(약 −0.25–0.3 MB 추정)는 공개 API 변경 대비 이득이 작다. 하려면 직렬화기 계층만 빼는 단계가 비용 대비 낫다. Release 템플릿에는 `System.Text.Json.dll`이 공용으로 남으므로 Release 크기는 어느 경우에도 줄지 않는다.

## 재현

```sh
# 키트 map·DGML (스크립트 없음)
dotnet publish engine/src/Engine.Player -c Release -r osx-arm64 --self-contained true -p:PublishAot=true \
  -p:OptimizationPreference=Size -p:IlcGenerateMapFile=true -p:IlcGenerateDgmlFile=true -o "$out/bin" --artifacts-path "$out/a"
# 기록기 비교만
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --player-yaml-only
```

DGML에서 남은 쓰기 코드의 원인을 찾을 때는 `VirtualMethodUse [VYaml]VYaml.Serialization.IYamlFormatter`1<…>.Serialize` 노드 중 이유가 `Canonically equivalent virtual method use`가 아닌 것의 선행 노드를 따라가면 된다. 거기서 `ProjectYaml.Encode<T>`의 호출자가 나온다. scan 그래프(`*.scan.dgml.xml`)를 봐야 한다. codegen 그래프는 scan 단계의 가상 메서드 사용 결과를 그대로 물려받는다.
