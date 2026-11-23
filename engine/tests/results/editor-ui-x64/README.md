# 로드맵 12.8: 세 OS 패키지 에디터 회귀와 캡처

2026-11-23 04:49–07:10(KST). 작업 트리 `wip/12-integration`(9f876df)과 아래 [수정](#찾은-문제와-수정)을 반영한 같은 소스로 Mac 에디터, Windows·Linux 에디터 패키지, CLI, 플레이어 템플릿, 검사 프로브를 만들었다. 세 OS에서 KeyRoom으로 패널 이동·분할·플로팅·레이아웃 복원·Play를 확인하고, 창 너비 1280·1600·1920px과 긴 오브젝트 이름 캡처를 남겼다. 6.8 에디터 조작 회귀도 새 패널 구조에서 다시 실행했다.

## 환경

| OS / 아키텍처 | 실행 대상 | 화면 | 비고 |
| --- | --- | --- | --- |
| macOS 27.0.1 (26A434) arm64, Apple M4 Max | `editor/MoNoon.app` (Release, osx-arm64) | Retina 2× | 실제 창 캡처는 `screencapture -l`. 저장소에는 절반 크기로 줄여 넣었다. |
| Windows 11 Enterprise LTSC x64 10.0.26100 (QEMU TCG, 4 vCPU, 6 GB, 네트워크 없음) | 패키지 `editor\MoNoon.exe` (Release, win-x64, self-contained) | 1920×1080, 100% | 로그인 데스크톱은 1280×800이다. 검사 시작 때 `ChangeDisplaySettingsW`(레지스트리에 쓰지 않는 플래그 0)로 이 세션만 1920×1080으로 바꿨고, 다음 부팅에서 다시 1280×800이었다. |
| Debian 12 x64, 커널 6.1.0-56 (QEMU TCG, 4 vCPU, 4.5 GB, SSH) | 패키지 `editor/MoNoon` (Release, linux-x64, self-contained) | Xvfb 2560×1440×24, 창 관리자 없음 | 시스템 디스크(가득 참)는 쓰지 않고 작업 디스크와 tmpfs만 썼다. 작업 디스크에 있던 `libosmesa6` .deb를 다시 풀어 썼다(새로 내려받지 않음). |

- Windows·Linux 결과는 x86-64를 에뮬레이션한 게스트에서 얻었다. 물리 GPU, 실제 키보드·마우스, 다른 화면 배율(125%·150%)을 검증한 기록이 아니다. Windows·Linux 에디터의 Scene은 Avalonia가 그리며 SDL Scene 뷰(NSView)가 없다. 그 검사(그리기 호출 수, NSView 유지)는 두 OS에서 해당 없음으로 건너뛴다.
- 에디터 UI 글꼴은 OS 시스템 글꼴이다(Mac SF, Windows Segoe UI, Linux 기본 sans). Inspector 레이블 열은 고정 폭이라 세 OS의 위치 값이 같다. Linux에서는 `Local pos.`·`Controller` 레이블이 레이블 열보다 넓어 말줄임표와 툴팁으로 줄었다(테마 검사 기준을 만족).
- 게스트에서는 [guest.py](guest.py)가 각 검사기를 그대로 불렀다. 검사기가 띄우는 프로세스 제한 시간을 15배, 6.8 검사기의 대기 시간을 15배, 시작 훅의 대기·제한 시간을 `MONOON_QA_SLOW=10`으로 늘렸다. 확인 항목과 합격 기준은 바꾸지 않았다.
- Windows는 평가판 만료 상태(1시간 뒤 자동 종료)라 단계마다 결과를 가상 USB에 복사하고 끝난 단계를 건너뛰게 했다. 최종 실행은 한 번의 부팅(06:31–07:07)에 끝났다.

## 결과

최종 패키지로 실행한 결과다. 시간은 TCG 게스트의 단계별 소요 시간이다.

| 검사 | macOS arm64 | Windows x64 | Linux x64 |
| --- | --- | --- | --- |
| 6.8 배포 에디터 조작(Player X 274→190, 저장, Play F5, 재실행) | 통과(Xcode UI 테스트, 아래 참고) | 통과 264 s | 통과 158 s |
| EditorDocking `first` | 통과 | 통과 233 s | 통과 224 s |
| EditorDocking `restore` | 통과 | 통과 70 s | 통과 69 s |
| EditorDocking `fallback` | 통과 | 통과 61 s | 통과 49 s |
| EditorDocking `panels` | 통과 | 통과 303 s | 통과 292 s |
| EditorDocking `panels-restore` | 통과 | 통과 69 s | 통과 65 s |
| EditorTheme | 통과 | 통과 196 s | 통과 175 s |
| EditorPanelsDetail(게스트에서 CLI로 Release 비교 빌드 포함) | 통과 | 통과 554 s | 통과 437 s |
| PrefabStructure `editor.py` | 통과 | 통과 57 s | 통과 54 s |
| Rebinding `editor.py`(원래 시간) | 통과 | **실패** 84 s | **실패** 81 s |
| Rebinding `editor.py`(진단: 추가 씬 대기 200 ms) | – | 통과 85 s | 통과 81 s |

- Mac: 최종 빌드로 Docking·Theme·PanelsDetail·Prefab·Rebinding을 다시 통과했다. 6.8 Xcode UI 테스트는 `…` 패딩 수정 전 빌드(DockHost·패널 머리줄 수정 포함)에서 통과했다. 최종 빌드로 다시 돌릴 때는 Mac 화면이 잠겨 있어 XCUITest가 앱을 앞으로 가져오지 못했다(`Failed to activate application … Running Background`). `…` 수정은 Project 경로 줄의 글자 하나에만 영향을 준다. `./monoon test`도 수정 후 통과했다.
- Rebinding 원래 시간의 첫 실패는 두 OS 모두 `FAIL Restore defaults brings back E and North`다. 10.2–10.8 게스트 실행과 같은 원인(TCG에서 추가 씬이 Enter 전에 열리지 않음)이다. 대기 시간만 바꾼 진단 실행은 통과했고 trace·설정 파일이 Mac 기준과 같다.
- EditorPanelsDetail의 Release 비교 빌드는 게스트에서 패키지 CLI(`MONOON_CLI`)로 만들었다. 빌드 ID를 뺀 전 파일이 같았다(Windows 268개, Linux 270개, Mac 269개 파일). Avalonia·Skia·`editor-state` 파일은 없었다.

## 세 OS 비교

| 항목 | macOS | Windows | Linux |
| --- | --- | --- | --- |
| Inspector 레이블 x / 입력 x / 간격 / 어긋남(Docking `first`) 1280·1600·1920·긴 이름 | 1070/1130, 1390/1450, 1710/1770, 1070/1130 · 60 · 0 | 같음 | 같음 |
| Inspector(PanelsDetail, Door) 1280·1600·1920·긴 이름 | 간격 60, drift 0, 세로 0 | 같음 | 같음 |
| Theme 행(27행) 레이블 x / 필드 x / 세로 중심 차 | 8 / 68 / 0 (1280·1920·긴 이름) | 같음 | 같음 |
| 분할선(Inspector 폭 전/넓힘/최소/최종) | 218/298/218/278 | 같음 | 같음 |
| 레이아웃 저장→재실행 | `Row[(Hierarchy*,Project) (Scene*,Game) (Inspector*)]`, Inspector 360, 창 1500×860 일치 | 일치 | 일치 |
| 플로팅 Console 저장→복원 | (538,299) 520×300 일치 | (250,270) 520×300 일치 | (130,150) 520×300 일치 |
| 패널로 연 예전 창 | Console, Profiler, Animator, Clip, Tileset, Input Actions, Dialogue, Data | 같음 | 같음 |
| Console 개수 / 전체 / 로그 끔 / 묶기 / 묶기+전체 / 검색 | 5·2·1 / 8 / 3 / 2 / 5 / 1 | 같음 | 같음 |
| Hierarchy 검색 `satchel`·`Door`·`zzz` | 3·2·0 | 같음 | 같음 |
| 눈·자물쇠 전후·재실행 후 씬 SHA-256 | `FB0C1FD5…715EC8` 다섯 번 같음 | 같음 | 같음 |
| 6.8 저장한 `KeyRoom.scene` | `f2dfa225…c6a42abe` | 같음 | 같음 |
| 6.8 원래 위치 F5 / 편집 후 F5 / 재실행 F5 | `75bbe69f…` / `dc5135b1…` / `dc5135b1…`(Mac 기준 [collected.save](../editor-packaging/collected.save)) | 같음 | 같음 |
| PrefabStructure `editor.txt` | 저장소 Mac 기록과 같음 | 같음 | 같음 |

- 사용자 레이아웃 파일의 크기 값만 다르다. Mac은 Retina에서 0.5px 단위(예: 403.5), 두 게스트는 정수(388)다. 화면에 놓인 패널 위치는 위 표처럼 같다.
- 전체 값: [compare.txt](compare.txt). OS별 JSON(`docking-*.json`, `panels-*.json`, `theme-result.json`)과 사용자 설정 파일(`*-editor-layout.json`, `panels-profile-editor-state.json`), 게스트 로그(`guest.log`), 단계 요약(`summary.json`)을 각 폴더에 두었다.

## 캡처

| | macOS(실제 창, ½ 크기) | Windows | Linux |
| --- | --- | --- | --- |
| 1280 | [main](macos-arm64/panels-main-1280.png) | [main](windows-x64/panels-main-1280.png) | [main](linux-x64/panels-main-1280.png) |
| 1600 | [main](macos-arm64/panels-main-1600.png) | [main](windows-x64/panels-main-1600.png) | [main](linux-x64/panels-main-1600.png) |
| 1920 | [main](macos-arm64/panels-main-1920.png) | [main](windows-x64/panels-main-1920.png) | [main](linux-x64/panels-main-1920.png) |
| 긴 이름 1280 | [main](macos-arm64/panels-long-name-1280.png) · [theme](macos-arm64/theme-main-1280-long-name.png) | [main](windows-x64/panels-long-name-1280.png) · [theme](windows-x64/theme-main-1280-long-name.png) | [main](linux-x64/panels-long-name-1280.png) · [theme](linux-x64/theme-main-1280-long-name.png) |
| Docking 기본 배치 | [1280](macos-arm64/docking-default-1280.png) · [1600](macos-arm64/docking-default-1600.png) · [1920](macos-arm64/docking-default-1920.png) · [긴 이름](macos-arm64/docking-long-name-1280.png) | [1280](windows-x64/docking-default-1280.png) · [1600](windows-x64/docking-default-1600.png) · [1920](windows-x64/docking-default-1920.png) · [긴 이름](windows-x64/docking-long-name-1280.png) | [1280](linux-x64/docking-default-1280.png) · [1600](linux-x64/docking-default-1600.png) · [1920](linux-x64/docking-default-1920.png) · [긴 이름](linux-x64/docking-long-name-1280.png) |
| Play 중 Game 이동 · 플로팅 Scene | [Play](macos-arm64/docking-play-game-moved.png) · [Scene](macos-arm64/docking-float-scene.png) | [Play](windows-x64/docking-play-game-moved.png) · [Scene](windows-x64/docking-float-scene.png) | [Play](linux-x64/docking-play-game-moved.png) · [Scene](linux-x64/docking-float-scene.png) |
| Console | [1280](macos-arm64/panels-console-1280.png) | [1280](windows-x64/panels-console-1280.png) | [1280](linux-x64/panels-console-1280.png) |
| 6.8 | Xcode 결과 번들(저장소 밖) | [Inspector](windows-x64/editor68-edit-inspector.png) · [Play 가방](windows-x64/editor68-reopen-edited-inventory.png) | [Inspector](linux-x64/editor68-edit-inspector.png) · [Play 가방](linux-x64/editor68-reopen-edited-inventory.png) |

Windows·Linux의 `panels-*`·`docking-*` 캡처는 RenderTargetBitmap이다(각 JSON의 `captures`에 방법을 기록). 두 OS는 Scene도 Avalonia가 그려서 Scene 영역이 비지 않는다. Mac의 `docking-default-*`·`docking-long-name-*`은 RenderTargetBitmap이라 Scene이 비어 있다. Linux의 Scene 미리보기에서 게임 HUD 글자 일부가 잘려 보이는 현상(`COIN`→`CCIN`)은 11월 16일 [Linux 캡처](../editor-packaging/linux-inspector.png)에도 있던 것으로, 이번 변경과 무관하다.

## 찾은 문제와 수정

세 가지 모두 100% 배율에서만 드러난다. Mac(2×)에서는 0.5px도 장치 픽셀이라 숨어 있었다.

| 처음 실패(첫 예외) | 원인 | 수정 |
| --- | --- | --- |
| Linux Docking `first`: `InvalidDataException: split drop: Hierarchy at 0, 446, 1058, 334 outside 0, 64, 1280, 715 or below its minimum`. `panels`: `Console at 180, 446, 878, 334 outside …`(Windows는 창 높이에 따라 재현) | 놓기 분할이 Scene 옆 패널 크기를 `span / 2`(357.5px)로 정했다. 반 픽셀 위치의 패널은 위치와 크기를 따로 반올림해 도킹 영역 아래로 1px 넘쳤다. | [DockHost.cs](../../../src/Engine.Editor/Docking/DockHost.cs): 주 패널 옆 고정 크기 열·행의 `GridLength`를 정수 픽셀로 반올림한다. |
| 두 OS PanelsDetail: `Console toolbar centres 459,460`(Windows `449,450`) | 패널 머리줄 28px에서 아래 구분선 1px을 빼면 27px다. 22px 토글은 2.5, 24px 검색 칸은 1.5에 놓이고 짝수 반올림이 둘을 다른 줄로 보냈다. | [PanelStyles.axaml](../../../src/Engine.Editor/PanelStyles.axaml): `panelHeader` 아래쪽 안쪽 여백 1px(내용 영역 26px). 머리줄 높이 28px은 그대로다. |
| 두 OS PanelsDetail: `Project path 1280 toolbar controls are off one centre line: 525.5,525` | 좁은 Project 패널에서 보이는 `…` TextBlock 높이가 13px(홀수)이라 중심이 반 픽셀 어긋났다. | [MainWindow.ProjectView.cs](../../../src/Engine.Editor/Project/MainWindow.ProjectView.cs): `…`에 아래 패딩 1px(14px). |

검사기 수정(동작은 Mac과 같음):

- [verify_editor.py](../../KeyRoom.Desktop/verify_editor.py)(6.8): Play가 메인 도구 모음 가운데로 옮겨져 Position X 클릭 위치와 Inspector 레이블 영역을 오른쪽 가장자리·Play 버튼 기준으로 다시 잡았다. Windows에서 게임이 저장 파일을 바꾸는 순간 읽으면 `PermissionError: [Errno 13] … slot-001.save`가 났다(첫 Windows 실행의 첫 예외). 이때는 다시 시도한다.
- EditorDocking·EditorPanelsDetail 시작 훅: `screencapture`가 없는 OS에서는 RenderTargetBitmap으로 찍고 `captures`에 기록한다. SDL Scene 뷰가 없으면 같은 확인을 Avalonia 뷰포트로 한다. `MONOON_QA_SLOW`로 대기·제한 시간을 늘릴 수 있다(기본 1). PanelsDetail은 1600px 창도 재고 찍는다. `MONOON_CLI`가 있으면 그 CLI로 비교 빌드를 만든다.
- 첫 Windows 실행에서 1600·1920 창이 1284px로 줄고 `restore`가 `restored window size`로 실패했다. 데스크톱이 1280×800(작업 영역 1280×752)이라 1500×860 저장값이 기본 크기로 돌아간 것으로, 12.3의 의도한 대체 동작이다. 1920×1080 세션에서 다시 실행해 통과했다.

## 한계

- 게스트는 TCG 에뮬레이션이며 GPU는 소프트웨어 렌더링이다. 시간 값과 Play 틱 수는 Mac과 비교할 수 없다.
- Linux Xvfb에는 창 관리자가 없다. 플로팅 창의 제목 표시줄·최소 크기 강제는 창 관리자가 있는 데스크톱과 다를 수 있다. 이번 검사에서는 위치와 크기가 저장값대로 복원됐다.
- 탭 끌기와 분할선은 시작 훅이 포인터 이벤트를 보내 확인했다. 실제 마우스 입력은 6.8 검사(Windows SendInput, Linux xdotool, Mac XCUITest)만 사용했다.
- Windows 디스플레이 1920×1080은 검사 세션에서만 바꾼 값이다. 다른 배율과 여러 모니터는 확인하지 않았다.

재실행 스크립트와 전체 결과는 이 Mac의 `.test-env/work/ui12/`에 있다(`pack.py`, `guest.py`, `linux.sh`, `start-win.sh`, `compare.py`, `evidence.py`).
