# DisplayLab 데스크톱 화면 설정

각 프로세스는 실제 게임 창을 열었다. 검사는 캡처에서 게임 영역과 버튼 경계를 찾아 운영체제의 마우스 입력으로 버튼을 눌렀다. 설정은 스크립트가 SDL에서 읽은 값, 종료 후 프로필의 `display.settings`, 다음 실행의 첫 상태를 대조했다. VSync 측정값은 렌더러의 동기화 설정이다.

| 환경 | 실행 결과 |
| --- | --- |
| macOS arm64 · Release | 720×540 창에서 전체 화면으로 전환했다. 다시 실행해 전체 화면·VSync 끔을 확인하고 일반 창으로 복귀했다. 세 번째 실행은 720×540 창·VSync 켬으로 시작했다. [측정값](osx-arm64.json) · [복귀한 게임 창](osx-returned.png) |
| macOS arm64 · Native AOT | 같은 세 번의 실행에서 창 크기, 클릭 횟수, 두 토글의 복원값이 Release와 일치했다. [측정값](osx-arm64-aot.json) |
| Windows 11 Enterprise LTSC x64 · Release | 전체 화면으로 재실행했을 때 1280×800 데스크톱 안에 게임이 1280×720으로 표시됐다. 창 복귀와 다음 실행에서 720×540과 VSync 켬을 확인했다. [측정값](win-x64.json) · [전체 화면 재실행](windows-fullscreen.png) |
| Debian 12 x64 · Xvfb + JWM · Release | 1280×800 전체 화면에서 1280×720 게임 영역을 확인했다. 재실행 상태, 창 모드 복귀와 세 번째 실행의 크기·VSync·버튼 경계를 대조했다. [측정값](linux-x64.json) · [전체 화면 재실행](linux-fullscreen.png) |
| Debian 12 x64 · Xvfb 단독 · Release | 720×540 창과 VSync 끔을 재실행 후 복원했다. 창 관리자가 없는 상태에서 전체 화면 요청을 거절하면 창과 포인터가 계속 동작했다. [측정값](linux-x64-windowed.json) · [거절 후 게임 창](linux-windowed.png) |

Mac의 전체 화면 콘텐츠는 노치 아래 2056×1290포인트였다. 그 안에 그린 게임 영역은 2056×1156포인트였다. 720×540 창의 16:9 게임 높이는 405포인트이며, 화면을 정수 좌표로 측정할 때 경계 반올림이 발생한다. 버튼의 안쪽 두 모서리를 누르면 클릭 수가 두 번 증가했다. 네 변 바깥과 검은 여백은 클릭 수를 바꾸지 않았다.

Windows와 Linux는 기존 QEMU VM에서 실행했다. Linux에서는 Debian 공식 JWM 패키지와 필요한 라이브러리를 게스트의 RAM 파일 시스템에 풀었다. 창 모드의 X11 버튼 위치는 창 테두리를 제외한 클라이언트 원점에서 측정했다. 검증 후 RAM 파일 시스템을 해제하고 두 VM을 정상 종료했다.

[재현 명령과 검사 코드](../../DisplayLab.Desktop/README.md) · [샘플 사용법](../../../../docs/manual/game-ui.md#데스크톱-화면-설정)
