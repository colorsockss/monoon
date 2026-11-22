# PlatformerLab 경사 타일과 단방향 발판 검증

시작 위치에서 Space로 발판을 통과해 착지한 뒤, 오른쪽 경사를 올라갔다가 왼쪽으로 내려왔다. 세 실행 경로 모두 같은 [380프레임 입력 기록](../../PlatformerLab.TileCollision/Traverse.inputreplay)을 사용했다. 관찰 스크립트를 붙인 샘플 복사본으로 기록했다. TSV는 매 고정 틱의 캐릭터 좌상단 X·Y, 수직 속도, 접지 오브젝트 ID, 접지 법선을 담는다. 캐릭터의 높이는 30px다.

| 측정 항목 | 결과 |
| --- | --- |
| 점프 전 | 머리 Y=300, 발 Y=330. 발판 윗면은 Y=288 |
| 아래에서 위로 통과 | 발이 Y=280.3889까지 올라갔다. 상승 중 발판에 접지한 틱은 0개 |
| 하강 후 착지 | 35번째 틱에 윗면 Y=288에 착지했다. 오른쪽 입력 전까지 같은 높이와 접지를 유지했다 |
| 경사 이음새 | 다섯 타일의 이음새 X=218·256·294·332를 오르막과 내리막에서 모두 통과했다 |
| 경사 중 연속성 | 각 방향의 측정 구간 70틱 모두 경사에 접지했다. 수평 이동은 틱당 2.5px, 발과 경사선의 최대 오차는 기록 정밀도에서 0.0001px였다 |

## 실행 경로

에디터에서는 배포 패키지의 타일셋 창으로 다각형 꼭짓점과 면의 방향을 바꿔 저장한 뒤 다시 열어 확인했다. 원래 형상을 복구하고 Scene Play를 시작했다. 각 OS에서 배포용 Release와 Native AOT를 빌드해 같은 입력을 창 없이 재생했다. 별도로 SDL 게임 창에 점프와 이동 키를 보내 실제 착지와 경사 주행 화면을 캡처했다. 실시간 조작의 틱 번호는 실행마다 달라진다.

| 환경 | Scene Play | Release | Native AOT | 실제 SDL 창 |
| --- | --- | --- | --- | --- |
| macOS arm64 | [380틱](osx-arm64/scene-play.tsv) | [380틱](osx-arm64/release.tsv) | [380틱](osx-arm64/native-aot.tsv) | [Release 착지](osx-arm64/release-on-ledge.png) · [AOT 경사](osx-arm64/native-aot-on-slope.png) |
| Windows 11 x64 · QEMU | [380틱](win-x64/scene-play.tsv) | [380틱](win-x64/release.tsv) | [380틱](win-x64/native-aot.tsv) | [Release 착지](win-x64/release-on-ledge.png) · [AOT 경사](win-x64/native-aot-on-slope.png) |
| Debian 12 x64 · QEMU, Xvfb | [380틱](linux-x64/scene-play.tsv) | [380틱](linux-x64/release.tsv) | [380틱](linux-x64/native-aot.tsv) | [Release 착지](linux-x64/release-on-ledge.png) · [AOT 경사](linux-x64/native-aot-on-slope.png) |

아홉 TSV는 기록 정밀도에서 바이트 단위로 일치했다. 위치와 법선은 소수 넷째 자리, 수직 속도는 소수 셋째 자리까지 기록했다. 공통 SHA-256은 `58ba77ef847ecafb01391c6d741ed7c75880ebf3bbc7c185419bc999343c64ed`다. 에디터에서는 창 폭 700과 380에서 입력 열과 저장 버튼 정렬도 확인했다. [작은 창의 경사 다각형](osx-arm64/polygon.png) · [단방향 면과 차단 방향](osx-arm64/face.png) · [재현 명령](../../PlatformerLab.TileCollision/README.md)
