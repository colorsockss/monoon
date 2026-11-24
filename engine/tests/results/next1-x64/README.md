# wip/next1 Linux x64 회귀 (13–16장)

2026-11-24–25. 기준 커밋 `40d6020`(wip/next1, main `d523ec7` 이후 13–16장: Inspector, 파티클, 배포 용량 15.x, 엔진 기능 16.x, YAML 기본값, 물리 할당 제거 등). 결과 브랜치 `wip/next1-x64`. 대상은 **Linux x64 게임(Release·Native AOT, 배포용 `--profile distribution`)의 헤드리스 재생과 패키지**다. Linux 에디터 검사(Scene Play, 에디터 프로브)는 하지 않았다(Linux 에디터 미지원). Windows 게스트는 켜지 않았다([아래](#windows-x64--장비-준비-후)).

| 항목 | 값 |
| --- | --- |
| Linux | 보존한 QEMU Debian 12 amd64 게스트(`.test-env/work/ch11/boot.sh`, keys96 작업 디스크 재사용, TCG `-cpu Haswell-v4`, 4 vCPU, 4.5 GB + 스왑 1.5 GB, Xvfb 2560×1440, SDL 더미 오디오). 커널 6.1.0-56-cloud-amd64, glibc 2.36, .NET SDK 10.0.101, Python 3.11.2. x86-64 **에뮬레이션** 결과이며 실제 x64 CPU·GPU 검증이 아니다. 시간 값은 비교하지 않았다 |
| Mac | macOS 27 arm64(Apple Silicon), .NET SDK 10.0.101. 비교용 osx-arm64 Release·Native AOT 실행(`run-harness.py --mode release,native-aot`)은 게스트가 켜진 동안(`qemu-system` 실행 중) 돌렸다. 결정성 값만 비교한다 |
| 빌드 종류 | Release(15.7 게임별 Trim + ReadyToRun, JIT), Native AOT(15.12 기능 스위치, 15.9 `OptimizationPreference=Size`). **둘 다 게스트에서** 이 Mac이 만든 CLI(`MoNoon.Cli.dll`)와 linux-x64 템플릿·AOT 키트로 빌드했다. 따로 Mac에서 `--target linux-x64 --package on`으로 만든 Release `tar.gz` 4개를 게스트에서 풀어 검사했다 |
| 내려받은 것 | 게스트에서 `Microsoft.NETCore.App.Crossgen2.linux-x64` 10.0.1 하나(승인된 crossgen2 팩, NuGet 캐시 38 MB). 게스트 Release(ReadyToRun) 빌드가 호스트 RID용 crossgen2를 요구한다. `Ahwoo.BRUTAL.*` 16개 `.nupkg`는 이 Mac의 NuGet 캐시에서 게스트 오프라인 피드로 복사했다(내려받지 않음) |
| 게스트 디스크 | 루트 0 B·작업 디스크 약 340 MB 여유라 실행 폴더(`/run/n1x64`)와 빌드(`/tmp`)는 RAM tmpfs. 새 가상 디스크·이미지·키는 만들지 않았고 `.test-env`의 어떤 것도 저장소에 넣지 않았다. 끝에 정상 종료 |

## 진행 중 바뀐 지시 (기록)

회귀 도중 사용자 지시가 네 번 바뀌었다. 끝난 결과는 모두 그대로 두었다.

1. 처음 계획대로 하네스마다 게스트에서 Release·AOT를 빌드해 24단계(아래 표의 1–24행 + Mac 패키지 4개)를 끝냈다.
2. "Linux는 빌드 서버(CI)만" 지시로 남은 단계를 멈추고(그때 돌던 InspectorComponents·Buses는 중단, 결과 없음) 빌드 호스트 검사(win-x64 Release를 게스트에서 교차 빌드)를 시작했다. 첫 시도는 `NU1101: Unable to find package Microsoft.AspNetCore.App.Runtime.win-x64`(게스트 피드에 win-x64 aspnetcore 런타임 팩 없음)로 실패했고, 다음 지시로 취소했다. summary의 `win-x64-release-KeyRoom: fail`이 그 기록이다. 같은 빌드를 이 Mac에서 만든 목록(KeyRoom 110파일 34,316,967 B, InventoryLab 93파일 85,202,934 B)만 남았고 비교는 하지 않았다.
3. "Linux 게임도 배포, 원래 계획대로" → "빌드는 Mac, 확인만 Linux" 지시가 이어졌다. 남은 5개 하네스의 linux-x64 Release를 Mac에서 만들어 두었으나([mac-release.py](scripts/mac-release.py)) 다음 지시로 **쓰지 않았다**.
4. 최종: "하네스 빌드를 샘플별로 묶고 Release·AOT 모두 Linux에서". 남은 하네스를 [아래](#묶은-빌드와-mac-증명)처럼 묶어 Release·AOT 모두 게스트에서 빌드했다.
5. 마무리 지시: `./monoon test --tier fast`는 전체를 다시 돌리지 않고 실패 항목만 확인, Windows 관련은 보류로 기록. 그대로 따랐다([아래](#monoon-test---tier-fast-게스트)).

## 배포본 영향

엔진 코드는 바꾸지 않았다. 배포 파일·용량·시작 시간·프레임 비용은 그대로다. 바꾼 것은 검사기 두 곳(아래)과 이 결과 폴더다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [AudioLab.Buses/verify.py](../../AudioLab.Buses/verify.py) | linux-x64 PCM 기준값 추가: 기본(버스 안 씀) 첫 네 바퀴 `ddacda7e…`(`playback-linux-x64.csv`와 같은 값), 덕킹 `DUCKING_SHA256`(osx-arm64 `f6c485bb…`, linux-x64 `f7b7a44c…`)을 `check`에서 대조. linux-x64 값은 게스트 Release = Native AOT 확인 뒤 기록. Mac Release·AOT `check` 다시 통과 |
| [Engine.Checks/ParticleBudgetChecks.cs](../../Engine.Checks/ParticleBudgetChecks.cs) | `MONOON_QA_SLOW`(에뮬레이션 게스트 표시, 1이 아님)일 때 파티클 1만 개 Step+Draw 중앙값을 기록만 하고 판정하지 않는다(개수·할당 0 B는 그대로 판정). TCG에서 8.6 ms로 500 µs 예산을 넘어 `./monoon test --tier fast`가 거기서 멈췄다. Mac 기본 실행의 판정은 같다(153 µs PASS) |
| [Engine.Checks/HarnessManifestChecks.cs](../../Engine.Checks/HarnessManifestChecks.cs), [run-harness.py](../../../scripts/run-harness.py) | 하네스 폴더 목록에서 `__pycache__`를 뺀다. Python이 하네스 모듈(`desktop_players.py` 등)을 불러오면 `engine/tests/__pycache__`가 생겨(바이트코드 캐시가 켜진 Linux, Mac 메인 체크아웃의 `KeyRoom.Desktop/__pycache__`도 같은 경우) `harnesses.json is out of date: add __pycache__`로 실패했다. Mac `--tier fast`·`run-harness.py --check` 통과 |
| `scripts/` | 게스트 실행기와 Mac 쪽 스크립트([아래](#재현)) |

## 검증: Linux x64 게스트 (Release · Native AOT)

모든 행은 게스트에서 빌드한 두 플레이어로 하네스의 `play`·`compare` 단계를 그대로 실행했다(`harnesses.json`에서 Release·Native AOT 모드 단계만, `.app` 경로를 `<이름>-linux-x64/MoNoonPlayer`로 바꿈). "R = A"는 Release 출력 폴더와 AOT 출력 폴더의 파일을 바이트로 비교한 결과다. 모든 플레이어가 `--smoke-test`를 통과했고 금지 파일(SkiaSharp·Avalonia·HarfBuzz·Engine.Editor·Engine.Authoring·Roslyn·`MoNoon.dll`)은 0개였다. 단계 결과 원본: [linux/summary.json](linux/summary.json)

| # | 하네스 (로드맵) | Release | Native AOT | R = A | Mac과 비교 |
| --- | --- | --- | --- | --- | --- |
| 1 | PlatformerLab.Joints | 통과 | 통과 | 같음 | **다름(알려진 x64 Box2D 차이)**: trace `86280b27…`(Mac `2d523cd0…`), phase1 Linux 기록과 바이트까지 같음(300행 중 236행, 60틱부터, 최대 0.001) |
| 2 | PlatformerLab.TileCollision | 통과 | 통과 | 같음 | 같음 `f30ed686…` |
| 3 | PlatformerLab.Paths | 통과 | 통과 | 같음 | trace 같음 `e37de6c0…`. 캡처 5장은 HUD 글자만 다름(OS 글꼴, [마스크](paths-diff.png)) |
| 4 | BarrageLab.Lines | 통과 | 통과 | 같음 | trace 같음 `6969a524…`. 캡처는 HUD 글자만 다름(한글 OS 글꼴이 게스트에 없어 상자 글자, [마스크](lines-diff.png)) |
| 5 | PlatformerLab.Colliders (16.4 Slopes) | 통과 | 통과 | 같음 | 같음 `635f9029…`(trace·result·저장) |
| 6 | CollisionLab.Spatial (1천·1만) | 통과 | 통과 | 해시 같음 | 같음 `32FA7AB7…`·`B5C817DB…`, 후보 수 같음 |
| 7 | ParticleLab.Trace 7씬 (14.x) | 통과 | 통과 | 7씬 모두 같음 | Curves·Fireworks·Presets·Sorting trace 같음. **Modules·Rain·Stress는 행의 상태 해시 열만 다름**(600행 중 50, 360행 중 7, 300행 중 222. 입자 수·방출 수 열은 같음, OS `sinf`/`cosf` 차이로 기록만 하는 항목). 캡처는 모든 씬에서 HUD 글자만 다르고 입자 픽셀은 같음 |
| 8 | BarrageLab.PrefabPools | 통과 | 통과 | 명중 같음 | 명중 2,338·exits 2,338·created 587·reused 6,721 같음(시간·할당 열은 비교 안 함) |
| 9 | InventoryLab.Replay + Herbalist | 통과 | 통과 | 같음 | 저장 171 B `eed41def…`(Mac fixture와 같음), `check_recovery.py` 저장 6개 같음 |
| 10 | KeyRoom.InputInteractions (16.12) | 통과 | 통과 | 같음 | 같음 `ca0e358b…` |
| 11 | CameraLab.Framing (16.11) | 통과 | 통과 | 같음 | 같음 `5fd358e7…` |
| 12 | SeedLab.Autotile (16.9) | 통과 | 통과 | 같음 `63f1b9e3…` | trace 같음. 캡처는 HUD 글자만 다름([마스크](tile-diff.png)) |
| 13 | AnimatorLab.Conditions (16.5) | 통과 | 통과 | 같음 `fdcea3e1…` | trace 같음. 캡처는 HUD 글자만 다름 |
| 14 | AnimatorLab.Directions (16.5) | 통과 | 통과 | 같음 `91ac4c19…` | trace 같음. 캡처는 HUD 글자만 다름 |
| 15 | PlatformerLab.Lighting Cave (16.7) | 통과 | 통과 | 캡처 5장 같음 | 소프트웨어 렌더러 캡처에서 조명·그림자 픽셀은 같고 HUD 한 줄만 다름([마스크](light-diff.png)) |
| 16 | InventoryLab.WorldText (16.6) | 통과 | 통과 | 같음 | **캡처까지 14파일 모두 바이트 같음**(프로젝트 글꼴) |
| 17 | InventoryLab.BagGrid (16.13) | 통과 | 통과 | 같음 | **trace·캡처 22파일 모두 같음** |
| 18 | SeedLab.NavigationFree (16.15) | 통과 | 통과 | 같음 | 같음 trace `9b006ee8…`, 200명 도착 |
| 19 | KeyRoom.Tweens (16.3) | 통과 | 통과 | 같음 `37447b9d…` | 같음 |
| 20 | PlatformerLab.Parallax (16.2) | 통과 | 통과 | 같음 `3d67c881…` | 같음 |
| 21 | KeyRoom.InspectorComponents (13.2) | 통과 | 통과 | 같음 | 10파일 모두 같음 |
| 22 | KeyRoom.WorldCheckpoints (C1 코덱) | 통과 | 통과 | – | `ci.py check_player`의 대조 11개씩(Release·AOT 합쳐 22개, Mac에서 기록한 fixture 저장 포함) 모두 PASS. 21행과 같은 빌드([묶음](#묶은-빌드와-mac-증명)) |
| 23 | InventoryLab.ScriptFields (13.8) | 통과 | 통과 | 같음 | trace 같음 `261d59a3…`(Mac Scene Play·Release·AOT와 같음) |
| 24 | AudioLab.Buses (16.10) | 통과 | 통과 | 첫 네 바퀴 같음 | 덕킹 −8.0000 dB(16,639샘플, 최대 오차 4.4e-9), 해제 후 기준 복귀 프레임 62,719 Mac과 같음. PCM 해시는 아키텍처별(기본 `ddacda7e…`/Mac `ced6c800…`, 덕킹 `f7b7a44c…`/Mac `f6c485bb…`) → linux-x64 기준값 추가 |
| 25 | AudioLab.Vorbis PCM (`ci.py` 캡처·비교) | 통과(2회째, 첫 회는 TCG 타이밍 흔들림, [아래](#처음-실패한-것과-처리)) | 통과 | 첫 네 바퀴 같음 `ddacda7e…` | `playback-linux-x64.csv` 기준과 같음(루프 57,600·페이드 인 19,200·아웃 28,800, 경계·내부 진폭). xdotool로 F 전송 |
| 26 | Samples.GameText SpriteLab (15.13 재패킹) | 통과 | 통과 | 캡처 4장 같음 | **Mac과 같음**: 5·120·290 `44afce2f…`, 250 `dc5b028e…`(size-wave3 기록) |
| – | DisplayLab.IntegerScale (16.8) | 실행 안 함 | 실행 안 함 | – | 검사기가 Mac 창(`mac-input`, `screencapture`)을 띄워 재는 방식이라 헤드리스 게스트로 옮길 수 없다. 이식하지 않았다 |

### Mac과 다른 것 요약

- **Box2D 물리(Joints)**: phase1과 같은 알려진 아키텍처 차이. 나머지 물리 trace(TileCollision, Paths, Slopes, Lines, Spatial)는 Mac과 같다.
- **ParticleLab Modules·Rain·Stress**: 상태 해시만 다르고 개수는 같다. 결정 6·11대로 기록만 한다.
- **SDL_mixer PCM**: 마지막 비트 수준(phase1에서 잰 값: 최대 5.96e-8, −138 dB). 아키텍처별 기준값으로 판정한다.
- **캡처**: 프로젝트 글꼴이 없는 샘플(PlatformerLab, BarrageLab, SeedLab, AnimatorLab, ParticleLab)은 OS 글꼴(Linux 기본 sans, 한글 없음)로 그린 HUD 글자만 다르다. 마스크를 뽑은 모든 프레임에서 차이는 글자 영역뿐이었다. 프로젝트 글꼴을 쓰는 InventoryLab·SpriteLab 캡처는 Mac과 바이트까지 같다.

## 빌드 출력 검사

### 15.7 · 15.11 · 15.12 · 15.13 (게스트 빌드와 Mac에서 만든 linux-x64 패키지)

| 검사 | 결과 |
| --- | --- |
| Mac에서 만든 linux-x64 Release `tar.gz`(KeyRoom 아이콘 포함, InventoryLab, SpriteLab, Joints용 PlatformerLab) | 4개 모두 통과. `MoNoonPlayer`·`install-desktop-entry.sh` `-rwxr-xr-x`, `Game/GameSettings` `-rw-r--r--`, **`libSDL3.so`는 tar 안의 심볼릭 링크 `-> libSDL3.so.0`**, `--smoke-test` 통과. KeyRoom 데스크톱 항목 생성 + 체크포인트 대조 11개, InventoryLab Herbalist `eed41def…` + 슬롯 복구, SpriteLab 캡처 4장 Mac과 같음, PlatformerLab Joints trace `86280b27…`(게스트 빌드와 같음) |
| deps.json | 관리 어셈블리는 빠진 것 없음. 빠진 네이티브 파일(15.1의 `createdump`·`libmscordaccore.so`·`libmscordbi.so`·`libclrgc*.so`·`libcoreclrtraceptprovider.so`, 15.11에서 뺀 `libSDL3_ttf/mixer`·`libbox2d`, 버전 이름 사본 `libSDL3.so.0.4.12` 등 13–16개)은 **deps.json에 남아 있지만** Linux 호스트가 문제 삼지 않는다: 모든 Release가 시작·재생했다(size-wave1이 남긴 확인 항목) |
| 15.7 R2R Release 시작·재생 | 게스트에서 빌드한 모든 Release(하네스 25개 + SpriteLab 패키지)가 시작·스모크·재생 통과(빌드 약 7.5–8.5분/개, TCG). Mac에서 만든 Release도 위처럼 통과 |
| Mac 빌드 ↔ 게스트 빌드 Release | **바이트로 같지 않다**(Joints용 PlatformerLab, 108파일 중 7개): `Engine.Core.dll`·`Engine.Desktop.dll`·`MoNoonPlayer.dll`·`MoNoon.GameScripts.dll`(R2R 이미지), `Game/Assets/Torch.shader.frames.png`와 그 해시를 담은 `Game/.monoon-assets`(셰이더 시트를 Linux는 Mesa로 굽는다), `MoNoonBuild`(buildId). 동작은 같았다(Joints trace 같음). R2R 이미지 차이의 원인은 조사하지 않았다 |
| 15.11 라이브러리 제외 | SpriteLab(게스트 Release·AOT, Mac Release): `libSDL3_ttf.so`·`libSDL3_mixer.so`·`libbox2d.so` 없음, 실행·캡처 통과. PlatformerLab mixer 없음, InventoryLab·AudioLab·CameraLab·AnimatorLab Box2D 없음(CameraLab·AnimatorLab은 mixer도 없음) |
| 15.12 기능 스위치 AOT | 게스트에서 빌드한 모든 Native AOT가 `--smoke-test`와 재생 통과. 실행 파일: SpriteLab 6,630,440, KeyRoom 7,299,432, InventoryLab 7,050,120, PlatformerLab 7,878,936, AudioLab 6,880,280, CameraLab 6,846,520, AnimatorLab 6,817,640, BarrageLab 7,079,080, ParticleLab 7,654,728, SeedLab 7,424,248, CollisionLab 7,087,672 B |
| 15.13 SpriteLab 아틀라스 재패킹 | 게스트 Release·AOT `--package on` 패키지와 Mac Release 패키지에서 캡처 4장이 서로 같고 Mac 기록과도 같다 |

### 15.9 InventoryLab Linux Native AOT 크기

같은 게스트에서 빌드했다. Size는 9행(InventoryLab.Replay)의 AOT를 그대로 썼고, Speed는 `PrefersSize`에서 linux-x64만 뺀 측정용 CLI(커밋하지 않은 별도 워크트리), 기준은 `d523ec7`의 CLI·템플릿·AOT 키트와 그 커밋의 `samples/InventoryLab`(HEAD 샘플은 16.x API를 써서 기준 엔진으로 컴파일되지 않는다)다. 세 빌드 모두 Herbalist 저장 `eed41def…`와 스모크 통과.

| 빌드 | 실행 파일 | 배포 폴더 | 파일 |
| --- | ---: | ---: | ---: |
| `d523ec7` (기준, Size 아님, 15.8·15.11·15.12 전) | 13,098,472 | 83,126,311 | 34 |
| `40d6020` Speed(측정용) | 7,225,608 | 67,720,154 | 31 |
| `40d6020` Size(배포 설정) | 7,050,120 | 67,544,666 | 31 |

`OptimizationPreference=Size`의 효과는 실행 파일 −175,488 B(−2.43%)로 osx-arm64 측정(−2.26%)과 비슷하다. 기준 대비 −6.05 MB(−46.2%)는 Size 설정과 15.8 직렬화 축소, 15.11 Box2D 제외(파일 3개), 15.12 기능 스위치가 합친 값이다. 같은 게임의 osx-arm64 AOT 실행 파일 기준값은 6,646,112 B다.

### 새 linux-x64 기준값

| 기준 | 값 | 근거 |
| --- | --- | --- |
| AudioLab.Buses 기본(버스 안 씀) 첫 네 바퀴 | `ddacda7e39ea…` | 게스트 Release = AOT, `playback-linux-x64.csv`와 같음 |
| AudioLab.Buses 덕킹 첫 네 바퀴 | `f7b7a44c4feb…` | 게스트 Release = AOT. osx-arm64 `f6c485bb…`도 같은 표에 넣었다 |

다른 하네스는 Mac 기준값과 같거나(위 표) 기록만 하는 항목이라 새 기준 파일을 만들지 않았다.

## 묶은 빌드와 Mac 증명

게스트 빌드 한 번에 Release 약 8분, AOT 약 8분이 걸려 마지막 지시 뒤 남은 하네스는 샘플별로 묶었다. 앞의 1–20행과 Mac 패키지는 지시 전에 하네스마다 따로 빌드해 이미 끝났으므로 다시 하지 않았다.

| 묶음 | 빌드 | 증명(Mac) | 결과 |
| --- | --- | --- | --- |
| KeyRoom: InspectorComponents + WorldCheckpoints | InspectorComponents가 준비한 KeyRoom(추적 스크립트 3개와 QA 오브젝트 추가)의 Release·AOT 하나씩 | 같은 프로젝트의 Mac osx-arm64 Release로 `ci.py check_player`를 돌려 체크포인트 대조 11개(원본 KeyRoom으로 기록한 fixture 저장 포함)가 모두 일치 | 묶음. AOT 1회·Release 1회 절약 |
| AudioLab: Buses + Vorbis | Buses `default` 프로젝트 | `default`가 `samples/AudioLab`과 `diff -r`로 같고, 두 하네스 방식으로 Mac에서 만든 linux-x64 Release가 `MoNoonBuild`의 buildId 말고 바이트로 같음 | 묶음. 다만 첫 실행에서 Vorbis 단계가 실행기 결함(아래)으로 멈춰 Vorbis만 원본 샘플로 한 번 더 빌드했다 |
| InventoryLab.ScriptFields | 단독 | – | Mac 에디터가 Inspector로 값을 바꿔 저장한 프로젝트를 재생해야 하고, 다른 InventoryLab 하네스는 이미 끝났다. 15.9 Size 행은 9행의 원본 샘플 빌드를 재사용했다 |
| SpriteLab 패키지, 15.9 Speed·기준 | 단독 | – | 샘플·CLI가 달라 묶을 수 없다 |

## 처음 실패한 것과 처리

| 처음 실패 | 원인 | 처리 |
| --- | --- | --- |
| Mac `InventoryLab.ScriptFields` Release·AOT만 실행: `AssertionError: …/release: trace differs` | 이 하네스의 Release·AOT는 에디터 단계가 Inspector로 바꿔 저장한 프로젝트를 재생한다. 매니페스트는 `release` 모드만 고를 때도 에디터 없이 원본을 빌드한다(검사기 의존 관계 문제, 엔진 결함 아님) | Mac에서 패키지 에디터를 빌드해 세 경로를 다시 돌려 통과, 그 저장 프로젝트를 게스트로 보내 재생 |
| 게스트 `mac-package-*`: 실행기 assert(deps.json 항목 중 없는 파일) | 실행기가 빠진 네이티브 항목까지 오류로 봤다 | 관리 어셈블리만 판정, 네이티브 항목은 기록. 다시 실행해 통과 |
| `g-AudioLab` Vorbis 단계: `ModuleNotFoundError: No module named 'measure'` | 실행기가 `ci.py`를 모듈로 불러올 때 그 폴더를 경로에 넣지 않았다(Buses 검사는 그 전에 통과) | 경로를 고치고 Vorbis만 따로 빌드해 다시 실행 |
| Vorbis Release 첫 캡처: `ValueError: SDL disk output differs from the captured mix or contains an audible discontinuity` (두 번 빌드한 실행 모두 첫 Release 캡처에서) | **TCG 타이밍 흔들림**(phase1과 같은 현상): JIT 시작이 느려 SDL 디스크 장치가 512프레임 무음 블록을 끼워 넣었다(406개, 292개). 무음 블록을 빼면 장치 출력이 캡처와 같고, 기준값 비교(해시·루프·페이드)는 그 전에 통과했다 | 한 번 다시 캡처해 통과. 로그: [첫 캡처](linux/vorbis/release/player.log)·[측정](linux/vorbis/release/measurements.json), [재캡처](linux/vorbis/release-retry/player.log)·[측정](linux/vorbis/release-retry/measurements.json), [AOT](linux/vorbis/native-aot/measurements.json) |
| `g-AudioLab` 종료 코드 99 | Buses의 모든 단계(빌드 4개, 재생 4개, `check` 2개)는 통과했다. 99는 실행기가 뒤이어 부른 Vorbis 단계의 `ModuleNotFoundError`(위)를 표시한 값이다 | Buses는 다시 돌리지 않았다 |
| `aot-size-variants` 기준 빌드: `error CS0246: The type or namespace name 'UiDrop' could not be found` | HEAD의 InventoryLab 샘플을 기준 엔진으로 빌드했다 | 기준 커밋의 샘플로 빌드 |
| `./monoon test --tier fast`: `DirectoryNotFoundException: …/packages/Dialogue.monopackage` | 게스트 스냅샷에 저장소 루트 `packages/`를 넣지 않았다 | `packages/`·`docs/` 추가 |
| `./monoon test --tier fast`: `System.Exception: 10,240 particles (Storm.particles): Step + Draw median 8600.0 µs … (budget 500 µs …)` | TCG 에뮬레이션 속도(Mac 153 µs). 할당 0 B·개수는 통과 | 검사가 `MONOON_QA_SLOW`에서 시간을 판정하지 않게 고침(위 표) |

## Windows x64 — 장비 준비 후

이번에도 Windows 게스트는 켜지 않았다. 이번 단계(13–16장)의 결과 README에 남은 Windows 항목을 모았다. phase1의 [Windows 목록](../phase1-regression/README.md#windows-x64--장비-준비-후) 1–7은 그대로 유효하다. 이 폴더의 [guest.py](scripts/guest.py)를 `rid = 'win-x64'`, `MoNoonPlayer.exe`, `.zip` 패키지로 바꿔 돌린다.

1. **15.7 게임별 Trim + ReadyToRun Release**: win-x64 Release(Mac에서 만든 R2R x64 이미지) 실행·재생·크래시 해석([size-wave2](../size-wave2/README.md)). 게스트에서 빌드하려면 피드에 `Microsoft.AspNetCore.App.Runtime.win-x64`가 필요하다(이번 Linux 교차 빌드 시도에서 확인).
2. **15.1 네이티브 한 벌·디버거 제외**: Windows는 링크를 만들지 않고 두 파일을 둔다. `mscordaccore`·`mscordbi`·`clrgc`·`createdump.exe`를 뺀 배포본 실행과 deps.json에 남은 항목의 영향([size-wave1](../size-wave1/README.md)).
3. **15.9**: win-x64 Native AOT는 아직 `OptimizationPreference=Size`가 아니다. Windows 검증 재개 때 크기·리플레이를 재고 판정 표를 정한다([size-wave3](../size-wave3/README.md)).
4. **15.11·15.12·15.13**: win-x64 배포본의 라이브러리 제외, 기능 스위치 AOT 스모크·재생, 재패킹 아틀라스 캡처([size-wave2](../size-wave2/README.md), [aot-feature-switches](../aot-feature-switches/README.md), [aot-size-2](../aot-size-2/README.md), [size-wave3](../size-wave3/README.md)).
5. **결정성 게이트 x64**: 이 README의 1–26행을 win-x64 Release·Native AOT로. Box2D(16.4 Slopes 포함)와 파티클 노이즈·충돌(14.x)은 차이를 기록만 한다([colliders-capsule-chain](../colliders-capsule-chain/README.md), [particles](../particles/README.md), [perf-physics](../perf-physics/README.md), [navigation-free](../navigation-free/README.md)).
6. **오디오**: AudioLab.Buses 덕킹과 AudioLab.Vorbis의 win-x64 PCM 기준값(Release = AOT 뒤 `playback-win-x64.csv`, `DEFAULT_SHA256`/`DUCKING_SHA256`에 win-x64 추가)([audio-buses](../audio-buses/README.md)).
7. **조명·렌더러**: 16.7의 `SDL_BLENDMODE_MOD`·렌더 타깃을 D3D 렌더러에서 실행, Windows 에디터 Avalonia 미리보기(그림자 없는 근사)([lighting](../lighting/README.md)). 16.8 정수 배율의 배율 1 미만 창([engine-features-1](../engine-features-1/README.md)).
8. **에디터(Windows 패키지 에디터)**: 13장 Inspector(I-1, 필드·에셋·컴포넌트 편집, Undo diff), 14장 파티클 편집기·`PreviewCanvas` Scene Play 캡처, 16.x 편집 UI(Animator 패널, Tileset 패널)([inspector-i1](../inspector-i1/README.md), [inspector-edit](../inspector-edit/README.md), [inspector-assets](../inspector-assets/README.md), [inspector-undo-diff](../inspector-undo-diff/README.md), [particles-editor](../particles-editor/README.md), [particles-p0](../particles-p0/README.md), [editor-input-audio](../editor-input-audio/README.md)).
9. **빌드 보고서(15.5)·WAV 경고(15.10)**: Windows 호스트에서 만든 보고서와 경로 구분자([build-report](../build-report/README.md)).

## ./monoon test --tier fast (게스트)

게스트가 스냅샷에서 Engine.Checks를 직접 컴파일해 돌렸다(Mesa `libosmesa6`를 `LD_LIBRARY_PATH`로). 순서 10050(`HarnessManifestChecks`)까지 **PASS 836줄**, 템플릿이 필요한 4묶음은 `SKIP`. [로그](linux/monoon-test-fast.log)

| 묶음 | 결과 |
| --- | --- |
| `ParticleBudgetChecks`(3000) 시간 예산 | **TCG에서는 해당 없음**: 첫 실행에서 Step+Draw 중앙값 8,600 µs > 500 µs로 멈췄다(개수 10,240·할당 0 B는 통과). `MONOON_QA_SLOW`가 켜진 호스트에서는 시간을 판정하지 않게 고쳤고, 두 번째 실행은 이 묶음을 지나갔다 |
| `HarnessManifestChecks`(10050) | 두 번째 실행에서 `System.Exception: engine/tests/harnesses.json is out of date: add __pycache__; remove .`(게스트에서 하네스 Python 모듈을 불러와 생긴 폴더). 검사 결함으로 고쳤다(위 표). 사용자 요청으로 전체 단계를 다시 돌리지 않고, 같은 판정(폴더 69개 = 목록 69개, `__pycache__` 제외)을 게스트에서 따로 확인했다: [manifest-guest.txt](linux/manifest-guest.txt). 이 묶음은 이름이 없어 `--only`로 다시 돌릴 수 없다 |
| `KeyRoomChecks.SceneEdits`(10100) | 위에서 멈춰 게스트에서는 실행하지 않았다(Mac `--tier fast`는 통과) |

## 확인하지 않은 것

- Linux 에디터 검사 전부(Scene Play, 에디터 프로브). 3경로 비교의 Scene Play 쪽은 Mac 기록을 쓴다.
- DisplayLab.IntegerScale(16.8): 창·화면 캡처 기반 검사기라 게스트에서 실행하지 않았다.
- 실제 x64 CPU·GPU, Wayland, 소리 출력. 모든 Linux 값은 TCG 에뮬레이션·SDL 더미/디스크 오디오다. 시간·성능 값(PrefabPools 평균 등)은 비교하지 않았다.
- Mac 빌드와 게스트 빌드 Release의 R2R DLL 차이 원인(동작은 같음).
- 빌드 호스트 교차 빌드(win-x64·osx-arm64 Release를 Linux에서): 첫 시도(팩 누락) 뒤 지시 변경으로 중단했고, Windows는 사용자 결정으로 보류 중이라 다시 하지 않았다(`win-x64-release-KeyRoom: fail`은 보류 기록).
- `./monoon test`(전체): 세 플랫폼 템플릿·크기 기준이 osx-arm64 전용이라 게스트에서는 `--tier fast`만 돌렸고, 그것도 순서 10050까지다(위).

## 재현

저장소 루트(`.worktrees/x64reg`), 게스트가 꺼진 상태에서. `S`는 작업 폴더(스크립트 안의 scratchpad 경로).

```sh
engine/scripts/bootstrap-worktree.sh
dotnet build engine/src/Engine.Cli -c Release -m:1 -p:NuGetAudit=false
sh engine/tests/results/next1-x64/scripts/mac-build.sh            # CLI 사본, Mac에서 만든 linux-x64 tar.gz 4개
python3 engine/tests/results/next1-x64/scripts/pack.py "$S"        # 게스트 묶음(repo 스냅샷, 템플릿·키트, CLI, BRUTAL nupkg)
python3 engine/tests/results/next1-x64/scripts/pack.py "$S" extra  # 15.9 Speed CLI, d523ec7 CLI·템플릿
python3 engine/scripts/run-harness.py <하네스…> --mode release,native-aot --out "$S/mac-h"   # Mac 비교 출력
cd ../.. && sh .test-env/work/ch11/boot.sh
sh .worktrees/x64reg/engine/tests/results/next1-x64/scripts/linux.sh fresh
sh .worktrees/x64reg/engine/tests/results/next1-x64/scripts/linux.sh extra "$S/n1x64-edited.zip"   # ScriptFields 저장 프로젝트
sh .worktrees/x64reg/engine/tests/results/next1-x64/scripts/linux.sh resume ["단계 이름 …"]
sh .worktrees/x64reg/engine/tests/results/next1-x64/scripts/linux.sh fetch
python3 .worktrees/x64reg/engine/tests/results/next1-x64/scripts/compare.py "$S/linux-results" "$S/mac-sf" "$S/mac-h2" "$S/mac-h"
```

| 스크립트 | 역할 |
| --- | --- |
| [guest.py](scripts/guest.py) | 게스트 실행기. `harnesses.json`의 Release·AOT 단계를 Linux 경로로 바꿔 두 개씩 병렬 실행, 스모크·파일 사실·R = A 비교, 패키지 검사, 묶은 빌드, 15.9 변형, `./monoon test --tier fast`. 통과한 단계는 건너뛰는 재개형 |
| [sitecustomize.py](scripts/sitecustomize.py) | 게스트의 하네스 Python 프로세스 제한 시간 ×15(TCG). AudioLab.Vorbis만 `time.monotonic` 기한도 늘림 |
| [monoon-guest](scripts/monoon-guest) | 게스트의 `./monoon build` → Mac에서 만든 CLI `--target linux-x64` |
| [linux.sh](scripts/linux.sh), [pack.py](scripts/pack.py), [mac-build.sh](scripts/mac-build.sh) | phase1 것을 고친 전송·실행·묶음 |
| [compare.py](scripts/compare.py) | Mac ↔ Linux 출력 바이트 비교와 PNG 픽셀 차이 |
| [mac-release.py](scripts/mac-release.py), [mac-win.sh](scripts/mac-win.sh) | 중간 지시 때 쓴 Mac 쪽 빌드(최종 결과에는 쓰지 않음, 기록) |

결과 원본: [linux/summary.json](linux/summary.json)(단계별 상태·시간·파일 사실·스모크·R = A. 중단·실패한 단계의 기록도 그대로 둠: `win-x64-release-KeyRoom`, `g-AudioLab`, `monoon-test-fast`), [linux/compare.txt](linux/compare.txt)(Mac 비교, `compare.py` 출력), [linux/monoon-test-fast.log](linux/monoon-test-fast.log). 게스트는 끝에 `systemctl poweroff`로 정상 종료했다(`qemu-system` 없음, pid 파일 없음).
