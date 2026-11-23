# x64 Native AOT 결정론

2026-11-23. 기준 커밋 `4c5816a`(wip/phase1)에서 [11장 Linux 검증](../chapter11-linux-x64/README.md)이 남긴 두 문제를 고쳤다.

1. Linux x64에서 Release(JIT)와 Scene Play(JIT)의 PlatformerLab.Joints·Paths trace는 같았지만 Native AOT만 마지막 자리가 달랐다.
2. [InventoryLab 게스트 실행기](../../InventoryLab.Replay/README.md)(`build_guest.py`)가 만든 Native AOT 플레이어가 `System.IO.FileNotFoundException: Could not find file 'Brutal.Sdl'`로 바로 종료했다.

Windows x64는 사용자 결정에 따라 이번에도 실행하지 않았다.

> 같은 날 사용자 결정으로 `Affine2D`를 "항상 합침"에서 "항상 따로"(곱셈과 덧셈을 각각 반올림)로 바꿨다. 아래 [곱셈·덧셈 분리 계산으로 변경](#곱셈덧셈-분리-계산으로-변경)이 현재 동작이다. 그 뒤의 "결과"와 "원인 1"의 "수정" 절은 처음 고친 방식(`MathF.FusedMultiplyAdd`)의 기록이다.

## 곱셈·덧셈 분리 계산으로 변경

2026-11-23, 기준 커밋 `11708ce`(wip/phase1). 처음 수정은 FMA가 없는 x64 Native AOT에서 `MathF.FusedMultiplyAdd`가 소프트웨어 fma로 돌아 느리다. 그래서 사용자 결정에 따라 곱셈과 덧셈을 따로 계산하도록 바꿨다. 모든 CPU에서 하드웨어 명령 두 개로 돌고, JIT/ILC가 둘을 FMA로 합치지 않으면 arm64·x64, JIT·AOT의 결과가 같다. 이 전제를 생성 코드로 확인했다(아래).

### 바꾼 것

- [Transform2D.cs](../../../src/Engine.Core/Transform2D.cs) 77–85행: `TransformNormal`은 `m.X * v.X + m.Y * v.Y`, `Transform`은 거기에 `m.Translation`을 더한다. `Lerp`(Vector2/3/4)는 `a * (1 - t) + b * t`다. .NET의 FMA 없는 경로(`(left * right) + addend`)와 같은 반올림이다. 처음에는 성분마다 `float`로 쓴 판(아래 표의 `unfusedScalar`)을 만들었지만 arm64에서 합친 판보다 느려서, `Matrix3x2.X`·`Y` 행 벡터를 그대로 쓰는 `Vector2` 연산으로 바꿨다. 성분별 반올림은 같다. 호출부 65곳과 샘플 스크립트 4곳은 그대로다.
- [TransformChecks.cs](../../Engine.Checks/TransformChecks.cs): 합치면 값이 달라지는 입력으로 `Affine2D.Transform`·`Lerp`(Vector2/3/4)가 따로 계산한 값과 같은지 확인한다(`unfused transform`, `unfused lerp`).
- 문서: [Transform 2D](../../../../docs/manual/transform-2d.md)에 시뮬레이션 스크립트 규칙(`Vector2.Transform`·`Lerp` 대신 `Affine2D`)을, [BRUTAL 기능 정리](../../../../docs/brutal.md)의 유지 결정에 Brutal.Numerics를 시뮬레이션 계산에 쓰지 않는 이유를 적었다.

### 생성 코드 확인

[프로브](codegen-probe.cs.txt)([프로젝트](codegen-probe.csproj.txt))는 `Engine.Core`를 참조하고, 인라인하지 않는 래퍼 `CallTransform`·`CallTransformNormal`·`CallLerp2/3/4`와 반복문 `LoopUnfused`(Affine2D), `LoopFused`(이전 판 복사), `LoopNumerics`(`Vector2.Transform`·`Lerp`)를 둔다. JIT은 `DOTNET_TieredCompilation=0 DOTNET_JitDisasm`, Native AOT는 ILC `--codegenopt:JitDisasm=*`(`--parallelism:1`, `Engine.Core` 전체를 `TrimmerRootAssembly`로 컴파일)로 목록을 뽑아 FMA 명령(arm64 `fmadd/fmsub/fnmadd/fnmsub/fmla/fmls`, x64 `vfmadd…/vfmsub…/vfnmadd…`)을 셌다. 게스트에서는 `linux.sh codegen`으로 같은 프로브를 빌드했고, 집계는 [codegen_report.py](scripts/codegen_report.py)다.

| 경로 | 목록에 나온 메서드 | FMA 명령이 있는 메서드 |
| --- | --- | --- |
| macOS arm64 JIT | 11개(프로브) | `LoopFused` 4, `LoopNumerics` 2. Affine2D 래퍼·`LoopUnfused`는 `fmul`+`fadd`만 |
| macOS arm64 Native AOT | 28,732개, 그중 `Engine.Core` 11,017개 | `LoopFused` 4, `LoopNumerics` 2, `Main` 2(바이트 비교용으로 이전 판을 인라인). `Engine.Core` 메서드는 0개 |
| Linux x64 게스트 JIT(`Fma.IsSupported=True`) | 7개(프로브 래퍼) | 없음. `vmulps`+`vaddps`. FMA가 있어도 합치지 않는다 |
| Linux x64 게스트 Native AOT(기본 명령어 집합) | 28,877개, 그중 `Engine.Core` 11,017개 | 없음. `mulps`+`addps`. 소프트웨어 fma(`RuntimeImports.fmaf`) 호출은 이전 판 복사(`LoopFused` 4, `Main` 2)에만 있다 |

arm64 `CallTransform`(JIT·AOT 같음): `ldp d16, d17, [x0]` → `fmul v16.4s, v16.4s, v0.s[0]` → `fmul v17.4s, v17.4s, v1.s[0]` → `fadd v16.4s, v16.4s, v17.4s` → `fadd v16.4s, v16.4s, v18.4s`. [arm64 목록](codegen-arm64.txt) x64 AOT `CallTransform`: `mulps` 두 번 → `addps` 두 번. [x64 목록](codegen-x64.txt)

RyuJIT(ILC도 같은 JIT)은 부동소수 곱셈과 덧셈을 자동으로 합치지 않는다. FMA는 `MathF.FusedMultiplyAdd`, `Fma.MultiplyAdd`, `MultiplyAddEstimate`처럼 명시적으로 부를 때만 나온다. 위 결과도 그렇다. 그래서 막기 위한 별도 장치(volatile, `NoInlining` 등)는 넣지 않았다. x64 JIT 목록은 프로브 래퍼뿐이고 `Engine.Core` 전체의 x64 JIT 코드는 뽑지 않았다. 대신 아래 게스트 실행에서 FMA가 있는 JIT 경로(Release, Scene Play)의 trace가 AOT와 바이트 단위로 같았다.

### 비용(macOS arm64)

[측정 출력](bench-arm64.txt). 4,096개 독립 입력에 `Transform` + `Lerp`를 한 번씩, 200회 반복을 31번 번갈아 쟀다. 측정 중 QEMU 게스트는 꺼져 있었다.

| 변형 | JIT 중앙값(범위), ns | Native AOT 중앙값(범위), ns |
| --- | --- | --- |
| Affine2D 분리(이번 변경, `Vector2` 연산) | 0.887 (0.876–0.919) | 0.887 (0.871–0.895) |
| 이전 판(`MathF.FusedMultiplyAdd`) | 1.460 (1.444–1.480) | 1.459 (1.441–1.476) |
| 성분별 분리(`unfusedScalar`, 채택 안 함) | 1.712 (1.693–1.725) | 1.710 (1.655–1.723) |
| `Vector2.Transform`·`Lerp`(참고, 비결정적) | 0.824 (0.814–0.846) | 0.824 (0.814–0.837) |

- 이번 변경은 arm64에서 이전 판보다 약 39% 빠르다. 두 중앙값 차이(0.57ns)가 두 범위를 합친 값보다 크다. 원인은 연산 방식보다 코드 모양이다. 이전 판은 성분마다 스칼라 `fmadd`를 하고 결과를 다시 벡터로 모았다. 이번 판은 행 벡터를 한 번에 읽어 두 성분을 함께 계산한다.
- `Vector2.Transform`보다는 약 7% 느리다(행 벡터 `fmla` 하나 대신 `fmul`+`fadd`). 이 차이가 결정론의 비용이다.
- x64 하드웨어에서는 재지 않았다. 게스트 TCG 시간은 의미가 없다. 이론상 x64에서도 SSE `mulps`/`addps`로 돌고 소프트웨어 fma 호출이 없다.
- 배포본 크기: 정적 함수 다섯 개의 본문만 바뀌었다. 새 의존성은 없다.

### 다시 기록한 기준값(macOS arm64)

모든 항목을 Scene Play·Release·Native AOT(일부는 Release·AOT)에서 실행해 경로끼리 바이트 단위로 같은 것을 확인한 뒤 기록했다. [실행 스크립트](scripts/macgates.sh) · [추가 실행 스크립트](scripts/macgates-extra.sh) · [전체 출력](mac-gates-unfused.txt)

| 기준값 | 이전 | 이번 | 경로 | 이유 |
| --- | --- | --- | --- | --- |
| PlatformerLab.Joints 300틱 trace | `a00b9e4b…`, 최종 X 738.9794 | **`2d523cd0…`**, 최종 X 738.9792. 225행 다름, 첫 차이 63틱, 최대 0.001 | 세 경로 같음 | 받침 운반(Rigidbody2D)의 역변환·변환이 한 번 더 반올림된다. [기록](../physics-box2d/osx-arm64/joints-release.tsv) 교체 |
| PlatformerLab.Paths 400틱 trace | `1a143d56…` | **`e37de6c0…`**. 107행 다름, 첫 차이 89틱, 최대 0.0002px | 세 경로 같음 | 경로 추종(`Lines2D`)의 변환·보간. 수정 전 Linux x64 AOT(FMA 없음) 값과 바이트 단위로 같다. [기록](../lines-paths/osx-arm64/paths-trace.tsv)·`paths-result.json`·`paths-compare.txt` 교체 |
| PlatformerLab.TileCollision 380틱 | `f30ed686…` | 같음 | 세 경로 같음 | – |
| BarrageLab.Lines 480행 | `6969a524…` | 같음 | 세 경로 같음 | – |
| KeyRoom.ScriptWaits trace | `98d74c78…` | 같음 | 세 경로 같음 | – |
| InventoryLab.RichText `reveal.tsv` | `76118c09…` | 같음(캡처 7장도 세 경로 같음) | 세 경로 같음 | – |
| SeedLab.GridNavigation `result.json` | `d80dc873…` | 같음 | 세 경로 같음 | – |
| SeedLab.TileTerrain trace·`result.json` | `c747efb9…`·`ae5d49b4…` | 같음 | 세 경로 같음 | – |
| CameraLab 효과 기록(헤드리스 `replay.py`) | `d56a1838…` | 같음 | Release·AOT 같음 | – |
| CollisionLab.Spatial 1천·1만 개 | `32FA7AB7…`·`B5C817DB…` | 같음 | Release·AOT | – |
| BarrageLab.PrefabPools | 명중 2,338, 재사용 6,721/7,200 | 같음 | Release·AOT | – |
| KeyRoom.WorldCheckpoints(`ci.py`) | 기준 슬롯 | 같음(22개 PASS) | Release·AOT | – |
| InventoryLab `build_guest.py` | 171바이트 `eed41def…` | 같음 | Release·AOT | – |
| `Engine.Checks` ContactChecks 순서 해시 | `E55DF236…` | 같음 | `./monoon test` | – |
| `Engine.Checks` TransformChecks | `fused transform`·`fused lerp` | `unfused transform`·`unfused lerp` | `./monoon test` | 검사 대상이 바뀜 |

- `engine/tests`의 하드코딩 해시는 위 두 `Engine.Checks` 항목, `CollisionLab.Spatial/verify.py`, `GenerationChecks`(SplitMix64·타일 해시, 변환과 무관), `build_guest.py`(v31 입력 파일 해시)뿐이다. 모두 그대로 통과했다.
- `./monoon test`: 720개 PASS, `All checks passed.`
- 바뀐 기준값을 가리키는 기능 문서([physics-box2d](../physics-box2d/README.md), [lines-paths](../lines-paths/README.md))를 고쳤다. 지난 실행을 적은 기록(11장, perf-scene, perf-physics 등)은 그때 값이라 고치지 않았다.
- Paths 캡처 5장은 세 경로끼리 같지만 기록과 다르다. 같은 실행에서 trace가 그대로인 BarrageLab.Lines 캡처도 기록과 달라서, 캡처 차이는 이번 변경이 아니라 그 사이의 그리기 변경(게임 텍스트 SDL_ttf 등) 때문이다. 캡처는 비교 기준이 아니다.
- 실행하지 않음: CameraLab의 진동·커서·Scene Play(창 도구·가상 패드가 필요한 `verify.py`). 진동 세기와 커서 픽셀은 변환 계산을 거치지 않는다. 효과 값 기록은 헤드리스로 확인했다.

### Linux x64 게스트

환경은 위 [환경](#환경)과 같다(Debian 12 amd64, QEMU TCG `-cpu Haswell-v4`, CPU 플래그 avx·avx2·fma·sse4_2). 이번 소스로 Mac에서 만든 linux-x64 템플릿·AOT 키트·CLI·에디터 패키지를 게스트에 넣고, 게스트에서 Release와 Native AOT를 빌드해 헤드리스로 재생했다. Scene Play는 Xvfb의 패키지 에디터다. 작업 디스크 여유가 623MB뿐이라 임시 폴더와 게임 출력은 RAM tmpfs(`/tmp`, 2GB)에 뒀다. 게스트는 `.test-env/work/ch11/boot.sh`로 켜고 끝난 뒤 정상 종료했다. 새 가상 디스크는 만들지 않았다. [게스트 실행기](scripts/guest.py) · [묶음](scripts/pack.py) · [실행 명령](scripts/linux.sh) · [전체 단계](linux-summary-unfused.json)

| 검사 | Release | Native AOT | Scene Play | 세 경로 | `DOTNET_EnableAVX2=0` Release | Mac 새 기준과 비교 |
| --- | --- | --- | --- | --- | --- | --- |
| PlatformerLab.Joints 300틱 | `86280b27…` | `86280b27…` | `86280b27…` | **같음** | 같음 | **다름**: 236행, 첫 차이 60틱(캐릭터 X 184.8062 / 184.8063), 최대 0.001, 최종 X 738.9791 / 738.9792. [Linux trace](linux-joints-unfused.tsv) |
| PlatformerLab.Paths 400틱 | `e37de6c0…` | `e37de6c0…` | `e37de6c0…` | **같음** | 같음 | **같음** |
| BarrageLab.Lines 480행 | `6969a524…` | `6969a524…` | `6969a524…` | **같음** | 같음 | **같음** |
| PlatformerLab.TileCollision 380틱 | `f30ed686…` | `f30ed686…` | `f30ed686…` | **같음** | 같음 | **같음** |
| InventoryLab `build_guest.py`(게스트에서 템플릿·키트·CLI를 소스로 빌드) | 171바이트 `eed41def…` | 171바이트 `eed41def…` | – | Release = AOT | – | **같음**. [출력](inventorylab-replay-unfused.txt) |

- 같은 OS 안의 세 경로는 모두 바이트 단위로 같고, `DOTNET_EnableAVX2=0`(FMA 끔) Release도 같다. 합침 판과 마찬가지로 x64 JIT·AOT 사이 차이가 없다. Paths·Lines·TileCollision은 Mac 새 기준과도 바이트 단위로 같다.
- Joints의 Linux 값 `86280b27…`은 수정 전 Linux Native AOT(FMA 없는 따로 계산) 값과 같다. Linux와 Mac이 모두 따로 계산하는데도 Joints는 60틱부터 0.0001px 단위로 다르다. 합침 판에서는 같은 비교가 84틱부터 달랐다. 이 차이는 JIT/AOT와 무관한 아키텍처 차이다(후보: Box2D 네이티브 빌드, `Matrix3x2.CreateRotation`이 부르는 OS 수학 라이브러리의 `sin`/`cos`). 원인은 이번에도 특정하지 않았다.
- 게스트 결과는 x86-64 에뮬레이션이다. 실제 x64 CPU·GPU 검증이 아니며 시간 값은 비교하지 않았다.

## 환경

| 항목 | 값 |
| --- | --- |
| 호스트 | macOS 27.0.1 (26A434), Apple M4 Max, .NET SDK 10.0.101(런타임 10.0.1) |
| 게스트 | 보존한 Debian 12 amd64(`.test-env/linux`, 작업 디스크 `keys96/linux-work.qcow2`), QEMU TCG `-cpu Haswell-v4`(CPU 플래그 fma·avx·avx2·sse4_2), 4 vCPU, Xvfb 2560×1440, SDL 더미 오디오. 게스트의 .NET SDK 10.0.101과 clang 14 |
| 빌드 종류 | Release(배포용, JIT), Native AOT(배포용), 에디터 Scene Play(JIT). 게스트의 Release·AOT는 Mac에서 만든 linux-x64 템플릿·AOT 키트·CLI로 게스트에서 빌드했다. 에디터는 Mac에서 만든 linux-x64 패키지다 |
| 패키지 | 새로 내려받은 것은 없다. 게스트 오프라인 피드에 없던 `Ahwoo.BRUTAL.*` `.nupkg`(Sdl 2026.10.3, Sdl.Mixer, Sdl.Ttf 등)를 이 Mac의 NuGet 캐시에서 복사해 두 번째 소스로 줬다 |

게스트 결과는 x86-64 에뮬레이션 결과이며 실제 x64 CPU나 GPU 검증이 아니다. 시간 값은 비교하지 않았다.

## 결과

| 검사 | OS/아키텍처 | 수정 전 | 수정 후 |
| --- | --- | --- | --- |
| PlatformerLab.Joints Release·AOT·Scene Play 세 trace | Linux x64 | AOT만 다름. 60틱 첫 차이(캐릭터 X 184.8063 / 184.8062), 300행 중 235행, 최대 0.001 | **세 경로 같음**(`38020261…`, `result.json`도 같음). `compare` 통과 |
| PlatformerLab.Paths 세 trace | Linux x64 | AOT만 다름. 89틱 첫 차이, 400행 중 107행, 최대 0.0002px | **세 경로 같음, Mac과도 같음**(`1a143d56…`) |
| BarrageLab.Lines 세 trace | Linux x64 | 같음(`6969a524…`) | 같음, Mac과도 같음 |
| Release + `DOTNET_EnableAVX2=0` (Joints·Paths·Lines) | Linux x64 | AOT와 같은 값으로 바뀜 | 기본 Release와 같음 |
| Joints Linux ↔ Mac | – | 84틱부터 다름(JIT), 60틱부터 다름(AOT) | 84틱부터 다름(세 경로 모두). JIT/AOT와 무관한 아키텍처 차이로 그대로 남음(아래) |
| Mac 기준(Joints `a00b9e4b…`, TileCollision `f30ed686…`, Paths `1a143d56…`, Lines `6969a524…`, 세 경로) | macOS arm64 | – | 모두 같음 |
| CollisionLab 공간 질의 Release·AOT | macOS arm64 | `32FA7AB7…`, `B5C817DB…` | 같음 |
| BarrageLab 프리팹 풀 Release·AOT | macOS arm64 | 명중 2,338 | 같음(재사용 6,721/7,200) |
| InventoryLab `build_guest.py` Release | macOS arm64 | 171바이트 `eed41def…` | 같음 |
| InventoryLab `build_guest.py` Native AOT | macOS arm64 | **시작 즉시 `FileNotFoundException: Brutal.Sdl`** | 171바이트 `eed41def…`(Mac 기준과 같음) |
| InventoryLab `build_guest.py` Release·AOT | Linux x64 | 둘 다 171바이트 `eed41def…`(링크 없는 경로라 재현되지 않음) | 둘 다 171바이트 `eed41def…` |
| `./monoon test` | macOS arm64 | – | 717개 PASS, `All checks passed.`(새 항목: fused transform, Native AOT kit path resolves symlinks) |

세 trace는 Scene Play·Release·AOT를 바이트 단위로 비교한 결과다. 게스트 전체 단계: [linux-summary.json](linux-summary.json). 수정 후 Linux AOT trace: [Joints](linux-joints-native-aot.tsv) · [Paths](linux-paths-native-aot.tsv). Mac 값: [mac-gates.txt](mac-gates.txt).

## 원인 1: `Vector2.Transform`·`Lerp`가 CPU에 FMA가 있을 때만 곱셈·덧셈을 합친다

.NET 10의 `System.Numerics.Vector2.Transform(Vector2, Matrix3x2)`, `Vector2.TransformNormal`, `Vector2/3/4.Lerp`는 내부에서 `MultiplyAddEstimate`를 부른다([IL 호출 그래프](il-scan.txt)). 이 함수는 하드웨어 FMA가 있으면 `a*b+c`를 한 번만 반올림하고(fused), 없으면 곱셈과 덧셈을 따로 반올림한다. 결과는 마지막 비트가 다를 수 있다.

| 실행 경로 | FMA | `Vector2.Transform` 결과 |
| --- | --- | --- |
| arm64(Mac) JIT·AOT·Scene Play | 항상 있음(AdvSimd) | 합침 |
| x64 JIT(Release·Scene Play), AVX2 CPU | 있음 | 합침 |
| x64 Native AOT, 기본 명령어 집합 | 없음 | 따로 |
| x64 JIT, `DOTNET_EnableAVX2=0` | 없음 | 따로 |

- .NET 10에서 `DOTNET_EnableAVX2=0`은 x86-64-v3 묶음 전체(FMA 포함)를 끈다. `DOTNET_EnableFMA=0`은 효과가 없다. 그래서 11장에서 AVX2 스위치만 AOT 결과를 재현하고 FMA 스위치는 아무것도 바꾸지 않았다. 게스트에서 직접 확인했다: 무작위 입력 10만 개 중 결과가 갈리는 13,544개에서 JIT은 합친 값, `DOTNET_EnableAVX2=0`과 Native AOT는 따로 계산한 값을 돌려줬다. `DOTNET_EnableFMA=0`에서는 `Fma.IsSupported`가 그대로 `True`였다. [탐침 출력](fma-probe.txt) · [탐침 소스](fma-probe.cs.txt)
- 엔진 코드에는 명시적 SIMD가 없지만 `Engine.Core`의 65곳(27개 파일)이 이 함수들을 불렀다. 게스트에서 `Engine.Core.dll`만 바꿔 끼운 Release(JIT) 게임을 `DOTNET_EnableAVX2=0` 유무로 재생해 범위를 좁혔다([bisect 결과](linux-bisect.json)).

| `Engine.Core` 변형 | Joints 기본 / AVX2 끔 | Paths 기본 / AVX2 끔 |
| --- | --- | --- |
| v0 수정 전(호출부 그대로) | `38020261…` / `86280b27…`(= 수정 전 AOT) | `1a143d56…` / `e37de6c0…`(= 수정 전 AOT) |
| v1 v0 + Rigidbody2D 모터 운반 블록만 합침 | 같음 | 다름(`e37de6c0…`) |
| v2 수정본 − Rigidbody2D 모터 운반 블록 | 다름(`47703b04…`) | 같음 |
| v3 v0 + Lines2D.cs만 합침 | – | 같음 |
| 수정본 | 같음 | 같음 |

  - **Joints**: [Rigidbody2D.cs](../../../src/Engine.Core/Rigidbody2D.cs) 220행 `Center`와 368–370행 `local`·`carried`. 캐릭터 모터가 흔들다리 판자(받침)에 실려 움직인 거리를 이전 자세의 역행렬과 새 자세로 변환해 구한다. 이 블록만 고쳐도 같아지고, 이 블록만 남기면 다르다.
  - **Paths**: [Lines2D.cs](../../../src/Engine.Core/Lines2D.cs)의 경로 추종 `PathMotion.Advance`(264·273행, 수정 전 `Vector2.Transform`)와 `CurvePath.At`(139행, 수정 전 `Vector2.Lerp`). 이 파일만 고쳐도 같아진다. 89틱부터 발판에 탄 캐릭터의 Y가 0.0001px 갈렸다(225.0384 / 225.0383). 기록한 발판 위치는 넷째 자리까지 같아서, 차이는 그보다 작은 발판 이동에서 시작해 캐릭터로 옮겨 갔다.
- Brutal.Numerics·Brutal.Box2D가 원인이 아니다. MoNoon은 Brutal.Numerics를 Box2D 인자용 `float2`로만 쓰고, Brutal.Box2D 관리 코드에는 이런 호출이 없다. 다만 Brutal.Numerics 2026.10.2의 `float4x4.Invert`, `floatQuat` 곱, `float4x4.Rows.Transform`, `float2/3/4.Lerp`(→ `float.Lerp`)도 `MultiplyAddEstimate`를 쓴다. 시뮬레이션에서 이 API들을 쓰면 같은 문제가 생긴다. BRUTAL 쪽 이슈로 기록해 둔다(패키지는 고치지 않았다).

### 수정

- [Transform2D.cs](../../../src/Engine.Core/Transform2D.cs) 77–86행: `Affine2D.Transform`, `Affine2D.TransformNormal`, `Affine2D.Lerp`(Vector2/3/4)를 추가했다. `MathF.FusedMultiplyAdd`(Vector3/4는 `Vector3/4.FusedMultiplyAdd`)로 항상 합쳐 계산한다. 연산 순서는 .NET 구현과 같다(`M11*x` 곱 → `M21*y`와 합침 → 이동 더하기). 그래서 arm64 결과는 비트 단위로 그대로다. FMA가 없는 x64(AOT 기본값, 구형 CPU의 JIT)에서는 .NET이 소프트웨어 fma로 정확히 같은 값을 낸다.
- `Engine.Core`의 65곳을 이 함수로 바꿨다. `Engine.Core.dll`에는 이제 `MultiplyAddEstimate`로 이어지는 호출이 없다([IL 스캔](il-scan.txt) after).
- 샘플 스크립트 4곳(PlatformerLab `Runner.cs`, KeyRoom `DragCrate.cs`·`OpenChest.cs`, SeedLab `MazeRun.cs`)도 바꿨다. `Runner.cs`의 측정값은 Joints `result.json`에 들어가 세 경로 비교 대상이다. 게임 스크립트용 규칙은 [Transform 2D 문서](../../../../docs/manual/transform-2d.md)에 적었다.
- `Engine.Desktop`의 `SdlSpriteBatch`(정점 위치 5곳)는 바꾸지 않았다. 그리기 전용이라 시뮬레이션 상태에 들어가지 않고, 가장 자주 도는 경로라 FMA 없는 x64 AOT에서 소프트웨어 fma 호출을 늘리고 싶지 않았다.
- 명령어 집합 고정(`IlcInstructionSet=x86-x64-v3`)은 쓰지 않았다. AVX2·FMA가 없는 x64 CPU(Haswell 이전, 일부 Atom·Celeron·Pentium)에서 게임이 시작하지 못한다. 또 FMA가 없는 CPU의 JIT은 여전히 따로 계산하므로 CPU 사이 결정론은 얻지 못한다.
- 비용: arm64는 같은 `fmadd` 명령이다. AVX2 x64 JIT은 스칼라 `vfmadd`다. FMA 없는 x64 AOT에서는 변환 한 번에 소프트웨어 fma 두 번이 든다. 호출 수는 강체·조인트·경로 객체당 틱마다 몇 번 수준이다. 실제 x64 하드웨어에서 시간을 재지 않았다(TCG 시간은 의미가 없다). 배포본 크기 변화는 작은 정적 함수 다섯 개 분량이다.
- 회귀 검사: `Engine.Checks`의 Transform 검사에 따로 계산하면 값이 달라지는 입력으로 `Affine2D.Transform`·`Lerp`가 합친 값을 내는지 확인하는 항목을 넣었다.

## 원인 2: 심볼릭 링크를 거친 AOT 키트 경로에서 Brutal.Sdl이 빠짐

- 첫 예외(Mac, 수정 전): `Unhandled exception. System.IO.FileNotFoundException: Could not find file 'Brutal.Sdl'.` `at Program.<Main>$(String[] args) + 0x18`. AOT 출력에 `libSDL3*.dylib`와 `EngineAssets`도 없었다.
- `build_guest.py`는 `tempfile.TemporaryDirectory()` 아래에 AOT 키트를 만든다. macOS에서 이 경로는 `/var/folders/…`이고 `/var`는 `/private/var`의 심볼릭 링크다. MSBuild 바이너리 로그(`MSBuildDebugEngine=1`)에서 NuGet 복원이 상대 경로로 참조한 `Engine.Desktop`·`Engine.Core`를 `/private/var/…`로, 명령줄과 스크립트 프로젝트가 절대 경로로 참조한 `Engine.Player`·`Engine.Core`를 `/var/…`로 기록했다. 같은 프로젝트가 두 경로로 갈리면서 `Engine.Desktop`의 패키지(`Ahwoo.BRUTAL.Sdl`, `Ahwoo.BRUTAL.Sdl.Native`)가 `Engine.Player`의 복원 결과에서 빠졌다. 컴파일은 통과하지만 ILC가 `Brutal.Sdl` 참조를 찾지 못해 시작 시 예외를 던지는 코드를 넣는다. 저장소 안의 `player-templates/native-aot`를 쓰는 평소 빌드는 링크가 없어 문제가 없었다.
- 수정: [NativeAotBuild.cs](../../../src/Engine.Authoring/NativeAotBuild.cs) 10행에서 키트의 `Engine.Player.csproj` 경로를 실제 경로로 바꾼 뒤 빌드한다(`RealPath`, 29–40행). 사용자가 `MONOON_PLAYERS_DIR`이나 에디터 폴더를 `/tmp`처럼 링크를 거친 위치에 둬도 같은 문제가 생기므로 테스트 스크립트가 아니라 빌더에서 고쳤다. `Engine.Checks`에 링크를 거친 경로가 실제 경로로 풀리는지 확인하는 항목을 넣었다.
- Linux 게스트에서는 수정 전 소스(`git archive 4c5816a`)로도 Release·AOT 모두 Mac 기준 저장과 같았다. 임시 폴더가 링크 없는 경로(tmpfs `/tmp`)였기 때문이다. 즉 이 문제는 경로에 심볼릭 링크가 있을 때만 생기고, 이번에 재현된 곳은 Mac이다. 수정 후 소스도 Linux에서 두 저장이 Mac 기준과 같았다. [네 실행의 출력](inventorylab-replay.txt) · [바이너리 로그 발췌](aot-kit-symlink.txt)

## 재현

- Mac 기준: [macgates.sh](scripts/macgates.sh)는 각 하네스 README의 명령(`verify.py prepare` → `./monoon build … release/native-aot` → `verify.py play` → `editor.py` → `verify.py compare`)을 그대로 따랐다. 에디터 실행은 GUI 잠금 스크립트로 감쌌다.
- Linux: [scripts](scripts/)의 `pack.py`가 Mac에서 만든 linux-x64 템플릿·AOT 키트·CLI·에디터 패키지·준비한 프로젝트·시작 훅 프로브·소스 스냅숏을 묶고, `linux.sh fresh`가 게스트에서 [guest.py](scripts/guest.py)(11장 실행기를 줄인 것, 판정 기준은 같음)를 Xvfb에서 실행한다. `linux.sh bisect`는 [bisect.py](scripts/bisect.py), `linux.sh replay before|after`는 소스 스냅숏에서 `build_guest.py`를 실행한다. 게스트는 `.test-env/work/ch11/boot.sh`로 켰고 끝난 뒤 정상 종료했다. 새 가상 디스크는 만들지 않았고, 작업 디스크의 `/run/keys96/x64det`만 썼다.
- 2026-11-23 이후: Linux 에디터 배포를 중단해(사용자 결정) `pack.py`는 에디터 패키지를 묶지 않고, `guest.py`는 Scene Play 단계 없이 Release와 Native AOT trace만 비교한다. 위 결과는 중단 전 에디터 패키지로 실행한 기록이다.
- 게스트 작업 디스크 여유가 760MB뿐이라 `build_guest.py`의 임시 폴더와 게임 출력은 RAM tmpfs(`/tmp`, 2.2GB)에 뒀다. 링크가 없는 경로라 원인 2의 조건과 무관하다.

## 남은 것

- Windows x64: 실행하지 않았다. 같은 원인(AOT 기본 명령어 집합에 FMA 없음)이므로 같은 수정이 적용되지만 확인하지 않았다.
- 아키텍처 사이 차이: Joints의 Linux 결과는 Mac과 84틱부터 0.0001px 단위로 다르다(수정 전후 같음, 곱셈·덧셈 분리 변경 후에는 60틱부터). JIT/AOT 차이와 별개다. 원인은 특정하지 않았다(후보: Box2D 네이티브 빌드, `Matrix3x2.CreateRotation`이 부르는 OS 수학 라이브러리의 `sin`/`cos`).
- `Engine.Desktop` 그리기 경로의 `Vector2.Transform`, 게임 스크립트가 직접 부르는 `Vector2.Transform`·`Lerp`는 여전히 CPU에 따라 마지막 자리가 다를 수 있다.
- (곱셈·덧셈 분리 변경 후) 소프트웨어 fma 호출은 없어졌다. x64 하드웨어에서 변환 비용을 재지 않았다.
- (곱셈·덧셈 분리 변경 후) CameraLab의 진동·커서·Scene Play GUI 검증, Windows x64는 실행하지 않았다.
