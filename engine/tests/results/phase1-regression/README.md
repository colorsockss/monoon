# phase1 최종 회귀 검사 (macOS arm64, Linux x64)

2026-11-23. 브랜치 `wip/phase1`(`7b0a83a`, 작업 트리 `.worktrees/phase1`)의 마지막 회귀 검사다. 대상은 11장, BRUTAL Sdl 2026.10.3·Mixer·Ttf·Logging 전환, Engine.Core·에디터 성능 묶음, 에디터 구조 정리, 곱셈·덧셈 분리(x64 결정론)다. **엔진 결함은 찾지 못했고 엔진 코드는 고치지 않았다.** 검사기는 한 곳만 바꿨다: 사용자 결정에 따라 [AudioLab.Vorbis/ci.py](../../AudioLab.Vorbis/ci.py)가 PCM 기준값을 RID별 파일에서 읽는다([아래](#오디오-pcm-기준값을-아키텍처별로-나눔)). 회귀 중 사용자 결정으로 Linux x64는 CI 전용이 되어 게스트의 에디터 검사를 뺐다. Windows x64는 사용자 결정에 따라 실행하지 않았다([아래](#windows-x64--장비-준비-후)).

## 환경과 빌드 종류

| 항목 | 값 |
| --- | --- |
| Mac | macOS 27.0.1 (26A434), Apple M4 Max(arm64), .NET SDK 10.0.101, AC 전원 |
| Linux | 보존한 QEMU Debian 12 amd64 게스트(TCG `-cpu Haswell-v4`, 4 vCPU, 4.5 GB, Xvfb 2560×1440, SDL 더미 오디오). x86-64 에뮬레이션 결과이며 실제 x64 CPU·GPU 검증이 아니다 |
| 빌드 종류 | Release(배포용, JIT), Native AOT(배포용), 에디터 Scene Play(JIT, Mac. Linux는 사용자 결정 전에 끝난 Joints 하나뿐). Release·AOT는 모두 `--profile distribution`(프로파일러·나눠 읽기 검사만 `development`) |
| Mac 빌드 | `engine/scripts/build-players.sh`(osx-arm64·win-x64·linux-x64·AOT 키트), `./monoon test`, `engine/scripts/build-editor-mac.sh` 모두 통과. `./monoon test`는 PASS 720줄, `All checks passed.` |
| 내려받은 것 | 없음. 게스트 피드에는 이 Mac의 NuGet 캐시에서 `Ahwoo.BRUTAL.*` 15개 `.nupkg`를 복사해 줬다(게스트 전역 캐시에 이미 있었다) |

GUI를 여는 명령은 모두 GUI 잠금 스크립트로 감쌌다. 실행 스크립트는 [scripts](scripts/)에 있다: [1-basics.sh](scripts/1-basics.sh), [2-harnesses.sh](scripts/2-harnesses.sh), [3-envknown.sh](scripts/3-envknown.sh), [4-perf.sh](scripts/4-perf.sh)·[perf.py](scripts/perf.py), 게스트는 [mac-build.sh](scripts/mac-build.sh)·[pack.py](scripts/pack.py)·[linux.sh](scripts/linux.sh)·[guest.py](scripts/guest.py). 단계별 결과: [Mac 하네스](mac/harness-steps.txt), [환경 실패](mac/envknown-steps.txt), [기준값 비교](mac/records.txt).

## macOS arm64 결과

"기준값"은 이번 단계에서 바꾼 현재 기록(Joints `2d523cd0…`, Paths `e37de6c0…`, 오디오 `ced6c800…` 등)이다. 세 경로는 Scene Play·Release·Native AOT 출력의 바이트 비교다.

| 하네스 | Scene Play / 에디터 | Release | Native AOT | 비교 |
| --- | --- | --- | --- | --- |
| PlatformerLab.Joints | 통과 | 통과 | 통과 | 세 경로 같음, 기준값 `2d523cd0…`과 같음(300행) |
| PlatformerLab.TileCollision | 통과 | 통과 | 통과 | 세 경로 같음, `f30ed686…` |
| PlatformerLab.Paths | 통과 | 통과 | 통과 | 세 경로 같음, 기준값 `e37de6c0…`과 같음(400행) |
| BarrageLab.Lines | 통과 | 통과 | 통과 | 세 경로 같음, `6969a524…` |
| KeyRoom.ScriptWaits + `test project samples/KeyRoom` | 통과 | 통과 | 통과 | 세 trace `98d74c78…`, 프로젝트 테스트 9개 통과 |
| InventoryLab.RichText | 통과 | 통과 | 통과 | `reveal.tsv` `76118c09…`, 캡처 7장 세 경로 같음 |
| Samples.GameText (KeyRoom·PlatformerLab) | 통과 | 통과 | 통과 | 캡처 4장·3장 세 경로 바이트 같음 |
| AudioLab.Vorbis | 통과(루프 57,600, 페이드 인 19,200, 아웃 28,800, `ced6c800…`) | **환경 실패**(아래) | **환경 실패** | 실패 전까지의 캡처: 첫 네 바퀴 `ced6c800…`, 2–4바퀴 같음 |
| CameraLab.ScreenEffects `verify.py`(가상 패드·진동) | 통과 | 통과 | 통과 | 효과·진동 기록이 에디터와 같음 |
| CameraLab.ScreenEffects `replay.py`(헤드리스) | – | 통과 | 통과 | 효과 기록 `d56a1838…` Release = AOT |
| SpriteLab.Aseprite | 통과(재임포트) | 통과 | 통과 | 프레임 시작 틱 1·240·282·522·564, 금지 파일 없음 |
| KeyRoom.CopyPaste | 통과 | 통과 | 통과 | 금지 파일 없음 |
| KeyRoom.FieldRename | 통과 | 통과 | 통과 | – |
| KeyRoom.ScriptReload·EditorExtensions·EditorDocking·EditorTheme·EditorPanelsDetail | 통과 | – | – | – |
| KeyRoom.Rebinding | 통과 | 통과 | 통과 | – |
| KeyRoom.PrefabStructure | 통과 | 통과 | 통과 | – |
| KeyRoom.ComponentKeys | 통과 | – | – | – |
| KeyRoom.ObjectGroups(패키지 에디터) | 통과 | – | – | – |
| InventoryLab.DataList + `test inventorylab` + `test replay` | – | 통과 | 통과 | 저장 171바이트 `eed41def…` |
| InventoryLab.Replay `build_guest.py` | – | 통과 | 통과 | 둘 다 `eed41def…` |
| InventoryLab.Incremental `verify.py --dependencies` | – | 통과 | – | 증분·전체 저장 `eed41def…`, 재빌드 1·재사용 6 |
| SeedLab.TileTerrain | 통과 | 통과 | 통과 | trace `c747efb9…`, `result.json` `ae5d49b4…` |
| SeedLab.GridNavigation | 통과 | 통과 | 통과 | `result.json` `d80dc873…` |
| CollisionLab.FrameProfiler(개발용·배포용) | 통과 | 통과 | 통과 | – |
| CollisionLab.Spatial | – | 통과 | 통과 | `32FA7AB7…`·`B5C817DB…` |
| CollisionLab.SplitLoading(3회) | 통과 | 통과 | 통과 | – |
| CollisionLab.EditorPerf(동작 검사) | 통과 | – | – | 선택·접힘·행 투명도 유지 |
| BarrageLab.PrefabPools | – | 통과 | 통과 | 명중 2,338, 재사용 6,721/7,200 |
| KeyRoom.WorldCheckpoints `ci.py` | – | 통과 | 통과 | 기준 슬롯 일치 |
| 11.12 InventoryLab `--package on`(서명·ZIP·재생) | – | 통과 | 통과 | `codesign --verify --deep --strict` 앱·ZIP 해제본 통과, 서명본·해제본 재생 저장 같음. win-x64 ZIP·linux-x64 tar.gz 생성 |
| KeyRoom.Desktop XCUITest(`mac/verify.sh`) | 통과(`TEST SUCCEEDED`, `edited.save` `dc5135b1…`) | – | – | – |
| 금지 파일(모든 빌드 출력, ZIP·tar 목록) | – | 없음 | 없음 | SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·StbTrueTypeSharp·NVorbis·HarfBuzz·`.glyphs`·`MoNoon.dll` 0개 |

### 환경 실패 (입력·접근성 권한)

이 Mac의 자동화 세션에는 키보드 이벤트·창 포커스 권한이 없다. 권한을 우회하지 않았다. 판정 기준은 바꾸지 않았다.

| 검사 | 경로 | 첫 예외 |
| --- | --- | --- |
| AudioLab.Vorbis `ci.py` | Release, Native AOT | `subprocess.CalledProcessError: Command '[…/mac-input', 'focus', '39584']' returned non-zero exit status 2.` 로그: `No keyboard event permission`(`ci.py:51 focus_game`). F 정지 페이드를 보내지 못했다. 그 전까지의 캡처는 기준값과 같다(위 표) |
| InventoryLab.Bundles `measure_mac.py` | Release | `TimeoutError: …/InventoryLab.app/Contents/MacOS/MoNoonPlayer: event,elapsed_ms,…`(`measure_mac.py:78`, `first_action`을 기다림) |
| KeyRoom.PlayInspector `verify_play.py` | 패키지 에디터 Play | `RuntimeError: System.TimeoutException: Scene Play`(`verify_play.py:95`) |
| Accessibility.Mac `verify.py` | 에디터 | `RuntimeError: Play button did not enter game mode`(`verify.py:54`). 검사기가 쓰는 결과 폴더(`results/accessibility`)는 실행 뒤 저장소 상태로 되돌렸다 |
| AudioLab.Widgets `verify_widgets.py` | Release | `RuntimeError: controls/window focus: Mixer: music=0.8 effects=0.75 master=0.8`(`verify_widgets.py:47`, 창 포커스 실패) |
| DisplayLab.Desktop `verify_display.py` | Release | `ModuleNotFoundError: No module named 'PIL'`(`verify_display.py:12`). 이 Mac의 Python에 Pillow가 없다. 설치하지 않았다 |

앞의 세 건은 11장 회귀와 오디오 전환 기록에 있던 같은 환경 실패다.

## 성능 (macOS arm64, QEMU 게스트 정지)

기준은 `b524375`를 `git archive`로 따로 풀어 그 트리의 `build-players.sh osx-arm64`·`build-editor-mac.sh`로 만든 자체 템플릿·에디터다. 후보는 이 브랜치다. 기준·후보를 라운드마다 순서를 바꿔 5회씩 번갈아 실행했다. 측정 시작·끝과 [perf.py](scripts/perf.py)의 매 실행 전에 `pgrep -f qemu-system`이 비어 있었다(게스트 정지, 게스트는 이 측정이 끝난 뒤 켰다). 측정 중 다른 빌드·GUI 검사는 돌지 않았다. 값은 중앙값 [최소–최대]이고, 중앙값 차이가 두 범위의 합보다 클 때만 "개선"이라 적었다. 원본: [timing.json](mac/timing.json), [에디터 summary.json](mac/editor-perf-summary.json), [믹서](mac/mixbench.txt).

| 지표 | 기준 `b524375` | 후보 | 판정 |
| --- | --- | --- | --- |
| CollisionLab Field10000 재생, 프레임 11–150 Physics.Contacts(µs, Release 개발용) | 3,017 [2,938–3,090] | 660 [654–665] | 개선(−78%) |
| Physics2D 벤치 정적 1만 개, Physics.Contacts(ms) | 1.431 [1.409–1.447] | 0.694 [0.491–0.787] | 개선(−52%) |
| Physics2D 벤치 정적 1만 개, Physics.Rigidbodies(ms) | 0.132 [0.132–0.155] | 0.154 [0.151–1.120] | 차이 없음(범위 겹침) |
| Physics2D 벤치 타일 64×64 + 충돌체 1천 개, Physics.Contacts(ms) | 6.180 [6.118–6.292] | 0.868 [0.859–0.872] | 개선(−86%) |
| 같은 타일 벤치, 프레임당 할당 | 1,266,320 B | 632,032 B | −50% |
| BarrageLab `Instantiate`, Release 평균(µs/프레임) | 1,237 [1,205–1,308] | 860 [814–903] | 개선(−30%) |
| 같은 경로, Release p95(µs) | 2,485 [2,116–2,643] | 1,522 [1,481–1,717] | 개선 |
| 같은 경로, Release 프레임당 할당 | 967,877 B | 419,071 B | −57% |
| BarrageLab `Instantiate`, Native AOT 평균(µs/프레임) | 921 [919–928] | 514 [509–515] | 개선(−44%) |
| 같은 경로, Native AOT p95(µs) | 1,452 [1,449–1,461] | 1,265 [1,245–1,268] | 개선 |
| 같은 경로, Native AOT 프레임당 할당 | 969,584 B | 437,176 B | −55% |
| Scene.Loading 1만 개 진입 프레임 시간(ms, 개발용 Release) | 163.5 [157.2–164.1] | 134.5 [134.0–134.6] | 개선(−18%) |
| 그 프레임의 Scene.Loading(ms) | 148.6 [142.1–149.2] | 119.5 [119.1–119.7] | 개선(−20%) |
| 그 프레임의 관리 할당 | 88.94 MB | 55.46 MB | −38% |
| 텍스트 그리기, 캐시된 프레임 12개 문구(µs) | 7.1 [7.0–7.2] (StbTrueTypeSharp) | 2.3 [2.3–3.5] (SDL_ttf) | 개선(−68%) |
| 같은 측정, 프레임당 관리 할당 | 768 B | 0 B | – |
| 믹싱, 32보이스 512프레임 블록당(µs, 같은 프로세스 5회 번갈아) | 107.7 [107.2–174.7] (관리 코드 믹서) | 75.0 [74.8–92.9] (SDL_mixer) | 범위가 겹쳐 개선으로 보지 않음 |
| 에디터 Hierarchy 활성 전환 1회: `SyncPlayInspector`(ms) | 809.6 [792.6–866.9], 523 MB | 5.6 [5.1–6.2], 0.93 MB | 개선 |
| 활성 전환 뒤 레이아웃(ms) | 5,506 [5,440–5,989], 6,315 MB | 11.8 [11.3–21.6], 5.6 MB | 개선(행 재사용: 기준 아니오, 후보 예) |
| 편집 뷰 그리기 순서 `EditItems()` 열거(ms) | 5.41 [5.27–5.58], 6.59 MB | 0.20 [0.19–0.21], 0 MB | 개선 |
| 편집 뷰 프레임 전체 `DrawFrame`(ms) | 9.85 [9.64–9.98], 10.63 MB | 5.29 [4.49–6.66], 0.14 MB | 개선 |

- 텍스트 그리기 비교의 "기준"은 `b524375`의 `SdlCanvas`(StbTrueTypeSharp, 시트 없이 TTF에서 래스터)이고, 믹싱의 "기준"은 `b524375`의 관리 코드 믹서를 별도 네임스페이스로 옮긴 사본이다([text-ttf](../text-ttf/README.md), [audio-mixer](../audio-mixer/README.md)와 같은 프로그램: [텍스트](scripts/textbench-Program.cs.txt), [믹서](scripts/mixbench-Program.cs.txt)).
- 에디터 항목은 [CollisionLab.EditorPerf](../../CollisionLab.EditorPerf/README.md)를 기준 에디터(`b524375` 트리에서 빌드한 같은 훅)·후보 에디터로 번갈아 5회 실행했다. 두 에디터 모두 매 실행의 동작 검사를 통과했다.
- 결정론 값(명중 2,338, Joints·Paths trace 등)은 모든 실행에서 같았다.

### 배포본 크기 (바이트, 심볼릭 링크 제외, `--profile distribution`)

| 출력 | `b524375` | 후보 | 차이 |
| --- | ---: | ---: | ---: |
| KeyRoom Release `.app` | 97,446,773 | 99,847,104 | +2,400,331 |
| KeyRoom Native AOT `.app` | 17,928,145 | 20,736,615 | +2,808,470 |
| KeyRoom AOT 실행 파일 | 11,994,736 | 12,410,368 | +415,632 |
| PlatformerLab Release `.app` | 95,272,218 | 97,710,426 | +2,438,208 |
| PlatformerLab Native AOT `.app` | 15,582,390 | 18,412,225 | +2,829,835 |
| PlatformerLab AOT 실행 파일 | 11,812,272 | 12,211,392 | +399,120 |
| InventoryLab Release `.app` | 148,669,854 | 151,066,518 | +2,396,664 |
| InventoryLab Native AOT `.app` | 68,980,538 | 71,752,333 | +2,771,795 |
| InventoryLab AOT 실행 파일 | 11,828,656 | 12,211,280 | +382,624 |

증가분 약 2.4–2.8 MB는 이번 단계에서 받아들인 BRUTAL 네이티브 라이브러리(SDL_ttf 약 2 MB, SDL_mixer 약 0.34 MB, Logging 등)다. 각 항목의 이유와 대안은 [text-ttf](../text-ttf/README.md)·[audio-mixer](../audio-mixer/README.md)에 있다. AOT 실행 파일은 0.38–0.42 MB 늘었다. 이번 회귀에서 새로 줄이거나 늘린 것은 없다. [sizes.tsv](mac/sizes.tsv)

## Linux x64 결과 (게스트)

이 Mac에서 만든 linux-x64 템플릿·AOT 키트·CLI(`dotnet build engine/src/Engine.Cli`)로 게스트에서 Release·Native AOT를 빌드하고 헤드리스(`--replay-input`, SDL 더미 비디오·오디오)로 재생했다. AudioLab만 Xvfb의 게임 창에 xdotool로 F를 보냈다. 검사기 프로세스 제한 시간은 15배(TCG)로 늘렸고 판정 기준은 같다. 게스트 작업 디스크 여유가 376 MB뿐이라 실행 폴더와 게임 출력은 RAM tmpfs에 두고 기능마다 출력을 지웠다. 새 가상 디스크는 만들지 않았고, 결과를 Mac으로 복사한 뒤 게스트를 정상 종료했다. [전체 단계](linux/summary.json)

**사용자 결정(회귀 도중): Linux는 CI 전용이다.** 그 시점 이후 게스트의 에디터 검사(Scene Play, `editor.py`·시작 훅 프로브, 패키지 에디터)는 실행하지 않았고 아래 표에 "제외(사용자 결정: Linux는 CI 전용)"로 적었다. 실행 중이던 TileCollision Scene Play는 중단했다(결과 없음). 결정 전에 끝난 Joints Scene Play 결과는 남겼다. Joints·Paths·Lines·TileCollision의 Linux 판정은 Release = Native AOT(+ Mac과 비교)다.

| 검사 | Release | Native AOT | Scene Play | Linux 안 비교 | Mac과 비교 |
| --- | --- | --- | --- | --- | --- |
| Box2D 네이티브 파일 | 템플릿에 `Brutal.Box2D.dll`, `libbox2d.so`(+`.3`, `.3.1.1`), `ldd`에 `not found` 없음 | – | – | – | – |
| PlatformerLab.Joints 300틱 | 통과 | 통과 | 통과(결정 전 실행) | 세 경로 같음 `86280b27…`, `DOTNET_EnableAVX2=0` Release도 같음 | **다름(알려진 아키텍처 차이)**: 236행, 첫 차이 60틱(캐릭터 X 184.8062 / 184.8063), 최대 0.001. [x64-determinism](../x64-determinism/README.md)의 Linux 값과 같다. [Linux trace](linux/joints-trace.tsv) |
| PlatformerLab.TileCollision 380틱 | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | Release = AOT = AVX2 끔 `f30ed686…` | 같음 |
| PlatformerLab.Paths 400틱 | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | Release = AOT = AVX2 끔 `e37de6c0…` | 같음 |
| BarrageLab.Lines 480행 | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | Release = AOT = AVX2 끔 `6969a524…` | 같음 |
| CollisionLab.Spatial 1천·1만 개 | 통과 | 통과 | – | 대상 해시 같음 | 같음(`32FA7AB7…`·`B5C817DB…`) |
| KeyRoom.ScriptWaits | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | Release = AOT `98d74c78…` | 같음 |
| `test project samples/KeyRoom`(CLI) | 9 passed, 0 failed | – | – | – | 같음 |
| CameraLab `replay.py` | 통과 | 통과 | – | 효과 기록·설정 파일 같음 | 같음(`d56a1838…`) |
| InventoryLab.RichText | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | `reveal.tsv`·캡처 7장 같음 | `reveal.tsv` `76118c09…` 같음, **캡처 7장도 Mac과 바이트 같음**(11장 때는 0.22–0.72% 달랐다. 게임 텍스트가 SDL_ttf와 프로젝트 글꼴로 바뀐 결과) |
| Samples.GameText KeyRoom(프로젝트 글꼴) | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | 캡처 4장 같음 | 4장 모두 Mac과 바이트 같음 |
| Samples.GameText PlatformerLab(OS 글꼴) | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | 캡처 3장 같음 | 3장 다름: 프로젝트 글꼴이 없어 Linux 기본 sans와 Mac 시스템 글꼴로 그린다(예상된 차이). [Linux](linux/platformer-text-0320-linux.png) · [Mac](linux/platformer-text-0320-mac.png) |
| SpriteLab.Aseprite | 통과 | 통과 | 제외(사용자 결정: Linux는 CI 전용) | 프레임 시작 틱 같음 | 같음(1·240·282·522·564) |
| AudioLab(SDL_mixer) | 통과 | 통과 | – | 첫 네 바퀴 PCM 같음 `ddacda7e…`, 루프 57,600·페이드 인 19,200·아웃 28,800, 장치 출력 연속 | 마지막 비트가 다름(반올림 수준, 아래). 아키텍처별 기준값으로 정리 |
| 11.12 Mac에서 만든 tar.gz(KeyRoom 아이콘 포함, InventoryLab) | 통과: `MoNoonPlayer`·`install-desktop-entry.sh` `-rwxr-xr-x`, 데이터 `-rw-r--r--`, 데스크톱 항목 생성, `--smoke-test` 통과, 약초상 재생 저장 같음 | – | – | – | 저장 `eed41def…` 같음 |
| InventoryLab 약초상 재생(CLI 빌드) | 통과 | 통과 | – | 171바이트 같음 | 같음(`eed41def…`) |
| InventoryLab.Replay `build_guest.py`(게스트에서 템플릿·키트·CLI를 소스로 컴파일) | 통과 | 통과 | – | 같음 | 같음(`eed41def…`) |
| 금지 파일(모든 게스트 빌드 출력) | 없음 | 없음 | – | – | – |
| KeyRoom.CopyPaste, ScriptReload, EditorExtensions, 6.8 배포 에디터 조작 | 제외(사용자 결정: Linux는 CI 전용) | 제외 | 제외 | – | – |

CopyPaste의 Release·AOT는 에디터 검사가 붙여 넣고 저장한 사본을 빌드하므로 에디터 검사와 함께 뺐다.

### 게스트에서 처음 실패했던 것

| 처음 실패 | 원인 | 처리 |
| --- | --- | --- |
| `lines-release`, `lines-native-aot`: `json.decoder.JSONDecodeError: Expecting property name enclosed in double quotes` | 이번에 만든 게스트 실행기가 BarrageLab.Lines의 `result.json`(Python repr 형식)을 JSON으로 읽었다. 플레이어와 판정은 통과했다 | 실행기를 고쳐(해시만 기록) 두 단계를 다시 실행해 통과 |
| `audio-release`, `audio-native-aot`: `ValueError: first_four_loops_sha256: Mac ced6c800…, runner ddacda7e…` | x64 PCM을 arm64 기준값과 비교했다. SDL_mixer는 아키텍처별 네이티브 코드다 | **아키텍처별 기준값으로 해결**(아래). 다시 실행해 Release·AOT 모두 통과 |
| (첫 실행의 Release) 장치 출력 검사 | TCG에서 JIT 시작이 느려 SDL 디스크 장치가 512프레임 무음 블록 470개를 끼워 넣었다. 무음 블록을 뺀 564블록은 캡처와 같았다 | 다시 실행한 Release는 장치 출력 검사까지 통과. 에뮬레이션 속도에 따른 일시적 결과로 본다 |

### 오디오 PCM 기준값을 아키텍처별로 나눔

사용자 결정에 따라 [ci.py](../../AudioLab.Vorbis/ci.py)가 `results/audio-mixer/playback.csv`(osx-arm64, 값 그대로) 또는 `playback-<rid>.csv`를 읽는다. 기록 전에 Linux Release·Native AOT의 PCM이 같은지 먼저 확인했다. [비교 스크립트](scripts/audio_arch.py) · [출력](audio-arch.txt) · [linux-x64 기준](../audio-mixer/playback-linux-x64.csv) · [audio-mixer 기록](../audio-mixer/README.md#아키텍처별-pcm-기준값-phase1-최종-회귀)

| 비교 | 결과 |
| --- | --- |
| x64 Release ↔ Native AOT | 첫 네 바퀴 바이트 같음, 먼저 멈춘 쪽의 F 정지(245,760프레임)까지 전체 같음, 프레임 수·포락선 오차 같음 |
| arm64 ↔ x64(첫 네 바퀴 460,800샘플) | 291,748개 다름, 첫 차이 1프레임, 최대 차 5.96e-8(24비트 PCM의 0.5 LSB), 차이 RMS는 신호 대비 −138 dB. 루프·페이드 인·페이드 아웃 프레임 수와 경계·내부 진폭 같음 → 반올림 수준 |

Windows x64 기준값은 장비 준비 후 같은 방법으로 기록한다(`ci.py win-x64`는 기준 파일이 없다는 메시지로 멈춘다).


## 검증하지 않은 것

- Windows x64 전체(아래 목록).
- Mac의 키 입력·창 포커스가 필요한 경로(위 환경 실패 여섯 건). AudioLab Release·AOT의 F 정지 페이드 실측은 이번에도 없다.
- Linux x64의 에디터 검사 전부(사용자 결정: Linux는 CI 전용). 이번 회귀의 Linux 표에 "제외"로 적었다.
- 실제 x64 CPU·물리 GPU, Wayland. Linux 결과는 TCG 에뮬레이션이고 시간 값은 비교하지 않았다.
- 성능은 Mac(arm64)에서만 쟀다. x64에서 곱셈·덧셈 분리의 비용은 재지 않았다.
- 11.12 Build Game 대화 상자 정렬(11장 회귀의 스크래치 훅)은 다시 실행하지 않았다. 에디터 UI 정렬은 EditorDocking·EditorTheme·EditorPanelsDetail·EditorExtensions 검사로만 확인했다.

## Windows x64 — 장비 준비 후

Windows 게스트는 이번에도 켜지 않았다. 이번 단계의 README에 남은 Windows 항목을 모았다. 보존한 Windows 11 Enterprise LTSC x64 게스트에서 [11장 guest.py](../chapter11-linux-x64/guest.py)·[x64 결정론 guest.py](../x64-determinism/scripts/guest.py)와 이 폴더의 [guest.py](scripts/guest.py)를 `rid`·실행 파일 이름만 바꿔 실행한다(평가판이 1시간 뒤 꺼지므로 단계마다 결과를 가상 USB에 저장).

1. **VC 런타임(미결)**: BRUTAL의 `SDL3_mixer.dll`(`VCRUNTIME140.dll`)과 `SDL3_ttf.dll`(`VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`)이 자체 포함 게시에 없는 VC 런타임을 가져온다. VC++ 재배포 패키지가 없는 Windows에서 오디오·첫 텍스트가 열리는지 확인하고, 공식 빌드로 바꿀지 VC 런타임을 함께 넣을지 정한다([brutal-sdl-upgrade](../brutal-sdl-upgrade/README.md), [audio-mixer](../audio-mixer/README.md), [text-ttf](../text-ttf/README.md)).
2. **SDL3.dll 교체**: win-x64 템플릿·배포본의 `SDL3.dll`이 공식 3.4.12(`2a2704678bf6…`)인지와 실제 실행([brutal-sdl-upgrade](../brutal-sdl-upgrade/README.md)). Windows Native AOT 빌드는 Mac에서 만들 수 없다.
3. **x64 결정론**: PlatformerLab.Joints·Paths·BarrageLab.Lines·TileCollision의 Release = Native AOT(= Scene Play), `DOTNET_EnableAVX2=0` Release, Mac·Linux 기록과 비교. AOT 기본 명령어 집합의 FMA 부재라는 같은 원인이라 곱셈·덧셈 분리 수정이 적용될 것으로 보지만 확인하지 않았다([x64-determinism](../x64-determinism/README.md)).
4. **11장 게스트 묶음**([chapter11-linux-x64](../chapter11-linux-x64/README.md)): Box2D 네이티브 파일(`box2d.dll`, `Brutal.Box2D.dll`), CollisionLab.Spatial, KeyRoom.ScriptWaits와 `test project samples/KeyRoom`, CameraLab `replay.py`, InventoryLab.RichText, SpriteLab.Aseprite, KeyRoom.CopyPaste(Windows 클립보드로 원래 검사), KeyRoom.ScriptReload·EditorExtensions, 11.12 win-x64 ZIP을 풀어 `--smoke-test`와 약초상 재생(`signtool`은 인증서가 없으면 건너뜀), 6.8 배포 에디터 조작과 InventoryLab Release·AOT 재생.
5. **오디오·텍스트**: AudioLab Release·AOT(`ci.py win-x64`, 정지 페이드 포함)와 win-x64 PCM 기준값 `playback-win-x64.csv`(Release = AOT 확인 뒤 기록), Samples.GameText·InventoryLab.RichText의 Release·AOT 캡처, Avalonia Scene 패널의 텍스트 표시, 프로젝트 글꼴이 없는 게임의 Windows 기본 글꼴 목록([text-ttf](../text-ttf/README.md), [rich-text](../rich-text/README.md)).
6. **에디터**: 에디터 구조 정리·성능 변경 뒤의 Windows 패키지 에디터(Hierarchy 활성 전환, 편집 뷰 그리기 순서, 도킹·테마·패널 검사)([editor-structure](../editor-structure/README.md), [perf-editor](../perf-editor/README.md), [editor-extensions](../editor-extensions/README.md), [copy-paste](../copy-paste/README.md)의 Ctrl 키 경로).
7. **기타 기록의 미실행 항목**: 물리 성능 묶음 해시의 x64 확인([perf-physics](../perf-physics/README.md), [perf-scene](../perf-scene/README.md)), BRUTAL Logging·Jobs의 win-x64 실행([brutal-logging-jobs](../brutal-logging-jobs/README.md)), 화면 효과·커서의 Avalonia 미리보기([screen-effects](../screen-effects/README.md), [rumble-cursor](../rumble-cursor/README.md)), Aseprite 임포트([aseprite-import](../aseprite-import/README.md)), 패키지를 풀어 실행하는 검사와 `signtool`([signing-packages](../signing-packages/README.md)).
