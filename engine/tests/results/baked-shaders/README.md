# PlatformerLab 배포용 셰이더 프레임 시트

2026-11-16, Apple M4 Max, macOS 27.0.1 arm64. MoNoon.app에서 PlatformerLab의 Play를 누르고, 배포 프로필로 빌드한 Release와 Native AOT도 각각 독립 SDL 창에서 실행했다. 세 창 모두 원본 샘플에서 횃불이 움직이고 주변 발판의 밝기가 달라졌다. 자유 재생 화면을 창별로 16장 캡처했다.

## 같은 게임 틱에서 비교한 결과

창마다 다른 캡처 시점을 맞추기 위해 별도 샘플 복사본에 게임 시계만 정지·진행시키는 테스트 컴포넌트를 추가했다. 동일하게 Scene Play와 Release·Native AOT의 실제 창을 열고, 60 Hz 게임 시계에서 불꽃 32개 셀 각각과 다음 루프 첫 셀에 해당하는 총 33개 시점에 멈췄다. 원본 배포본과 검증용 배포본의 횃불 YAML, 불꽃 PNG, 광원 PNG, 먼지 PNG는 각각 바이트 단위로 동일했다. [테스트 시계](../../PlatformerLab.Shaders/ShaderClock.cs) · [33개 시점의 원본 RGB와 프레임 번호](macos-arm64.csv)

| 관찰 항목 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| 횃불 옆 발판, 38틱·15번 셀 | [RGB 125, 190, 168](scene-play-dim.png) | [RGB 125, 190, 168](release-dim.png) | [RGB 125, 190, 168](aot-dim.png) |
| 같은 위치, 56틱·22번 셀 | [RGB 154, 210, 177](scene-play-bright.png) | [RGB 154, 210, 177](release-bright.png) | [RGB 154, 210, 177](aot-bright.png) |
| 반경 밖의 같은 재질 발판 | 33회 모두 112, 182, 164 | 동일 | 동일 |
| 불꽃 셀 순서 | 0–31 → 0 | 0–31 → 0 | 0–31 → 0 |

밝기는 논리 해상도 800×450의 (432, 244)와 (744, 244) 픽셀 중앙에서 읽었다. 33개 시점 모두 세 실행의 발판 RGB가 일치했다. 불꽃 셀은 창마다 레터박스와 배율을 보정하고 24×32개 셀 내부 픽셀을 읽어 식별했다. 가로로 인접한 색의 차이를 구해 넓게 퍼지는 광원의 영향을 줄인 다음, 매 시점의 무늬를 Scene Play의 32개 기준 프레임과 비교했다. 33회 모두 가장 가까운 프레임이 하나로 결정됐다. [화면 비교 코드](../../PlatformerLab.Shaders/compare_mac.py)

## 배포 자산

- `Game/Assets/Torch.shader`는 24×32픽셀, 32프레임, 24 fps, 시트 GUID를 담은 84바이트 YAML이다. 원본 프로젝트의 SkSL은 에디터에서 계속 편집할 수 있다.
- `Game/Assets/Torch.shader.frames.png`는 768×32픽셀, 14,665바이트다. 두 배포본의 SHA-256은 `baff508880d7d9f41619894f93509ef8e6b15803edbac255828c1e47bd94c889`이다. 광원 감쇠 PNG는 96×96픽셀, 먼지 PNG는 12×12픽셀이다.
- 두 앱의 세 PNG를 디코딩해 같은 Mac GPU에서 Scene Play 방식으로 생성한 시트의 RGBA 전체와 비교했다. 투명 가장자리까지 동일했다. 콘텐츠 번들에 셰이더를 넣은 경우에도 마운트한 YAML이 올바른 PNG를 찾는 회귀 검사를 통과했다. [엔진 검사](../../Engine.Checks/BakedShaderChecks.cs)

## 재현

저장소 루트에서 실행한다. 아래 마지막 검사는 실제 두 배포본의 PNG와 저작용 셰이더의 전체 픽셀을 비교한다.

```sh
engine/scripts/build-players.sh osx-arm64
./monoon build samples/PlatformerLab release --profile distribution --output game-builds/PlatformerLab
./monoon build samples/PlatformerLab native-aot --profile distribution --output game-builds/PlatformerLab
dotnet run --project engine/tests/Engine.Checks -c Release -- . --check-shader-players \
  game-builds/PlatformerLab/PlatformerLab.app/Contents/MacOS/Game \
  game-builds/PlatformerLab/NativeAOT/PlatformerLab.app/Contents/MacOS/Game
```

고정 틱 실험에는 `python3 engine/tests/PlatformerLab.Shaders/fixture.py <새 프로젝트 폴더>`로 복사본을 만든 후 같은 방법으로 두 게임을 빌드한다. 각 실행에 별도의 `MONOON_SHADER_TEST_CLOCK` 폴더를 지정하고 그 안의 `target`에 처음에는 0을 쓴다. CSV의 틱을 차례로 지정하고 `ready`가 해당 숫자가 된 뒤 화면이 표시되면 창을 캡처한다. 각 모드 폴더(`scene-play`, `release`, `aot`)에 `00.png`에서 `32.png`까지 저장한 다음 `python3 engine/tests/PlatformerLab.Shaders/compare_mac.py <캡처 상위 폴더> <결과.csv>`로 대조한다. [엔진 매뉴얼](../../../../docs/manual/effects.md#배포용-프레임-시트)
