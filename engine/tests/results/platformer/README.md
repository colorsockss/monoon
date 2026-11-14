# PlatformerLab — macOS 실행 검증

2026-11-14, macOS 27.0.1 / Apple Silicon arm64. 새로 패키징한 MoNoon 에디터에서 프로젝트 복사본의 **Play** 버튼을 눌렀다. Release와 Native AOT는 각각 내보낸 앱 자체를 실행했다. 각 모드에서 새 프로세스와 별도 저장 경로로 두 번 플레이했다. 첫 실행은 실제 **R** 키로 같은 경로를 재생했고 두 번째는 **오른쪽 방향키를 약 2.95초 누른 뒤 놓았다**. 캐릭터의 위치는 두 방법 모두 강체와 캐릭터 모터가 계산했다.

| 실행 | 화면 | R 재생 결과 | 방향키 조작 결과 |
| --- | --- | --- | --- |
| 에디터 Scene Play | [경사를 오르는 중](scene-play-incline.png) · [발판 탑승 후](scene-play-result.png) | [측정값](scene-play.save) | [측정값](scene-play-manual.save) |
| Release arm64 | [발판 탑승 후](release-result.png) | [측정값](release.save) | [측정값](release-manual.save) |
| Native AOT arm64 | [발판 탑승 후](native-aot-result.png) | [측정값](native-aot.save) | [측정값](native-aot-manual.save) |

여섯 번 모두 경사 콜라이더에 실제로 접지했고 높이 **90px**를 올랐다. 이후 오른쪽 입력이 0인 상태로 이동 발판 위에서 100틱을 관찰했다. 첫 표본에서 마지막 표본까지 발판과 캐릭터가 함께 **118.8012px** 이동했고, 두 물체 사이의 최대 상대 변위는 **0px**였다. 같은 시간 동안 충격량을 받은 진자가 수평으로 최대 **28.2996px** 흔들렸고 앵커와의 64px 거리에서 벗어난 최대 오차는 **0.00000763px**였다. 게임이 기록한 여섯 YAML 파일은 바이트 단위로 동일했다(SHA-256: `914a03b99b83c0a9d2c0f7ccd5e8e9e97137ab5e54eebecbc9c8b787129efa54`).

Pendulum의 거리 조인트를 [기본 창](joint-inspector.png)과 [880×660 창](joint-narrow.png)에서 열어 Connect와 Length 필드가 같은 열에 정렬되는지 확인했다. [전체 엔진 검사](checks.txt)는 229개가 통과했다. [물리 검사 코드](../../Engine.Checks/RigidBodyChecks.cs)에는 발판에서 점프로 이탈, 12° 한도를 넘는 경사 차단, 수직 발판과 천장, 빠른 물체의 벽 충돌, 질량비·회전 충격량 및 독립적인 자식 강체가 포함된다.

재현하려면 저장소 루트에서 다음 두 명령으로 게임을 내보내고, 각 앱에서 오른쪽으로 걸어 주황색 발판 위에 멈춘다. **Route complete**와 HUD의 측정값을 확인한다. [샘플과 Inspector 조작법](../../../../docs/manual/platformer-physics.md).

```sh
editor/MoNoon.app/Contents/MacOS/MoNoon --build-release samples/PlatformerLab game-builds/PlatformerLab
editor/MoNoon.app/Contents/MacOS/MoNoon --build-native-aot samples/PlatformerLab game-builds/PlatformerLab
```
