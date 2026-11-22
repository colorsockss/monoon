# roadmap 10.2–10.8 x64 게스트 실행

2026-11-22 23:40 – 11-23 02:17(KST)에 이 Mac(Apple M4 Max)의 QEMU 게스트 두 대에서 커밋 `222306c`의 기능 회귀를 다시 실행했다. 두 게스트는 x86-64를 TCG로 에뮬레이션한다. 시간 값(프레임 시간, 로딩 시간, 업데이트 µs)은 Mac 값과 비교할 수 없으며 표에서는 “TCG”로 표시한다.

| 게스트 | 사양 | 에디터 실행 | Release·Native AOT |
| --- | --- | --- | --- |
| Windows 11 Enterprise LTSC x64 (10.0.26100.1742) | 4 vCPU, 6 GB, 네트워크 없음. FAT 가상 USB로 입출력 | 로그인 세션의 패키지 에디터 `editor\MoNoon.exe` | 게스트에서 MSVC 도구로 빌드한 `KeyRoom-win-x64\MoNoonPlayer.exe` 등 |
| Debian 12 x64 (커널 6.1.0-56) | 4 vCPU, 4.5 GB, SSH | Xvfb와 Mesa 소프트웨어 GL(`libosmesa6` 22.3.6-1+deb12u2를 작업 디스크에 풀어서 사용, 설치하지 않음)에서 `editor/MoNoon` | 게스트에서 clang으로 빌드한 `…-linux-x64/MoNoonPlayer` |

- Mac에서 같은 작업 트리로 플레이어 템플릿(`engine/scripts/build-players.sh`), Windows·Linux 에디터 패키지(`engine/scripts/package-editor.py`), CLI, 검사용 에디터 프로브 DLL을 다시 만들었다. 관찰 스크립트를 붙이는 각 검사기의 `prepare` 단계도 Mac에서 실행했다.
- 게스트에서는 CLI로 프로젝트마다 Release와 Native AOT를 빌드했다(Native AOT는 대상과 같은 OS의 호스트가 필요하다). 빌드 결과에 SkiaSharp 파일이 없는지 확인했다. 독립 게임은 모두 `--replay-input` 헤드리스 재생이다. 물리 GPU와 실제 키보드·패드 입력은 사용하지 않았다.
- [guest.py](guest.py)가 각 검사기의 `verify.py`·`replay.py`·`editor.py`를 그대로 불러 실행했다. 에뮬레이션 속도 때문에 검사기가 띄우는 프로세스의 제한 시간만 15배로 늘렸다. 단계마다 결과를 [Windows 요약](win-x64-summary.json)과 [Linux 요약](linux-x64-summary.json)에 기록했다. 실패한 단계의 첫 예외도 이 파일에 있다.
- 저장소의 엔진·검사기 코드는 바꾸지 않았다. 두 진단 실행(아래)은 게스트 스크립트 안에서만 값을 바꿨다.

## 실행 중 문제

- Windows 평가판이 만료된 상태다(“Windows License is expired”). 부팅 후 약 1시간에 Windows가 스스로 꺼졌다. 첫 실행은 BarrageLab Native AOT 재생 중에 중단됐다. 이후 단계마다 결과를 USB에 복사하고, 끝난 단계는 건너뛰도록 해서 세 번에 나눠 실행했다(23:40, 00:45, 01:47 시작). 중단된 단계는 처음부터 다시 실행했다. Split Loading 에디터 검사(실행 3회)는 1회씩 세 단계로 나눴다.
- Debian 시스템 디스크가 가득 찬 상태(12 GB 중 여유 0)였다. 시스템 디스크의 파일은 지우지 않았다. 모든 작업은 별도 작업 디스크와 tmpfs에서 했고, `libosmesa6`은 apt 캐시를 작업 디스크로 지정해 내려받았다.
- 두 게스트를 동시에 실행했다(합계 10.5 GB). 메모리 부족은 없었다.

## 진단 실행

| 이름 | 바꾼 값 | 이유 |
| --- | --- | --- |
| `rebinding-scene-play-wait200` | 사본의 TraceBindings가 추가 씬을 기다리는 시간 20 ms → 200 ms(재생 프레임당) | 원래 검사의 Scene Play가 두 OS에서 같은 이유로 실패했다. [조작키 설정 기록](../player-rebinding/README.md#windows-x64와-linux-x64-게스트) |
| `split-long-release`, `split-long-native-aot` | 입력 기록 300 → 1,200프레임, 실행 1회, 취소 재시도 3회·2프레임 → 최대 8회·8프레임 | 예산 4 ms 로딩이 300프레임 안에 연결되지 않았다. [나눠 읽기 기록](../split-loading/README.md#windows-x64와-linux-x64-게스트) |
