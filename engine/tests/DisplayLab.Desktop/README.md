# DisplayLab 화면 설정 검증

DisplayLab Release를 실제 데스크톱에서 세 번 시작한다. 처음에는 720×540으로 창 크기를 바꾼 뒤 VSync를 끄고 전체 화면으로 전환한다. 두 번째 실행에서 전체 화면과 VSync 해제를 확인하고 창 모드로 돌아와 VSync를 켠다. 마지막 실행에서 창 크기와 VSync 복원을 확인한다.

검사기는 창 캡처에서 800×450 논리 화면과 버튼의 실제 경계를 측정한다. 버튼의 네 변 바깥, 레터박스, 안쪽 두 모서리를 마우스로 누르고 클릭 횟수를 비교한다. 각 실행의 SDL 상태 로그와 저장된 YAML도 검사한다. 결과 경로에는 새 폴더를 지정한다.

```sh
./monoon build samples/DisplayLab release --profile distribution
swiftc engine/tests/AudioLab.Vorbis/mac-input.swift -o /tmp/monoon-display-input
python3 engine/tests/DisplayLab.Desktop/verify_display.py \
  game-builds/DisplayLab/DisplayLab.app/Contents/MacOS/MoNoonPlayer \
  /tmp/display-results /tmp/monoon-display-input
```

Python에는 Pillow가 필요하다. Mac에서는 창 크기 변경·포인터 입력·화면 캡처 권한이 필요하다. Windows와 Linux에서는 입력 도우미 인자를 생략한다. 게스트에 검사기를 복사할 때는 [데스크톱 창 조작 코드](../KeyRoom.Desktop/verify.py)도 같은 폴더에 둔다. Linux는 xdotool과 전체 화면을 지원하는 X11 데스크톱을 사용한다.

별도로 창 관리자가 없는 Xvfb에서 `--no-window-manager`를 세 번째 인자로 전달하면 거절 경로를 검사한다. 거절 후 버튼이 계속 동작하는지, 다음 실행에서 창 크기와 VSync가 유지되는지 확인한다.
