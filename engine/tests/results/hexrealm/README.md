# HexRealm 제작 기록

2026-11-25 · macOS 27.0.1 (26A434) · Apple Silicon arm64. [샘플 계획](../../../../docs/plans/samples/3-hex-strategy.md) · [조작 매뉴얼](../../../../docs/manual/hexrealm.md)

## 구현

- 시드로 30 × 20 Hex Pointy 지도를 만든다. 지형 6종과 유닛 5종, 도시 건설·인구·생산, 농장·광산·도로, 턴제 전투, 연구 10단계, 안개, 상대 AI와 세 가지 승리 판정까지 한 씬에서 진행한다.
- S2는 `GridDirections.Hex`, 출력 버퍼를 받는 `FindPath`와 `Reachable`로 구현했다. [독립 비교 검사](../../Engine.Checks/GridNavigationChecks.cs)는 가중 육각 그래프의 경로·비용·이동 범위를 대조했다. 격자당 검색 작업 배열은 셀당 32 B(600칸의 배열 데이터 약 19.2 KB)이며 첫 탐색에서 한 번 만든다. 이후 버퍼형 호출에서는 관리 할당이 0 B다. 기존 직교 경로 호출의 동작도 검사했다.
- 안개·커서와 이동 범위는 기존 `ReplaceTiles`로 갱신한다. 유닛 그림은 프리팹을 재사용한다. 게임 진행에는 시드, 플레이어 명령 목록과 상태 해시를 저장한다. AI 명령은 복원할 때 다시 계산한다.
- 4 × 4 생성 시트에서 지형과 도시 그림을 48 × 56 셀로 자르고, 유닛은 24 × 24 PNG로 정리했다. 안개와 이동 범위 마스크는 격자 외곽선에 맞춰 만들었다. [아트 프롬프트](../../../../docs/plans/samples/asset-prompts.md) · [첫 도시 화면](first-city.png)

## 검증

- macOS Scene Play·Release·Native AOT에서 [40턴 리플레이](../../HexRealm.Play/README.md)의 [상태 해시 40줄](macos-arm64-route.txt)과 [캡처 6장](macos-arm64-sha256.tsv)이 일치했다. 40턴째 상태 해시는 `3e899ae3450cd0bf`다. 프로젝트 검사에서는 저장·재로드로 같은 상태 해시를 얻고 60턴 점수 판정 후 추가 턴이 열리지 않는 것을 확인했다. [정찰병 마우스 녹화](../../HexRealm.Play/Scout.inputreplay)의 두 배포 플레이어 저장 파일도 같다.
- macOS에서 교차 빌드한 Linux Release와 Debian 12 x86_64 QEMU 게스트에서 빌드한 Native AOT를 같은 게스트에서 재생했다. [Linux 상태 해시 40줄](linux-x64-route.txt)이 두 모드와 macOS 세 경로에서 같다. Windows는 장비 준비 후 확인한다.

| 모드 | 앱 바이트 | 플레이어 실행 파일 바이트 | 첫 프레임 ms | 세션 시작 ms |
| --- | ---: | ---: | ---: | ---: |
| Release | 29,374,459 | 124,712 | 157 | 52.6 |
| Native AOT | 11,186,366 | 6,762,288 | 58 | 10.0 |

Mac 헤드리스에서 AI·생산·연구·안개를 포함한 턴 종료 업데이트 40개의 중앙값/최대는 Release 0.376/1.740 ms, Native AOT 0.103/0.431 ms였다. 관리 할당 중앙값은 각각 56,816 B, 56,784 B다. 에셋·UI 갱신을 포함한 값이며 CPU 게임 업데이트만 측정했다. [배포 크기 기준](../build-size/baseline.tsv)

첫 버전에서는 연구를 직렬로 진행하고 도시 수확을 자동으로 계산한다. 분기형 기술 트리, 건물 4종, 일구는 칸 직접 지정, 지도 가장자리·드래그 이동, 미니맵과 경로선은 후속 항목이다.

## 2026-11-26 독립 검수

macOS arm64의 프로젝트 테스트 2개가 통과했고, 기존 Release·Native AOT에서 턴별 해시 40줄과 기준 캡처 6장이 일치했다.

- **P1. 저장 파일이 없을 때 F9를 누르면 진행 중인 판이 초기화된다.** 새 프로필에서 B로 도시를 세우고 Enter로 턴을 넘긴 뒤, 저장 전에 F9를 누른다. 임시 프로젝트에서 2턴·도시 2개가 1턴·도시 1개로 돌아갔다. macOS Release·Native AOT 검증 입력에서도 F9 전후에 서로 다른 상태로 `turn 01`이 두 번 기록됐다. 불러오기 입력이 슬롯 존재 여부에 관계없이 씬을 다시 연다. [불러오기 처리](../../../../samples/HexRealm/Scripts/RealmBoard.cs#L77) · [판 초기화](../../../../samples/HexRealm/Scripts/RealmBoard.cs#L35)
- **P2. 승리 저장을 다시 열면 새 판을 시작할 수 없다.** 60턴 승리 후 F5로 저장하고 F9로 다시 열거나 재실행한다. 이미 끝난 판이 복원되지만 승리 안내가 표시되지 않고 Enter에는 `Order unavailable.`만 나온다. 게임 안에 새 판을 여는 입력이 없다. [저장 복원](../../../../samples/HexRealm/Scripts/RealmBoard.cs#L35) · [종료 상태의 명령 처리](../../../../samples/HexRealm/Scripts/RealmRules.cs#L118)
- **P2. 이동 범위에 막힌 칸이 표시되고 열린 우회로도 쓰지 못한다.** 새 판에서 정찰병이 있는 (6, 7)을 클릭한 뒤 녹색으로 표시된 (3, 7)을 우클릭한다. Native AOT에서 `Order unavailable.`이 표시됐다. 임시 규칙 테스트에서 이동력 40으로 갈 수 있는 우회로를 확인했지만, 판정은 아군 개척자를 지나가는 비용 30의 첫 경로를 검사하다 거부됐다. [이동 범위 표시](../../../../samples/HexRealm/Scripts/RealmBoard.cs#L193) · [경로 검사](../../../../samples/HexRealm/Scripts/RealmRules.cs#L165)
- **P2. 첫 도시를 출발 좌표 밖에 세우면 수도 함락이 패배로 기록되지 않는다.** 임시 규칙 테스트에서 (5, 6)에 세운 첫 도시가 상대에게 넘어갔지만 게임이 계속됐다. 상대의 승리 판정은 실제 건설 위치 대신 개척자 출발점 (5, 7)을 확인한다. [수도 함락 판정](../../../../samples/HexRealm/Scripts/RealmRules.cs#L160)
- **P3. 유닛 생산 상한에 전사한 유닛이 계속 포함된다.** 임시 규칙 테스트에서 기록 48개 중 살아 있는 유닛이 5개여도 추가 생산을 거부했다. [생산 대기열 검사](../../../../samples/HexRealm/Scripts/RealmRules.cs#L222)

## 2026-11-26 검수 수정과 재검증

기준 커밋 `5b9db95`의 샘플 워크트리에서 macOS 27.0.1 arm64 프로젝트 검사, Scene Play, Release·Native AOT Distribution으로 확인했다.

| 우선순위 | 재현 입력과 확인값 |
| --- | --- |
| P1 빈 슬롯 F9 | 빈 프로필에서 `B → Enter → F9 → Enter`를 재생했다. 프로젝트 검사에서 F9 직후 2턴·도시 2개·상태 해시가 유지됐다. 수정 전 로그는 `01 → 01`이었고, 수정한 두 배포본 모두 `01 → 02`를 기록했다. [원래 입력](../../HexRealm.Play/EmptySlot.inputreplay) |
| P2 종료한 저장 | 60턴까지 진행하고 `F5 → F9 → Enter → N → F9`를 눌렀다. 승리 안내와 해시는 재로드 후 유지됐다. N은 1턴·도시 1개로 시작하며 한 번 더 F9를 누르면 기존 결과가 복원된다. 두 Mac 배포본에서 수정 전 승리 저장도 열고 N으로 다시 시작했으며 저장 바이트를 비교했다. [승리 저장을 연 화면](review-victory.png) |
| P2 이동 표시와 실제 명령 | 개척자에서 정찰병이 막는 (7,7)은 녹색 표시가 사라졌다. 정찰병 (6,7) 선택 후 (3,7)로 우클릭하면 우회 이동이 실행되며 두 Mac 배포본의 저장에 `0,1,3,7;`이 남았다. [재현 입력](../../../../samples/HexRealm/Tests/ScoutDetour.inputreplay) |
| P2 수도 점령 | (5,6)에 첫 도시를 세우고 검수의 전투 배치에서 상대 창병·궁병이 들어온 상태로 턴을 넘겼다. 도시 소유권과 승자가 모두 2가 됐다. |
| P3 생산 한도 | 유닛 기록 48개와 전사자가 있는 검수 상태에서 창병을 생산했다. 생산이 완료됐고 비어 있던 인덱스를 사용해 기록 수는 48개였다. |

기존 저장에서 `legacyOrders`가 없으면 그 시점까지의 명령을 예전 규칙으로 재생한다. 이어서 내리는 명령에는 현재 규칙을 적용한다. 다음 저장에 두 규칙의 경계가 남는다. 두 Mac 배포본에서 수정 전 40턴 저장으로 이어서 진행하고, 41턴을 F5로 저장한 뒤 F9와 Enter를 눌러 42턴까지 기록했다. 이동 표시와 플레이어 경로는 같은 `GridNavigation`을 쓰며, 통행 비용과 검색 버퍼를 보드에서 재사용한다. 새 런타임 의존성은 없다.

프로젝트 검사 6개가 통과했다. 40턴 전체 리플레이의 해시 40줄은 이전 [기준 로그](macos-arm64-route.txt)와 같다. 세 Mac 실행 경로에서 수정 후 [캡처 6장](macos-arm64-review-sha256.tsv)도 일치했다. 이전 패키지 대비 Release 앱은 9,772 B, Native AOT 앱은 60 B 늘었다.

Debian 12 x64 QEMU 헤드리스 Release(교차 빌드)에서도 40턴 로그가 일치했다. 빈 슬롯 F9 입력은 `turn 01: b3c436f115c77457`, `turn 02: a6d5412a6c1d79c0`을 기록했다.

QEMU가 동작하는 동안 이전·수정 패키지를 교대로 예열하고 각각 5번 더 실행했다. 헤드리스 `session_ready`와 턴 종료 업데이트의 중앙값·p95 차이는 두 측정 범위의 합 이내였다. 관리 할당 중앙값은 Release 56,816 B, AOT 56,784 B였고, AOT 할당 p95는 77,856 B에서 82,136 B로 늘었다. `MONOON_STARTUP_TRACE`와 `MONOON_REPLAY_CAPTURE`로 측정했다.

```sh
./monoon test project samples/HexRealm
python3 engine/scripts/run-harness.py HexRealm.Play
# PLAYER는 내보낸 MoNoonPlayer 실행 파일의 경로다.
SDL_VIDEO_DRIVER=monoon-no-video SDL_AUDIO_DRIVER=monoon-no-audio \
  "$PLAYER" --replay-input engine/tests/HexRealm.Play/EmptySlot.inputreplay "$(mktemp -d)"
```
