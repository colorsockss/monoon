# KeyRoom x64 데스크톱 검증

이 Mac의 기존 환경은 저장소 루트의 `.test-env/`에 있다. 두 VM이 꺼진 상태에서 저장소 루트에서 실행한다. 인자로 새 결과 폴더 또는 빈 폴더를 지정한다.

```sh
python3 engine/tests/KeyRoom.Desktop/retest.py linux   .test-env/results/linux-01
python3 engine/tests/KeyRoom.Desktop/retest.py windows .test-env/results/windows-01
```

현재 플랫폼별 Release 배포본을 게스트에 넣고 실제 게임 창을 두 번 실행한다. 매번 첫 방에서 시작해 방향키와 WASD 각각으로 열쇠를 얻고 두 번째 방의 벽·열쇠 HUD·글자가 나타나는지 화면 픽셀로 판정한다. 이어서 상하 이동과 창 크기 두 가지의 16:9 표시 영역을 확인한다. Mac에서 게스트가 기록한 엔진 DLL 및 YAML 파일의 SHA-256을 배포본과 대조한다.

첫 실행에서는 이동하기 전에 **Controls**를 별도로 검사한다. 800×450, 960×432, 640×600 각각에서 Tab으로 포커스 테두리가 나타나는지 확인한 뒤 Enter로 안내를 열고 닫는다. 화면에서 실제로 측정한 버튼 경계의 왼쪽 위와 오른쪽 아래 안쪽을 클릭하고, 네 변 바로 바깥과 레터박스 여백을 클릭한다. 판정에는 게임 내부 상태 대신 창 캡처에 찍힌 안내 배경과 글자를 사용한다.

결과에는 운영체제·아키텍처·해시·측정값(`results.json`), 실행 로그, 입력 방식별 게임 창 PNG가 남는다. `controls/base`, `controls/wide`, `controls/tall`에는 키보드 포커스, 안내를 열고 닫은 상태, 바깥쪽 클릭 후 화면이 있다. `controls_ui[].button_measured`는 창의 콘텐츠 왼쪽 위를 원점으로 하는 [왼쪽, 위, 오른쪽, 아래) 픽셀 좌표다. `--attest-only`는 배포된 버튼 스크립트와 엔진·콘텐츠 해시, 세 크기의 측정값·캡처를 대조한다.

Linux는 Debian x64의 X11 가상 디스플레이와 기본 SDL 렌더러, Windows는 Windows 11 Enterprise LTSC x64의 데스크톱과 기본 SDL 렌더러를 쓴다. 정상 종료 시 게스트만 꺼지며 디스크와 도구는 유지된다.

이미 설치된 다른 x64 데스크톱에서도 `python verify.py GAME_DIRECTORY OUTPUT_DIRECTORY`로 게임을 검사할 수 있다. Pillow가 필요하며 Linux X11에서는 xdotool도 필요하다.

2026-11-12: 이 Mac에서 두 게스트를 종료한 뒤 다시 부팅해 같은 YAML 배포본의 두 입력 경로를 재검증했다. Windows 오프라인 평가판 바탕화면에는 라이선스 만료 표시가 있었으며 실행·입력·캡처 검사는 통과했다. 장기 사용에는 유효한 라이선스 상태를 확인한다.

## 배포 에디터 조작 회귀

게임 창과 별도로 세 OS의 배포 에디터를 검사한다. Apple Silicon Mac에서는 Xcode UI 테스트가 원래 배치, 편집, 재열기를 각각 새 `MoNoon.app` 프로세스와 저장 폴더에서 실행한다. 저장소 루트에서 다음 명령을 실행한다.

```sh
engine/scripts/build-editor-mac.sh
python3 engine/scripts/package-editor.py keyroom
engine/tests/KeyRoom.Desktop/mac/verify.sh editor/MoNoon.app samples/dist/KeyRoom-sample.zip \
  /tmp/monoon-editor-result engine/tests/results/editor-packaging/collected.save
```

결과 경로는 새 디렉터리여야 한다. macOS 결과에는 씬과 편집·재열기 F5 파일이 남는다. Xcode에서 `ScenePlay.xcresult`를 열면 단계별 캡처와 원래 위치의 F5 저장도 확인할 수 있다.

Windows·Linux에서는 배포 압축을 푼 뒤, Pillow가 있는 Python 3으로 아래 명령을 실행한다. 두 검사 스크립트는 같은 폴더에 둔다. 화면 배율 100%, 가려지지 않은 기본 크기의 에디터 창을 사용한다. Linux X11에서는 xdotool이 필요하다.

```powershell
python verify_editor.py .\editor\MoNoon.exe .\KeyRoom-sample.zip .\editor-result
```

```sh
SDL_AUDIO_DRIVER=dummy xvfb-run -a -s "-screen 0 1400x900x24" \
  python3 verify_editor.py ./editor/MoNoon ./KeyRoom-sample.zip ./editor-result
```

검사기는 새 결과 폴더 안에 샘플을 풀고 배포 실행 파일을 두 번 시작한다. 원래 배치는 이동 전 F5 저장에 `key: false`를 기록해야 한다. 검사기가 Inspector로 Player X를 190으로 바꾼 뒤 Ctrl+S를 보내면 씬 파일과 다음 실행의 Play에 그 값이 반영되어야 한다. 각 실행의 저장 폴더와 창 캡처를 결과 폴더에서 대조할 수 있다.

Windows·Linux 검사기에 `--expected-save`와 [Mac 기준 파일](../results/editor-packaging/collected.save)의 경로를 주면 편집 직후와 재시작 후의 F5 저장을 각각 바이트 단위로 비교한다. 불일치하면 로그에 실제값과 기준값의 바이트 수와 SHA-256이 남는다.
