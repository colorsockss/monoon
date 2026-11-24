# PlatformerLab 효과 화면 회귀

[Platform 씬](../../../samples/PlatformerLab/Scenes/Platform.scene)에서 60Hz 120틱 동안 10틱에 Space를 눌러 제자리 점프한다. 입력은 [verify.py](verify.py)가 만든다. 8·30·50·54·58·62·70·80·110프레임을 SDL 소프트웨어 렌더러로 저장한다. 캡처에는 횃불 셰이더 셀, 점광원 깜빡임, 50·54프레임의 착지 먼지가 들어 있다.

- `play`: Release·Native AOT 플레이어의 헤드리스 리플레이(`MONOON_REPLAY_SNAPSHOTS`).
- `editor`: 패키지 에디터에서 Play를 누르고 [프로브](StartupHook.cs)가 같은 기록을 재생해 플레이어와 같은 `SdlCanvas`로 그린다. 시트는 원본 SkSL로 만든다.
- `compare`: 주어진 실행들의 캡처가 바이트 단위로 같은지 본다. 다른 엔진 커밋으로 빌드한 결과끼리도 비교할 수 있다.

명령은 [결과](../results/particles-p0/README.md#재현)에 있다.

입자 런타임 교체(P.1–P.3) 뒤 착지 먼지가 나오는 50–70프레임의 기준 캡처가 바뀌었다. 새 기준과 이유는 [입자 결과](../results/particles/README.md#platformerlab-착지-먼지-기준-갱신)에 있다.
