# AudioLab의 Ogg Vorbis 스트리밍 검증

> 2026-11-23부터 오디오는 SDL_mixer로 믹싱한다. 아래 해시와 [playback.csv](playback.csv)는 이전 MoNoon 믹서의 기록이다. `ci.py`는 [새 기준](../audio-mixer/playback.csv)과 비교한다. [SDL_mixer 전환 기록](../audio-mixer/README.md)

[Windows·Linux x64의 Release·Native AOT 재생 결과와 가상 사운드 카드 녹음](x64/README.md)

2026-11-16, macOS 27.0.1 arm64. MoNoon.app의 Project 패널에서 `Tide.ogg`를 임포트하고 AudioLab의 배경음으로 지정했다. 원본은 24 kHz 스테레오 WAV의 28,800프레임 루프이며, 같은 음원을 libvorbis 품질 5로 인코딩했다. WAV 비교용 프로젝트에는 같은 Audio Source와 믹서 설정을 사용했다. [샘플](../../../../samples/AudioLab) · [오디오 사용법](../../../../docs/manual/audio.md)

## 배포 용량

| 측정 대상 | WAV | Ogg Vorbis | 절감량 |
| --- | ---: | ---: | ---: |
| 배경음 한 곡 | 115,244 B | 6,923 B | 108,321 B (94.0%) |
| Release 앱 전체 | 127,626,872 B | 127,518,551 B | 108,321 B |
| Native AOT 앱 전체 | 61,722,230 B | 61,613,909 B | 108,321 B |

두 버전 모두 같은 엔진의 배포 프로필로 빌드했다. 앱 전체 크기는 `.app` 안에 들어 있는 파일의 논리 크기 합계다. 디버그 심볼은 앱 외부에 보관했다. 각 앱은 해당 배경음 한 개와 공통 효과음 `Beacon.wav`를 담고 있다. [파일 크기와 빌드 ID](sizes.csv)

## 루프와 페이드

기본 오디오 장치로 실제 Scene Play·Release·Native AOT를 각 형식당 한 번씩 실행했다. 비콘을 끈 상태에서 음악을 네 바퀴 이상 재생하고 **F**를 눌러 정지했다. 측정 지점은 SDL 출력 큐에 전달된 48 kHz 스테레오 float32 PCM이다.

| 실행 | 루프 주기 (WAV / Vorbis) | 페이드 인 (두 형식) | 페이드 아웃 (두 형식) |
| --- | ---: | ---: | ---: |
| Scene Play | 1.200초 / 1.200초 | 0.400초 | 0.600초 |
| Release | 1.200초 / 1.200초 | 0.400초 | 0.600초 |
| Native AOT | 1.200초 / 1.200초 | 0.400초 | 0.600초 |

- 형식별로 첫 네 바퀴(230,400 출력 프레임)의 PCM이 세 실행 경로에서 바이트 단위로 같았다. 각 녹음에서도 페이드 인이 끝난 둘째·셋째·넷째 바퀴가 정확히 57,600프레임 간격으로 일치했다.
- Vorbis 루프 경계에서 인접한 두 샘플의 진폭 차이는 좌 0.00283, 우 0.00676이었다. 두 채널 모두 곡 안쪽에서 측정한 최대 변화량(좌 0.00640, 우 0.00726) 이내였다. 경계 전후 각 10 ms의 Vorbis와 원본 WAV 파형 간 코사인 유사도는 0.99979였다.
- 페이드 인은 19,200 출력 프레임, **F** 이후 페이드 아웃은 28,800 출력 프레임이었다. 여섯 실행 모두 선형 페이드의 예상 샘플값과 실측값 차이가 0.00001 미만이었고 정지 후 두 채널 모두 무음이었다. [여섯 실행의 측정값과 해시](playback.csv) · [Native AOT 실제 출력 듣기](native-aot-output.wav)

같은 음원을 120초로 늘린 Vorbis 파일로 스트리밍 시작 시 할당도 확인했다. 이 파일을 float32 PCM으로 모두 펼치면 23,040,000 B다. 관리 힙에서 파일 준비·디코더 열기·첫 블록 출력까지 해당 스레드가 할당한 메모리는 1,145,872 B였다. [스트리밍·임포트 회귀 검사](../../Engine.Checks/VorbisChecks.cs)

## 다시 측정하기

1. 저장소 루트에서 아래 명령으로 편집기·템플릿을 갱신하고 Vorbis 배포본 두 개를 만든다.

   ```sh
   ./engine/scripts/build-editor-mac.sh
   ./engine/scripts/build-players.sh osx-arm64
   ./monoon build samples/AudioLab release --profile distribution --output game-builds/AudioLab
   ./monoon build samples/AudioLab native-aot --profile distribution --output game-builds/AudioLab
   ```

2. AudioLab을 별도 폴더에 복사한다. 복사본에서 배경음의 **Audio Source → Sound**를 `Tide.wav`로 지정해 저장한 뒤 같은 명령과 프로필로 두 앱을 별도 출력 폴더에 빌드한다.
3. 각 모드의 실행 파일을 시작할 때 `MONOON_AUDIO_CAPTURE`를 새 출력 파일의 절대 경로로 지정한다. 여섯 경로를 `captures/{wav,ogg}/{scene-play,release,aot}/output.f32` 형태로 준비한다. 에디터는 Scene Play를 누르고, 모든 모드에서 배경음 시작 후 5초 이상 기다렸다가 **F**를 한 번 누른다. 페이드가 끝난 뒤 종료한다.
4. 아래 분석기는 여섯 캡처의 반복·페이드·경계 샘플을 대조하고, 빌드별 크기와 재생 가능한 16비트 WAV를 결과 폴더에 기록한다.

   ```sh
   python3 engine/tests/AudioLab.Vorbis/measure.py captures WAV빌드폴더 game-builds/AudioLab 결과폴더
   ```
