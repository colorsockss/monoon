# PlatformerLab 경로 발판 회귀

코스 쇼케이스([ShowcaseCourse](../../../samples/PlatformerLab/Scenes/ShowcaseCourse.scene))에서 [CourseRunner](../../../samples/PlatformerLab/Scripts/Showcase/CourseRunner.cs)가 발판(`Path lift`: Kinematic 강체 + Path Follower 2D, 초당 90px, Once)을 `PlayTween`으로 출발시키고 그 위에 서서 기둥 위 곡선 경로를 탄 뒤 건너편 선반에 내린다. 입력 없이 60Hz 1,200틱을 재생한다(2026-11-26부터, 이전에는 옛 Paths 씬을 입력으로 탔다).

복사본에만 붙이는 [관찰 스크립트](PathTrace.source)가 매 틱 캐릭터·발판 위치(소수 4자리)와 탑승 여부를 기록하고, 플레이어의 헤드리스 리플레이(`MONOON_REPLAY_SNAPSHOTS`)가 40·150·380·520·660프레임을 SDL 소프트웨어 렌더러로 저장한다. 검사 기준:

- 일정 속도: 발판이 움직이기 시작한 다음 틱부터 경로 끝까지 틱당 이동 거리가 1.5px ± 0.2%.
- 운반: 발판이 움직이는 동안(370–670틱, 끝 10틱 앞에서 걸어 내린다) 탑승 중 캐릭터와 발판의 상대 위치 변화 < 0.05px.
- 캐릭터가 코스 끝 선반(x > 2200, y = 204)에 도착.
- 세 실행 경로의 기록과 캡처가 바이트 단위로 같다.

에디터 실행([StartupHook](StartupHook.cs), `MONOON_LINES_EDIT=1`)은 Play 전에 발판 이름을 길게 바꾸고 Scene 뷰에서 경로를 편집한다: 두 번째 점 끌기 → **+**(Add point) → **Smooth** → 바깥 핸들 끌기 → **×**(Delete point). 각 단계의 점 값을 확인하고, 실행 취소 5번·다시 실행 5번·실행 취소 5번으로 원래 경로와 이름이 돌아오는지 본다. 1280·1600·1920px 창에서 Path Follower 2D 행의 레이블 열·값 열(버튼 줄 포함)이 하나이고 섹션 밖으로 나간 컨트롤이 없는지 검사한다. 같은 프로브를 [BarrageLab.Lines](../BarrageLab.Lines/README.md)도 쓴다.

Mac에서는 저장소 루트에서 실행한다. 플레이어 템플릿과 에디터를 먼저 빌드한다([빌드](../../../docs/manual/build.md)).

```sh
qa=engine/tests/PlatformerLab.Paths
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/PlatformerLab "$work/PlatformerLab"
./monoon build "$work/PlatformerLab" release --profile distribution --output "$work/builds"
./monoon build "$work/PlatformerLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/PlatformerLab" \
  engine/.artifacts/bin/PathsProbe/release/PathsProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play" "$work/release" "$work/aot"
```

[결과](../results/lines-paths/README.md)
