# KeyRoom 프리팹 구조 변경의 개별 적용·복원

2026-11-22, macOS 27.0.1 arm64. KeyRoom 사본을 이 브랜치의 배포 에디터에서 [검사기](../../KeyRoom.PrefabStructure/README.md)로 수정했다. 저장한 사본으로 Distribution 프로필의 Release와 Native AOT를 빌드했다.

| 편집 단계 | Chest.prefab | Chest 1 | Chest 2 | Chest 3 |
| --- | --- | --- | --- | --- |
| Chest 2에 Lock 자식과 Audio Source 추가 | Lock 없음 | 없음 | Lock · 효과음 | 없음 |
| Overrides…에서 Lock만 Apply to Source | Lock | Lock | Lock · 효과음 | Lock |
| Undo 한 번 | Lock 없음 | 없음 | Lock · 효과음 | 없음 |
| Redo 후 씬 저장 | Lock | Lock | Lock · 효과음 | Lock |

검사기는 Hierarchy의 Add Child Square, Add Component와 같은 에디터 명령을 호출했다. **Overrides…**·**Apply to Source**·**Done** 버튼에는 클릭 이벤트를 보냈고, Undo와 Redo는 Edit 메뉴의 명령을 실행했다. 실제 키 입력과 마우스 포인터는 사용하지 않았다. Overrides 창의 구조 행은 `Chest 2 · audioSource (added)`, `Lock (added object)` 두 개였다. Lock을 적용한 뒤에는 효과음 행만 남았다. 저장한 씬을 다시 열어도 Chest 2의 구조 오버라이드는 효과음 하나였다. 창을 최소 너비 350으로 줄였을 때 긴 행과 세 버튼이 창 안에 정렬됐다. [구조 행 선택](overrides.png) · [작은 창](compact.png) · [단계별 상태](editor.txt)

| 실행 | 상자 x 좌표 | 자물쇠 | 효과음 |
| --- | --- | --- | --- |
| [macOS arm64 Release](release.json) | 210 · 370 · 530 | 1 · 1 · 1 | 0 · 1 · 0 |
| [macOS arm64 Native AOT](native-aot.json) | 210 · 370 · 530 | 1 · 1 · 1 | 0 · 1 · 0 |

두 플레이어는 이벤트 없는 10프레임 리플레이를 헤드리스로 실행했다. 사본에만 넣은 관찰 스크립트가 `Lock` 태그 오브젝트의 부모와 각 상자의 Audio Source를 런타임 API로 읽었다. 두 배포본에서 SkiaSharp, Avalonia, 에디터, 빌드 도구 어셈블리는 발견되지 않았다.

엔진 전체 회귀 `./monoon test`도 통과했다. 구조 검사는 추가 자식·삭제 자식·추가 컴포넌트·삭제 컴포넌트를 하나씩 적용하고 복원한다. Undo에 해당하는 원본·씬 복원, 저장 후 다시 열기, 씬 전용 참조를 가진 컴포넌트의 적용 거부도 확인한다. [회귀 검사](../../Engine.Checks/PrefabStructureChecks.cs)

Scene 뷰포트(SDL 패널)의 화면은 캡처하지 않았다.

## Windows x64와 Linux x64 게스트

2026-11-23, 이 Mac의 QEMU(TCG) 게스트에서 같은 순서로 실행했다. 각 게스트의 패키지 에디터(Linux는 Xvfb)에서 검사기가 사본을 수정·저장했고, 저장한 사본을 같은 게스트에서 Release와 Native AOT(distribution)로 빌드했다. [게스트 환경과 실행 방법](../x64-guest-10.2-10.8/README.md)

| 게스트 | 에디터 단계 | Release | Native AOT |
| --- | --- | --- | --- |
| Windows 11 x64 | 통과. 단계별 상태가 [macOS의 editor.txt](editor.txt)와 같음 | `210 1 0` · `370 1 1` · `530 1 0` | 같음 |
| Debian 12 x64 | 통과. 단계별 상태가 macOS와 같음 | `210 1 0` · `370 1 1` · `530 1 0` | 같음 |

네 플레이어의 `result.json`은 macOS [release.json](release.json)과 바이트 단위로 같았다. [실행별 결과](x64/runs.tsv). Overrides 창 캡처: Windows [구조 행](x64/win-x64-overrides.png) · [작은 창](x64/win-x64-compact.png), Linux [구조 행](x64/linux-x64-overrides.png) · [작은 창](x64/linux-x64-compact.png). Windows의 작은 창 캡처는 macOS처럼 350px이었고 긴 행과 세 버튼이 창 안에 있었다. Linux(Xvfb, 창 관리자 없음)의 작은 창 캡처는 435px 그대로여서 최소 너비 배치는 확인하지 못했다. 검사기는 이 너비를 판정하지 않고 캡처만 남긴다. 독립 게임은 헤드리스 재생이며 물리 GPU에서 실행하지 않았다.
