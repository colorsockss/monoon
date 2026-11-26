# HexRealm 솔트 해안 리플레이

[route.tsv](route.tsv)는 첫 도시를 세우고 일꾼과 정찰병을 움직인 뒤 Saltmere를 정착시키는 경로다. 창병과 궁병으로 Redwatch와 Greyharbor의 공세를 막고 25턴 점수 판정에서 144 대 123으로 승리한다. 결과를 F5로 저장하고 타이틀에서 F9로 이어서 연다.

각 줄은 입력할 명령, 선택할 축 좌표, 목적지나 선택지, 명령 뒤의 턴과 승자를 기록한다. 실행기는 좌표를 게임 픽셀로 바꾸어 일반 플레이어 입력으로 전달한다. 1–25턴의 해시와 승리·복원 해시를 대조한다. macOS에서는 타이틀, 이동 범위, Haven 성장 예고와 편입, 턴 전환, 전투 예측, 피해 숫자, 승리와 복원 캡처도 비교한다. [해시 기준](../results/hexrealm/macos-arm64-route.txt)

```sh
UseSharedCompilation=false ./monoon test project samples/HexRealm
UseSharedCompilation=false python3 engine/scripts/run-harness.py HexRealm.Play --mode release,native-aot
UseSharedCompilation=false python3 engine/scripts/run-harness.py HexRealm.Play --mode scene-play
```

Linux x64에서는 `python3 engine/tests/HexRealm.Play/verify.py play <실행 파일> <새 결과 폴더>`로 실행한다. [제작·검증 결과](../results/hexrealm/README.md)

## 시연 연속성

촬영은 HexRealm의 시연 입력을 한 번 재생해 60Hz 입력의 짝수 틱마다 1280×720 PNG를 저장한다. 출력 폴더에는 30fps 시퀀스와 실제로 재생한 입력과 로그가 남는다. 검사는 인접 프레임을 320×180 회색조로 줄인 뒤 밝기가 24를 초과해 바뀐 픽셀을 센다. 2.5% 이상인 모든 프레임을 `changes.tsv`와 앞뒤 이미지로 기록한다. `--at`에는 임계값과 관계없이 확인할 초를 추가할 수 있다.

```sh
UseSharedCompilation=false python3 -B engine/tests/HexRealm.Play/continuity.py capture /private/tmp/hexrealm-capture
python3 -B engine/tests/HexRealm.Play/continuity.py audit /private/tmp/hexrealm-capture/joined /private/tmp/hexrealm-audit --at 5.43 8.53 12.83 13.24 25.47 50.97 58.37
```
