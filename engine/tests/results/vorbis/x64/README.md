# Windows·Linux x64에서 AudioLab 배경음 재생

2026-11-16, 기존 QEMU Windows 11 Enterprise LTSC x64(빌드 26100.1742)와 Debian 12 amd64에서 검증했다. 네 배포본 모두 Ogg 배경음이 1.200초마다 끊김 없이 반복됐고, F 키를 누르면 0.600초에 걸쳐 멈췄다. 페이드 인을 포함한 첫 네 바퀴의 SDL 출력은 맥 기준과 바이트 단위로 일치했다. [맥 기준 측정](../README.md)

| 게스트 | 빌드 | 루프 주기 | 페이드 인 | F 입력 후 페이드 아웃 | 게임 화면 |
| --- | --- | ---: | ---: | ---: | --- |
| Windows · WASAPI | [Release](win-x64/release/MoNoonBuild) | 1.200초 | 0.400초 | 0.600초 | [F 입력 후](win-x64/release/after-F.png) |
| Windows · WASAPI | [Native AOT](win-x64/native-aot/MoNoonBuild) | 1.200초 | 0.400초 | 0.600초 | [F 입력 후](win-x64/native-aot/after-F.png) |
| Debian · ALSA | [Release](linux-x64/release/MoNoonBuild) | 1.200초 | 0.400초 | 0.600초 | [F 입력 후](linux-x64/release/after-F.png) |
| Debian · ALSA | [Native AOT](linux-x64/native-aot/MoNoonBuild) | 1.200초 | 0.400초 | 0.600초 | [F 입력 후](linux-x64/native-aot/after-F.png) |

## 출력 파형

각 게임 창에 포커스를 주고 비콘을 끈 채 배경음 네 바퀴를 재생한 후 F 키를 눌렀다. 48 kHz 스테레오에서 루프 한 바퀴는 57,600프레임, 페이드 인은 19,200프레임, 페이드 아웃은 28,800프레임이었다. F를 누른 시점은 실행마다 달라서 페이드 길이는 각 녹음의 감쇠 시작점부터 무음에 도달할 때까지 측정했다. 맥에서 사용한 분석기로 확인한 네 실행의 첫 230,400프레임 SHA-256은 모두 `68479024dc1a6d8a0a89a57bef545e1d66c5d020bf6043460d422e8610db16f2`였다. [SDL 측정값](playback.csv) · [샘플 분석기](../../../AudioLab.Vorbis/measure.py)

게스트 OS의 오디오 경로를 거친 뒤 QEMU의 Intel HDA 가상 사운드 카드 출력도 별도로 녹음했다. 각 녹음을 SDL 파형과 시간 정렬하고 네 번의 루프 경계 직전과 직후를 각각 20 ms씩 대조했다. 네 실행을 통틀어 경계에서 추가로 이동한 시간은 0프레임이었고, 경계 구간의 최소 파형 상관도는 0.99999997이었다. Native AOT의 장치 녹음에는 시작 페이드부터 F로 정지한 뒤의 무음까지 담았다. [장치 측정값](device.csv) · [Windows 녹음](win-x64/native-aot/device.flac) · [Linux 녹음](linux-x64/native-aot/device.flac)

## 실행 환경

- Release는 맥에서 최신 x64 템플릿으로 교차 내보냈고, Native AOT는 각 게스트의 .NET 10.0.101 SDK와 네이티브 링커로 빌드했다. 모두 배포 프로필이며, 네 실행 파일의 해시를 각 빌드 기록과 대조했다. 배포본에 포함된 `Tide.ogg`의 SHA-256도 맥과 동일했다. [Windows 빌드·실행 로그](win-x64/build-and-play.log) · [Debian 빌드 로그](linux-x64/build.log)
- Debian의 기본 클라우드 커널에는 사운드 드라이버가 없어 같은 디스크를 공식 일반 커널 `6.1.0-54-amd64`로 한 번 부팅했다. ALSA의 HDA Intel 장치를 열고 Xvfb의 실제 게임 창에 F를 보냈다. 기본 커널과 GRUB 설정은 보존했다. [Debian 재생 장치·실행 로그](linux-x64/play.log)
- 두 VM은 결과를 회수한 뒤 정상 종료했다. 게스트 빌드는 Windows의 `C:\MNAudio78\exports`와 Debian의 `~/MNAudio78/exports`에 있다. 이 Mac의 `.test-env/work/vorbis78-x64/`에는 원본 녹음과 재실행 스크립트가 있다.
