# SpriteLab Aseprite 임포트 회귀

이 Mac에는 Aseprite가 설치되어 있지 않다. 그래서 [aseprite_fixture.py](aseprite_fixture.py)가 공개된 파일 형식대로 `.aseprite`를 직접 쓴다.

- `fixtures`는 엔진 회귀용 [Rgba·Indexed·Multiply 픽스처](../fixtures/Aseprite)를 만든다. 크기는 8×6, 4프레임, 프레임 시간은 100·150·200·250ms다. 레이어는 세 개다. 빨간 Body, 숨김 처리된 초록 Hidden, 불투명도 128의 파란 Glow가 있다. 태그는 여섯 개로 정방향, 역방향, 핑퐁, 역핑퐁과 반복 횟수 2·3을 포함한다. 압축 셀, 비압축 셀과 링크 셀도 들어 있다.
- `sample`은 SpriteLab의 `Arrow.png`와 `Dot.png`를 읽어 `Assets/ArrowCycle.aseprite`를 만든다. 두 프레임은 4000ms와 700ms이며, 숨긴 Sketch 레이어는 캔버스 전체를 자홍색으로 덮는다.
- `variant`는 같은 샘플을 1000ms·300ms 프레임과 빨간 점으로 다시 쓴다. 열린 에디터의 자동 재임포트를 확인할 때 쓴다.

`./monoon test`의 [AsepriteChecks](../Engine.Checks/AsepriteChecks.cs)는 픽스처를 파싱하고 임시 프로젝트에서 임포트한다. 커밋한 `ArrowCycle.png`, `.png.meta`, `ArrowCycle.clip`이 임포터 결과와 바이트 단위로 같은지, 두 프레임이 원본 PNG와 같은지도 검사한다.

`verify.py prepare`는 샘플 복사본의 Animated copy에 관찰 스크립트를 붙인다. 생성된 클립 복사본의 두 프레임에는 이벤트 `F0`, `F1`만 추가한다. 프레임 순서, 시간과 셀은 생성된 그대로다. 스크립트는 프레임이 시작한 틱을 기록하고, 검사기는 `.aseprite` 헤더에서 직접 읽은 프레임 시간과 비교한다.

저장소 루트에서 실행한다. 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md)를 최신 소스로 빌드한다. 창을 여는 명령은 공용 GUI 잠금 스크립트로 감싼다.

```sh
qa=engine/tests/SpriteLab.Aseprite
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/SpriteLab "$work/SpriteLab"
./monoon build "$work/SpriteLab" release --profile distribution --output "$work/builds"
./monoon build "$work/SpriteLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/SpriteLab.app/Contents/MacOS/MoNoonPlayer" "$work/release" "$work/SpriteLab/Assets/ArrowCycle.aseprite"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/SpriteLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot" "$work/SpriteLab/Assets/ArrowCycle.aseprite"
python3 "$qa/verify.py" forbidden "$work/builds/SpriteLab.app"
python3 "$qa/verify.py" forbidden "$work/builds/NativeAOT/SpriteLab.app"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/SpriteLab" \
  engine/.artifacts/bin/AsepriteProbe/release/AsepriteProbe.dll "$work/scene-play"
```

`editor.py`는 Scene Play에서 같은 600틱 입력 기록을 재생한다. Play를 멈춘 뒤 에디터 창을 연 채로 `ArrowCycle.aseprite`를 변형본으로 덮어쓴다. 시트 메타데이터의 원본 해시와 Scene 패널이 가진 PNG 해시가 모두 새 값이 될 때까지 기다린다. 이후 클립 시간이 1.0·0.3초인지, 두 번째 셀이 빨간 점인지 확인한다.
