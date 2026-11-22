# AudioLab 공통 위젯 검증

2026-11-17. 각 운영체제에서 AudioLab Release 게임 창을 열었다. 마우스, 키보드, 게임패드는 각각 새 사용자 프로필로 시작했다. 비교한 값은 샘플의 표시 문구와 각 플레이어가 읽은 실제 믹서의 음악·효과음·마스터 게인이다.

| 입력 | 조작 | 음악 | 효과음 | 마스터 |
| --- | --- | --- | --- | --- |
| 마우스 | 음악 트랙의 60% 지점을 클릭 | 80% → 60% | 75% 유지 | 80% 유지 |
| 키보드 | Tab으로 포커스를 준 뒤 왼쪽 방향키 네 번 | 80% → 75% → 70% → 65% → 60% | 75% 유지 | 80% 유지 |
| 게임패드 | Start로 포커스를 준 뒤 D-pad 왼쪽 네 번 | 80% → 75% → 70% → 65% → 60% | 75% 유지 | 80% 유지 |

마우스와 키보드는 macOS 이벤트, Windows 데스크톱 입력, Debian X11 입력을 사용했다. 패드 검사는 SDL 가상 게임패드를 연결해 실제 플레이어의 장치 검색과 폴링을 거쳤다. 각 입력을 완료한 뒤 게임을 닫고 같은 프로필로 새 프로세스를 실행했다. Linux는 SDL 더미 오디오 장치를 사용했다. 아래 화면은 게임패드 조작 직후의 HUD이며, 복원값은 새 프로세스의 믹서 로그에서 읽었다.

| 환경 | 재실행 직후 음악 / 효과음 / 마스터 | 화면 |
| --- | --- | --- |
| macOS 27 arm64 · Apple Silicon | 60% / 75% / 80% | [Mac 게임 창](osx-arm64.png) |
| Windows 11 Enterprise LTSC x64 · 기존 QEMU VM | 60% / 75% / 80% | [Windows 게임 창](win-x64.png) |
| Debian 12 x64 · 기존 QEMU VM, Xvfb | 60% / 75% / 80% | [Linux 게임 창](linux-x64.png) |

세 OS와 세 입력 방식에서 나온 아홉 개의 설정 파일은 모두 72바이트였다. SHA-256은 `427101e21887416bd3ccbf7f001fb8bc8d5c3005310dfd5ae1ff657564f4297c`로 일치했다. 별도 프로필에서는 효과음 드롭다운을 75% → 37.5% → 75%로 바꾸고 마스터 음소거를 켰다가 해제했다. 이때 음악은 80%를 유지했다. [실제 설정 파일](player.settings) · [빌드 ID와 단계별 믹서 값](runs.json)

## Mac 에디터와 Native AOT

MoNoon.app의 Scene Play에서 Tab과 왼쪽 방향키로 음악을 60%로 맞췄다. Play를 멈추고 에디터를 종료한 뒤 같은 프로젝트를 다시 열었다. 첫 Play 화면에 음악 60%, 효과음 75%가 표시됐다. 슬라이더 Inspector도 1,170 × 750과 880 × 660 창에서 확인했다. 최소·최대·간격·기본값 필드가 동일한 열에 정렬됐다. [재실행한 Scene Play](scene-reopened.png) · [작은 창의 Inspector](inspector-compact.png)

macOS Native AOT 배포 앱에서는 마우스로 음악을 60%로 바꾼 뒤 게임을 재실행했다. 복원된 믹서 값과 설정 파일이 Release와 같았다. [Native AOT 재실행 화면](aot-reopened.png)

[재현 명령](../../AudioLab.Widgets/README.md) · [위젯 입력과 값 변경 이벤트 회귀 검사](../../Engine.Checks/UiControlChecks.cs) · [테마 회귀 검사](../../Engine.Checks/UiThemeChecks.cs)
