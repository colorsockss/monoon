# 에디터와 빌드된 게임: 오류가 난 곳부터 찾기

| 증상이 나타난 곳 | 먼저 할 일 |
| --- | --- |
| Scene 편집 또는 에디터 안의 Play | 같은 장면에서 **Run → Save & Run**도 실행한다. 별도 게임 창만 정상이면 [macOS](macos.md) / [Windows](windows.md)의 **엔진·에디터** 절에서 Scene 호스트, 포커스, 좌표를 확인한다. Linux 에디터는 지원하지 않는다([Linux](linux.md)). |
| 편집은 되지만 Build가 실패 | 아래에서 템플릿 위치, 시작 씬, 에셋 참조, 빌드 종류를 확인한다. |
| 내보낸 게임만 실패 | 에디터를 닫고 해당 OS 문서의 명령으로 **배포본 자체**를 실행해 첫 예외를 확보한다. 샘플 소스를 `dotnet run`한 결과는 배포 검증이 아니다. |

## 내보내기: 증상 → 조치

- **`Missing … player template`**: 기본 Release는 에디터 밖의 `player-templates/<RID>/`를 사용한다. `MONOON_PLAYERS_DIR`를 설정했다면 값은 `win-x64` 같은 개별 폴더가 아니라 그 **부모의 절대 경로**여야 한다. 소스에서 예컨대 `./engine/scripts/build-players.sh win-x64`로 템플릿을 갱신한 뒤, 에디터의 **Build → Build Game…**에서 플랫폼을 선택해 게임을 **다시 내보낸다**. 템플릿 갱신만으로 기존 배포본은 바뀌지 않는다.
- **시작 씬/카메라/참조 오류**: Build에서 시작 씬을 포함 목록에 넣고 해당 씬의 시작 카메라를 확인한다. KeyRoom은 ShowcaseRoom2와 겹쳐 여는 UI 씬(ShowcaseGuide, ShowcaseBindings)도 포함해야 한다. 오류가 지목한 GUID는 프로젝트의 `.meta`와 대응시킨다. 파일을 옮겼다면 원본과 `.meta`를 함께 복원해 같은 GUID를 유지한다. 옛 JSON 프로젝트는 **복사본을 에디터에서 열어** YAML 변환·재열기를 확인한 뒤 내보낸다. 배포 플레이어는 변환기가 아니다.
- **에디터와 게임의 내용이 다름**: 씬을 저장하고 새 빈 출력 위치에 빌드한다. 실행 파일 옆 `Game/`(Mac은 앱 안 `Contents/MacOS/Game/`)에서 `GameSettings`, `.monoon-assets`, 포함한 씬·클립과 PNG를 확인한다. 배포는 앱 또는 대상 폴더 **전체**를 복사한다. `.monoon-player`는 빌더가 기존 게임을 식별하는 표식이다. 다른 폴더를 덮어쓰기 위해 직접 만들지 않는다.
- **Release는 되지만 Native AOT 실패**: Release 템플릿은 타깃별로 교차 내보낼 수 있다. Native AOT는 대상 OS·아키텍처와 일치하는 호스트, .NET 10 SDK, 네이티브 링커, `player-templates/native-aot/` 빌드 키트가 필요하다. 키트가 없거나 엔진 변경 전 것이라면 [키트 생성 스크립트](../../engine/scripts/build-native-aot-kit.sh)로 갱신한다. Mac AOT 결과는 별도 `NativeAOT/`에 남기고 Release와 구분한다.

## 이 Mac에 보존한 x64 환경 재사용

두 게스트가 꺼진 상태에서 저장소 루트에서 실행한다. 아래 마지막 인자는 **매번 새 이름 또는 빈 디렉터리**로 지정한다. 정상 종료 후 디스크와 도구는 유지된다.

```sh
python3 engine/tests/KeyRoom.Desktop/retest.py windows .test-env/results/windows-next
python3 engine/tests/KeyRoom.Desktop/retest.py linux   .test-env/results/linux-next
```

각 게스트에서 YAML Release 게임을 두 번 연다. **방향키와 WASD 각각**으로 첫 방에서 열쇠를 얻고 둘째 방의 벽과 HUD(`ROOM 2 / KEY`)를 확인한다. 960×432 창의 게임 영역은 768×432(좌우 96 px), 640×600 창은 640×360(상하 120 px)이어야 한다. 로그만 보지 말고 `results.json`과 두 입력별 캡처를 함께 확인한다. 새로 플레이하지 않고 보관된 기록이 **현재 배포본**과 일치하는지만 대조하려면:

```sh
python3 engine/tests/KeyRoom.Desktop/retest.py windows engine/tests/results/screen-ui/windows-x64 --attest-only
python3 engine/tests/KeyRoom.Desktop/retest.py linux   engine/tests/results/screen-ui/linux-x64 --attest-only
```

[인게임 UI(v7) 게스트 실행 기록](../../engine/tests/results/screen-ui/README.md)에는 Controls 버튼의 클릭·Tab·Enter와 세 창 크기의 경계 측정값도 있다. 예전 `yaml-windows-x64`와 `yaml-linux-x64`는 UI 도입 전 기록이다. 새 게임을 내보내면 예전 기록의 해시가 달라질 수 있으니 게스트 재실행 명령으로 새 결과를 만든다.

[검사기와 준비 조건](../../engine/tests/KeyRoom.Desktop/README.md) · [빌드가 수집하는 파일과 검증 규칙](../../engine/src/Engine.Authoring/GameBuild.cs). 위 방향키·WASD 및 창 크기 검사는 KeyRoom Release에서 수행했다. 공용 번역과 기본·대체 폰트는 두 게스트에서 각각 Release와 Native AOT 게임 창으로 검증했다. [한국어 화면과 실행별 글꼴 기록](../../engine/tests/results/localization/x64/README.md). InventoryLab은 별도 [x64 게스트 빌드·재생 절차](../../engine/tests/InventoryLab.Replay/README.md)로 Release와 Native AOT의 헤드리스 입력 경로를 검사한다. 13–16장의 Linux x64 회귀는 하네스의 Release·Native AOT 단계를 게스트에서 빌드해 다시 실행했다. 게스트 구성과 Mac과 다른 값은 [next1-x64 기록](../../engine/tests/results/next1-x64/README.md)에 있다. Windows 검증은 장비 준비 전까지 보류 중이다.

Windows 배포 에디터의 Inspector와 Play 문제는 [패키지 조작 회귀](../../engine/tests/KeyRoom.Desktop/README.md#배포-에디터-조작-회귀)로 재현한다. [두 게스트와 Mac의 KeyRoom 화면 및 저장 결과(Linux 에디터 지원 중단 전 기록)](../../engine/tests/results/editor-packaging/README.md)에서 기준을 확인할 수 있다.
