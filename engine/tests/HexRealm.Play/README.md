# HexRealm 40턴 리플레이

기본 시드로 도시를 세우고 일정 간격으로 창병을 생산하며 40턴을 진행한다. AI 명령은 매번 게임의 규칙으로 계산한다. 턴이 끝날 때 남기는 상태 해시 40줄을 비교한다. F5로 저장하고 F9로 합류해 동일한 상태를 복원한다. macOS에서는 Scene Play·Release·Native AOT의 SDL 소프트웨어 렌더러 캡처 6장도 비교한다.

```sh
./monoon test project samples/HexRealm
python3 engine/scripts/run-harness.py HexRealm.Play
```

Linux x64 배포 플레이어는 `python3 engine/tests/HexRealm.Play/verify.py play <실행 파일> <새 결과 폴더>`로 실행한다. [제작·검증 결과](../results/hexrealm/README.md)

[마우스 입력 녹화](Scout.inputreplay)는 기본 배율에서 도시를 세운 후 정찰병을 왼쪽 클릭하고 오른쪽 클릭으로 (7, 7)에 옮긴 뒤 저장한다. 각각 빈 프로필에 재생하면 `slot-001.save`의 명령 두 개가 `1,0,0,0;0,1,7,7;`로 같다.
