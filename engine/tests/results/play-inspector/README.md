# KeyRoom Play Inspector 실시간 편집

2026-11-17에 각 데스크톱의 배포 에디터에서 KeyRoom 복사본을 열었다. Inspector 입력, 체크 해제, Apply selected, Stop, Undo와 Save는 해당 OS의 마우스·키보드로 조작했다. 이동 거리는 실행 중인 KeyRoom 세션에 같은 Up 입력을 60 Hz로 6틱 전달해 측정했다. 표의 거리는 소수 넷째 자리에서 반올림한 월드 단위다.

| 환경 | 원본 240 첫 틱 / 6틱 | 실시간 320 첫 틱 / 6틱 | 단순 Stop 뒤 6틱 | 선택 적용 후 다음 Play 6틱 | 기록 |
| --- | ---: | ---: | ---: | ---: | --- |
| macOS arm64 · MoNoon.app | 4 / 24 | 5.333 / 32 | 24 | 32 | [측정값](osx-arm64.json) · [Inspector](osx-arm64.png) |
| Windows 11 Enterprise LTSC x64 · QEMU | 4 / 24 | 5.333 / 32 | 24 | 32 | [측정값](win-x64.json) · [Inspector](win-x64.png) |
| Debian 12 x64 · QEMU/X11 | 4 / 24 | 5.333 / 32 | 24 | 32 | [측정값](linux-x64.json) · [Inspector](linux-x64.png) |

Speed 320을 처음 시험한 뒤에는 디스크의 씬과 Undo 스택이 그대로였다. 다음 Play에서는 Player의 Local X를 274에서 280으로 옮긴 뒤 X의 체크를 해제하고 Speed 320만 적용했다. 저장 파일을 씬 로더로 다시 읽었을 때 Player의 X는 274였다. 씬 전체를 비교해 Speed만 달라졌음을 확인했다.

Stop 후 macOS에서는 Cmd+Z, Windows·Linux에서는 Ctrl+Z를 사용한다. 세 OS 모두 Undo 한 번으로 씬 전체가 최초 상태로 복원됐고 Undo 스택이 비었다. 다시 저장한 파일도 씬 로더로 읽어 최초 상태와 속도 240을 확인했다.

[회귀 실행 방법](../../KeyRoom.PlayInspector/README.md) · [사용법](../../../../docs/manual/play-diagnostics.md#play-중-값-조정과-선택-적용)
