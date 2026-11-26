# IsoBand 원정 리플레이

[route.tsv](route.tsv)는 60Hz 입력이다. 지면을 클릭해 Haven의 문으로 이동하고, 두 필드와 지하 묘지에서 자동 공격 및 Pulse로 적을 쓰러뜨린 뒤 Haven으로 돌아온다. 배포 프로젝트의 시작 씬에서 3,480프레임을 재생한다. 각 지역의 처치 수, 경험치, 동전 수가 같아야 하며, 마지막에는 Haven 씬 진입 완료 로그가 있어야 한다. macOS의 Scene Play·Release·Native AOT는 SDL 소프트웨어 렌더러 캡처 7장도 비교한다.

```sh
./monoon test project samples/IsoBand
python3 engine/scripts/run-harness.py IsoBand.Play
```

Linux x64 배포 플레이어는 `python3 engine/tests/IsoBand.Play/verify.py play <실행 파일> <새 결과 폴더>`로 실행한다. [제작·검증 결과](../results/isoband/README.md)
