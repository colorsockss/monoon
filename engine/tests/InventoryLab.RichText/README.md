# InventoryLab 서식 있는 대사·글자 단위 표시 회귀

약초 마당(`Scenes/Showcase.scene`, 시작 씬) 약초상 대화의 서식 태그(색·굵기·크기·아이콘), 번역 키로 넣은 긴 대사(기본 언어 영어), 초당 45자 표시와 **Space** 넘기기를 같은 입력 재생으로 Scene Play, Release, Native AOT에서 확인한다. 관찰 스크립트 `TraceReveal`이 매 프레임 대사 칸의 `RevealProgress`를 `reveal.tsv`에 남기고, 정해 둔 7개 프레임을 PNG로 저장한다.

저장소 루트에서 실행한다. 엔진 코드가 바뀌었으면 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md)를 다시 빌드한다.

```sh
qa=engine/tests/InventoryLab.RichText
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/InventoryLab "$work/InventoryLab"
./monoon build "$work/InventoryLab" release --profile distribution --output "$work/builds"
./monoon build "$work/InventoryLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/native-aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/InventoryLab" \
    engine/.artifacts/bin/RichTextProbe/release/RichTextProbe.dll "$work/runs/scene-play"
python3 "$qa/verify.py" compare "$work/runs"
```

- 입력(60 Hz, 360프레임): 0–10 **↑**(약초상 앞으로) → 10 **T** 인사말 → 90 **4** 약초가 자라는 곳(번역 키, 긴 문장) → 150 **Space** 넘기기 → 170 **1** 처음으로 → 230 **1** 의뢰(아이콘·색·굵기·크기). 캡처 프레임은 30, 85, 120, 152, 175, 262, 345다.
- `play`는 헤드리스 재생에 `MONOON_REPLAY_SNAPSHOTS=<폴더>;<프레임,…>`를 주어 플레이어의 SDL 캔버스(소프트웨어 렌더러, SDL_ttf)로 해당 프레임을 저장한다.
- `editor.py`는 `DOTNET_STARTUP_HOOKS`로 [검사기](StartupHook.cs)를 넣는다. Inspector를 1280·1600·1920 px 너비로 캡처한 뒤 Play를 눌러 같은 입력을 재생하고, Scene 패널과 같은 SDL 캔버스·SDL_ttf 텍스트 경로로 같은 프레임을 오프스크린 렌더링한다. 끝난 뒤 Scene 패널 창을 캡처한다.
- `compare`는 세 실행의 `reveal.tsv`가 바이트 단위로 같은지, 세 실행의 캡처가 바이트 단위로 같은지 확인해 `comparison.tsv`를 쓴다. [검증 기록](../results/rich-text/README.md)
