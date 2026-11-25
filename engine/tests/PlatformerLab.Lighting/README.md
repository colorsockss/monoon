# PlatformerLab 동굴 조명 화면 회귀

조명 쇼케이스([ShowcaseCave](../../../samples/PlatformerLab/Scenes/ShowcaseCave.scene))를 60Hz로 120틱 실행하고 1·20·45·70·100프레임을 SDL 소프트웨어 렌더러로 저장한다. 입력은 없다. [CaveNight](../../../samples/PlatformerLab/Scripts/Showcase/CaveNight.cs)가 주인공을 걷게 하고 상자가 트윈으로 움직이므로 캡처마다 라이트맵과 그림자가 다르다. `prepare`는 복사본의 시작 씬을 이 쇼케이스로 바꾼다(2026-11-26부터, 이전에는 옛 Cave 씬).

- `play`: Release·Native AOT 플레이어의 헤드리스 리플레이(`MONOON_REPLAY_SNAPSHOTS`).
- `editor`: 패키지 에디터에서 Play를 누르고 [프로브](StartupHook.cs)가 같은 기록을 재생해 플레이어와 같은 `SdlCanvas`로 그린다.
- `compare`: 주어진 실행들의 캡처가 바이트 단위로 같은지 본다.
- [window.py](window.py): Metal 렌더러로 그린 창(에디터 Scene 편집 미리보기 또는 게임 창)을 캡처한다. GUI 잠금 안에서 실행하고 `MONOON_QA_WINDOW`에 clip-qa 창 도구를 준다.

명령은 [결과](../results/lighting/README.md#재현)에 있다. 프레임 비용 벤치는 [engine/benchmarks/Lighting](../../benchmarks/Lighting/Program.cs)이다.
