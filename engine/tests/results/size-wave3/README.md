# 배포 용량 3차 묶음 (B8·B9·T10)

2026-11-24 · macOS 27.0.1 · Apple Silicon arm64 · .NET SDK 10.0.101. 기준 커밋 `ce49a2a`(wip/next1)에서 로드맵 15.9(B8), 15.10(B9), 15.13(T10)을 차례로 적용했다. 항목마다 커밋 하나다. 측정 중 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음). VM은 시작하지 않았다. Linux x64 실행 확인은 coordinator의 x64 회귀 몫이다.

## B8: Linux Distribution Native AOT 크기 우선 (15.9)

### 배포본 영향

- Native AOT는 같은 OS에서만 빌드되므로 이 Mac에서는 linux-x64 AOT를 만들 수 없다. 같은 설정의 osx-arm64 효과로 크기를 추정한다.
- osx-arm64 Native AOT 키트(게임 스크립트 없음)를 `dotnet publish -c Release -r osx-arm64 -p:PublishAot=true`로 두 번 publish했다. 차이는 `-p:OptimizationPreference=Size` 하나다.

| 설정 | `MoNoonPlayer` 실행 파일 | publish 폴더 합계(dSYM 포함) |
| --- | ---: | ---: |
| 기본(지정 없음) | 8,056,288 B | 46,559,502 B |
| `OptimizationPreference=Size` | 7,873,968 B | 46,108,549 B |
| 차이 | −182,320 B (−2.26%) | −450,953 B |

- 조사 B8의 추정치는 −1.3%였다. 위 값은 게임 스크립트가 없는 키트라 실제 게임과 조금 다를 수 있다. Linux x64 실행 파일의 실제 크기 차이와 리플레이 결과는 x64 회귀에서 기록한다.
- 프레임 비용은 osx-arm64 배포 빌드가 이미 같은 설정을 쓰고 있어 새로 생기는 위험이 없다. Linux 시작 시간·프레임 비용 비교도 x64 회귀 몫이다.

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [NativeAotBuild.cs](../../../src/Engine.Authoring/NativeAotBuild.cs) | `compactMac` 조건을 `PrefersSize(rid, profile)`로 바꿨다. Distribution 프로필의 `osx-arm64`·`linux-x64` Native AOT에 `-p:OptimizationPreference=Size`를 준다. win-x64는 Windows 검증을 재개할 때 다룬다. Release(ReadyToRun) 경로는 그대로다. |
| [BuildSizeChecks.cs](../../Engine.Checks/BuildSizeChecks.cs) | 크기 기준 검사 앞의 자체 검사에 판정 표를 넣었다: osx-arm64·linux-x64 Distribution은 Size, win-x64와 Development는 아님. |

### 검증

| 검사 | 결과 |
| --- | --- |
| `PrefersSize` 판정 표(`./monoon test`의 build-size 단계) | PASS |
| 키트 평가: `dotnet msbuild <키트> -getProperty:OptimizationPreference -p:RuntimeIdentifier=linux-x64 -p:PublishAot=true -p:OptimizationPreference=Size` → `Size`. 키트의 csproj·props·targets에 이 값을 덮어쓰는 설정 없음(`grep` 결과 없음) | PASS |
| osx-arm64 크기 효과(위 표) | −2.26% |
| linux-x64 Native AOT 빌드·리플레이·크기 | 미실행(x64 회귀) |

### 재현

```sh
kit=$(readlink -f player-templates)/native-aot/src/Engine.Player/Engine.Player.csproj
dotnet msbuild "$kit" -getProperty:OptimizationPreference -p:RuntimeIdentifier=linux-x64 -p:PublishAot=true -p:OptimizationPreference=Size
for pref in Size Default; do
  extra=; [ $pref = Size ] && extra=-p:OptimizationPreference=Size
  dotnet publish "$kit" -c Release -r osx-arm64 --self-contained true -p:PublishAot=true $extra -p:NuGetAudit=false -nr:false -m:1 \
    --artifacts-path "$work/aot-$pref/a" -o "$work/aot-$pref/bin"
done
ls -l "$work"/aot-*/bin/MoNoonPlayer
```

x64 회귀에서 할 일: Linux 게스트에서 `./monoon build samples/InventoryLab native-aot --profile distribution`를 이 커밋과 기준 커밋으로 빌드해 `MoNoonPlayer` 크기를 비교하고, InventoryLab 리플레이 저장 SHA가 기준(`EED41DEF…`)과 같은지 확인한다.

## B9: 큰 PCM 오디오 경고 (15.10)

로드맵 행이 요구한 보고서·빌드 로그 경고는 15.5([에셋 빌드 보고서](../build-report/README.md))에서 이미 들어갔다. 이번에는 경고 문구를 한 곳(`AssetBuildReport.WavWarning`)으로 모으고, 에디터의 **Import Assets…**가 1 MiB를 넘는 WAV를 가져올 때 같은 문구를 Console 경고로 남기게 했다. 자동 변환은 하지 않는다(결정 6. BRUTAL SDL_mixer는 디코딩만 한다).

### 배포본 영향

없다. 빌드 도구와 에디터만 바뀌었다. InventoryLab을 Ogg로 바꾸면 약 −48 MB(7.8의 −94% 근거, 추정)이지만 샘플 자체는 대용량 시험 데이터라 바꾸지 않았다.

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [AssetBuildReport.cs](../../../src/Engine.Authoring/AssetBuildReport.cs) | `WavWarning(path, bytes)`(보고서와 임포트가 같이 씀), `ImportWarnings(project, paths)` |
| [MainWindow.AssetCatalog.cs](../../../src/Engine.Editor/Project/MainWindow.AssetCatalog.cs) | Import Assets가 복사를 마친 뒤 `ImportWarnings`의 줄을 Console에 경고 수준으로 쓴다 |
| [AssetReportChecks.cs](../../Engine.Checks/AssetReportChecks.cs) | 2 MiB WAV와 1 MiB를 조금 넘는 WAV는 경고, PNG는 경고 없음. 문구가 빌드 보고서 경고와 같음 |
| [audio.md](../../../../docs/manual/audio.md), [build.md](../../../../docs/manual/build.md) | 경고 위치와 Ogg 근거 |

### 검증

| 검사 | Release | Native AOT |
| --- | --- | --- |
| `--only asset-report`: 보고서 경고 1건(`Unlock.wav`), 임포트 경고 2건(WAV만) | PASS | PASS |
| InventoryLab 빌드 로그에 `MarketMorning.wav` 경고 | PASS (아래) | 15.5 기록에서 PASS([build-report](../build-report/README.md)) |

`./monoon build samples/InventoryLab release --profile distribution` 출력(osx-arm64):

```text
Shipped 13 files, 66.0 MiB: .wav 64.0 MiB, .ttf 2.0 MiB, .scene 28.4 KiB, .license 4.4 KiB, (none) 2.5 KiB, .data 1.8 KiB
    64.0 MiB  Assets/Market/MarketMorning.wav [Assets/Market] — Scenes/Inventory.scene > Market ambience > AudioSource
Warning: Assets/Market/MarketMorning.wav is 64.0 MiB of PCM WAV; import it as Ogg Vorbis to ship less.
```

## T10: 빌드용 아틀라스 재패킹 (15.13)

빌드가 쓰는 셀(셀과 테두리 1픽셀)이 아틀라스 시트의 70%(`GameBuild.RepackBelow`)보다 적게 차지하면, 쓰는 셀만 새 시트로 패킹해 배포한다. 사용 정보는 15.5 보고서와 같은 의존성 탐색이 모은다(스프라이트 렌더러, 클립 프레임, 스크립트 PNG 필드가 `Pack`을 거칠 때 셀 ID를 기록). 배포본의 `.atlas`는 경로·ID가 같고 쓰는 셀만 담는다. 시트 이름은 새 PNG의 SHA-256이다. 원본 시트는 배포하지 않는다. 프로젝트의 `.atlas`·시트는 바꾸지 않는다.

- 배치: 아틀라스 창의 **Pack**과 같은 선반 배치 코드(`AtlasPacker.Layout`)를 같이 쓴다.
- 픽셀: 원본 시트를 비premultiplied RGBA8888로 읽어 셀과 테두리(가장자리 픽셀 복제)를 32비트 단위로 그대로 복사한다. 그래서 스프라이트가 샘플링하는 텍셀이 원본과 같다. 원본 PNG가 프로젝트에 없어도(시트만 있는 아틀라스) 재패킹된다.
- 캐시: 만든 시트는 콘텐츠 빌드 캐시(`atlas <경로>`, 입력은 `.atlas` 해시와 쓰는 셀 ID 목록)에 두고 다시 쓴다. SpriteLab 두 번째 Release 빌드: `reuse atlas Assets/Icons.atlas — inputs unchanged`.
- 보고서: `assets.yaml`에 `atlases`(셀 수, 쓰는 셀 수, 사용률, 재패킹 여부, 원본·배포 시트 바이트와 픽셀 수), CLI·Console 요약에 아틀라스마다 한 줄.
- SkiaSharp는 빌드 도구(Engine.Authoring)에서만 쓴다. 배포 게임은 지금처럼 SDL3로 PNG를 읽는다. BRUTAL에는 아틀라스 패킹 기능이 없다. PNG 읽기·쓰기는 `Brutal.Stb`·`Brutal.Texture`에 있지만, 로드맵 행이 정한 대로 기존 `AtlasPacker`(SkiaSharp, 에디터의 Pack과 같은 코드)를 재사용했다. 같은 인코더를 써야 에디터 Pack과 빌드 재패킹이 한 경로로 유지되고 새 패키지를 들이지 않는다.

### 배포본 영향

| 대상 | 셀(사용/전체) | 사용률 | 시트 픽셀(VRAM, RGBA 4 B) | 시트 PNG | `.atlas` |
| --- | --- | ---: | --- | --- | --- |
| SpriteLab `Icons.atlas` (Release·AOT 같음) | 1/2 | 65% | 13×17=221 → 13×11=143 (884 → 572 B) | 171 → 153 B | 429 → 257 B |
| 픽스처 `Props.atlas` (KeyRoom 사본, `Key` + 미사용 128×128 노이즈 8장) | 1/9 | 0.2% | 148,720 → 338 (594,880 → 1,352 B) | 466,425 → 228 B (Release) · 466,190 → 228 B (AOT) | – |

- 저장소의 다른 샘플은 아틀라스를 쓰지 않는다(`find samples -name '*.atlas'` 결과 SpriteLab 하나). 크기 기준 검사의 KeyRoom·PlatformerLab·InventoryLab 배포본은 바뀌지 않는다.
- 픽스처의 원본 시트 바이트가 Release와 AOT에서 다른 것은 두 빌드가 각자 새 사본을 만들고 noise PNG의 ID(같은 크기 셀의 정렬 기준)가 달라서다. 배포 시트(228 B)는 같다.
- 런타임 코드는 바뀌지 않았다. 시작 시간·프레임 비용은 시트가 작아진 만큼만 줄어든다(측정하지 않음).

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [AtlasPacker.cs](../../../src/Engine.Authoring/AtlasPacker.cs) | 배치 루프를 `Layout`으로 분리(Pack 결과는 그대로, SpriteChecks로 확인), `Layout(atlas, keep)`·`WriteSheet`(원본 시트에서 쓰는 셀만 바이트 단위 복사) 추가 |
| [GameBuild.cs](../../../src/Engine.Authoring/GameBuild.cs) | `Pack`이 아틀라스별 쓰는 셀을 모은다. `RepackAtlases`가 사용률을 계산하고 70% 미만이면 캐시로 새 시트를 만들어 `Game/`에 넣고 `.atlas`를 다시 쓴다. 원본 시트를 의존성에서 뺀다(다른 아틀라스가 같은 시트를 그대로 쓰면 남긴다). 재패킹한 `.atlas`는 매니페스트 해시 검사 대신 원본 해시를 재패킹 전에 확인한다. |
| [AssetBuildReport.cs](../../../src/Engine.Authoring/AssetBuildReport.cs) | `AtlasUsage` 레코드, `atlases` 항목과 요약 줄 |
| [AssetReportChecks.cs](../../Engine.Checks/AssetReportChecks.cs) | KeyRoom 사본에 큰 아틀라스 픽스처를 넣고 Release·AOT에서 확인: 배포 `.atlas`는 Key 셀 하나(캔버스·크기 같음), 원본 시트·미사용 PNG 미배포, 프로젝트 `.atlas`·시트 바이트 불변, 셀과 테두리 픽셀이 원본과 같음, 배포 시트는 테두리 포함 셀 하나 크기, 보고서 `atlases`·요약 줄 |
| [Samples.GameText/verify.py](../../Samples.GameText/verify.py), [README](../../Samples.GameText/README.md) | SpriteLab 캡처 시나리오(300프레임, 5·120·250·290). 입력이 없는 시나리오는 `events: []`를 쓴다 |
| [build.md](../../../../docs/manual/build.md), [sprites.md](../../../../docs/manual/sprites.md) | 재패킹 동작·조건·보고서 항목 |
| [BuildSizeChecks.cs](../../Engine.Checks/BuildSizeChecks.cs) | 직렬화 코드 가드가 키트를 `NativeAotBuild.RealPath`로 publish한다. 워크트리에서 심볼릭 링크로 연결한 공유 키트를 두 경로로 restore해 `Engine.Core`를 찾지 못하던 문제(`CS0234`)를 고쳤다. 게임 빌드는 이미 같은 방법을 쓴다. |

### 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| SpriteLab 캡처 4장(5·120·250·290) 바이트 비교, `Samples.GameText` | 원본 아틀라스로 그림 | 재패킹 아틀라스 | 재패킹 아틀라스 |
| 결과 | 세 경로 모두 같음 | PASS | PASS |
| 큰 아틀라스 픽스처(`--only asset-report`) | – | PASS | PASS |
| SpriteLab 두 번째 빌드에서 시트 캐시 재사용 | – | PASS | – |
| 금지 파일 검색(SpriteLab·InventoryLab `.app`) | – | PASS | PASS |

캡처 SHA-256(앞 12자): 5·120·290 → `44afce2f4d98`, 250 → `dc5b028e2c9c`. 세 경로가 프레임마다 같다.

### 공통 회귀 (세 커밋 적용 후, osx-arm64)

| 검사 | 결과 |
| --- | --- |
| `./monoon test` 전체(787 PASS, `All checks passed.`) | PASS |
| `./monoon test size`(빌드 크기 기준, KeyRoom·PlatformerLab·InventoryLab × Release·AOT) | PASS. 여섯 배포본 모두 기준과 바이트 단위로 같아 기준 파일은 바꾸지 않았다 |
| `./monoon test replay`(InventoryLab, Release·AOT) | PASS, 171 B, SHA-256 `EED41DEF…F795` |
| `KeyRoom.WorldCheckpoints/ci.py osx-arm64` | PASS |
| 금지 파일 검색(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·Aseprite) | PASS |

### 재현

```sh
qa=engine/tests/Samples.GameText
./monoon build samples/SpriteLab release --profile distribution --output "$work/sl"
./monoon build samples/SpriteLab native-aot --profile distribution --output "$work/sl"
python3 $qa/verify.py play SpriteLab "$work/sl/SpriteLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/SpriteLab/release"
python3 $qa/verify.py play SpriteLab "$work/sl/NativeAOT/SpriteLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/SpriteLab/native-aot"
dotnet build $qa -c Release -m:1 -p:UsedAvaloniaProducts=
rsync -a --exclude .monoon samples/SpriteLab "$work/projects/"
sh engine/scripts/gui-lock.sh python3 $qa/editor.py SpriteLab editor/MoNoon.app/Contents/MacOS/MoNoon "$work/projects/SpriteLab" \
  engine/.artifacts/bin/GameTextProbe/release/GameTextProbe.dll "$work/runs/SpriteLab/scene-play"
python3 $qa/verify.py compare SpriteLab "$work/runs/SpriteLab"
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only asset-report
```

## 확인하지 않은 것

- linux-x64 Native AOT의 실제 크기 차이와 리플레이(15.9): Native AOT는 같은 OS에서만 빌드된다. coordinator의 x64 회귀에서 확인한다. win-x64는 Windows 검증 보류로 다루지 않았다.
- 에디터 Import Assets의 WAV 경고(15.10)는 GUI로 실행하지 않았다. 경고 문구와 판정(`ImportWarnings`)은 Engine.Checks가 확인하고, 에디터는 그 줄을 Console에 쓰는 한 줄이다. 에디터 패키지 빌드(컴파일)는 통과했다.
- 재패킹(15.13)의 시작 시간·프레임 비용은 재지 않았다. 샘플 시트가 수백 바이트라 차이를 잴 수 없다. 큰 아틀라스 픽스처는 파일·픽셀 수준으로만 확인했고 실행 캡처는 SpriteLab으로 확인했다.
- x64(Windows·Linux) 배포본의 재패킹 결과 실행: 빌드 도구 코드는 같지만 x64 실행 확인은 x64 회귀 몫이다.
