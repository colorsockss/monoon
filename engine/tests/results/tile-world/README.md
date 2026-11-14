# PlatformerLab: 오토타일과 월드 청크

2026-11-14, macOS 27.0.1 / Apple Silicon arm64. 에디터에서는 샘플 복사본으로 **Play**를 눌렀고, 배포판은 각각 내보낸 앱 자체를 새 저장 경로로 실행했다. 자동 경로 없이 **→ 약 2.5초, ← 약 2.6초**로 경사를 왕복했다. 각 방향의 기록은 키를 놓은 시각이 아닌 지정한 청크 경계를 처음 넘은 틱에 저장한다.

| 창 | 시작 | 청크 2 진입 | 청크 0 복귀 |
| --- | --- | --- | --- |
| Scene Play | [화면](scene-play-start.png) | [화면](scene-play-outbound.png) · [저장값](scene-play-outbound.save) | [화면](scene-play-return.png) · [저장값](scene-play-return.save) |
| Release arm64 | [화면](release-start.png) | [화면](release-outbound.png) · [저장값](release-outbound.save) | [화면](release-return.png) · [저장값](release-return.save) |
| Native AOT arm64 | [화면](native-aot-start.png) | [화면](native-aot-outbound.png) · [저장값](native-aot-outbound.save) | [화면](native-aot-return.png) · [저장값](native-aot-return.save) |

전체 지형은 가로로 배치한 9개 청크다. 추적 반경 1에서 시작 시 -1·0·1 세 청크만 로드됐다. 중심 X가 2가 됐을 때는 1·2·3이 상주했다(누적 로드 5, 해제 2). 원점의 타일을 향한 레이는 빗나갔고 3번 청크를 향한 레이는 지형에 맞았다. 중심 X가 0으로 돌아오자 누적 로드 7, 해제 4였고 두 레이의 결과도 뒤바뀌었다. 왕복 모두 상주 수는 3이었으며, 세 실행 방식의 왕로 기록끼리, 귀로 기록끼리 각각 바이트 단위로 같았다. 두 배포 앱 모두 씬에는 인덱스만 있었고, 외부 YAML 청크 9개의 SHA-256이 참조와 일치했다.

에디터 복사본의 Ridge terrain에서 같은 브러시로 **(31,5)** 한 셀을 칠하고, 다른 청크에 속하는 **(32,5)**를 다음 획으로 칠했다. [한 셀](brush-single.png) → [두 셀 연결](brush-connected.png) → [Cmd+Z 한 번](brush-undo.png). 확대 캡처: [한 셀](detail-single.png) · [연결됨](detail-connected.png) · [Undo](detail-undo.png). Cmd+S로 두 논리 셀이 저장됐고, Undo 후 다시 저장하면 첫 셀만 남았다. [기본 창](inspector-layout.png)과 [작은 창](inspector-narrow.png)에서 Tilemap Inspector의 Follow·Radius 행도 확인했다.

[전체 검사: 242개 통과](checks.txt). [추가한 회귀 검사](../../Engine.Checks/TileWorldChecks.cs)는 음수 좌표 경계, 지우기·취소, 다른 브러시 분리, 등각·육각 이웃, 인접 청크가 없어도 변형 유지, 손상된 청크의 지연 검출, 추적 대상 부재 시 해제, 복귀 후 충돌 복원을 포함한다. [사용법](../../../../docs/manual/tilemaps.md#오토타일-브러시).
