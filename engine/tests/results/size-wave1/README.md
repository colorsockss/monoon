# 배포 용량 1차 묶음 (T1·T6·T7·B7)

2026-11-24 · macOS 27.0.1 · Apple Silicon arm64 · .NET SDK 10.0.101. 기준 커밋 `d523ec7`에서 [다음 작업 계획](../../../../docs/plans/next-work/README.md)의 1차 묶음 중 네 항목을 차례로 적용했다. 항목마다 커밋 하나다. 측정 중 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음). 시간은 재지 않았고 크기와 결정성 검사만 했다.

- 크기: 각 단계에서 `./engine/scripts/build-players.sh`로 세 템플릿과 AOT 키트를 다시 만든 뒤 `./monoon build samples/<샘플> <release|native-aot> --profile distribution`으로 내보냈다. `.app` 합계는 심볼릭 링크를 0바이트로 센 일반 파일 크기 합이다. 실행 파일은 `Contents/MacOS/MoNoonPlayer`다.
- 결정성 검사(단계마다 실행): [KeyRoom 월드 체크포인트](../../KeyRoom.WorldCheckpoints/README.md) `ci.py osx-arm64`, InventoryLab `./monoon test replay`(저장 171바이트, SHA-256 `EED41DEF…`), [PlatformerLab Joints](../../PlatformerLab.Joints/README.md) Release·AOT trace(`2d523cd0…`), [InventoryLab.Crashes](../../InventoryLab.Crashes/check.py)(`QuestJournal.cs:35` 해석), 금지 파일 검색, `./monoon test`.
- Windows·Linux: 이 Mac에서 템플릿과 KeyRoom Release를 만들어 파일 목록만 확인했다. 실행 확인은 다음 x64 회귀에서 한다.

## 요약 (osx-arm64, Distribution `.app`, 바이트)

| 산출물 | 기준 `d523ec7` | T1 | T6 | T7 | B7(최종) | 합계 차이 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| KeyRoom Release | 99,847,104 | 93,017,120 | 93,017,120 | 92,670,496 | 89,543,912 | −10,303,192 (−10.3%) |
| PlatformerLab Release | 97,710,426 | 90,880,442 | 90,880,442 | 90,533,818 | 87,407,234 | −10,303,192 (−10.5%) |
| InventoryLab Release | 151,066,518 | 144,236,534 | 144,236,534 | 143,889,910 | 140,763,326 | −10,303,192 (−6.8%) |
| KeyRoom Native AOT | 20,736,615 | 20,193,367 | 19,447,383 | 18,335,303 | 17,690,063 | −3,046,552 (−14.7%) |
| PlatformerLab Native AOT | 18,412,225 | 17,868,977 | 17,139,489 | 15,911,057 | 15,265,817 | −3,146,408 (−17.1%) |
| InventoryLab Native AOT | 71,752,333 | 71,209,085 | 70,479,597 | 69,251,149 | 68,605,909 | −3,146,424 (−4.4%) |
| KeyRoom AOT 실행 파일 | 12,410,368 | 12,410,368 | 11,664,384 | 10,552,304 | 10,552,304 | −1,858,064 (−15.0%) |

InventoryLab은 의도된 대용량 번들(51.3 MB)이 비율을 낮춘다. 결정성 검사는 네 단계 모두 기준값과 같았다.

## T1(B3): 네이티브 라이브러리 한 벌, 디버거 네이티브 제외

### 바꾼 것

- [Directory.Build.targets](../../../Directory.Build.targets): `Ahwoo.BRUTAL.Box2D.Native`의 버전 사본(`libbox2d.3.dylib`, `libbox2d.3.1.1.dylib`, `libbox2d.so.3`, `libbox2d.so.3.1.1`)과 Linux `libSDL3.so.0.4.12`를 빌드·publish 출력에서 뺀다. SDL_ttf 사본을 빼던 대상에 조건을 더했다. 에디터 출력과 AOT 키트도 같은 파일을 쓴다.
  - `Brutal.Box2D`는 `box2d`, `Brutal.Sdl`은 `SDL3`으로 P/Invoke한다. 런타임은 `libbox2d.dylib`·`libbox2d.so`·`libSDL3.so`를 찾는다. Linux `libSDL3_ttf.so`·`libSDL3_mixer.so`의 `NEEDED`는 `libSDL3.so.0`이고, `libbox2d.so`를 링크하는 파일은 없다(`objdump -p`). 그래서 `libSDL3.so`와 `libSDL3.so.0`은 남긴다.
- [GameBuild.cs](../../../src/Engine.Authoring/GameBuild.cs):
  - 배포용(Distribution) Release는 템플릿에서 `mscordaccore*`, `mscordbi`, `createdump`, `clrgc`, `clrgcexp`, Linux `libcoreclrtraceptprovider.so`를 복사하지 않는다. 개발용은 디버거를 붙이도록 그대로 둔다. 크래시 보고서는 관리 코드([CrashDiagnostics.cs](../../../src/Engine.Player/CrashDiagnostics.cs))가 만들고 덤프를 쓰지 않는다. 대체 GC는 `DOTNET_GCName`을 줄 때만 읽힌다. LTTng 공급자는 CoreCLR가 있으면 열어 보는 선택 파일이다.
  - macOS만 하던 SDL 링크 공유를 Linux로 넓혔다. 배포용에서 `libSDL3.so`를 `libSDL3.so.0`의 상대 심볼릭 링크로 바꾼다. Linux `.tar.gz`는 링크를 `SymbolicLink` 항목으로 담는다(확인함). Windows 호스트에서는 링크를 만들지 않고 두 파일을 둔다(기존 macOS 규칙과 같음).
- [Engine.Checks](../../Engine.Checks/Program.cs): `./monoon test`가 세 RID로 내보낸 KeyRoom에서 `Game/` 밖에 내용이 같은 파일이 없고, 위 디버거 네이티브가 없고, macOS·Linux의 SDL 로더 이름이 링크인지 단언한다.
- 템플릿에는 `libSDL3.so`·`libSDL3.so.0` 두 파일이 남는다. 내보내기가 템플릿 파일을 복사(링크를 따라감)한 뒤 배포용에서만 링크로 바꾸고, Windows 에디터도 같은 템플릿을 읽기 때문이다. Windows의 `Microsoft.DiaSymReader.Native.amd64.dll`·`msquic.dll`·`clretwrc.dll`은 이번 범위 밖이라 그대로 뒀다.

### 크기 (osx-arm64, Distribution)

| 산출물 | 기준 `d523ec7` | T1 | 차이 |
| --- | ---: | ---: | ---: |
| KeyRoom Release `.app` | 99,847,104 | 93,017,120 | −6,829,984 |
| KeyRoom Native AOT `.app` | 20,736,615 | 20,193,367 | −543,248 |
| PlatformerLab Release `.app` | 97,710,426 | 90,880,442 | −6,829,984 |
| PlatformerLab Native AOT `.app` | 18,412,225 | 17,868,977 | −543,248 |
| InventoryLab Release `.app` | 151,066,518 | 144,236,534 | −6,829,984 |
| InventoryLab Native AOT `.app` | 71,752,333 | 71,209,085 | −543,248 |

Release 차이 = Box2D 사본 543,248 + 디버거·GC 네이티브 6,286,736(`libmscordaccore` 2,407,424, `libmscordbi` 1,786,416, `libclrgcexp` 982,896, `libclrgc` 903,808, `createdump` 206,192). 실행 파일 크기는 바뀌지 않았다.

### 템플릿과 x64 내보내기 (이 Mac에서 빌드)

| 대상 | 기준 | T1 | 차이 | 빠진 것 |
| --- | ---: | ---: | ---: | --- |
| osx-arm64 템플릿 | 101,264,847 | 100,721,561 | −543,286 | Box2D 사본 2개 |
| linux-x64 템플릿 | 104,845,484 | 99,882,018 | −4,963,466 | `libSDL3.so.0.4.12` 4,211,016, Box2D 사본 2개 752,400 |
| win-x64 템플릿 | 91,478,230 | 91,478,230 | 0 | Windows는 Box2D·SDL이 한 파일이다 |
| KeyRoom linux-x64 Release(배포용) | 106,262,585* | 90,101,129 | −16,161,456 | 위 템플릿 차이 + `libSDL3.so` 링크화 4,211,016 + 런타임 네이티브 6,987,024 |
| KeyRoom win-x64 Release(배포용) | 92,895,451* | 87,502,155 | −5,393,296 | `mscordaccore` 2개 2,703,024, `mscordbi` 1,235,288, `clrgcexp` 709,432, `clrgc` 673,552, `createdump.exe` 72,000 |

\* 기준 x64 내보내기는 다시 빌드하지 않았다. T1 결과에 빠진 파일의 템플릿 크기를 더한 값이다. KeyRoom linux-x64 `.tar.gz`는 39,617,314바이트, win-x64 `.zip`은 39,430,217바이트다. 두 x64 배포본은 **이 Mac에서 빌드하고 파일만 확인했다. 실행 확인은 다음 x64 회귀에서 한다.**

### 검사

| 검사 | Release | Native AOT |
| --- | --- | --- |
| KeyRoom 월드 체크포인트 | PASS | PASS |
| InventoryLab 리플레이 저장 일치 | PASS | PASS |
| PlatformerLab Joints trace `2d523cd0891660063e65…` | PASS | PASS |
| InventoryLab.Crashes 해석 | PASS | PASS |
| 금지 파일 | 없음 | 없음 |
| `./monoon test` | PASS(새 단언 3개 포함) | |

## T6(B2): 런타임 Regex 제거

### 바꾼 것

- [ProjectAssets.cs](../../../src/Engine.Core/ProjectAssets.cs)의 에셋 이름 검사와 [ScriptLibrary.cs](../../../src/Engine.Core/ScriptLibrary.cs)의 스크립트 이름 검사가 쓰던 `Regex.IsMatch`를 `ProjectAssets.WindowsDeviceName`으로 바꿨다. 첫 `.` 앞(없으면 이름 전체)이 `CON`·`PRN`·`AUX`·`NUL`이거나 `COM`·`LPT` 뒤에 ASCII `1`–`9` 한 자리면 예약 이름이다. 대소문자 비교는 `Ascii.EqualsIgnoreCase`다. 이전 패턴의 글자에는 문화권별 대소문자 예외(`I`, `K`, `S`)가 없고, 스크립트 이름에는 `.`이 들어갈 수 없어 두 패턴을 함수 하나로 처리한다.
- 정규식 `$`는 끝의 `\n` 앞에서도 맞지만, 두 검사 모두 제어 문자(에셋)나 글자·숫자·`_` 외 문자(스크립트)를 먼저 거부하므로 결과가 같다.
- [ReservedNameChecks.cs](../../Engine.Checks/ReservedNameChecks.cs): 30개 줄기 × 9개 꼬리(270개 이름, `com1`, `LPT9.txt`, `CONX`, `COM10`, `COM0`, `A.CON`, 위첨자·아랍 숫자, 전각 `ＣＯＮ` 포함)에서 새 검사와 이전 두 정규식의 판정이 같은지, `ProjectAssets.ValidName`·`ScriptLibrary.ValidName` 전체 결과가 이전 식과 같은지 확인한다. `--names-only`로 따로 실행할 수 있다.

### 크기 (osx-arm64, Distribution)

| 산출물 | T1 | T6 | 차이 |
| --- | ---: | ---: | ---: |
| KeyRoom Native AOT 실행 파일 | 12,410,368 | 11,664,384 | −745,984 |
| PlatformerLab Native AOT 실행 파일 | 12,211,392 | 11,481,904 | −729,488 |
| InventoryLab Native AOT 실행 파일 | 12,211,280 | 11,481,792 | −729,488 |
| KeyRoom Native AOT `.app` | 20,193,367 | 19,447,383 | −745,984 |
| PlatformerLab Native AOT `.app` | 17,868,977 | 17,139,489 | −729,488 |
| InventoryLab Native AOT `.app` | 71,209,085 | 70,479,597 | −729,488 |
| Release `.app` 세 개 | 93,017,120 / 90,880,442 / 144,236,534 | 같음 | 0 |

AOT 키트만 publish(게임 스크립트 없음, `IlcGenerateMapFile=true`)한 map에서 `System_Text_RegularExpressions` 기호가 5,748개(노드 335,893바이트)에서 **0개**가 됐다. 실행 파일은 11,697,392 → 10,951,344(−746,048)이다. Release는 `System.Text.RegularExpressions.dll`이 공용 템플릿에 그대로 있어 크기가 같다(Engine.Core.dll도 파일 정렬 단위 안에서 바뀌어 크기가 같다). 조사 추정치(−729 KB)와 일치한다.

### 검사

| 검사 | Release | Native AOT |
| --- | --- | --- |
| KeyRoom 월드 체크포인트 | PASS | PASS |
| InventoryLab 리플레이 저장 일치(`EED41DEF…`) | PASS | PASS |
| PlatformerLab Joints trace `2d523cd0…` | PASS | PASS |
| InventoryLab.Crashes 해석 | PASS | PASS |
| 금지 파일 | 없음 | 없음 |
| `./monoon test`(이름 검사 270개 포함) | PASS | |

## T7: 레거시 JSON 메타데이터를 Engine.Authoring으로 이동

### 바꾼 것

- [GameJson.cs](../../../src/Engine.Core/GameJson.cs)에서 `GameSettings`, `SceneDocument`, `SpriteClip`, `AssetEntry[]`를 뺐다. 이 형식은 YAML 이전 프로젝트를 바꾸는 [LegacyProject](../../../src/Engine.Authoring/LegacyProject.cs)만 JSON으로 읽는다. 새 [LegacyJson](../../../src/Engine.Authoring/LegacyJson.cs) 컨텍스트가 같은 옵션(`JsonSerializerDefaults.Web`, 메타데이터 모드)으로 이 형식과 `int`를 담는다. Engine.Authoring은 `InternalsVisibleTo`로 `SceneObject.ParentId`·`Components`의 internal setter를 그대로 쓴다(소스 생성 경고 없음).
- 런타임 `GameJson`에는 스크립트 필드 값(`ReadOverride<T>`: `int`, `float`, `double`, `bool`, `string`, `Guid`, 에셋 참조 5종), `Guid[]` 목록, 프리팹 병합용 `JsonNode`, `GameData` 값만 남는다. 이전에는 `int`·`float`·`bool`·`string`·`Guid`가 `SceneDocument` 멤버를 통해 간접으로 생성됐으므로 이번에 직접 적었다. 빠뜨리면 `GetTypeInfo`가 null을 돌려 필드 읽기가 실패한다.
- `GameBehaviour.ReadOverride`의 `GameJson.Default.GetTypeInfo(typeof(T))`가 컨텍스트 전체를 루트로 잡아, 장면·설정 형식의 다형 JSON 메타데이터(컴포넌트 40종)와 그 System.Text.Json 변환기까지 플레이어에 남아 있었다.
- 현재 형식 프로젝트는 처음부터 YAML로 읽으므로 플레이어 동작은 바뀌지 않는다. 플레이어가 레거시 JSON 프로젝트를 직접 열지 않는 것은 이전과 같다(`./monoon migrate` 또는 에디터에서 변환).

### 크기 (osx-arm64, Distribution)

| 산출물 | T6 | T7 | 차이 |
| --- | ---: | ---: | ---: |
| KeyRoom Native AOT 실행 파일 | 11,664,384 | 10,552,304 | −1,112,080 |
| PlatformerLab Native AOT 실행 파일 | 11,481,904 | 10,253,472 | −1,228,432 |
| InventoryLab Native AOT 실행 파일 | 11,481,792 | 10,253,344 | −1,228,448 |
| KeyRoom Native AOT `.app` | 19,447,383 | 18,335,303 | −1,112,080 |
| PlatformerLab Native AOT `.app` | 17,139,489 | 15,911,057 | −1,228,432 |
| InventoryLab Native AOT `.app` | 70,479,597 | 69,251,149 | −1,228,448 |
| KeyRoom Release `.app` | 93,017,120 | 92,670,496 | −346,624 |
| PlatformerLab Release `.app` | 90,880,442 | 90,533,818 | −346,624 |
| InventoryLab Release `.app` | 144,236,534 | 143,889,910 | −346,624 |

Release 차이는 `Engine.Core.dll`(1,660,416 → 1,313,792)이다. AOT 키트 map에서 `GameJson` 노드는 271,815 → 12,204바이트(`PropInit` 181,590 → 3,200), System.Text.Json 소유 노드는 917,426 → 276,737바이트, Engine.Core는 3,226,779 → 2,961,416바이트가 됐다. 키트 실행 파일은 10,951,344 → 9,938,416(−1,012,928)이다. 조사 추정치(−0.15–0.25 MB)보다 훨씬 크다. 추정은 `GameJson` 자체 코드만 셌고, 다형 컴포넌트 메타데이터가 끌어오던 System.Text.Json 변환기 코드는 세지 않았기 때문이다.

### 검사

| 검사 | Release | Native AOT |
| --- | --- | --- |
| KeyRoom 월드 체크포인트(스크립트 필드 `ReadOverride` 사용) | PASS | PASS |
| InventoryLab 리플레이 저장 일치(`EED41DEF…`) | PASS | PASS |
| PlatformerLab Joints trace `2d523cd0…` | PASS | PASS |
| InventoryLab.Crashes 해석 | PASS | PASS |
| 금지 파일 | 없음 | 없음 |
| `./monoon test`: v1–v5 JSON 장면 변환, `.scene.json`·`.prefab.json` 이중 확장자 변환, 변환 충돌, 작업 공간 백업 | PASS | |
| 에디터 컴파일(`dotnet build engine/src/Engine.Editor`) | 오류 0 | |

## B7: macOS dylib 로컬 심볼 정리와 재서명

### 바꾼 것

- [GameBuild.cs](../../../src/Engine.Authoring/GameBuild.cs) `StripMacLibraries`: macOS 호스트에서 osx-arm64 배포용(Distribution)을 내보낼 때 `Contents/MacOS`의 모든 dylib(링크 제외)에 `strip -x`를 실행하고 `codesign --force --sign -`로 다시 서명한다. Release와 Native AOT 모두 해당한다. 개발용은 네이티브 디버깅을 위해 그대로 둔다.
  - 대상: BRUTAL의 `libSDL3.0.dylib`, `libSDL3_ttf`, `libSDL3_mixer`, `libbox2d`(원래 링커 애드혹 서명)와 Release의 .NET 런타임 dylib(`libcoreclr`, `libclrjit`, `libhostfxr`, `libhostpolicy`, `libSystem.*.Native` 5개, 원래 Microsoft Developer ID 서명). 런타임 dylib도 로컬 심볼이 남아 있어 조사 추정(BRUTAL 네 개만 617 KB)보다 Release 효과가 크다.
  - 서명이 애드혹으로 바뀌어도 동작은 같다. 패키지 없이 내보낸 앱은 라이브러리 검증을 하지 않는 애드혹 플레이어가 읽는다. `--package on`은 기존대로 안쪽 파일을 설정한 인증서(없으면 애드혹 + `disable-library-validation`)로 다시 서명하므로 정리 → 서명 순서가 유지된다.
  - `MoNoonPlayer` 실행 파일과 `MoNoonPlayer.dSYM`은 건드리지 않는다. dSYM은 기존대로 `Symbols/…/native-aot/<빌드 ID>/`에 옮겨지고, 실행 파일과 dSYM의 UUID 검사도 그대로 통과한다. 지운 dylib 로컬 심볼은 보관하지 않는다(원본은 NuGet 패키지에 있다).
  - Xcode 명령줄 도구가 없어 `strip`이 실패하면 정리를 건너뛰고 원래 서명된 dylib을 넣는다(코드 주석의 `shortcut`). 크기 회귀 기준(B4)이 생기면 그 검사가 잡는다.
- [Engine.Checks](../../Engine.Checks/Program.cs): `./monoon test`의 osx-arm64 KeyRoom 내보내기에서 모든 dylib이 `codesign --verify --strict`를 통과하고 템플릿 원본보다 작은지 확인한다.

### 크기 (osx-arm64, Distribution, 패키지 없음)

| 산출물 | T7 | B7 | 차이 |
| --- | ---: | ---: | ---: |
| KeyRoom Release `.app` | 92,670,496 | 89,543,912 | −3,126,584 |
| PlatformerLab Release `.app` | 90,533,818 | 87,407,234 | −3,126,584 |
| InventoryLab Release `.app` | 143,889,910 | 140,763,326 | −3,126,584 |
| KeyRoom Native AOT `.app` | 18,335,303 | 17,690,063 | −645,240 |
| PlatformerLab Native AOT `.app` | 15,911,057 | 15,265,817 | −645,240 |
| InventoryLab Native AOT `.app` | 69,251,149 | 68,605,909 | −645,240 |

dylib별(재서명 포함): `libSDL3.0` −360,688, `libSDL3_ttf` −256,088, `libSDL3_mixer` −15,048, `libbox2d` −13,416(AOT 합계 −645,240), `libcoreclr` −1,496,400, `libclrjit` −515,072, `libhostpolicy` −127,984, `libhostfxr` −118,608, `libSystem.*.Native` 5개 −223,280. 실행 파일 크기는 바뀌지 않는다.

### `--package on` (애드혹 서명, 공증 없음)

| 샘플 | 빌드 | 서명한 `.app` | ZIP | DMG | `codesign --verify --deep --strict` |
| --- | --- | ---: | ---: | ---: | --- |
| KeyRoom | Release | 89,609,903 | 34,312,913 | 39,606,453 | PASS |
| KeyRoom | Native AOT | 17,644,321 | 7,083,591 | 7,924,384 | PASS |
| PlatformerLab | Release | 87,472,201 | 33,579,084 | 38,790,727 | PASS |
| PlatformerLab | Native AOT | 15,220,779 | 6,215,040 | 6,950,588 | PASS |
| InventoryLab | Release | 140,824,917 | 85,593,001 | 92,397,139 | PASS |
| InventoryLab | Native AOT | 68,557,495 | 58,224,307 | 59,932,140 | PASS |

패키지한 InventoryLab으로도 `./monoon test replay`(저장 `EED41DEF…`)와 InventoryLab.Crashes(`QuestJournal.cs:35`)가 통과했다. 조사 당시 KeyRoom ZIP은 Release 37,654,621, AOT 8,237,923이었다(측정 방식이 `ditto -c -k`로 같다).

### 검사

| 검사 | Release | Native AOT |
| --- | --- | --- |
| KeyRoom 월드 체크포인트 | PASS | PASS |
| InventoryLab 리플레이 저장 일치(`EED41DEF…`), 패키지 전·후 | PASS | PASS |
| PlatformerLab Joints trace `2d523cd0…`(Box2D dylib 정리 후) | PASS | PASS |
| InventoryLab.Crashes 해석, 패키지 전·후 | PASS | PASS |
| 금지 파일 | 없음 | 없음 |
| `./monoon test`(정리·재서명 단언 포함) | PASS | |
| AudioLab Ogg PCM 첫 네 바퀴 `ced6c800e697…`(정리한 SDL3·SDL_mixer) | PASS | PASS |

AudioLab은 [AudioLab.Vorbis](../../AudioLab.Vorbis/README.md) `ci.py`를 먼저 실행했지만 이 세션에 키보드 이벤트 권한이 없어(`No keyboard event permission`, `mac-input focus` 종료 코드 2) F 입력 단계에서 멈췄다. 코드 문제가 아니라 권한 문제다. 그래서 같은 환경 변수(SDL 디스크 오디오 48 kHz 스테레오 float32, `MONOON_AUDIO_CAPTURE`)로 배포용 Release·AOT 창을 GUI 잠금 안에서 열고, 네 바퀴(230,400프레임)가 쌓이면 종료해 첫 네 바퀴 SHA-256을 [기준](../audio-mixer/playback.csv)의 release·aot 행과 비교했다. 기준 행도 같은 이유로 F 없이 기록됐다. 페이드 아웃 길이는 이번에 확인하지 않았다.

## 확인하지 못한 것

- Windows·Linux x64 실행: T1의 x64 템플릿과 KeyRoom 배포본(`libSDL3.so` 링크, 런타임 네이티브 제외)은 이 Mac에서 빌드하고 파일 목록·`.tar.gz` 항목만 확인했다. 게스트 실행과 Box2D 로딩, deps.json에 남은 제외 파일 항목이 Windows·Linux 호스트에서 문제를 일으키지 않는지는 다음 x64 회귀에서 확인한다.
- Linux AOT(게스트에서만 빌드)와 Linux `strip --strip-unneeded`(B7의 Linux 부분)는 이번 범위가 아니다.
- 에디터 Scene Play·KeyRoom.EditorBuild(에디터와 CLI 빌드의 파일별 SHA 비교)는 실행하지 않았다. 에디터는 컴파일만 확인했다. 두 빌드 경로 모두 같은 `GameBuild`를 거치고 `strip`·애드혹 서명은 결정적이라 영향이 없을 것으로 보지만 확인하지 않았다.
- Developer ID 서명·공증: 인증서가 없어 애드혹으로만 확인했다.
- 시작 시간·메모리: 재지 않았다.
