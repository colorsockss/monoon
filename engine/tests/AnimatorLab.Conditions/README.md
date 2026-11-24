# AnimatorLab 애니메이터 조건 회귀 (16.5)

[guard.py](guard.py)가 AnimatorLab의 Guard 클립 4개, [Guard.animator](../../../samples/AnimatorLab/Assets/Guard.animator), `Guard.scene`을 만든다. 컨트롤러는 Bool·Int·Float·Trigger 파라미터, 두 조건 AND 전환, 조건 없는 Exit time 전환, 조건과 Exit time을 함께 쓰는 전환, Any State 전환을 모두 쓴다.

`verify.py prepare`는 샘플 복사본의 시작 씬을 Guard로 바꾸고 관찰 스크립트 [TraceGuard](TraceGuard.source)를 GuardDemo 뒤에 붙인다. 이 스크립트는 매 틱 애니메이터의 상태와 프레임을 기록한다(그 틱의 애니메이터 단계 전 값). 입력 기록은 320프레임이고 → 5, Space 30·170·185·200, ↑ 90, ↓ 100·140, ← 160·260이다.

- `play`/`editor.py`: 기록의 모든 틱 상태가 `verify.py`의 독립 모델(컨트롤러 규칙을 손으로 옮긴 Python)과 같은지, 다섯 상태를 모두 지나는지 확인한다.
- `compare`: 에디터 미리보기·Scene Play·Release·Native AOT 기록이 바이트 단위로 같은지 비교한다.
- `snapshots`: Release와 Native AOT가 창 없는 재생에서 40·95·215프레임에 저장한 PNG가 같은지 비교한다.
- 에디터 프로브는 Idle → Walk 전환 대화상자(조건 두 행)를 열어 조건 행이 From·To와 같은 열에, Conditions 레이블이 첫 행 높이에 있는지 확인하고 420·340px에서 캡처한다. 그다음 Animator 패널의 미리보기 플레이어를 타이머 대신 고정 틱으로 진행하며 같은 파라미터 변경을 넣어 `preview.trace`를 만들고, 같은 기록으로 Scene Play를 실행한다.

조건별 동작, 우선순위, Trigger 보존, 할당 0, 이전 파일 호환과 잘못된 입력 거부는 `./monoon test`의 [AnimatorConditionChecks](../Engine.Checks/AnimatorConditionChecks.cs)(`--only animator-conditions`)가 확인한다.

```sh
qa=engine/tests/AnimatorLab.Conditions
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/AnimatorLab "$work/AnimatorLab"
./monoon build "$work/AnimatorLab" release --profile distribution --output "$work/builds"
./monoon build "$work/AnimatorLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/AnimatorLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/AnimatorLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
python3 "$qa/verify.py" snapshots "$work/release" "$work/native-aot"
python3 "$qa/verify.py" prepare samples/AnimatorLab "$work/editor-copy"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/editor-copy" \
  engine/.artifacts/bin/GuardProbe/release/GuardProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play/preview.trace" "$work/scene-play/scene-play.trace" \
  "$work/release/release.trace" "$work/native-aot/native-aot.trace"
```

[검증 결과](../results/engine-features-3/README.md#165-애니메이터-조건-확장)
