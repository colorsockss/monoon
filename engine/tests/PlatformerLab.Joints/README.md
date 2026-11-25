# PlatformerLab 조인트 회귀

코스 쇼케이스([ShowcaseCourse](../../../samples/PlatformerLab/Scenes/ShowcaseCourse.scene))에서 [CourseRunner](../../../samples/PlatformerLab/Scripts/Showcase/CourseRunner.cs)가 입력 없이 흔들다리(Revolute Joint 2D 사슬)를 건너고, 도르래 발판(Slider Joint 2D 두 개와 [PulleyPair](../../../samples/PlatformerLab/Scripts/PulleyPair.cs)) 사이를 점프해 옮겨 탄다. [Cross.inputreplay](Cross.inputreplay)는 이벤트 없는 600틱이다. 검사: 캐릭터가 x > 690(실측 1247.6)까지 가고, 다리가 y > 300(실측 319.4)까지 처지고, 두 발판이 반대로 10px 넘게(실측 +16.01, −16.00) 움직인다(2026-11-26부터, 이전에는 옛 Joints 씬을 입력으로 건넜다).

복사본에만 붙이는 [관찰 스크립트](JointTrace.source)가 매 틱 캐릭터 위치, 판자 8개와 도르래 발판 2개의 위치·각도를 소수 4자리(각도 3자리)로 기록한다. 준비 단계는 복사본의 시작 씬을 코스 쇼케이스로 바꾸고 관찰 스크립트를 CourseRunner 앞에 붙인다. 독립 게임은 배포 플레이어의 입력 리플레이 진입점(헤드리스)을, Scene Play는 에디터의 Play 세션에 같은 기록을 재생한다. 세 기록은 바이트 단위로 같아야 한다.

Mac에서는 저장소 루트에서 다음 명령을 실행한다. 에디터를 실행하기 전에 Release와 Native AOT용 [플레이어 템플릿](../../../docs/manual/build.md)을 준비한다.

```sh
qa=engine/tests/PlatformerLab.Joints
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/PlatformerLab "$work/PlatformerLab"
./monoon build "$work/PlatformerLab" release --profile distribution --output "$work/builds"
./monoon build "$work/PlatformerLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/PlatformerLab" \
  engine/.artifacts/bin/JointsProbe/release/JointsProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play" "$work/release" "$work/aot"
```

각 실행은 캐릭터가 언덕(x > 690)에 닿고, 다리의 가장 낮은 판자가 300px 아래로 처지고, 도르래 발판이 서로 반대로 10px 넘게 움직였는지 검사해 `result.json`에 남긴다. 에디터 실행은 Play 전에 Plank 3(Revolute)과 Pulley deck A(Slider)를 선택해 1280·1600·1920px 창에서 Rigidbody 2D와 조인트 Inspector 행의 값 열·레이블 열이 하나로 맞는지 확인하고 `inspector-*.png`를 저장한다. 이웃 판자 이름을 길게 바꿔 Connect 목록이 잘리지 않는지도 본다.

Windows에서는 같은 스크립트에 에디터와 플레이어 경로를 넘긴다. Linux에서는 플레이어 검사만 한다(Linux 에디터는 2026-11-23부터 지원하지 않는다). [판정 기준과 결과](../results/physics-box2d/README.md)
