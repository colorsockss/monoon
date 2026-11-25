# SeedLab 탑다운 자유 이동 회귀

`verify.py prepare`는 SeedLab을 복사하고 [TraceCrowd.source](TraceCrowd.source)를 `Scripts/CrowdRun.Trace.cs`로 넣는다. 이 파일이 CrowdRun의 `partial void Traced`를 구현해 배달원 200명의 위치를 기록한다. 원본 샘플에서는 이 호출이 컴파일되지 않는다. 기록은 30틱마다 도착 수와 지금까지 모든 위치 비트의 FNV-1a 해시이고, 모두 도착하면 최종 위치 200개를 쓴다. 벽 안에 들어간 위치가 있으면 `inside` 줄이 생겨 검사가 실패한다.

`prepare`는 복사본의 시작 씬을 쇼케이스 군중 마당(`ShowcaseCrowd.scene`, 같은 `CrowdRun`)으로 바꾼다. 입력 기록은 130프레임에 **O**(문 열기) 하나이고 1,300프레임이다. 검사는 문 열기가 한 번 기록됐는지, 200명이 모두 자기 목표 좌표에 정확히 있는지 확인하고 `result.json`에 도착 틱, 위치 해시, 기록 파일 SHA-256을 쓴다. `compare`는 여러 실행의 `result.json`이 같은지 본다.

Mac에서 저장소 루트 기준으로 실행한다. 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md)를 최신 소스로 빌드한다.

```sh
qa=engine/tests/SeedLab.NavigationFree
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/SeedLab "$work/SeedLab"
./monoon build "$work/SeedLab" release --profile distribution --output "$work/builds"
./monoon build "$work/SeedLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/SeedLab" \
  engine/.artifacts/bin/CrowdProbe/release/CrowdProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/release" "$work/native-aot" "$work/scene-play"
# Scene 편집 미리보기: Yard를 선택한 창 캡처와 Inspector의 Top-down 줄
MONOON_QA_WINDOW=engine/.artifacts/clip-qa/window sh engine/scripts/gui-lock.sh python3 "$qa/gizmo.py" \
  editor/MoNoon.app/Contents/MacOS/MoNoon samples/SeedLab engine/.artifacts/bin/CrowdProbe/release/CrowdProbe.dll "$work/gizmo"
```

비용은 `dotnet run --project engine/benchmarks/Navigation -c Release -- 5 2000`으로 잰다.
