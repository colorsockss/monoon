# ParticleLab Particle 패널 검사

패키지 에디터로 [ParticleLab](../../../samples/ParticleLab/) 복사본을 Curves 씬으로 열고 **Flame**을 선택해 Particle 패널(로드맵 14.6)과 곡선·그라데이션·색 편집 컨트롤(14.7)을 검사한다. [editor.py](editor.py)는 샘플을 결과 폴더에 복사하고 시작 씬을 Curves로 바꾼 뒤 `DOTNET_STARTUP_HOOKS`로 [프로브](StartupHook.cs)를 넣는다. 원본 샘플은 바꾸지 않는다.

| 단계 | 합격 기준 |
| --- | --- |
| 패널 열기 | Inspector 버튼 경로(`OpenParticlePanel`)로 열린 패널이 `Assets/Flame.particles`를 편집한다. Scene 편집 화면은 선택한 방출기 1개만, **Play all in Scene**을 켜면 3개를 시뮬레이션한다 |
| trace 대조 | 패널이 만든 Scene 미리보기의 틱 0·1·60·150·299 입자 수와 해시가 커밋된 [Curves trace](../results/particles/osx-arm64/curves-trace.tsv)(Scene Play·Release·Native AOT 공통)와 같다 |
| Scrub | 도구줄 시간 막대를 1.5 s에 두면 두 미리보기가 0부터 90틱 진행한 상태와 같다 |
| 재생 | 2초 재생해 틱이 진행되고, `Assets`·`Scenes`의 모든 파일 해시가 그대로다 |
| 곡선 키 | Size 곡선의 두 번째 키를 세 번 끌어 (0.3, 1.25)에 놓으면 Undo 기록 1개, 파일의 키가 그 값, 편집기 표본 = `ParticleLut.Bake` 결과. Undo → 원래 바이트, Redo → 편집 바이트, Undo → 원래 바이트, 패널이 되돌린 값을 다시 읽음 |
| 그라데이션 키 | 두 번째 색 키를 0.5로 끌면 기록 1개, 파일 시간 0.5, 막대 표본 = 런타임 표. Undo/Redo 바이트 |
| 색 | Main Color 견본의 ColorView 플라이아웃에서 색을 두 번 바꾸고 닫으면 기록 1개, 파일 `#3366CC`. Undo/Redo 바이트 |
| 정렬 | 1280·1600·1920·880×660 창과 긴 이름(1280)에서 섹션 행 46개의 레이블·필드 왼쪽 x가 하나, 중심 차이 ≤ 1px, 입력 높이 하나, 잘린 레이블은 말줄임과 툴팁, 필드는 패널 안. 곡선·그라데이션 키 선택이 다른 필드를 옮기지 않음. 긴 이름은 패널 제목에서 말줄임·툴팁 |

저장소 루트에서 실행한다. 창을 여는 실행은 GUI 잠금으로 감싼다.

```sh
qa=engine/tests/ParticleLab.Editor
engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon samples/ParticleLab \
  engine/.artifacts/bin/ParticleEditorProbe/release/ParticleEditorProbe.dll "$(mktemp -d)/particle-editor"
```

결과 폴더에는 `result.json`, `panel-*.png`·`main-*.png`(RenderTargetBitmap, 네이티브 SDL 미리보기는 비어 보인다), `color-flyout.png`, 그리고 플레이어의 소프트웨어 SDL 캔버스로 그린 `isolated-0090.png`·`scene-preview-0299.png`가 남는다. 곡선·그라데이션·색은 포인터 처리기가 부르는 같은 내부 단계(`BeginDrag`·`DragTo`·`EndDrag`, 플라이아웃 `ShowAt`·`Hide`)로 조작한다. [결과](../results/particles-editor/README.md)
