# AudioLab 공통 UI 위젯 회귀 검사

AudioLab Release를 실제 게임 창으로 실행한다. 마우스, 키보드, SDL 가상 게임패드는 각각 빈 사용자 프로필에서 시작한다. 음악을 80%에서 60%로 맞춘 다음 프로세스를 종료한다. 각 프로필을 다시 열어 첫 믹서 상태가 음악 60%, 효과음 75%, 마스터 80%인지 확인한다. 추가 실행에서는 드롭다운과 토글을 조작한다.

1. [빌드 매뉴얼](../../../docs/manual/build.md)에 따라 호스트용 AudioLab Release를 만든다.
2. 저장소 루트에서 `dotnet build engine/tests/AudioLab.Widgets/VirtualPad.csproj -c Release`를 실행한다. 테스트 훅은 `engine/.artifacts/bin/VirtualPad/release/VirtualPad.dll`에 생성된다. 게임패드 검사에서는 이 DLL로 가상 장치를 연결한다. 기존 물리 패드가 있으면 테스트 동안 분리한다.
3. Python 3으로 아래 검사기를 실행한다. 첫 세 인자는 플레이어 실행 파일, 훅 DLL, 새 결과 폴더다. Mac에서는 네 번째 인자로 입력 도우미를 전달한다.

```sh
swiftc engine/tests/AudioLab.Vorbis/mac-input.swift -o /tmp/monoon-audio-input
python3 engine/tests/AudioLab.Widgets/verify_widgets.py \
  game-builds/AudioLab/AudioLab.app/Contents/MacOS/MoNoonPlayer \
  engine/.artifacts/bin/VirtualPad/release/VirtualPad.dll \
  /tmp/audiolab-widget-results /tmp/monoon-audio-input
```

Mac에서는 입력 도우미에 키보드·마우스 이벤트를 보낼 권한이 필요하다. Windows는 대화형 데스크톱과 Pillow를 사용한다. Linux는 X11과 xdotool, Pillow를 사용한다. 두 플랫폼은 [데스크톱 입력 도우미](../KeyRoom.Desktop/verify.py)를 가져온다. VM으로 검사기만 복사할 때는 이 파일도 같은 폴더에 둔다.

결과 폴더에는 각 프로세스의 게임 화면과 로그, 실제 플레이어 설정 파일, `report.json`이 남는다. 보고서의 믹서 배열은 음악, 효과음, 마스터 순서다.
