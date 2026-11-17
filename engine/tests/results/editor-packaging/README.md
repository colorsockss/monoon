# 배포 에디터에서 KeyRoom 편집과 재열기

2026년 11월 16일. 각 에디터의 실행 파일에 프로젝트 폴더를 넘겨 KeyRoom 첫 씬을 열었다. Hierarchy의 Player가 선택된 상태에서 Inspector의 Position X를 274에서 190으로 바꿨다. Y는 114다. 이 위치에서는 게임을 시작할 때 플레이어와 열쇠 트리거가 겹친다.

## Mac 실행 결과

macOS 27.0.1 arm64에서 Inspector에 값을 입력하고 ⌘S로 씬을 저장했다. 에디터 프로세스를 다시 시작한 뒤에도 X가 190이었다. 같은 프로젝트를 별도의 출력 폴더에 배포용 프로필로 빌드하고 두 앱을 각각 실행했다. 매 실행에서 새 저장 폴더로 시작해 이동 없이 F5로 진행 상황을 저장하고 I로 가방을 열었다.

| 실행 | 가방에 표시된 의뢰 | 화면 |
| --- | --- | --- |
| Scene Play | Quest complete / Brass key | [재열기 후 Inspector](mac-inspector.png) · [Play](mac-scene-play.png) |
| Release | Quest complete / Brass key | [게임 창](mac-release.png) |
| Native AOT | Quest complete / Brass key | [게임 창](mac-native-aot.png) |

세 경로의 [F5 저장 파일](collected.save)은 바이트 단위로 일치했다. 첫 방의 방 ID와 함께 `key: true`, `chest: false`, `coin: false`가 기록됐다. Release 빌드 ID는 `ee36d6119bbb4e23993edf131316339d`, Native AOT는 `17f79974be1b42fd9640baafbdf9e1a9`다.

## x64 에디터 조작

검사기는 각 VM에 배포한 에디터를 실행하고 실제 마우스·키보드 입력을 보낸다. 원래 위치에서 Play와 F5를 먼저 확인하고, Play를 멈춘 후 Transform을 수정해 Enter와 Ctrl+S로 저장한다. 같은 에디터에서 한 번, 새 프로세스와 새 게임 저장 폴더에서 한 번 더 Play한다. [재실행 명령](../../KeyRoom.Desktop/README.md#배포-에디터-조작-회귀) · [검사기](../../KeyRoom.Desktop/verify_editor.py)

| 보존한 QEMU 게스트 | 원래 배치에서 열기 | 재실행 후 X | 재실행 후 Play |
| --- | --- | --- | --- |
| Debian 12.15 amd64, X11/Xvfb 1400×900 | [Find Brass key](linux-baseline.png) | [190](linux-inspector.png) | [Quest complete / Brass key](linux-play.png) |
| Windows 11 Enterprise LTSC x64, 빌드 26100 | [Find Brass key](windows-baseline.png) | [190](windows-inspector.png) | [Quest complete / Brass key](windows-play.png) |

두 게스트의 검사 종료 코드는 모두 0이었다. 기본 위치에서 생성한 체크포인트는 두 OS 모두 `key: false`였고, 편집 직후와 재열기 후에는 Mac에서 얻은 `collected.save`와 바이트 단위로 일치했다. 세 OS의 에디터로 저장한 `KeyRoom.scene`과 두 Mac 게임에 포함된 씬의 SHA-256도 같았다. 원본 샘플의 시작 X는 274다.

| 비교한 파일 | SHA-256 |
| --- | --- |
| 편집한 KeyRoom.scene | `5d22a675383c9e6c87156d02220119db69954b6ce8f7ebbf1c95494f2b4d5f68` |
| 열쇠 획득 후 F5 저장 | `dc5135b1765858e339c9279219053ff931976ec64c804690fffb14b8d3f3cfd5` |
| Windows 에디터 ZIP | `040624d8876aa3d39ccafaed0099ffa937f4c5f78a5716be883e198167841314` |
| Linux 에디터 tar.gz | `46677eaa0a5b51b48b438fa7372f2e5a01f4bf51ccbc8a83523311d8de2bbe17` |
| 편집용 KeyRoom ZIP | `69f0dc109d4ea97cbf5f03a52752bc5975399c171c0dceead4a8590e4711fb41` |
