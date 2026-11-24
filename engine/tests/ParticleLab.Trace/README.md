# ParticleLab 입자 trace 회귀

[ParticleLab](../../../samples/ParticleLab/)의 일곱 씬을 같은 입력으로 재생하고 Scene Play·Release·Native AOT의 틱별 입자 상태를 바이트 단위로 비교한다.

| 씬 | 프레임 | 입력 | 캡처 |
| --- | ---: | --- | --- |
| Modules (방출기 9개: 다섯 형태, 버스트, Prewarm, 로컬·월드 공간, 수동) | 600 | 120·400프레임에 Space(수동 버스트) | 90·300·590 |
| Sorting (정렬 사이의 가산 입자, 곱하기, 텍스처 시트) | 300 | 없음 | 240 |
| Stress (입자 10,240개) | 300 | 없음 | 200·290 |
| Curves (크기 곡선과 Max 곡선, 색·알파 그라데이션, 회전 속도) | 300 | 없음 | 150·290 |
| Rain (타일 충돌 비와 물방울 서브 방출기, 노이즈·drag·속도 곡선 안개, 콜라이더 충돌 불씨) | 360 | 없음 | 180·350 |
| Fireworks (로켓 Death → 불꽃 48개 서브 방출기) | 420 | 없음 | 100·400 |
| Presets (프리셋 6종, Embers 에셋을 함께 쓰는 프리팹 Torch·Brazier) | 360 | 60프레임 Space, 200프레임 1(Dust·Hit 버스트) | 70·300 |

[prepare](verify.py)는 샘플을 복사해 시작 씬을 바꾸고, 각 씬 카메라에 [ParticleTrace](ParticleTrace.source)를 붙인다. 이 스크립트는 매 틱 `Particles` 태그 오브젝트의 `ReadParticles` 결과(위치·크기·나이 비트와 색)를 FNV-1a로 해시하고, 방출기마다 `ReadParticleEvents`의 `born/died/collided/dropped`를 붙여 `틱 방출기수 입자수 해시 이벤트` 한 줄을 쓴다. `same-as <trace> <이전 trace>`는 앞 네 열이 이벤트 열이 없던 이전 기록과 같은지 본다. 플레이어 기록의 시작 씬은 빌드의 시작 씬과 같아야 해서 씬마다 복사본과 빌드를 따로 만든다. Scene Play는 [프로브](StartupHook.cs)가 Play를 누르고 같은 기록을 재생하며 플레이어와 같은 `SdlCanvas`(소프트웨어 렌더러)로 캡처한다.

`check`는 행 번호가 0부터 이어지는지, 해시가 틱마다 바뀌는지, Stress가 10,240개에 도달하는지, Modules의 두 버스트가 입자를 40개 넘게 늘리는지, Rain에서 빗방울 충돌마다 물방울이 2개씩 나오거나 dropped로 세어지는지(방출 + 버림 = 2 × 충돌), Fireworks에서 불꽃 수가 로켓 죽음 수 × 48과 같고 버림이 0인지, Presets에서 방출기 8개(프리팹 자식 포함)와 두 버스트 입력마다 입자가 25개 넘게 늘어나는지 본다. `compare`는 trace와 캡처가 모두 같아야 통과한다.

## 한 번에 실행(회귀 기준, 14.11)

`all`이 일곱 씬을 차례로 준비·빌드(Release·Native AOT)·재생하고, `--editor`를 주면 Scene Play도 돌려 세 경로를 비교한다. 배포본마다 금지 파일(Avalonia·SkiaSharp·Engine.Editor·Engine.Authoring·`MoNoon.dll`)이 없는지 보고, macOS arm64에서는 [커밋된 trace](../results/particles/osx-arm64/)와도 비교한다(이벤트 열이 없던 기록은 앞 네 열). 에디터 실행만 씬마다 `engine/scripts/gui-lock.sh`로 감싸므로 명령 전체를 잠금으로 감싸지 않는다. 감싸는 명령은 `MONOON_GUI_LOCK`으로 바꿀 수 있다. 결과는 `<out>/summary.txt`에 씬마다 한 줄이다. 일부 씬만 돌릴 때는 `--scenes Presets,Rain`.

```sh
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet build engine/tests/ParticleLab.Trace -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/tests/ParticleLab.Trace/verify.py all samples/ParticleLab "$(mktemp -d)/particle-trace" \
  --editor editor/MoNoon.app/Contents/MacOS/MoNoon engine/.artifacts/bin/ParticlesProbe/release/ParticlesProbe.dll
```

`--editor`를 빼면 창 없이 Release·Native AOT만 돈다(Linux x64 게스트처럼 에디터가 없는 곳). 비용 상한은 `./monoon test`의 `particle-budget` 묶음이 따로 본다. [절차와 기준](../results/particles/README.md#1411-particlelab-회귀와-성능-기준)

## 단계별 실행

Mac에서는 저장소 루트에서 실행한다. 플레이어 템플릿과 에디터를 먼저 빌드한다([빌드](../../../docs/manual/build.md)).

```sh
qa=engine/tests/ParticleLab.Trace
work=$(mktemp -d)
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
for scene in Modules Sorting Stress Curves Rain Fireworks Presets; do
  python3 "$qa/verify.py" prepare samples/ParticleLab "$work/$scene/ParticleLab" "$scene"
  ./monoon build "$work/$scene/ParticleLab" release --profile distribution --output "$work/$scene/builds"
  ./monoon build "$work/$scene/ParticleLab" native-aot --profile distribution --output "$work/$scene/builds"
  python3 "$qa/verify.py" play "$work/$scene/builds/ParticleLab.app/Contents/MacOS/MoNoonPlayer" "$work/$scene/release" "$scene"
  python3 "$qa/verify.py" play "$work/$scene/builds/NativeAOT/ParticleLab.app/Contents/MacOS/MoNoonPlayer" "$work/$scene/aot" "$scene"
  python3 "$qa/verify.py" prepare samples/ParticleLab "$work/$scene/edit/ParticleLab" "$scene"
  python3 "$qa/verify.py" editor editor/MoNoon.app/Contents/MacOS/MoNoon "$work/$scene/edit/ParticleLab" \
    engine/.artifacts/bin/ParticlesProbe/release/ParticlesProbe.dll "$work/$scene/scene-play" "$scene"
  python3 "$qa/verify.py" compare "$work/$scene/scene-play" "$work/$scene/release" "$work/$scene/aot"
done
```

[결과](../results/particles/README.md)
