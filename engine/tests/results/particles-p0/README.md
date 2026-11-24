# 셰이더·입자·점광원 그리기 무할당화 (P.0)

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10.0.101. 기준은 main `d523ec7`이다. 측정하는 동안 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음).

## 원인과 수정

- **입자 하나마다 32 B 할당.** `SdlCanvas.DrawShader`가 매 호출 `shader.Region(frame)`을 불렀고, `SpriteRegion`은 `sealed record` 클래스라 호출마다 힙 객체가 생겼다. 이제 `ShaderSheets`가 시트를 만들 때 프레임별 셀 배열을 한 번 만들고, 새 오버로드 `Get(shader, tint, additive, frame, out cell)`가 그 셀을 돌려준다. `SdlCanvas`(Release·Native AOT·Mac Scene Play)와 에디터 `PreviewCanvas`(Win·Linux Scene Play)가 이 오버로드를 쓴다. 범위를 벗어난 프레임은 이전처럼 `Region(frame)`을 계산한다.
- **사전 키 해시.** 키 `(ShaderClip, Color, bool)`의 기본 해시가 record의 모든 필드, 즉 SkSL `Source` 문자열 전체를 조회마다 해시했다. 사전에 크기 필드(Width·Height·Frames·Fps·Sheet·Source 길이)와 색·블렌드만 해시하는 비교자를 넣었다. 같은 값의 클립은 크기도 같으므로 동등성(record 값 비교)과 어떤 클립이 시트를 공유하는지는 그대로다. `Release`도 이전과 같이 값으로 비교한다.
- `SpriteRegion`·`ShaderClip`의 타입, YAML 직렬화, 스크립트 API는 바꾸지 않았다. 씬·에셋 파일과 배포 콘텐츠는 바이트 단위로 같다.
- BRUTAL: 해당 기능이 없다(사전 비교자와 셀 배열은 엔진 내부 자료). 그리기는 그대로 `Brutal.Sdl`의 `SDL_RenderGeometry`다.

## 비용과 할당

[벤치](../../../benchmarks/Particles/)는 `ParticleBurst`의 Step+Draw를 실행하고, 캔버스는 `SdlCanvas.DrawShader`가 입자마다 하는 일(시트 조회, 셀, `SdlSpriteBatch.Add`의 꼭짓점 계산)을 SDL 제출만 빼고 똑같이 한다. 방출기당 512개(상한)이고, 1,024개는 방출기 2개, 10,240개는 20개다. 예열 600틱 뒤 2,000프레임 평균이며, 기준과 P.0을 번갈아 5회 실행했다. [원본 출력](bench.txt)

| 입자 수 | 기준 main: ns/입자 | P.0: ns/입자 | 기준: µs/프레임 | P.0: µs/프레임 | 기준: B/프레임 | P.0: B/프레임 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,024 | 85.0 (81.9–85.3) | **17.3** (17.0–17.4) | 87.0 (83.8–87.4) | **17.7** (17.4–17.8) | 32,768 | **0** |
| 10,240 | 85.3 (82.7–87.4) | **17.5** (17.3–17.5) | 873.0 (846.8–895.4) | **178.7** (177.6–179.6) | 327,680 | **0** |

중앙값(범위). 두 중앙값의 차이(1만 개에서 694 µs)가 두 범위의 합(50.6 µs)보다 훨씬 크다. 1만 개 기준 약 4.9배 빠르고, 연구 단계 완료 기준(≤ 300 µs)을 만족한다. 같은 조회 경로를 쓰는 횃불 셰이더와 점광원도 같은 비용이 줄었다(개수가 적어 따로 재지 않았다).

회귀 검사에 실제 `SdlCanvas`(소프트웨어 렌더러, 64×64 메모리 표면)로 먼지·조명·횃불 셰이더(프레임 0–31 순환) 그리기 1,000회를 예열 뒤 실행해 관리 할당이 0 B인지 단언하는 항목을 추가했다. main에서는 같은 검사가 실패한다(그리기당 32 B). [검사 코드](../../Engine.Checks/EffectsChecks.cs)

## 화면이 바뀌지 않았는지

[PlatformerLab.Effects](../../PlatformerLab.Effects/README.md)로 Platform 씬에서 10틱에 Space를 눌러 점프하고, 착지 먼지·횃불 셀·점광원 깜빡임이 들어간 9개 프레임(8·30·50·54·58·62·70·80·110)을 SDL 소프트웨어 렌더러로 저장했다. 50·54프레임에 착지 먼지가 보인다. [50프레임](landing-0050.png)

| 실행 | 엔진 | 9개 캡처 |
| --- | --- | --- |
| Release (헤드리스 리플레이) | main `d523ec7` | 기준 |
| Native AOT (헤드리스 리플레이) | main `d523ec7` | 기준과 동일 |
| Scene Play (패키지 에디터, 프로브) | P.0 | 기준과 바이트 단위 동일 |
| Release (헤드리스 리플레이) | P.0 | 기준과 바이트 단위 동일 |
| Native AOT (헤드리스 리플레이) | P.0 | 기준과 바이트 단위 동일 |

[SHA-256 목록](captures.txt). 첫 예외 없음. Scene Play는 굽지 않은 SkSL 시트(`ShaderPixels.Preview`)를, 배포본은 빌드 때 구운 PNG를 쓴다.

## 회귀·배포 검사

| 검사 | 결과 |
| --- | --- |
| `./monoon test` (세 플랫폼 템플릿 갱신 후) | 통과, PASS 722줄. [로그](checks.txt) |
| 금지 파일(Release `.app`, Native AOT `.app`): Engine.Authoring, Engine.Editor, SkiaSharp, Avalonia, MoNoon.dll | 없음 |
| 용량: Release `Engine.Desktop.dll` | 69,632 → 70,656 B (+1,024) |
| 용량: Native AOT `MoNoonPlayer` | 12,211,392 → 12,227,920 B (+16,528, 16 KB 페이지 정렬. `__managedcode` +1,824 B, `__const` +1,424 B) |

## 재현

저장소 루트에서 실행한다. `<main>`은 `git archive d523ec7`을 푼 폴더이며 그 안에서 `engine/scripts/build-players.sh osx-arm64`를 먼저 실행한다.

```sh
# 벤치: 기준은 <main>에 같은 벤치 폴더를 복사해 -p:Cells 없이 빌드
dotnet build engine/benchmarks/Particles -c Release -m:1 -p:Cells=true
dotnet engine/.artifacts/bin/Particles/release/Engine.Checks.dll
# 화면 비교
qa=engine/tests/PlatformerLab.Effects
python3 $qa/verify.py prepare samples/PlatformerLab "$work/PlatformerLab"
./monoon build "$work/PlatformerLab" release --profile distribution --output "$work/builds"
./monoon build "$work/PlatformerLab" native-aot --profile distribution --output "$work/builds"
python3 $qa/verify.py play "$work/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 $qa/verify.py play "$work/builds/NativeAOT/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
dotnet build $qa -c Release -m:1 -p:UsedAvaloniaProducts=
python3 $qa/verify.py editor editor/MoNoon.app/Contents/MacOS/MoNoon "$work/edit/PlatformerLab" \
  engine/.artifacts/bin/EffectsProbe/release/EffectsProbe.dll "$work/scene-play"
python3 $qa/verify.py compare "$work/main-release" "$work/main-aot" "$work/scene-play" "$work/release" "$work/aot"
```

## 확인하지 않은 것

- Windows·Linux x64(조정 담당자 몫). 에디터 `PreviewCanvas`(Avalonia Scene Play, Win·Linux)는 Mac에서 컴파일만 했다. 같은 셀 값을 쓰므로 화면은 같아야 하지만 x64 게스트에서 Scene Play 캡처를 확인해 주기 바란다.
- main 에디터의 Scene Play는 따로 빌드하지 않았다. P.0 Scene Play 캡처가 main Release·AOT 캡처와 같다는 것으로 대신했다.
- SDL 제출·GPU 비용(벤치는 제출을 뺀다)과 실제 창에서의 프레임 시간.
