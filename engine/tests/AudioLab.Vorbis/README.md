# AudioLab Ogg 재생 회귀

검사기는 현재 플랫폼(macOS arm64, Windows x64 또는 Linux x64)에서 AudioLab의 Release와 Native AOT 배포본을 빌드해 게임 창을 연다. 각 창에서 배경음을 네 바퀴 이상 재생한 다음 F 키로 정지한다. macOS는 CoreGraphics, Windows는 Win32 키보드 입력, Linux는 Xvfb와 xdotool을 사용한다.

48 kHz 스테레오 출력에서 루프 한 바퀴는 57,600프레임, 페이드 인은 19,200프레임, F를 누른 뒤 페이드 아웃은 28,800프레임이다. 정상 진폭에서 연속 세 바퀴가 샘플 단위로 같아야 하며, 경계의 진폭 변화는 곡 내부 범위 이내여야 한다. 첫 네 바퀴의 SHA-256과 페이드 길이를 미리 기록한 맥 기준값과 비교한다. [Mac 기준 CSV](../results/vorbis/playback.csv)

검사기는 실제 SDL 디스크 오디오 장치를 48 kHz 스테레오 float32로 열어, 믹서가 큐에 넣은 PCM 전체가 장치 파일에도 연속으로 기록됐는지 확인한다. 장치가 먼저 열리거나 나중에 닫히면서 생기는 시작·끝 무음은 허용한다. 지정한 결과 폴더에 빌드별 두 녹음(`mix.f32`, `device.f32`), 측정값, F 입력 시점, 게임 화면과 빌드 기록이 남는다.

저장소 루트에서 현재 OS에 맞는 명령을 실행한다. 결과 경로는 새 폴더를 지정한다. Windows와 Linux에서는 Python 환경에 Pillow가 필요하고, macOS에서는 Xcode의 Swift 컴파일러와 키보드 입력 권한을 사용한다.

```sh
python3 engine/tests/AudioLab.Vorbis/ci.py osx-arm64 /tmp/monoon-audio-results
```

```powershell
python engine/tests/AudioLab.Vorbis/ci.py win-x64 audio-results
```

```sh
xvfb-run -a -s "-screen 0 1280x800x24" dbus-run-session -- \
  python3 engine/tests/AudioLab.Vorbis/ci.py linux-x64 /tmp/monoon-audio-results
```
