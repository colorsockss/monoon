# 오디오를 SDL_mixer로 전환

2026-11-23 · macOS 27.0.1 (26A434) · Apple M4 Max · .NET SDK 10.0.101. 기준 커밋은 `dad0968`(BRUTAL Sdl 2026.10.3)이다. MoNoon이 직접 하던 디코딩·리샘플링·루프·합산을 `Ahwoo.BRUTAL.Sdl.Mixer` 2026.10.3(SDL_mixer 3.2.4, WAV와 stb_vorbis 디코더 내장)으로 바꿨다. 스크립트 API(`IGameAudio`, `AudioBus`, `MixerLevels`)와 오디오 에셋 형식은 그대로다. 내려받은 패키지는 없다. `Ahwoo.BRUTAL.Sdl.Mixer`·`.Native`는 이미 NuGet 캐시에 있었다.

## 바뀐 구조

| 맡는 쪽 | 기능 | 이유 |
| --- | --- | --- |
| SDL_mixer | WAV·Ogg Vorbis 디코딩, 48 kHz 리샘플링, 무한 루프, 스테레오 게인(`MIX_SetTrackStereo`), 트랙 합산, 파일 헤더 검사(`MIX_CreateAudioDecoder`) | BRUTAL 우선. `WaveStream`, `VorbisStream`, NVorbis 패키지를 지웠다 |
| MoNoon [AudioMixer](../../../src/Engine.Core/AudioMixer.cs) | 등전력 팬·선형 거리 감쇠 계산(결과를 스테레오 게인으로 전달), 96개 보이스 제한과 교체 순서 | 기존 규칙 유지. SDL_mixer의 3D 위치 음향은 쓰지 않는다 |
| MoNoon, 트랙 cooked 콜백 | 출력 프레임 단위 선형 페이드, 공간 음원의 스테레오→모노 합치기 | SDL_mixer의 페이드는 리샘플링 전 원본 프레임에 걸린다. 1,000프레임을 미리 만든 뒤 정지하면 480 출력 프레임 뒤에도 소리가 남았다. `MIX_SetTrackStereo`는 스테레오 원본을 채널별로만 곱하고 합치지 않는다 |
| MoNoon, 그룹 post-mix 콜백 | Music·Effects 버스와 Master의 240프레임 램프 | SDL_mixer 게인은 블록 단위로만 바뀐다 |
| MoNoon | 출력 ±1 제한, 출력 시계(`AudioOutputClock`), macOS 지연 보정(`MacAudioOutput`), SDL 출력 큐 작업자([SdlSounds](../../../src/Engine.Desktop/SdlSounds.cs)) | 그대로 |

`MIX_CreateMixerDevice`(장치가 직접 당겨 가는 믹서)는 쓰지 않았다. 메모리 믹서(`MIX_CreateMixer`)의 `MIX_Generate`를 512프레임 블록마다 호출하고, 그 결과를 기존 `SdlSounds`가 SDL 출력 큐에 넣는다. 이렇게 하면 에디터 Scene Play, Release, Native AOT, 장치 없는 오프라인 검사가 같은 코드로 같은 PCM을 만들고, 리듬 판정이 쓰는 장치 큐 관찰 시점과 `MONOON_AUDIO_CAPTURE` 기록 방식이 바뀌지 않는다. 장치 믹서로 바꾸면 출력 시계를 post-mix 콜백 시점에 맞춰 다시 보정하고 리듬 실측을 다시 해야 한다.

소스마다 트랙 하나를 만들고 파일을 `MIX_SetTrackIOStream`으로 스트리밍한다. `MIX_LoadAudio(predecode=false)`는 파일 전체를 메모리에 올린다. 96 MiB WAV에서 작업 집합이 96 MiB 늘었고, 디코더·IO 스트림 방식은 0이었다. 이전처럼 재생 인스턴스마다 파일을 열지만 긴 음악을 메모리에 두지 않는다. 트랙은 재사용하지 않는다. 정지 후 같은 트랙을 다시 재생하면 리샘플러에 남은 이전 데이터가 먼저 나왔다.

## SDL_mixer 3.2.4에서 확인한 동작

같은 패키지로 만든 확인용 프로그램과 엔진 검사에서 측정했다.

- `MIX_PlayTrack`은 옵션의 루프 횟수로 트랙 설정을 덮는다. `MIX_SetTrackLoops(-1)` 뒤 옵션 없이 재생하면 한 번만 재생된다. 루프는 `SDL_mixer.play.loops = -1` 속성으로 요청한다.
- `MIX_Generate` 한 번에 cooked·그룹·post-mix 콜백이 한 번씩 불린다(루프 경계에서는 cooked가 두 조각). cooked 콜백은 스테레오 게인보다 먼저다. 모노 원본은 게인 전에 두 채널로 복제된다(기존과 같음).
- **48 kHz가 아닌 음원:** 새 트랙의 첫 요청은 리샘플러 지연 d만큼 덜 채워진다(실측 d: 8 kHz 38, 11.025 kHz 28, 16 kHz 20, 22.05 kHz 15, 24 kHz 14, 32 kHz 11, 44.1 kHz 8, 96 kHz 4프레임). 요청이 r프레임이면 앞의 r−d프레임만 채우고 끝에 min(r, d)프레임의 빈 구간을 남긴 뒤, 다음 요청부터는 끊김 없이 이어진다. 512프레임 블록이면 498–511 출력 프레임이 비고 음원이 14프레임 늦어졌다. 루프 경계에는 빈 구간이 없고, 48 kHz 음원은 빈 구간과 지연이 없다.
- 이미 재생 중인 트랙(48 kHz와 리샘플링 모두)은 블록을 어떻게 나눠 요청해도 같은 샘플을 낸다(512 대 14+498, 1+511 비교). 새 48 kHz 트랙도 첫 요청이 1프레임이어도 같은 샘플을 낸다.

### 시작 빈 구간 제거 (후속 작업)

처음 전환에서는 위 빈 구간(약 0.3 ms)이 남아 시작이 큰 효과음에서 클릭이 날 수 있었다. 아래 순서로 시도했다.

1. **보이스 전용 그룹으로 첫 블록 밀기 — 실패.** 새 보이스를 자기 그룹에 넣고 그룹 post-mix 콜백에서 첫 블록 데이터를 빈 구간만큼 뒤로 옮기려 했다. 그룹 버퍼가 트랙이 채운 길이(498프레임)뿐이라 빈 구간을 채울 수 없었다. 시작만 14프레임 늦어지고 498–511의 빈 구간은 그대로였다.
2. **첫 요청을 1프레임으로 — 채택.** 48 kHz가 아닌 새 보이스가 있는 블록만 `MIX_Generate`를 1프레임과 나머지 511프레임 두 번으로 나눈다. 새 트랙은 첫 1프레임 요청에서 아무것도 내지 않고, 다음 요청부터 끊김 없이 나온다. 빈 구간이 1프레임 시작 지연으로 바뀌고, 이후 지연도 14프레임에서 1프레임으로 줄었다. 페이드는 그 1프레임도 출력 시간으로 진행시킨다. 위 확인대로 다른 트랙의 샘플은 나눠 요청해도 같다. 블록을 나누는 조건이 보이스 시작으로만 정해지므로 Scene Play·Release·Native AOT가 같은 PCM을 낸다.
3. 디코더 → SDL 코어 `SDL_AudioStream`으로 직접 48 kHz 변환 후 트랙에 넣는 방식은 2가 통해서 시도하지 않았다(디코딩·루프 처리를 다시 구현해야 한다).

[AudioChecks](../../Engine.Checks/AudioChecks.cs)에 두 검사를 추가했다. 24 kHz 비콘의 시작에서 2프레임 이상 이어지는 무음이 없고 첫 샘플이 2프레임 안에 나온다. 48 kHz 음악이 재생되는 중에 24 kHz 보이스를 시작해도(볼륨 0) 음악 샘플이 바이트 단위로 같다.
- 마지막 바이트를 지운 Ogg는 SDL_mixer가 끝까지(28,800프레임) 디코딩한다. 이전 Ogg 페이지 검사는 거부했다. 이제는 재생할 수 있는 파일이므로 받아들이고, 헤더만 남긴 파일(64바이트)·Opus·`.ogg` 이름의 WAV·잘린 WAV 헤더는 계속 거부한다.

## 이전 믹서와의 비교

`dad0968`의 `AudioMixer`·`WaveStream`·`VorbisStream`(NVorbis)을 별도 프로그램에 그대로 넣고, 같은 소스 설정과 버스 값(Master 0.8, Music 0.8, Effects 0.75)으로 512프레임 블록씩 두 믹서를 돌렸다. [전체 출력](old-vs-new.txt)

| 입력 | 시간차 | 최대 샘플 차이 | 좌/우 전력비 (이전 → 이후) |
| --- | ---: | ---: | --- |
| RhythmLab `Afterbeat.wav` 48 kHz 모노, Music 루프 | 0 | 2.98e-8 | 1 → 1 |
| 같은 음원, 0.4초 페이드 인 | 0 | 2.98e-8 | 1 → 1 |
| AudioLab `Tide.wav` 24 kHz 스테레오 루프 (0프레임부터) | 1프레임 | 2.5e-4 | 1.006654 → 1.006015 |
| 같은 음원, 0.4초 페이드 인 | 1프레임 | 2.5e-4 | 1.006475 → 1.005836 |
| AudioLab `Tide.ogg` 24 kHz 스테레오 루프 (0프레임부터) | 1프레임 | 4.1e-4 | 1.006534 → 1.005895 |
| `Beacon.wav` 24 kHz 공간 음원, 리스너 x = −300 / −100 / 0 / 150 / 390 | 1프레임 | 1.25e-3 이하 | 0.142857 / 0.6 / 1 / 2.2 / 79.0001 — 이전과 같음 |
| KeyRoom `Unlock.wav` 22.05 kHz 일회성 효과음 | 1프레임 | 2.4e-3 | 1 → 1 |

- 48 kHz 음원은 시간차 없이 부동소수 반올림 범위에서 같다. 리듬 음원이 여기에 해당한다.
- 48 kHz가 아닌 음원의 차이는 리샘플러(이전 선형 보간, 이후 SDL 리샘플러)와 1프레임 지연이다. 시작부터 비교해도 소리 안의 무음 구간은 없다(빈 구간 제거 전에는 498–511프레임이 비었고 지연은 14프레임이었다). 보이스별 팬·감쇠 게인은 같은 함수로 계산해 전력비가 같다.
- 보이스 제한: 루프 1개와 일회성 효과음 96개를 시작하면 첫 일회성 효과음만 빠지고 루프와 나머지 95개는 남는다([AudioChecks](../../Engine.Checks/AudioChecks.cs)에 추가).

## 바꾼 기준값

| 기준 | 이전 | 이후 | 이유 |
| --- | --- | --- | --- |
| AudioLab 첫 네 바퀴(230,400 출력 프레임) PCM SHA-256, Ogg | `68479024…` ([vorbis](../vorbis/playback.csv)) | `ced6c800e6973e67473b97b0c7e302b84cd3d94078a34e6ae785b1837be5281b` ([새 기준](playback.csv)) | 리샘플러 변경과 1프레임 지연. Scene Play·Release·Native AOT가 같은 값. 빈 구간 제거 전 첫 전환의 값은 `cd1d88e0…`(14프레임 지연과 빈 구간 포함)이었다 |
| 같은 PCM, linux-x64 | `ced6c800…`(Mac 값과 비교) | `ddacda7e39eaedd80c08ff02dee9b27eb7888f22ccc2d53bc9816457bdc8f9e8` ([linux-x64 기준](playback-linux-x64.csv)) | 아키텍처별 기준값(아래). Linux Release·Native AOT가 같은 값 |
| AudioLab 시작 67,200프레임 SHA-256, WAV | `47e6db52…` ([audio](../audio/README.md)) | 다시 재지 않음 | 같은 이유로 바뀐다. WAV 변형 프로젝트의 실제 창 측정은 하지 않았고, WAV 경로는 오프라인 비교(위 표)와 `./monoon test`로 확인했다 |
| [measure.py](../../AudioLab.Vorbis/measure.py) | — | 바꾸지 않음 | 첫 전환에서는 첫 블록을 건너뛰게 고쳤으나 빈 구간을 없앤 뒤 원래대로 되돌렸다. 0프레임부터의 페이드 파형 오차 9.5e-6 |
| [AudioChecks](../../Engine.Checks/AudioChecks.cs) 버스 분리 검사 | 비콘 시작 직후 4,800프레임 | 첫 4,800프레임을 버린 뒤 | 1프레임 시작 지연 때문에 첫 구간의 0번 프레임이 다음 구간과 다르다. 비교하는 두 구간은 모두 비콘 주기(48프레임)의 배수 |
| [VorbisChecks](../../Engine.Checks/VorbisChecks.cs) | MoNoon 디코더의 블록 경계 보간·되감기, 1바이트 잘린 Ogg 거부 | 같은 Ogg 두 보이스의 합이 한 보이스의 정확히 2배이고 둘째·셋째 바퀴가 같음, 64바이트 Ogg 거부 | 해당 디코더를 지웠다. SDL_mixer는 1바이트 잘린 파일을 끝까지 재생한다 |
| 리듬 (`RhythmChecks`, [rhythm](../rhythm/README.md)) | — | 바꾸지 않음 | 검사 코드 변경 없이 통과. 48 kHz 음원은 PCM이 같고 출력 시계 코드는 그대로다 |

## 아키텍처별 PCM 기준값 (phase1 최종 회귀)

SDL_mixer는 arm64와 x64용으로 따로 컴파일한 네이티브 라이브러리다(Joints의 Box2D와 같은 경우). 사용자 결정에 따라 PCM 기준값을 RID별 파일로 나눴다. osx-arm64는 [playback.csv](playback.csv) 그대로, linux-x64는 [playback-linux-x64.csv](playback-linux-x64.csv)(같은 열)이고, [ci.py](../../AudioLab.Vorbis/ci.py)는 `playback.csv`(osx-arm64) 또는 `playback-<rid>.csv`를 읽는다. win-x64 파일은 Windows 장비 준비 뒤 기록한다.

Debian 12 x64 QEMU 게스트(TCG)에서 이 Mac이 만든 linux-x64 템플릿·AOT 키트·CLI로 빌드한 Release·Native AOT를 Xvfb 창으로 실행하고 xdotool로 F를 보냈다. [비교 스크립트](../phase1-regression/scripts/audio_arch.py) · [출력](../phase1-regression/audio-arch.txt)

| 비교 | 결과 |
| --- | --- |
| x64 Release ↔ x64 Native AOT | 첫 네 바퀴 바이트 같음(`ddacda7e…`). 먼저 멈춘 쪽의 F 정지 프레임(245,760)까지 전체가 같음. 루프 57,600, 페이드 인 19,200, 페이드 아웃 28,800, 포락선 오차 9.53e-6이 두 경로 같음 |
| arm64(Mac `ced6c800…`) ↔ x64 | 460,800개 샘플 중 291,748개가 다름. 첫 차이 1프레임, 최대 차 5.96e-8(24비트 PCM의 0.5 LSB), 차이 RMS 8.9e-9(신호 대비 −138 dB). 루프·페이드 인·페이드 아웃 프레임 수와 경계·내부 진폭 값(seam·interior peak)이 같다. 반올림 수준이라 동작 변화가 아니다 |
| 장치 출력 검사(`device.f32`) | Native AOT 통과. Release는 TCG에서 JIT 시작이 느려 SDL 디스크 장치가 512프레임 무음 블록 470개를 끼워 넣어 실패했다. 무음 블록을 뺀 장치 출력 564블록은 캡처와 순서·바이트가 같다. 에뮬레이션 속도 문제로 기록했다 |

## 측정 결과 (macOS arm64)

[AudioLab.Vorbis](../../AudioLab.Vorbis/README.md) 검사기로 SDL 디스크 오디오 장치(48 kHz 스테레오 float32)를 열어 기록했다.

| 실행 | 루프 | 페이드 인 | 페이드 아웃 | 첫 네 바퀴 SHA-256 | 장치 출력 |
| --- | ---: | ---: | ---: | --- | --- |
| Scene Play (`editor.py`) | 57,600 | 19,200 | 28,800 | `ced6c800…` | 캡처 전체가 장치 파일에 연속으로 있음 |
| Release (`ci.py`) | 57,600 | 19,200 | 측정 못 함 | `ced6c800…` | 앞의 무음을 뺀 장치 파일이 캡처의 앞부분과 일치(종료 시 큐에 남은 끝 제외) |
| Native AOT (`ci.py`) | 57,600 | 19,200 | 측정 못 함 | `ced6c800…` | 같음 |

- 둘째·셋째·넷째 바퀴가 샘플 단위로 같고, 첫 경계와 둘째 경계 주변 960프레임도 같다. 루프 경계의 진폭 변화(좌 0.00280, 우 0.00673)는 곡 안쪽 최대 변화(좌 0.00642, 우 0.00727) 이내다. 첫 네 바퀴에 무음 구간이 없다. 페이드 인 파형 오차는 9.5e-6이다(Release·Native AOT는 둘째 블록부터 계산).
- Release·Native AOT는 F 키를 보낼 입력 권한이 없어(`No keyboard event permission`) 정지 페이드를 재지 못했다. 네 바퀴 이상 기록한 뒤 검사기가 멈춘 지점까지의 캡처를 분석했다. `playback.csv`의 두 행의 `fade_out_frames`는 Scene Play 값이며 `fade_out_source` 열에 적었다. 같은 페이드 코드가 Scene Play 실측과 `./monoon test`의 479+1프레임 검사를 통과했다.

## 크기 (바이트, 심볼릭 링크 제외)

`dad0968`과 이 변경에서 같은 명령(`build-players.sh`, `./monoon build … --profile distribution`)으로 만들었다.

| 출력 | 전 | 후 | 차이 |
| --- | ---: | ---: | ---: |
| AudioLab Release `.app` | 95,218,177 | 95,552,288 | +334,111 |
| AudioLab Native AOT `.app` | 15,441,015 | 15,693,914 | +252,899 |
| KeyRoom Release `.app` | 97,447,696 | 97,781,807 | +334,111 |
| KeyRoom Native AOT `.app` | 17,928,118 | 18,197,529 | +269,411 |
| Release 템플릿 osx-arm64 | 98,824,231 | 99,157,531 | +333,300 |
| Release 템플릿 win-x64 | 89,561,534 | 89,833,992 | +272,458 |
| Release 템플릿 linux-x64 | 102,029,092 | 102,425,664 | +396,572 |

- 추가: SDL_mixer 네이티브(`libSDL3_mixer.dylib` 341,176 / `libSDL3_mixer.so` 404,464 / `SDL3_mixer.dll` 280,576), `Brutal.Sdl.Mixer.dll` 65,536(Release만), `Licenses/BRUTAL/BRUTAL-SDL-MIXER-THIRD-PARTY-NOTICES.txt` 12,189.
- 제거: `NVorbis.dll` 80,896, `Licenses/NVorbis/LICENSE.txt`. `Engine.Core.dll`은 4,096 작아졌다. Native AOT 실행 파일은 AudioLab 99,376, KeyRoom 82,864 작아졌다.
- 패키지는 macOS·Linux에 같은 라이브러리를 세 이름(`libSDL3_mixer.dylib`, `.0.dylib`, `.0.2.4.dylib` 등)으로 넣는다. SDL_mixer를 읽는 이름은 하나뿐이고 다른 이름으로 링크하는 파일도 없어서 [Directory.Build.targets](../../../Directory.Build.targets)에서 나머지 둘을 뺐다(macOS 682,352, Linux 808,928바이트 절약). `deps.json`에는 세 이름이 남아 있지만 Release와 Native AOT 모두 정상 실행됐다.

## 성능

이전·이후 믹서를 같은 프로세스에서 번갈아 7회 돌렸다. `Tide.wav` 루프 16개와 공간 비콘 16개(32 보이스), 512프레임 블록 2,000개. 측정할 때 QEMU 게스트는 실행 중이 아니었다.

| 믹서 | 블록당 중앙값 | 범위 |
| --- | ---: | ---: |
| 이전 (관리 코드) | 109.6 µs | 108.1–160.7 µs |
| 이후 (SDL_mixer) | 77.5 µs | 76.2–94.9 µs |

빈 구간 제거 후 다시 쟀다. 중앙값 차이(32.1 µs)가 두 범위의 합보다 작아 개선으로 주장하지 않는다. 정상 상태에서 두 믹서 모두 블록당 관리 힙 할당이 0이다. 96 MiB WAV 준비 단계의 관리 할당은 4,464 → 104바이트다. SDL_mixer 초기화(`MIX_Init`)는 첫 오디오 파일을 검사할 때 한 번 한다. 시작 시간은 따로 재지 않았다.

## 회귀 검사 (macOS arm64)

| 검사 | 대상 | 결과 |
| --- | --- | --- |
| `./monoon test` | 전체 엔진 검사 714개 | 통과 (`All checks passed.`). 오디오·Vorbis·리듬 검사 포함 |
| AudioLab 재생 (`editor.py`) | Scene Play | 통과 |
| AudioLab 재생 (`ci.py`) | Release, Native AOT | 루프·페이드 인·해시·장치 출력 통과, 정지 페이드는 입력 권한 없음으로 미측정 |
| [KeyRoom 조작키 설정](../../KeyRoom.Rebinding/README.md) | Scene Play, Release, Native AOT | 통과. 세 실행의 `.trace` 세 개가 서로 바이트 동일(상자를 열 때 22.05 kHz `Unlock.wav` 재생) |
| [InventoryLab 입력 재생](../../InventoryLab.Replay/README.md) | Release, Native AOT (SDL 오디오 없음) | 통과. 두 저장 파일이 Mac 기준과 바이트 동일 |
| 금지 파일 | AudioLab·KeyRoom·InventoryLab 출력, 세 Release 템플릿 | 통과. Engine.Authoring, Engine.Editor, SkiaSharp, Avalonia, `MoNoon.dll`, NVorbis, Ttf 파일, vcruntime 없음. 모든 출력에 SDL_mixer 네이티브가 하나씩 있음 |
| `engine/scripts/build-editor-mac.sh` | `editor/MoNoon.app` | 통과. 패치한 SDL 3.4.12(`c7cd6e0a09d7…`)와 `libSDL3_mixer.dylib` 포함 |

빈 구간 제거 후 `./monoon test`, AudioLab Scene Play·Release·Native AOT, 에디터 빌드를 다시 실행했다. KeyRoom·InventoryLab 검사는 첫 전환 때 결과이며, 두 검사가 비교하는 기록(`.trace`, 저장 슬롯)은 오디오 PCM과 무관하다. 실패나 예외는 없었다. `@rpath/libSDL3.0.dylib`을 찾는 `libSDL3_mixer.dylib`은 에디터에서 패치한 SDL을, 게임에서는 BRUTAL SDL을 함께 읽는다(같은 3.4.12 ABI).

## Windows — 장비 준비 후

- `SDL3_mixer.dll`(SHA-256 `a724ab54d179481d…`)은 BRUTAL 빌드를 그대로 win-x64 템플릿과 배포본에 넣는다. 이 DLL은 `VCRUNTIME140.dll`을 가져오고, 자체 포함 게시에는 VC 런타임이 없다. VC 런타임이 설치되지 않은 Windows에서 오디오가 열리는지(또는 공식 SDL_mixer 빌드·VC 런타임 동봉이 필요한지) 확인해야 한다. 이번 작업에서는 다른 파일을 내려받지 않았다. `SDL3.dll`은 이전처럼 공식 SDL 3.4.12(`2a2704678bf6…`)로 교체된다.

## 검증하지 않은 것

- Release·Native AOT의 F 정지 페이드 실측(입력 권한 없음). AudioLab 위젯 검사(키보드·마우스 권한 필요)도 실행하지 않았다.
- AudioLab WAV 변형과 [audio](../audio/README.md)의 키 입력 기반 버스·공간 음향 실측. 같은 내용은 `AudioChecks`와 위 비교로 확인했다.
- RhythmLab의 ScreenCaptureKit 실측. 48 kHz 리듬 음원의 PCM은 이전과 같고 출력 시계 코드는 바뀌지 않았다.
- 120초 Ogg로 하는 긴 트랙 검사(`--check-vorbis-stream`): 저장소에 해당 파일이 없다.
- [나눠 읽기](../split-loading/README.md): CollisionLab에는 오디오 소스가 없고 검사기는 SDL 오디오를 끄고 실행하므로 다시 돌리지 않았다.
- Windows x64에서의 실행과 Native AOT 빌드, win-x64 PCM 기준값. (Linux x64 게스트 실행은 위 절에 기록했다.)

## 다시 측정하기

```sh
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
./monoon test
python3 engine/tests/AudioLab.Vorbis/ci.py osx-arm64 /tmp/monoon-audio-results
dotnet build engine/tests/AudioLab.Vorbis/AudioProbe.csproj -c Release -m:1 -p:UsedAvaloniaProducts=
cp -R samples/AudioLab /tmp/audiolab-copy
python3 engine/tests/AudioLab.Vorbis/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon \
  /tmp/audiolab-copy engine/.artifacts/bin/AudioProbe/release/AudioProbe.dll /tmp/audiolab-scene-play
```
