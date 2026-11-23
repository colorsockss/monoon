# PlatformerLab 타일 충돌 회귀

점프와 경사 왕복 입력은 [Traverse.inputreplay](Traverse.inputreplay)에 있다. 60Hz 고정 업데이트 380틱을 진행한다. 복사본에만 붙이는 관찰 스크립트가 매 틱 캐릭터 위치, 수직 속도, 접지 오브젝트와 법선을 기록한다. 독립 게임은 배포 플레이어의 입력 리플레이 진입점을 사용한다.

Mac에서는 저장소 루트에서 다음 명령을 실행한다. 에디터를 실행하기 전에 Release와 Native AOT용 [플레이어 템플릿](../../../docs/manual/build.md)을 준비한다.

```sh
qa=engine/tests/PlatformerLab.TileCollision
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/PlatformerLab "$work/PlatformerLab"
./monoon build "$work/PlatformerLab" release --profile distribution --output "$work/builds"
./monoon build "$work/PlatformerLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/PlatformerLab" \
  engine/.artifacts/bin/TileContactProbe/release/TileContactProbe.dll "$work/scene-play"
```

Windows에서는 같은 스크립트에 에디터와 플레이어 경로를 넘긴다. Linux에서는 플레이어 검사만 한다(Linux 에디터는 2026-11-23부터 지원하지 않는다). 에디터 회귀는 복사본의 타일셋에서 경사의 꼭짓점과 발판의 차단 방향을 편집해 저장하고, 다시 열어 복원한 다음 Scene Play를 시작한다. [판정 기준과 플랫폼별 결과](../results/tile-collision/README.md)
