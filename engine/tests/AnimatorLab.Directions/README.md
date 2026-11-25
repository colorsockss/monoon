# AnimatorLab 4·8방향 클립 세트 회귀 (16.5)

대상은 AnimatorLab 쇼케이스의 정찰병과 [Scout.animator](../../../samples/AnimatorLab/Assets/Showcase/Scout.animator)다(2026-11-26부터, 이전에는 `scout.py`가 만든 `Scout.scene`). [scout.py](scout.py)에는 엔진과 별개인 방향 판정 모델(`pick`)만 남았다. 컨트롤러는 Vector2 `Move`와 Float `Speed` 두 파라미터, 4방향 Idle과 8방향 Walk 두 상태, Speed로 오가는 전환 두 개다.

`verify.py prepare`는 샘플 복사본의 쇼케이스에서 정찰병의 ScoutWalker를 떼고, 애니메이터가 있는 Scout picture에 시험용 드라이버 [DriveScout](DriveScout.source)와 관찰 스크립트 [TraceScout](TraceScout.source)를 붙인다. 드라이버는 방향키로 Move를 정하고, Space를 누르면 정사각형 둘레의 64점(1/8 간격, float에서 정확)을 두 틱씩 지나는 한 바퀴를 넣는다. 관찰 스크립트는 매 틱 애니메이터의 상태, 프레임, 방향(`Facing`)을 기록한다(그 틱의 애니메이터 단계 전 값). 입력 기록은 420프레임이고 방향키로 N → NE → E → SE → S → SW → W → NW → N을 25틱씩 걷고 멈춘 뒤(230), 2틱 동안 SW로 걷고 멈추고(250), 270에 Space로 한 바퀴를 돈다.

- `play`/`editor.py`: 기록의 모든 틱 상태와 방향이 `verify.py`의 독립 모델과 같은지, Walk가 8방향을 모두 보여 주는지 확인한다. 한 바퀴에서는 히스테리시스 때문에 가장 가까운 방향이 바뀌는 점보다 한 점 늦게 클립이 바뀐다(예: NE → E는 가장 가까운 방향이 E가 되는 점 5(NE에서 24.4°)이 아니라 점 6(31°)에서 바뀐다).
- `compare`: 에디터 미리보기·Scene Play·Release·Native AOT 기록이 바이트 단위로 같은지 비교한다.
- `snapshots`: Release와 Native AOT가 창 없는 재생에서 30·95·240·330프레임에 저장한 PNG가 같은지 비교한다.
- 에디터 프로브는 Walk 상태 대화상자를 열어 Clips(8 directions)·Parameter와 N…NW 클립 행이 한 열에 있고 각 레이블이 자기 행 높이에 있는지, 클립이 방위 순서인지 확인하고 325·270px에서 캡처한다. 4 directions로 바꾸면 N·E·S·W 행만 남는지 본 뒤 적용하지 않고 닫는다. 그다음 Animator 패널의 미리보기 플레이어를 타이머 대신 고정 틱으로 진행하며 DriveScout와 같은 Move·Speed 값(`inputs.txt`)을 넣어 `preview.trace`를 만들고, 같은 기록으로 Scene Play를 실행한다.

히스테리시스 각도, 0 벡터, 4방향 상태의 대각선 처리, 클립 전환 시 재생 위치, 할당 0, 이전 파일 호환과 잘못된 입력 거부는 `./monoon test`의 [AnimatorDirectionChecks](../Engine.Checks/AnimatorDirectionChecks.cs)(`--only animator-directions`)가 확인한다.

```sh
qa=engine/tests/AnimatorLab.Directions
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
  engine/.artifacts/bin/ScoutProbe/release/ScoutProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play/preview.trace" "$work/scene-play/scene-play.trace" \
  "$work/release/release.trace" "$work/native-aot/native-aot.trace"
```

[검증 결과](../results/engine-features-3/README.md#165-48방향-클립-세트)
