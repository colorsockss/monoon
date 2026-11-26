# CollisionLab 프레임 프로파일러 회귀

CollisionLab의 충돌체 1만 개 씬에서 질의가 몰린 프레임을 찾고, 그 프레임의 작업 표에 기록된 질의 호출 수와 시간을 검사한다. [QueryField](../../../samples/CollisionLab/Scripts/QueryField.cs)는 씬에 들어올 때와 **R**을 누를 때 겹침·레이캐스트·스윕을 경로 96개에 대해 각각 14회(기준 2회와 6회 반복 × 두 검색 방식) 실행한다. 프레임 하나에 종류별로 1,344회, 합계 4,032회의 질의가 실행된다.

## 개발용 게임

저장소 루트에서 플레이어 템플릿을 갱신하고 같은 소스로 개발용·배포용 빌드를 만든다. 출력 폴더는 매번 새로 지정한다.

```sh
qa=engine/tests/CollisionLab.FrameProfiler
work=$(mktemp -d)
engine/scripts/build-players.sh osx-arm64
python3 engine/tests/Shared/collision_lab.py samples/CollisionLab "$work/CollisionLab"
./monoon build "$work/CollisionLab" release --profile development --output "$work/builds"
./monoon build "$work/CollisionLab" release --profile distribution --output "$work/builds"
python3 "$qa/replay.py" "$work/builds/Development/CollisionLab.app/Contents/MacOS/MoNoonPlayer" \
  "$work/builds/CollisionLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
./monoon profile "$work/release/development.csv"
```

Native AOT는 `native-aot`로 빌드하고 `Development/NativeAOT/`와 `NativeAOT/` 아래의 실행 파일을 넘긴다.

샘플은 쇼케이스만 배포하므로 [collision_lab.py](../Shared/collision_lab.py)가 복사본을 `ShowcaseCasts`(기존 랩 씬의 id를 그대로 쓴다)로 시작하게 하고, 카메라에 테스트 전용 FieldSelector를, 빌드 씬 목록에 Field1000·Field10000을 넣는다. [replay.py](replay.py)는 150프레임짜리 입력 기록을 만든다. 2번째 프레임에 **2**를 눌러 1만 개 씬을 열고, 90번째 프레임에 **R**을 누른다. 두 플레이어를 `MONOON_FRAME_PROFILE`과 함께 `--replay-input`으로 헤드리스 실행한다. 프레임마다 1/60초가 지나며 SDL 비디오·오디오는 사용하지 않는다. Release 런타임에는 `DOTNET_TieredCompilation=0`을 적용한다.

합격 기준은 다음과 같다.

- 배포용 플레이어는 CSV를 만들지 않고 무시했다는 경고를 출력한다. 두 플레이어의 `SPATIAL` 줄에서 시간 열을 뺀 대상 ID 해시와 후보 수가 같다.
- 개발용 CSV에 1–150번 프레임이 모두 있다. 종류별 1,344회 질의가 기록된 프레임은 정확히 두 개다. 다른 질의는 시작 씬 Probe의 겹침 3회·레이 3회·스윕 1회뿐이다.
- R 이후 가장 느린 프레임이 두 번째 질의 프레임이다. 두 질의 프레임 모두 Physics.Overlap·Raycast·Sweep의 Total 합이 프레임 시간의 50% 이상이다.

결과 폴더에는 입력 기록, 두 플레이어 로그, 개발용 CSV, `summary.json`이 남는다. `summary.json`에는 질의 프레임별 시간·점유율·할당, 씬 로딩 프레임, R 이후 가장 느린 프레임의 전체 작업 트리가 들어 있다.

## 에디터 Scene Play

```sh
engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/CollisionLab" \
  engine/.artifacts/bin/ProfilerProbe/release/ProfilerProbe.dll "$work/scene-play"
```

[editor.py](editor.py)는 샘플을 결과 폴더에 복사해 패키징한 에디터로 연다. [StartupHook](StartupHook.cs)이 Play를 누르고 Debug → Profiler를 켠다. 실제 Scene 프레임이 30개 이상 쌓이면 입력 액션으로 **2**를 누르고, 씬이 바뀐 뒤 1.5초 후 **R**을 누른다. 40프레임이 더 지나면 Stop한다. 창에 포커스가 없어도 Scene Play가 멈추지 않도록 콘솔 창이 열려 있을 때와 같은 설정을 사용한다.

Stop 후 기록 중 R 이후 가장 느린 프레임을 그래프 클릭과 같은 경로로 선택하고, 작업 표의 Physics.Overlap·Raycast·Sweep 행이 프로파일러 기록과 같은 호출 수를 표시하는지 확인한다. 창 너비 1400px과 820px에서 머리글과 첫·마지막 행의 열 위치가 1px 이내로 일치해야 한다. `result.json`과 두 너비의 창 캡처가 남는다. 그 프레임의 질의가 종류별 1,344회이고 프레임 시간의 50% 이상이면 `PASS`를 출력한다.

시간 값은 같은 Mac에서도 실행마다 달라지므로 호출 수와 비율만 판정에 쓴다. [Mac 측정 결과](../results/frame-profiler/README.md) · [사용법](../../../docs/manual/play-diagnostics.md#느린-프레임의-작업-분석)
