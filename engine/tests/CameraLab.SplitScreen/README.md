# CameraLab 합쳐지고 갈라지는 분할 화면·카메라별 화면 효과 검증

`verify.py prepare`는 CameraLab을 별도 폴더에 복사하고 쇼케이스(`Scenes/Showcase.scene`, 시작 씬)에 관찰 스크립트만 더한다. 원본 샘플은 건드리지 않는다(2026-11-26부터, 이전에는 기존 CameraLab 씬에 합치기 설정을 넣고 Space 자동 이동을 재생했다).

- 쇼케이스의 Player Camera: `splitWith: <Partner Camera>`, `mergeDistance: 260`, `splitDistance: 340`, `blendSeconds: 0.6`, 줌 1.5. 두 카메라 viewport는 왼쪽·오른쪽 절반(400×450)이고 HUD는 그 위에 그린다.
- 관찰 스크립트 [TraceSplit](TraceSplit.source)가 20틱에 Player Camera에 `Screen.For(Main).Adjust(Color.White, -1)`(검게), Partner Camera에 `Screen.For(Second).Flash(Color.Red, .3)`를 걸고 24틱에 Adjust를 끈다. 90틱에는 Player Camera 흔들림과 Partner Camera 줌 펀치를 건다. 게임 틱마다 두 카메라의 viewport·중심·줌과 카메라별·화면 전체 효과 값을 `MONOON_SPLIT_TRACE`에 한 줄씩 쓴다(`float.ToString("R")`).

입력(프레임): →(30–230), ←(240–440). 주인공이 장터 동쪽을 순찰하는 동료 쪽으로 걸었다가 돌아온다. 480프레임. Release·Native AOT는 창 없이 재생하고 SDL 소프트웨어 렌더러로 22·300프레임을 PNG로 저장한다(`MONOON_REPLAY_SNAPSHOTS`).

| 확인 | 기준 |
| --- | --- |
| 실행 경로 일치 | Release·Native AOT·Scene Play의 트레이스가 공통 틱까지 바이트 단위로 같다 |
| 시작 | 멀리 떨어진 채 시작하면 전환 없이 바로 갈라져 있다: Player 왼쪽 `0,0,400,450`, Partner 오른쪽 |
| 합치기·가르기 | 단계가 `split > blend > merged > blend > split` 한 번씩이다(깜빡임 없음, 합쳐진 구간은 257–356틱). 합쳐진 동안 Player Camera가 `0,0,800,450`을 쓰고 Partner Camera의 `TryGetView`는 같은 시야를 돌려준다 |
| 부드러운 전환 | viewport 폭이 틱마다 18px(0.6초 smoothstep 최대 기울기) 넘게 바뀌지 않는다 |
| 좌우 | 돌아온 뒤에는 다시 Player가 왼쪽, Partner가 오른쪽이다 |
| 카메라별 효과 | Partner의 섬광만 0보다 크고 Player·화면 전체 섬광은 0이다. 카메라별 흔들림·줌 펀치가 화면 전체 값(`Session.Screen.Current`)을 바꾸지 않는다 |
| 캡처(Release·Native AOT) | 22프레임: 위에 그린 HUD 패널(기능 태그, 설명, 이음매, 동료 이름표, 미니맵, 효과 패널)을 뺀 Player viewport는 90% 넘게 `(0,0,0)`, Partner viewport는 90% 넘게 빨강이고 Player viewport에는 빨강이 없다. 설명 패널에는 빨강도 검정 덮개도 없다. 300프레임: 합쳐진 화면에 빨강이 1% 미만(그림 자체의 붉은 픽셀)이고 검정이 10% 미만 |
| Inspector(Scene Play) | 시작 훅이 Player Camera를 선택해 창 1280·1600·1920px에서 Half-life·Padding·Split with·Merge at·Split at·Blend 행의 레이블 x, 입력 x, 입력 폭이 같은지 확인하고 Inspector를 PNG로 저장한다 |

저장소 루트에서 실행한다. 창을 여는 Scene Play만 GUI 잠금이 필요하다. 보통은 실행기로 돌린다: `python3 engine/scripts/run-harness.py CameraLab.SplitScreen --out <폴더>`.

```sh
q=engine/tests/CameraLab.SplitScreen; work=$(mktemp -d)
engine/scripts/build-editor-mac.sh
dotnet build "$q" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$q/verify.py" prepare samples/CameraLab "$work/CameraLab"
./monoon build "$work/CameraLab" release --profile distribution --output "$work/builds"
./monoon build "$work/CameraLab" native-aot --profile distribution --output "$work/builds"
python3 "$q/verify.py" play "$work/builds/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$q/verify.py" play "$work/builds/NativeAOT/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
python3 "$q/verify.py" prepare samples/CameraLab "$work/play/CameraLab"
sh engine/scripts/gui-lock.sh python3 "$q/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/play/CameraLab" \
  engine/.artifacts/bin/SplitScreenProbe/release/SplitScreenProbe.dll "$work/scene-play"
python3 "$q/verify.py" check "$work/scene-play" "$work/release" "$work/aot"
```

계산 규칙의 단위 검사(히스테리시스 구간, 전환 중 Bounds, 중점, 좌우, 할당 없음, schemaVersion 43, YAML, 검증, 복제, 카메라별 효과가 다른 viewport에 새지 않음, 접근성 배율, 결정성)는 `./monoon test`의 [CameraSplitChecks](../Engine.Checks/CameraSplitChecks.cs)이며 `--only camera-split`으로 따로 실행한다.
