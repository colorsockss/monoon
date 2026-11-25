# 샘플 게임 텍스트 캡처 회귀 (로드맵 11.13)

KeyRoom 쇼케이스의 가방(영어 → 한국어, 기본 글꼴 고운돋움과 대체 글꼴 나눔고딕이 섞인 줄)과 PlatformerLab 이동 발판 컷신 대사(프로젝트 글꼴 없음, OS 글꼴)를 같은 입력 재생으로 Scene Play, Release, Native AOT에서 캡처하고 바이트 단위로 비교한다. 세 경로 모두 BRUTAL의 SDL_ttf로 글자를 그리므로 캡처가 같아야 한다.

저장소 루트에서 실행한다. 엔진 코드가 바뀌었으면 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md)를 다시 빌드한다. 에디터를 여는 명령은 다른 GUI 검사와 겹치지 않게 실행한다.

```sh
qa=engine/tests/Samples.GameText
work=$(mktemp -d)
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
for game in KeyRoom PlatformerLab SpriteLab; do
  ./monoon build samples/$game release --profile distribution --output "$work/$game"
  ./monoon build samples/$game native-aot --profile distribution --output "$work/$game"
  python3 "$qa/verify.py" play $game "$work/$game/$game.app/Contents/MacOS/MoNoonPlayer" "$work/runs/$game/release"
  python3 "$qa/verify.py" play $game "$work/$game/NativeAOT/$game.app/Contents/MacOS/MoNoonPlayer" "$work/runs/$game/native-aot"
  rsync -a --exclude .monoon samples/$game "$work/projects/"
  python3 "$qa/editor.py" $game editor/MoNoon.app/Contents/MacOS/MoNoon "$work/projects/$game" \
      engine/.artifacts/bin/GameTextProbe/release/GameTextProbe.dll "$work/runs/$game/scene-play"
  python3 "$qa/verify.py" compare $game "$work/runs/$game"
done
```

- KeyRoom 입력(60 Hz, 240프레임): 10 **I** 가방 → 40 **L** 한국어 → 90 **I** 닫기 → 100–128 **←** 열쇠 줍기 → 160 **I** → 200 **L** 영어. 캡처 5, 70, 190, 230.
- PlatformerLab 입력(420프레임): 10–193 **→** 발판까지 달리기 → 컷신 → 290·350 **Return**. 캡처 260, 320, 380.
- SpriteLab 입력(300프레임, 입력 없음): 쇼케이스 ShowcaseYard. 캡처 5, 120, 250, 290. 동전·꽃·덤불은 `Props.atlas` 셀로, 표지판과 HUD는 SDL_ttf 글자로 그린다. 세 캡처가 같아야 한다. [검증 기록](../results/size-wave3/README.md)
- `play`는 헤드리스 재생에 `MONOON_REPLAY_SNAPSHOTS`를 주어 플레이어의 SDL 캔버스(소프트웨어 렌더러)로 저장한다. `editor.py`는 `DOTNET_STARTUP_HOOKS`로 [검사기](StartupHook.cs)를 넣어 Play를 누르고 같은 입력을 재생한 뒤 Scene 패널과 같은 `SdlCanvas`로 같은 프레임을 오프스크린 렌더링한다.
- [검증 기록](../results/text-ttf/README.md)
