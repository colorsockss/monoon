# SpriteLab · macOS arm64 검증

[SpriteLab](../../../../samples/SpriteLab)의 왼쪽은 트림 전 16×12인 화살표다. 가운데에는 X 반전, 픽셀 정렬, 색상과 알파 마스크를 적용했다. 오른쪽은 같은 Renderer 설정에서 아틀라스의 화살표(4초)와 점(0.7초)을 반복한다.

| 이 Mac에서 실행한 대상 | 화살표 프레임 | 점 프레임 |
| --- | --- | --- |
| 에디터 Scene Play | [Play](editor-play.png) | [다음 프레임](editor-dot.png) |
| 독립 Release | [게임 창](release-play.png) | [다음 프레임](release-dot.png) |
| 독립 Native AOT | [게임 창](aot-play.png) | [다음 프레임](aot-dot.png) |

세 창을 실행해 게임 좌표를 기준으로 배경, 흰색과 원래 색, 반전된 화살표, 마스크의 불투명·반투명·완전 투명 영역을 비교했다. 화살표 프레임의 아홉 위치에서 sRGB로 읽은 RGB 값이 세 실행 경로에서 모두 일치했다. 점 프레임에서는 오른쪽 화살촉이 사라지고 점에 마스크가 유지되며 가운데 그림은 그대로였다. [픽셀 및 배포 에셋 검사](pixels.txt)

- [편집 중 Inspector](editor-scene.png): 임시 복사본에서 Y 반전 설정을 바꿔 ⌘S로 저장했다. 에디터를 종료하고 같은 씬을 다시 열어 [Flip Y 체크 상태](inspector-reopened.png)와 씬 파일의 값을 확인했다.
- Project의 Icons.atlas를 더블클릭하고 [편집 창을 375×300으로 축소](atlas-narrow.png)한 뒤 Repack을 눌러 씬으로 돌아왔다.
- [자동 검사](../../Engine.Checks/SpriteChecks.cs)는 비대칭 투명 여백을 트림하고 원본을 개명한 뒤 이웃 셀의 크기를 바꿔 재패킹한다. 실제 셀 좌표가 이동해도 씬·클립 파일이 바뀌지 않고, 저장 후 재개와 내보낸 콘텐츠에서 정적 그림·애니메이션이 새 좌표를 읽는지 확인한다. 원본 누락으로 재패킹에 실패하면 이전 세대를 유지하는지도 검사한다.
- [전체 엔진 회귀 검사](checks.txt)는 KeyRoom의 macOS arm64·Windows x64·Linux x64 Release 패키지를 생성하고 그 안의 데이터로 검사를 실행했다. 이번 실제 창·픽셀 검증 대상은 macOS arm64다.
