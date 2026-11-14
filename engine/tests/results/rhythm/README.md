# RhythmLab 리듬 판정 검증

2026-11-14, macOS 27.0.1 (26A434), Apple Silicon arm64. 실제 에디터의 **Scene Play**와 독립 실행한 **Release**, **Native AOT**에서 같은 120 BPM 음원을 재생했다. 각 실행에서 빠름·정확·늦음·범위 밖을 같은 순서로 확인했다.

| 실행 | 목표 시차 | 캡처된 박자와 실제 키의 시차 | 화면 오차 | 판정 화면 |
| --- | ---: | ---: | ---: | --- |
| Scene Play | -85 ms | -81.46 ms | -81.1 ms | [빠름](scene-play-early.png) |
| Scene Play | +0 ms | +3.54 ms | +4.1 ms | [정확](scene-play-on-time.png) |
| Scene Play | +85 ms | +88.43 ms | +87.6 ms | [늦음](scene-play-late.png) |
| Scene Play | +185 ms | +188.52 ms | +186.9 ms | [판정 범위 밖](scene-play-outside.png) |
| Release | -85 ms | -83.85 ms | -80.4 ms | [빠름](release-early.png) |
| Release | +0 ms | +3.51 ms | +6.5 ms | [정확](release-on-time.png) |
| Release | +85 ms | +85.68 ms | +86.8 ms | [늦음](release-late.png) |
| Release | +185 ms | +186.82 ms | +187.2 ms | [판정 범위 밖](release-outside.png) |
| Native AOT | -85 ms | -84.99 ms | -82.8 ms | [빠름](native-aot-early.png) |
| Native AOT | +0 ms | +3.53 ms | +5.9 ms | [정확](native-aot-on-time.png) |
| Native AOT | +85 ms | +88.53 ms | +91.1 ms | [늦음](native-aot-late.png) |
| Native AOT | +185 ms | +186.94 ms | +189.4 ms | [판정 범위 밖](native-aot-outside.png) |

**측정 방법**  
macOS ScreenCaptureKit에서 실행 중인 테스트 앱의 오디오만 48 kHz로 받아 연속된 강박과 약박의 어택을 검출했다. 다음 박의 시각을 예측해 포커스가 있는 게임 창에 실제 Space 키 누름·뗌 이벤트를 보냈다. 보낸 뒤 같은 박의 오디오가 캡처될 때까지 기다려 실제 음원 시각과 키 시각을 대조했다. 표에서 음수는 빠른 입력, 양수는 늦은 입력을 뜻한다. 측정 지점은 Mac이 제공하는 앱의 디지털 오디오 출력이다. 스피커에서 귀까지 걸리는 시간은 측정 범위 밖이다.

게임 판정은 오디오 캡처 도구와 독립적으로 실행됐다. 키 시각에 해당하는 출력 프레임을 찾아 가장 가까운 박과 비교하고 화면에 결과를 표시했다. 에디터에서는 화면과 Play Inspector, 배포 앱에서는 화면과 게임 로그를 함께 읽었다. 가장 긴 판정 문구도 880×660 에디터 창에서 확인했다. [작은 창 화면](scene-play-compact.png)

목표 대비 실제 키 시각의 최대 차이는 3.54ms였다. 독립적으로 측정한 시차와 화면 오차의 최대 차이는 3.45ms였다. [실측 CSV](measurements.csv)

엔진 전체 회귀 검사 323개가 통과했다. 오프라인 검사에서는 ±35ms와 ±140ms 경계의 바로 앞·뒤 1프레임, 소수 BPM, 같은 틱에 들어온 두 입력, 길게 누르기, 루프·재시작 및 출력 정지도 확인했다. [검사 로그](checks.txt) · [검사 코드](../../Engine.Checks/RhythmChecks.cs)

[RhythmLab 사용법](../../../../docs/manual/rhythm.md) · [실제 게임 음원](../../../../samples/RhythmLab/Assets/Afterbeat.wav)
