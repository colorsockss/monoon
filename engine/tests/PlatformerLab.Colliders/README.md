# PlatformerLab 캡슐·체인 충돌체 회귀 (16.4)

코스 쇼케이스([ShowcaseCourse](../../../samples/PlatformerLab/Scenes/ShowcaseCourse.scene))에서 Capsule Collider 2D 주인공이 입력 없이 Edge Collider 2D 언덕(`Hill`, 480×100)을 넘어 깃발까지 간다. [RoundTrip.inputreplay](RoundTrip.inputreplay)는 이벤트 없는 1,200틱이다(2026-11-26부터, 이전에는 옛 Slopes 씬에서 8px 타일 이음새·경사를 왕복하고 체인 위 Box2D 캡슐 Boulder를 확인했다. 두 가지는 쇼케이스에 없다).

복사본에만 붙이는 [관찰 스크립트](ColliderTrace.source)가 매 틱 캐릭터 위치와 지면 여부를 기록한다. 준비 단계는 복사본의 시작 씬을 코스 쇼케이스로 바꾼다. 독립 게임은 배포 플레이어의 입력 리플레이 진입점(헤드리스)을, Scene Play는 에디터의 Play 세션에 같은 기록을 재생한다. 세 기록은 바이트 단위로 같아야 한다.

`check`는 언덕 위(x 1620–2040)에서 지면에 선 틱이 120개 넘고(실측 168) 그중 1.25px(반 걸음) 미만으로 움직인 틱(걸림)이 0개인지, 캐릭터가 언덕에서 y < 170까지 올랐는지, x > 2200(실측 2240.3)에 도착했는지 검사해 `result.json`에 남긴다.

에디터 실행은 Play 전에 Scene에서 Runner의 캡슐 반지름 핸들을 끌고(12 → 8), Hill의 두 번째 Edge 점을 10 올리고, 점을 더하고 지운 뒤 Undo 4회로 원래 씬이 되는지 본다. 1280·1600·1920px 창에서 Capsule Collider 2D와 Edge Collider 2D Inspector 행의 값 열·레이블 열이 하나로 맞는지 확인하고 `inspector-*.png`, `edge-edit.png`를 저장한다.

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
