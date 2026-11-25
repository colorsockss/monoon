# CollisionLab Inspector 편집 기록 비용 (I-1)

패키징한 에디터로 `samples/CollisionLab`의 [준비 복사본](../Shared/collision_lab.py)(`ShowcaseCasts`에서 시작한다)을 열고 Inspector 텍스트·숫자 입력의 비용과 Undo 결과를 확인한다. [I-1 결과](../results/inspector-i1/README.md) · [오브젝트 단위 차분 기록 결과](../results/inspector-undo-diff/README.md)

[StartupHook](StartupHook.cs)이 순서대로 실행한다. 실행 중 메인 창 외의 창(저장 확인 대화 상자 등)이 열리면 바로 실패한다.

1. **기록 시점 검사**(시작 씬 `ShowcaseCasts`, 새 에디터만): Probe를 프리팹으로 만든 뒤 인스턴스의 Sprite **Order**에 `12`를 입력하고 이어서 `3`을 입력한다. 값은 입력 즉시 123이 되고 씬 이름에 `*`가 붙지만 Undo 기록은 아직 없다. Enter를 누르면 기록이 정확히 1개 늘고 Overrides 목록에 `spriteRenderer.order`가 추가된다. Undo 한 번으로 원래 값과 오버라이드 목록이, Redo 한 번으로 123과 오버라이드가 돌아온다. 이어서 Position X에 `45`를 입력하고 필드에 포커스를 둔 채 저장해도 기록 1개가 남고 씬이 깨끗해지는지, Undo·Redo가 되는지 확인한다.
2. **입력 비용**(Field10000, 10,010개): 마지막 충돌체의 Position X에 5자리 숫자, Title의 UI Text **Text**에 10자를 입력하고 Scene 패널로 포커스를 옮겨 확정한다. 각각 첫 회는 예열로 버리고 8회·5회를 잰다. 키 입력 하나(입력 이벤트와 그 뒤 디스패처 작업), 확정 한 번, 입력 한 묶음 전체의 시간과 관리 할당을 기록한다. Undo 기록의 현재 상태 객체가 바뀐 횟수를 묶음마다 세어 장면 캡처 횟수로 쓴다. 새 에디터는 묶음마다 1회여야 한다.
3. **Undo 기록 100단계**: Position X와 Y를 번갈아 한 자리씩 100번 입력·확정한다. 걸린 시간, 할당, GC 후 남은 힙 증가, 기록과 현재 상태가 붙잡은 상태 수와 크기를 기록한다. 크기는 씬 전체 문자열이면 UTF-16 바이트, 오브젝트 항목으로 나눈 상태면 서로 다른 바이트 배열을 한 번씩 더하고 상태마다 오브젝트당 참조 8바이트를 더한다. 이어서 `SceneHistory.Restore`로 Undo 100번, Redo 100번을 각각 재고(`RestoreEdit`는 매번 1만 행 Hierarchy를 다시 만들기 때문에 기록만 잰다) Redo 뒤 씬 문자열이 100단계 직후와 같은지 확인한다.
4. 씬을 저장한다. `editor.py`가 실행마다 저장 파일의 SHA-256을 비교해 모든 에디터·실행에서 같은지 확인한다.

`MONOON_I1_SCENE=Showcase`를 주고 프로젝트로 `samples/KeyRoom`을 넘기면 1단계를 건너뛰고 KeyRoom의 쇼케이스 씬에서 Player의 Position X, `textKey`가 없는 첫 UI Text로 2–4단계를 실행한다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/CollisionLab.InspectorEdits -c Release -m:1 -p:UsedAvaloniaProducts=
work=$(mktemp -d); python3 engine/tests/Shared/collision_lab.py samples/CollisionLab "$work/CollisionLab"
python3 engine/tests/CollisionLab.InspectorEdits/editor.py "$work/CollisionLab" "$work/inspector-edits" 5 \
  base=<기준 에디터>/MoNoon.app/Contents/MacOS/MoNoon,engine/.artifacts/bin/InspectorEditsProbe/release/InspectorEditsProbe.dll \
  cand=editor/MoNoon.app/Contents/MacOS/MoNoon,engine/.artifacts/bin/InspectorEditsProbe/release/InspectorEditsProbe.dll
```

`label=에디터,훅` 쌍을 여러 개 주면 실행마다 번갈아 연다. 레이블 `base`는 1단계와 "확정마다 캡처 1회" 검사를 건너뛴다(이전 에디터는 키 입력마다 기록한다). 훅은 Engine.Core 공개 API와 리플렉션만 쓰므로 같은 훅을 기준 에디터에도 줄 수 있다. 결과 폴더의 `summary.json`에 실행별 값, 항목별 중앙값·최소·최대, 저장 파일 해시가 남는다.
