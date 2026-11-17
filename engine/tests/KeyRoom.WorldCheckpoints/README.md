# KeyRoom 월드 저장 회귀

로컬 검사기는 현재 플랫폼(macOS arm64, Windows x64 또는 Linux x64)에서 KeyRoom의 배포용 Release와 Native AOT를 각각 빌드한다. 각 입력은 새 게임 프로세스에서 `--replay-input`으로 창 없이 재생된다. F5·F9는 KeyRoom의 평소 저장·로드 액션을 거친다.

| 입력 | 저장 파일에서 확인하는 상태 |
| --- | --- |
| E → N → N → F5 | 상자를 열고 코인을 두 번 옮긴다. 코인이 하나이고 부모는 Chest, 로컬 위치는 (84, −5), 월드 위치는 (416, 126)이다. 새로 생성되는 코인 GUID를 제외한 전체 슬롯이 맥 기준과 일치해야 한다. |
| F9 → 2 → F5 | 첫 슬롯을 불러와 두 번째 슬롯에 저장한다. 처음 파일과 바이트 단위로 비교해 코인의 GUID·부모·위치까지 확인한다. |
| F9 → 오른쪽으로 이동 → F5 | 코인을 먹고 첫 슬롯에 저장한다. 뚜껑은 비활성, 획득 플래그는 참, 저장한 프리팹은 0개다. |
| F9 → F9 → E → 2 → F5 | 획득한 슬롯을 두 번 복원하고 상자 열기를 다시 시도한다. 새로 기록한 두 번째 슬롯이 획득 후 맥 기준과 일치해야 한다. |

각 배포본은 이 맥의 Scene Play에서 저장한 미획득 슬롯도 별도 프로필로 읽고 다시 저장한다. 실행 결과와 두 빌드의 영수증은 지정한 결과 폴더에 남는다. [미획득 기준](fixtures/pending.save) · [획득 후 기준](fixtures/collected.save)

같은 검사에서 새 프로필로 코인을 든 채 아래로 8틱 이동하고 내려놓는다. 든 상태와 내려놓은 상태 각각 F9 후 F5 결과를 첫 저장과 바이트 단위로 비교한다. 내려놓은 뒤 오른쪽으로 8틱 걸으면 코인이 남고, 2틱 더 걸으면 획득된다. 실행 간 비교에서는 처음 생성한 코인 GUID만 치환한다. [입력과 기준 슬롯](fixtures/carry) · [런타임 Transform 검증 기록](../results/runtime-transforms/README.md)

저장소 루트에서 다음과 같이 실행한다. 첫 인자는 현재 플랫폼에 맞춰 `osx-arm64`, `win-x64`, `linux-x64` 중 하나를 사용하고, 마지막 인자에는 새 결과 폴더를 지정한다.

```sh
python3 engine/tests/KeyRoom.WorldCheckpoints/ci.py osx-arm64 /tmp/keyroom-world-results
```

## 데스크톱 3종 검증 기록

2026-11-17에 macOS 27.0.1 arm64, Windows 11 Enterprise LTSC x64 VM(빌드 26100.1742), Debian 12 x64 VM에서 각각 Release와 Native AOT를 빌드해 위 입력을 헤드리스로 재생했다. 여섯 배포본 모두 새 코인의 부모가 Chest이고 로컬 위치가 (84, −5)였다. 게임을 다시 실행해 F9로 복원하고 F5로 저장한 1,129바이트는 해당 실행의 첫 슬롯과 동일했다. 실행마다 새로 생성된 GUID는 서로 달랐으며 각 실행의 저장·복원 사이에는 그대로 유지됐다.

코인을 획득한 후 게임을 다시 시작해 F9를 두 번 누르고 E를 눌렀을 때 여섯 저장 파일 모두 코인이 0개였고, [맥 획득 기준](fixtures/collected.save) 612바이트와 일치했다. SHA-256은 `0becae59f181da639d3442621ea0857c45ccc2a1aad317ab28ba3ce157184fb0`이다. 각 배포본은 [기존 Mac Scene Play 슬롯](fixtures/pending.save)도 바이트 단위로 동일하게 복원·저장했다.
