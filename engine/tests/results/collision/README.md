# CollisionLab — macOS 실행 검증

2026-11-14, macOS 27.0.1 / Apple Silicon arm64. 기존 Mac 창·입력 검증 도구로 각 앱을 열고 오른쪽 방향키를 한 번 보냈다. 에디터에서는 샘플 복사본의 **Play** 버튼으로 시작했다. Release와 Native AOT는 각각 내보낸 앱 자체를 실행했다. 각 프로세스는 별도의 플레이어 저장 경로를 사용했다.

| 실제 실행 | 입력 전 | 입력 후 | 게임이 기록한 값 |
| --- | --- | --- | --- |
| 에디터 Scene Play | [준비](editor-ready.png) | [정지](editor-result.png) | [editor.save](editor.save) |
| Release arm64 | [준비](release-ready.png) | [정지](release-result.png) | [release.save](release.save) |
| Native AOT arm64 | [준비](aot-ready.png) | [정지](aot-result.png) | [aot.save](aot.save) |

원 중심은 (100, 200), 반지름은 20이다. 한 틱에 오른쪽으로 800px 이동시켰다. 세 경로 모두 센서의 양 끝을 한 번에 가로질러 **진입 이벤트를 받았고**, 서로 다른 마스크 조건으로 두 개의 박스 게이트를 모두 통과했다. 삼각형은 X=500에서 맞았다. 레이 길이는 **400**, 원 스윕 거리는 **380**, 충돌 법선은 **(-1, 0)**, 실제 Transform의 정지 위치는 **(460, 180)**이었다. 필터가 트리거를 제외했고, 삼각형 내부가 아닌 경계 상자의 빈 모서리에서도 겹침이 없었다. 세 저장 파일은 바이트 단위로 동일했다. SHA-256: `0b35579c4015303f9cd266b8676dffe583e26acac81fa9801235058410f84991`.

다각형 [Inspector](inspector.png)를 [880×660으로 줄인 창](inspector-narrow.png)에서 확인했다. 레이어·마스크·꼭짓점 필드가 같은 열을 유지했다. 복사본에서 삼각형의 두 번째 점을 (1, 0.5)에서 (1, 0.75)로 바꾼 후 Cmd+S로 저장하고 앱을 다시 열어 [변경된 꼭짓점](inspector-reopened.png)을 확인했다.

[엔진 검사](../../Engine.Checks/CollisionChecks.cs)는 레이어 31, 반전·비균일 스케일의 원, 접선·거리 끝점·내부 시작, 원과 꼭짓점의 충돌 법선, 다각형 스윕과 잘못된 다각형 거부, 음수 셀 좌표와 회전된 네 가지 타일 레이아웃도 확인한다. 기존 KeyRoom 이동·문·코인 및 타일 빗면 회귀를 포함한 [전체 실행 로그](checks.txt).

재현: [CollisionLab](../../../../samples/CollisionLab)을 열어 Play 후 **→ 또는 D**. 독립 앱은 저장소 루트에서 아래처럼 빌드한 뒤 각 앱을 열고 같은 키를 누른다. HUD의 PASS와 수치를 비교한다.

```sh
editor/MoNoon.app/Contents/MacOS/MoNoon --build-release samples/CollisionLab game-builds/CollisionLab
editor/MoNoon.app/Contents/MacOS/MoNoon --build-native-aot samples/CollisionLab game-builds/CollisionLab
```
