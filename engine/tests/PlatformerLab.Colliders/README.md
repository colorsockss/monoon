# PlatformerLab 캡슐·체인 충돌체 회귀 (16.4)

[Slopes 씬](../../../samples/PlatformerLab/Scenes/Slopes.scene)에서 Capsule Collider 2D 캐릭터가 8px 타일 바닥의 이음새를 지나 Edge Collider 2D 경사(14°)와 언덕에 오른 뒤 돌아온다. 입력은 [RoundTrip.inputreplay](RoundTrip.inputreplay)에 있다. 60Hz 고정 업데이트 450틱 동안 10틱에 →를 누르고, 200틱에 →를 떼며 ←를 누르고, 390틱에 ←를 뗀다.

복사본에만 붙이는 [관찰 스크립트](ColliderTrace.source)가 매 틱 캐릭터 위치, 지면 여부, Box2D 캡슐 Boulder의 위치·각도를 기록한다. 준비 단계는 복사본의 시작 씬을 Slopes로 바꾼다. 독립 게임은 배포 플레이어의 입력 리플레이 진입점(헤드리스)을, Scene Play는 에디터의 Play 세션에 같은 기록을 재생한다. 세 기록은 바이트 단위로 같아야 한다.

`check`는 걷는 틱(12–198, 202–388)에 지면에 선 채 1.25px(반 걸음) 미만으로 움직인 틱(걸림)이 0개인지, 캐릭터가 x > 480·y < 215까지 올라갔다가 출발점 1px 안으로 돌아왔는지, Boulder가 체인 위(710, 204)에 서 있는지 검사해 `result.json`에 남긴다.

에디터 실행은 Play 전에 Scene에서 Runner의 캡슐 반지름 핸들을 끌고(11 → 7), Slope의 Edge 점을 옮기고, 점을 더하고 지운 뒤 Undo 4회로 원래 씬이 되는지 본다. 1280·1600·1920px 창에서 Capsule Collider 2D와 Edge Collider 2D Inspector 행의 값 열·레이블 열이 하나로 맞는지 확인하고 `inspector-*.png`, `edge-edit.png`를 저장한다.

Mac에서는 저장소 루트에서 다음 명령을 실행한다. 에디터를 실행하기 전에 Release와 Native AOT용 [플레이어 템플릿](../../../docs/manual/build.md)을 준비한다.

```sh
qa=engine/tests/PlatformerLab.Colliders
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/PlatformerLab "$work/PlatformerLab"
python3 "$qa/verify.py" prepare samples/PlatformerLab "$work/edit-project"
./monoon build "$work/PlatformerLab" release --profile distribution --output "$work/builds"
./monoon build "$work/PlatformerLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/edit-project" \
  engine/.artifacts/bin/CollidersProbe/release/CollidersProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play" "$work/release" "$work/aot"
```

에디터는 열었던 프로젝트에 편집 초안을 남기므로 에디터 실행마다 새로 준비한 복사본(`edit-project`)을 쓴다. 엔진 수준 검사(질의, 문서, Slopes 헤드리스 왕복, 할당, 1천·1만 격자 대조)는 `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only colliders`다. [판정 기준과 결과](../results/colliders-capsule-chain/README.md)
