# AudioLab — macOS 재생 출력 검증

2026-11-14 · macOS 27.0.1 · Apple Silicon arm64. [AudioLab](../../../../samples/AudioLab) 복사본을 MoNoon 에디터의 **Scene Play**로 실행하고, 같은 프로젝트에서 내보낸 Release와 Native AOT 앱을 각각 별도 프로세스로 실행했다. 세 경로 모두 Mac의 기본 SDL 재생 장치를 열었다. 측정 대상은 **SDL 출력 큐에 성공적으로 전달한 48 kHz 스테레오 float32 PCM**이다. 물리 스피커나 마이크 응답을 측정한 것은 아니다.

| 실측 | Scene Play | Release | Native AOT |
| --- | ---: | ---: | ---: |
| 음악 루프 주기 | 57,600 프레임 | 57,600 프레임 | 57,600 프레임 |
| 음악 페이드 아웃 | 0.60초 | 0.60초 | 0.60초 |
| 서쪽에서 듣는 비콘 (왼쪽 / 오른쪽 진폭) | .02564 / .05129 | .02564 / .05129 | .02565 / .05130 |
| 동쪽에서 듣는 비콘 (왼쪽 / 오른쪽 진폭) | .05133 / .02567 | .05134 / .02568 | .05130 / .02565 |

- **스트리밍·루프:** 24 kHz WAV를 재생했다. 세 실행의 시작 67,200 출력 프레임(1.4초)은 첫 루프 경계를 포함해 바이트 단위로 같았다. SHA-256: `47e6db52f2017d8292104d6a6a8e6ef8b48904859da30318454585755d8ebb66`. 페이드 인이 끝난 구간과 한 주기 뒤 같은 구간도 샘플 단위로 일치했다.
- **페이드:** 0.4초 페이드 인에서 처음 20ms의 평균 음악 신호는 정상 진폭의 약 2.5%, 0.20–0.22초 구간은 52.5%, 0.40초 이후는 100%였다. 20ms 구간으로 측정한 페이드 아웃의 90%→10% 시간에서 추정한 전체 길이는 세 경로 모두 0.60초였다. 정지 후 비콘은 계속 재생됐다.
- **버스:** Music을 80→20%로 바꿨을 때 음악만 1/4, Effects를 75→37.5%로 바꿨을 때 효과음만 1/2이 됐다. Master를 0으로 내리면 두 채널 모두 무음, 복귀 후 효과음은 이전 진폭으로 돌아왔다.
- **공간 음향:** 리스너가 X=160에서 X=640으로 이동하면서 중앙 X=400의 비콘이 오른쪽 우세에서 왼쪽 우세로 바뀌었다. 두 지점은 비콘에서 같은 거리다. 음악(왼쪽 250 Hz, 오른쪽 375 Hz)과 효과음(1 kHz)을 각각 0.2초 구간에서 분리 측정했다. 키 입력의 벽시계 타이밍은 달라지므로 전체 파일 대신 동일 재생 상태의 채널 진폭을 비교했다.

[전체 측정 데이터](measurements.json) · [Native AOT의 실제 믹서 출력 듣기](aot-output.wav). WAV는 수집한 Native AOT float32 출력을 재생하기 쉬운 16비트 스테레오로 변환한 파일이다.

| 실제 실행 | 비콘 서쪽 | 비콘 동쪽 | 버스 조절 후 |
| --- | --- | --- | --- |
| Scene Play | [서쪽](editor-west.png) | [동쪽](editor-east.png) | [믹스](editor-mix.png) |
| Release | [서쪽](release-west.png) | [동쪽](release-east.png) | [믹스](release-mix.png) |
| Native AOT | [서쪽](aot-west.png) | [동쪽](aot-east.png) | [믹스](aot-mix.png) |

편집 UI는 [880×710 Inspector](source-narrow.png)와 [Project → Audio Mixer](mixer.png)에서 확인했다. 샘플 복사본에서 Music만 약 40%로 바꿔 Save defaults로 저장한 후, 에디터를 닫았다 다시 열어 [변경값이 유지됨](mixer-reopened.png)을 확인했다. Master와 Effects는 기존 값을 유지했다.

[엔진 검사](../../Engine.Checks/AudioChecks.cs)는 루프의 정확한 반복, 페이드 종료 프레임, 버스 분리, 위치·거리 감쇠·범위 밖 무음, 리스너 비활성 시 카메라 대체, 소스 제거와 재활성화, 기존 KeyRoom의 소스 기본값을 검사한다. 96 MiB 테스트 WAV의 준비 단계에서 관리 힙 할당은 4,464바이트였다. [전체 회귀 로그](checks.txt)

**재현:** Play 후 Space로 비콘을 켜고 D를 약 1초 누른다. W, S, F, M, M, F 순으로 음악 레벨·효과음 레벨·페이드·전체 음소거·복귀를 확인한다. 배포본은 아래 명령으로 만든 후 같은 입력을 보낸다. 출력 자체가 필요하면 실행 환경에 `MONOON_AUDIO_CAPTURE`를 **새 파일 경로**로 지정한다. 기본 실행은 녹음하지 않는다. 진단 파일은 큐에 넣은 데이터만 저장하므로 재생 중인 소스가 전혀 없는 시간은 포함하지 않는다.

```sh
editor/MoNoon.app/Contents/MacOS/MoNoon --build-release samples/AudioLab game-builds/AudioLab
editor/MoNoon.app/Contents/MacOS/MoNoon --build-native-aot samples/AudioLab game-builds/AudioLab
```
