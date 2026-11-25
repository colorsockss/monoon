# ParticleLab Play 중 파티클 다시 불러오기

패키지 에디터로 [ParticleLab](../../../samples/ParticleLab/) 복사본을 공방 쇼케이스(`ShowcaseWorkshop.scene`, 프리셋 거점에 Torch·Brazier가 있다)로 열고 Scene Play 중에 Particle 패널로 `Assets/Presets/Embers.particles`를 고쳐 로드맵 14.8을 검사한다. 이 에셋은 프리팹 두 개(`Torch.prefab`, `Brazier.prefab`)가 함께 쓴다. [editor.py](editor.py)는 샘플을 결과 폴더에 복사하고 시작 씬을 Presets로 바꾼 뒤 `DOTNET_STARTUP_HOOKS`로 [프로브](StartupHook.cs)를 넣는다. 원본 샘플은 바꾸지 않는다.

| 단계 | 합격 기준 |
| --- | --- |
| Play 중 패널 | Play 중에 `OpenParticlePanel`로 연 패널이 활성 상태이고, 패널 미리보기가 계속 진행한다 |
| 색 바꾸기 | 패널에서 시작 색을 `#00FF00`으로 확정하면 Undo 기록 1개, Console에 `(2 emitter(s) kept their particles, 0 restarted)`, Play 유지. 2.5초(최대 수명 1.8초) 뒤 Torch·Brazier의 모든 불씨가 `00FF00` |
| Max particles 바꾸기 | 96 → 80을 확정하면 기록 1개, Console에 `(0 emitter(s) kept their particles, 2 restarted)`, 두 방출기의 입자가 거의 비었다가 다시 나온다. 파일의 `maxParticles`가 80 |
| Undo | Stop 뒤 Undo 두 번이면 에셋 파일이 원래 바이트로 돌아간다 |

살아 있는 입자가 바이트 단위로 그대로인지, 검사 실패 파일이 아무것도 바꾸지 않는지, 같은 틱의 같은 저장이 같은 결과를 내는지는 창 없이 [ParticleChecks](../Engine.Checks/ParticleChecks.cs)(`--only particles`)가 본다. 에디터에서는 저장을 알아채는 시점이 감시 타이머(0.75초)에 달려 있어 입자 수를 정확히 비교하지 않는다.

저장소 루트에서 실행한다. 창을 여는 실행은 GUI 잠금으로 감싼다.

```sh
qa=engine/tests/ParticleLab.Reload
engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon samples/ParticleLab \
  engine/.artifacts/bin/ParticleReloadProbe/release/ParticleReloadProbe.dll "$(mktemp -d)/particle-reload"
```

[결과](../results/particles/README.md#148-프리셋과-play-중-다시-불러오기)
