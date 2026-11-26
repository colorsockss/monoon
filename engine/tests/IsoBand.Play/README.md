# IsoBand.Play

새 슬롯의 Haven 타이틀에서 시작한다. 6크라운으로 치유사를 고용하고 습지·유적·묘지의 무리를 처치한다. Pulse로 교전하고 Stag Guardian의 원형 경고에서 빠져나온 뒤 마지막 관문을 통과한다. [입력](route.tsv) · [기대 원정 기록](../results/isoband/remake-route.txt)

```sh
./monoon test project samples/IsoBand
python3 engine/scripts/run-harness.py IsoBand.Play --mode release,native-aot
python3 engine/scripts/run-harness.py IsoBand.Play --mode scene-play
```

검사기는 지역별 처치 수·XP·크라운과 마지막 Haven 진입 로그를 대조한다. macOS 헤드리스에서는 SDL 소프트웨어 렌더러 캡처 9장의 SHA-256을 비교한다. 하네스는 macOS GUI 잠금을 걸고 패키지 에디터의 Scene Play도 실행한다. [캡처 기준](../results/isoband/macos-arm64-remake-sha256.tsv) · [실행별 결과](../results/isoband/README.md)
