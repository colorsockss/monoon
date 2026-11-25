# CollisionLab 나눠 읽기 회귀

CollisionLab의 충돌체 1만 개 방에 4096×4096 배경 PNG 네 장을 더한 방을 읽는다. 업데이트당 예산 4 ms로 나눠 읽는 경로와 예산 0(이전 방식)을 비교한다. 로딩 프레임의 최장·95백분위 시간, 전체 로딩 시간, 취소 후 남은 자원을 기록한다. [사용법](../../../docs/manual/scene-streaming.md#큰-방을-여러-프레임에-나눠-읽기)

## 준비

[replay.py](replay.py) `prepare`는 [collision_lab.py](../Shared/collision_lab.py)로 `samples/CollisionLab`을 새 폴더에 복사한다(시작 씬 `ShowcaseCasts`, 카메라의 FieldSelector, 빌드에 넣은 필드 씬). 그 복사본의 `Field10000.scene`에 배경 오브젝트 네 개를 추가하고, 그 씬의 QueryField 컴포넌트는 지운다. QueryField는 방에 들어온 첫 틱에 질의 4,032회를 실행해 연결 프레임을 약 140 ms 늘린다. 이 작업은 로딩이 아니므로 측정에서 제외한다.

배경 PNG는 한 행을 회전한 행으로 이루어진다. 파일은 약 470 KB지만 디코딩할 때 64 MiB를 풀고, 모든 행에 Paeth 필터를 적용한다. 저장소에는 생성 스크립트만 둔다.

```sh
qa=engine/tests/CollisionLab.SplitLoading
work=$(mktemp -d)
engine/scripts/build-players.sh osx-arm64
python3 "$qa/replay.py" prepare "$work/CollisionLab"
./monoon build "$work/CollisionLab" release --profile development --output "$work/builds"
./monoon build "$work/CollisionLab" native-aot --profile development --output "$work/builds"
```

## 개발용 게임 헤드리스 재생

```sh
python3 "$qa/replay.py" run "$work/builds/Development/CollisionLab.app/Contents/MacOS/MoNoonPlayer" "$work/release" --runs 3
python3 "$qa/replay.py" run "$work/builds/Development/NativeAOT/CollisionLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot" --runs 3
```

실행마다 300프레임짜리 입력 기록 세 개를 예산 0과 4 ms로 재생한다. 2번째 프레임에 **4**(추가) 또는 **3**(교체)를 누른다. 취소 기록은 **4**를 누른 뒤 같은 예산으로 측정한 추가 로딩의 첫 업로드와 마지막 업로드 사이에서 **Backspace**를 누른다. 취소가 늦어 방이 연결됐으면 2프레임 앞당겨 최대 세 번 다시 실행한다.

플레이어는 다음 환경 변수로 실행한다.

- `MONOON_FRAME_PROFILE`: 프레임별 작업 CSV. 요약을 만든 뒤 지운다.
- `MONOON_SCENE_LOAD_BUDGET_MS=0|4`: 개발용 빌드에서만 적용되는 로딩 예산.
- `MONOON_REPLAY_OFFSCREEN=1`: 창과 오디오 장치 없이 SDL 소프트웨어 렌더러에 텍스처를 올린다. 기록된 1/60초에 맞춰 프레임 사이를 기다린다. 기다린 시간은 프로파일에서 뺀다. 재생 시작과 끝에 상주 파일 수, 디코딩 버퍼 수, SDL 스프라이트 텍스처 수를 출력한다.
- SDL 비디오·오디오 드라이버를 비활성화한다. Release 런타임에는 `DOTNET_TieredCompilation=0`을 적용한다.

로딩 구간은 키를 누른 다음 프레임부터 Scene.Commit이 기록된 프레임(취소하면 마지막 Scene.Loading 프레임)까지다. 전체 로딩 시간은 구간 프레임마다 1/60초와 그 프레임 작업 시간 중 큰 값을 더한 값이다.

합격 기준은 다음과 같다.

- 모든 추가·교체 로딩이 300프레임 안에 연결된다.
- 예산 4 ms의 최장 프레임 중앙값이 예산 0보다 짧다.
- 취소한 재생의 끝 자원 수(상주 파일, 디코딩 버퍼, SDL 텍스처)가 시작과 같고, 방이 연결되지 않는다.

`summary.json`에는 실행별 구간 프레임 수, 최장·p95·합계, Scene.Instantiate·Upload·Commit 시간, 가장 느린 프레임의 작업 트리와 Gen0 횟수, 취소 프레임, 자원 수가 남는다.

## 에디터 Scene Play

```sh
engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/CollisionLab" \
  engine/.artifacts/bin/SplitLoadingProbe/release/SplitLoadingProbe.dll "$work/scene-play" 3
```

[editor.py](editor.py)는 준비한 프로젝트를 실행마다 복사하고, 패키징한 에디터를 예산 0과 4 ms로 번갈아 연다. [StartupHook](StartupHook.cs)이 Play와 프로파일러를 켠다. **4**로 방을 추가하고 연결된 뒤 **0**으로 실험실 방을 다시 연다. 다시 **4**를 누르고 진행도가 0.55를 넘으면 **Backspace**로 취소한다. 취소 후 상주 파일·디코딩 버퍼·SDL Scene 패널 텍스처 수가 취소 전과 같아야 한다. 연결 프레임은 Play Hierarchy가 오브젝트 1만 개를 다시 만드는 시간을 포함하므로 로딩 구간과 따로 기록한다. [Mac 측정 결과](../results/split-loading/README.md)
