# KeyRoom 월드 저장 회귀

GitHub Actions의 **KeyRoom world checkpoints**는 macOS arm64, Windows x64, Linux x64에서 KeyRoom의 배포용 Release와 Native AOT를 각각 빌드한다. 각 입력은 새 게임 프로세스에서 `--replay-input`으로 창 없이 재생된다. F5·F9는 KeyRoom의 평소 저장·로드 액션을 거친다.

| 입력 | 저장 파일에서 확인하는 상태 |
| --- | --- |
| E → N → N → F5 | 상자를 열고 코인을 두 번 옮긴다. 코인이 하나이고 부모는 Chest, 로컬 위치는 (84, −5), 월드 위치는 (416, 126)이다. 새로 생성되는 코인 GUID를 제외한 전체 슬롯이 맥 기준과 일치해야 한다. |
| F9 → 2 → F5 | 첫 슬롯을 불러와 두 번째 슬롯에 저장한다. 처음 파일과 바이트 단위로 비교해 코인의 GUID·부모·위치까지 확인한다. |
| F9 → 오른쪽으로 이동 → F5 | 코인을 먹고 첫 슬롯에 저장한다. 뚜껑은 비활성, 획득 플래그는 참, 저장한 프리팹은 0개다. |
| F9 → F9 → E → 2 → F5 | 획득한 슬롯을 두 번 복원하고 상자 열기를 다시 시도한다. 새로 기록한 두 번째 슬롯이 획득 후 맥 기준과 일치해야 한다. |

각 배포본은 이 맥의 Scene Play에서 저장한 미획득 슬롯도 별도 프로필로 읽고 다시 저장한다. 실행 결과와 두 빌드의 영수증은 `KeyRoom-world-<RID>` 아티팩트에 남는다. [미획득 기준](fixtures/pending.save) · [획득 후 기준](fixtures/collected.save)

로컬에서는 저장소 루트에서 다음과 같이 실행한다. 마지막 인자는 새 결과 폴더다.

```sh
python3 engine/tests/KeyRoom.WorldCheckpoints/ci.py osx-arm64 /tmp/keyroom-world-results
```
