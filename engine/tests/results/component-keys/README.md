# KeyRoom 컴포넌트 키프레임 측정

닫힌 상자의 **LidMotion.clip**과 E로 생성된 코인의 **CoinPulse.clip**을 각각 재생 시작 기준으로 측정했다. 클립 길이는 1초다. 60Hz 고정 업데이트의 0·15·30·45·60틱을 사용했다.

| 시점 | 뚜껑 로컬 회전 | 뚜껑 Y 스케일 | 코인 RGB | 코인 불투명도 |
| --- | ---: | ---: | --- | ---: |
| 0초 | 0° | 1.0 | `#FFFFFF` | 100% |
| 0.25초 | −12° | 1.2 | `#9FE1CF` | 60% |
| 0.50초 | −24° | 1.4 | `#3FC39F` | 20% |
| 0.75초 | −12° | 1.2 | `#9FE1CF` | 60% |
| 1초 | 0° | 1.0 | `#FFFFFF` | 100% |

## 실행 조건

- **클립 편집**은 배포 에디터에서 0.25초의 뚜껑 회전 키를 −18°로 추가했다. Undo에서 −12°, Redo에서 −18°를 확인했다. 저장하고 다시 열어 −18°가 유지된 뒤 키를 제거해 원래 보간값을 복원했다. `0.3333333333333333`초 키도 입력한 정밀도로 선택하고 삭제하는지 검사했다. 창 폭 710과 420에서 입력 열이 정렬되고 Set Key 버튼까지 스크롤할 수 있는지도 확인했다.
- **미리보기**는 실제 클립 창의 타임라인을 다섯 시점으로 이동해 미리보기 뷰포트에 전달된 포즈를 읽었다. **Scene Play**는 같은 프로젝트에서 실제 Play 세션을 시작했다.
- **독립 게임**은 각 OS의 Release와 Native AOT 실행 파일에 같은 152프레임 입력을 창 없이 재생했다. 검증 복사본의 관찰 스크립트가 로컬 Transform과 Sprite Renderer의 실제 그리기 색을 기록했다. TSV의 마지막 열은 알파가 앞에 오는 ARGB다. 비교 정밀도는 회전과 스케일 소수 셋째 자리 및 RGBA 채널당 8비트다.

| 환경 | 클립 편집 | 미리보기 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- | --- | --- |
| macOS arm64 | [클립 창](osx-arm64/lid-keys.png) | [포즈](osx-arm64/preview.json) | [측정](osx-arm64/scene-play.tsv) | [측정](osx-arm64/release.tsv) | [측정](osx-arm64/native-aot.tsv) |
| Windows 11 x64 · QEMU | [클립 창](win-x64/lid-keys.png) | [포즈](win-x64/preview.json) | [측정](win-x64/scene-play.tsv) | [측정](win-x64/release.tsv) | [측정](win-x64/native-aot.tsv) |
| Debian 12 x64 · QEMU, Xvfb | [클립 창](linux-x64/lid-keys.png) | [포즈](linux-x64/preview.json) | [측정](linux-x64/scene-play.tsv) | [측정](linux-x64/release.tsv) | [측정](linux-x64/native-aot.tsv) |

## Mac의 SDL 화면

관찰 스크립트가 없는 일반 배포 게임 두 개도 SDL 창으로 실행했다. E로 상자를 열고 밝기가 달라지는 동안 실제 렌더러에서 PNG를 얻었다. 라이브 입력으로 기록한 프레임 번호는 실행마다 달랐다. 세 쌍의 화면에서 코인 영역 44×44의 RGB 5,808바이트가 쌍별로 일치했다. [Release 화면](osx-arm64/release-coin-1.png) · [Native AOT 화면](osx-arm64/native-aot-coin-1.png) · [영역과 SHA-256](osx-arm64/screen-comparison.json)

[실행 방법과 입력 기록](../../KeyRoom.ComponentKeys/README.md) · [사용 설명서](../../../../docs/manual/sprites.md#컴포넌트-속성-키프레임)
