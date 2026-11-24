# 2D 조명: 어둠과 그림자 (16.7)

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10. 실행 대상: Scene 편집(Mac Metal 미리보기), Scene Play(패키지 에디터 + 프로브), Release, Native AOT(모두 Distribution). 기준 커밋은 `f48fc00`(wip/next1)이고, wip/next1 `ea068a4`를 병합한 뒤 Release·Native AOT 캡처와 `./monoon test`를 다시 실행했고, 마지막으로 `419690e`(Inspector 13.5·13.6·13.10·13.16)를 병합해 `./monoon test`를 한 번 더 실행했다. Metal 벤치를 도는 동안 `qemu-system`은 없었다. 소프트웨어 렌더러 벤치가 끝날 때 QEMU 프로세스 2개가 떠 있었다(측정 중간에 시작).

사용자 결정 14에 따라 셰이더 없이 만들었다. 라이트맵 렌더 타깃을 주변광 색으로 지우고 광원을 더한 뒤, SDL `MOD`(곱하기) 블렌드로 월드 위에 합성한다. 그림자는 충돌체·타일 변에서 만든 다각형을 `RenderGeometry`로 광원별 보조 타깃에 검게 칠한다. 렌더 타깃, 블렌드 모드, `RenderGeometry`는 모두 기존 `Ahwoo.BRUTAL.Sdl`(`Brutal.SdlApi`)로 호출했다. 새 패키지는 내려받지 않았고 노멀맵과 GPU 셰이더는 나중 후보로 남겼다.

[샘플 씬](../../../../samples/PlatformerLab/Scenes/Cave.scene) · [사용 설명](../../../../docs/manual/effects.md#global-light-2d-어둠과-그림자) · [화면 회귀 하네스](../../PlatformerLab.Lighting/README.md) · [벤치](../../../benchmarks/Lighting/Program.cs)

| 실행 경로 | 캡처 |
| --- | --- |
| Release 헤드리스(소프트웨어 렌더러), 45·70프레임 | [45](cave-0045.png) · [70](cave-0070.png) |
| Release 창(Metal, HiDPI) | [release-window.png](release-window.png) |
| Native AOT 창(Metal, HiDPI) | [aot-window.png](aot-window.png) |
| 에디터 Scene 편집 미리보기(Metal) | [editor-scene.png](editor-scene.png) |

## 배포본 영향

| 항목 | 값 |
| --- | --- |
| 프레임 비용, Metal, Cave 800×450, 그림자 광원 8개 | 0.831 ms(0.814–0.846), 조명 없이 같은 광원을 가산할 때 0.562 ms(0.493–0.585). **+0.27 ms**, 목표 +1 ms 이하 |
| 프레임당 관리 할당 | 조명 켬·끔 모두 352 B/프레임으로 같다(기존 타일맵 그리기 반복자). 조명이 더하는 할당은 0 B |
| 드로우 제출 | 조명 없이 7회 → 그림자 광원 8개 31회. 라이트맵 지우기·합성 2회, 그림자 없는 광원은 전부 한 번에 1회, 그림자 광원은 광원당 3회 |
| VRAM | 라이트맵 1장(캔버스 단위 크기): 800×450은 1.44 MB, 1920×1080은 8.29 MB(추정과 일치). 그림자 광원이 있으면 가장 큰 광원의 화면 사각형만큼 보조 타깃 1장(Cave: 340×340, 0.46 MB) |
| 용량: `Engine.Core.dll` | 1,546,240 → 1,564,160 B(+17,920, `f48fc00` 템플릿 대비) |
| 용량: `Engine.Desktop.dll` | 73,728 → 76,288 B(+2,560) |
| 용량: Native AOT 실행 파일(PlatformerLab, Cave 제외) | 8,371,424 → 8,404,528 B(**+33,104**, 16 KB 페이지 2장. `__managedcode` +19,392, `__const` +4,416) |
| 용량: Native AOT 앱 / Release 앱(PlatformerLab, Cave 제외) | 13,060,557 → 13,093,680 B(+33,123) / 30,303,066 → 30,358,381 B(+55,315, 구조체 정리 전 측정) |
| 용량: Cave 씬 | 16,688 B 파일, 앱 +28,289 B |
| 시작 시간 | 바뀐 경로 없음. 조명은 첫 그리기에서 렌더 타깃을 만든다 |

AOT·앱 비교는 같은 작업 트리에서 엔진 소스만 병합한 wip/next1 `ea068a4`로 되돌려 플레이어 템플릿(AOT 키트 포함)을 다시 만들고 PlatformerLab(Cave 제외)·KeyRoom을 빌드해 쟀다. ILC 맵에서 조명 관련 메서드는 약 11 KB다(`SceneGame.Shadows` 1,984 B, `SdlCanvas.DrawLight` 1,848 B, Global Light 2D YAML 읽기 1,872 B 등). record·`List<T>`·LINQ를 평범한 구조체와 배열로 바꿔 `__managedcode` 증가를 26,028 B에서 19,392 B로 줄였지만 페이지 수는 그대로다.

**용량 관문 통과.** `ea068a4`까지 병합했을 때는 기준표가 옛 값이라 KeyRoom·PlatformerLab·InventoryLab Native AOT가 +1%를 넘었다(wip/next1 끝 커밋만으로도 KeyRoom이 넘었다). `419690e` 병합으로 기준표가 갱신된 뒤에는 6개 모두 +1% 안이다: PlatformerLab Native AOT 실행 파일 8,404,528 B(기준 8,371,424, +0.40%), 앱 13,121,969 B(기준 13,060,557, Cave 포함 +0.47%), KeyRoom Native AOT 실행 파일 8,736,528 B(기준 8,719,936, 페이지 1장). 기준표는 이 브랜치에서 바꾸지 않았다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `Engine.Core/Effects2D.cs` | `GlobalLight2DComponent`(Ambient 색, Unlit layer), Point Light 2D `Shadows`, 감쇠 계산을 `Shade`로 분리(기존 출력 그대로) |
| `Engine.Core/Lighting2D.cs` | 그림자 다각형 생성(`LightShadows`: 광원을 등진 변 수집, 같은 직선에 붙은 가로·세로 변 합치기, 반경으로 자르기, 90° 넘는 변 분할, 반경 2배까지 늘이기)과 광원별 캐시(`CasterKey`: 콜라이더 자세·켜짐, 타일 셀 버전·스트리밍 카운터) |
| `Engine.Core/IGame2D.cs` | `ICanvas2D.BeginLighting/DrawLight/EndLighting`. 기본 구현(Avalonia 미리보기)은 Ambient 밝기만큼 어둡게 하고 빛을 더하며 그림자는 없다 |
| `Engine.Core/SceneGame.cs` | `DrawCamera`가 Unlit layer에 처음 닿는 지점(없으면 월드 끝)에서 조명을 합성. Global Light 2D가 없으면 기존 가산 경로 |
| `Engine.Core/SceneDocument.cs`, `SceneComponent.cs` | `globalLight2D` 유니온, 검증, 스키마 42(체인 맨 위, 프레이밍 카메라는 35 그대로) |
| `Engine.Desktop/SdlCanvas.cs`, `SdlSpriteBatch.cs` | 라이트맵·보조 렌더 타깃, 밑 타깃(창 또는 색 보정 레이어)의 논리 표시·뷰포트·클립 저장과 복원, 단색 사각형 묶음 |
| `Engine.Editor/Scene/MacSceneHost.cs`, `SceneViewport.Render.cs` | Mac Scene 패널 좌표 변환 전달, Scene 편집 화면의 조명·그림자 미리보기(같은 캔버스 API와 `LightShadows`) |
| `Engine.Editor/Inspector/MainWindow.Presentation.cs`, `EditorTokens.cs` | Cast shadows 체크박스, Global Light 2D의 Ambient·Unlit layer 행, 아이콘 |
| `samples/PlatformerLab` | Cave 씬(타일 441칸, 바위 12개, 움직이는 상자, 그림자 광원 8개 중 1개는 움직임), `includedScenes`에 추가 |
| `engine/tests/Engine.Checks/LightingChecks.cs`, `Program.cs` | `--only lighting` 묶음, 전체 실행에도 포함 |
| `engine/tests/PlatformerLab.Lighting/` | 헤드리스·Scene Play 캡처 비교, Metal 창 캡처(`window.py`) |
| `engine/benchmarks/Lighting/` | 프레임 비용 벤치(software·metal, 광원 수 N) |

## 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| Cave 5개 캡처(1·20·45·70·100프레임) 바이트 동일 | 통과 | 통과 | 통과 |
| 조명 없는 PlatformerLab.Effects 9개 캡처가 커밋된 기준([effects-captures.txt](../particles/effects-captures.txt))과 동일 | 통과 | 통과 | 통과 |
| Metal 창에서 라이트맵·그림자 표시(HiDPI 논리 표시) | 편집 미리보기 통과 | 통과 | 통과 |
| 금지 파일(SkiaSharp, Avalonia, Engine.Editor, Engine.Authoring, MoNoon.dll, libBrutalScenePanel) | — | 없음 | 없음 |

[캡처 SHA-256](captures.txt) · [Effects SHA-256](effects-captures.txt). 첫 예외 없음. 병합 뒤 Release·Native AOT 캡처는 병합 전 Scene Play 캡처와 같다.

`LightingChecks`([로그](checks.txt)). 최종 병합 후 `./monoon test` 전체 통과(PASS 877줄, 용량 관문 포함):

- 상자 뒤에만 그림자가 생기고 반경 2배에서 끝난다. 광원을 품은 충돌체는 가리지 않는다. 광원에서 1 px 떨어진 긴 벽도 뒤쪽 반평면 전체를 가린다. 타일 10칸의 아랫변 10개가 하나로 합쳐진다(변 22개 → 사각형 13개 이하).
- 소프트웨어 렌더러 64×32에서 그림자 픽셀이 정확히 Ambient(64), 빛 쪽 255, 라이트맵 영역 밖은 원래 색(255).
- Cave: 스키마 42와 YAML 왕복, Platform 씬은 31 유지. 조명 켬·끔의 프레임당 할당이 같다(352 B). 움직이는 등불·상자가 있을 때 60프레임에 230회, 둘을 멈추면 0회 다시 만든다. 타일 하나를 바꾸면 화면 안 그림자 광원 7개가 한 번씩 다시 만든다.

### 프레임 비용 (5회 교대 실행)

`engine/benchmarks/Lighting`, Cave 800×450, 예열 360프레임 뒤 600프레임, 한 틱 진행 후 `Draw` + 제출만 잰다. Metal은 숨긴 창의 Metal 렌더러로 800×450 타깃에 그리고 프레임마다 1×1 픽셀을 읽어 GPU 완료를 기다린다(창 드로어블에서 읽으면 컴포지터 대기 약 8 ms가 섞인다). 중앙값의 중앙값(범위). [Metal 원본](bench-metal.txt) · [소프트웨어 원본](bench-sw.txt)

| 모드 | Metal ms | 소프트웨어 ms | 제출/프레임 | 그림자 재생성/프레임 |
| --- | ---: | ---: | ---: | ---: |
| off: Global Light 끔, 광원 8개 가산(16.7 이전 경로) | 0.562 (0.493–0.585) | 0.460 (0.212–0.559) | 7 | 0 |
| 조명, 광원 0 | 0.542 (0.456–0.638) | 1.695 (1.678–1.706) | 6 | 0 |
| 조명, 그림자 광원 4 | 0.651 (0.643–0.693) | 4.304 (4.265–4.315) | 19 | 1.37 |
| 조명, 그림자 광원 8 | 0.831 (0.814–0.846) | 8.682 (8.671–8.713) | 31 | 3.89 |
| 조명, 광원 8, 그림자 없음 | 0.535 (0.502–0.567) | 3.219 (3.205–3.232) | 8 | 0 |

- Metal: 그림자 광원 8개와 off의 중앙값 차이 0.27 ms가 두 범위의 합(0.12 ms)보다 커서 증가로 판정한다. 광원 0개와 그림자 없는 8개는 off와 차이 없음.
- 그림자 광원 8개는 캐스터 657개(광원별 합), 광원을 등진 변 1,346개를 사각형 349개로 그린다. 로드맵의 "그림자 다각형 200개"보다 많다.
- 소프트웨어 렌더러(헤드리스 리플레이·테스트 캡처 경로)는 모든 픽셀을 CPU가 칠해 그림자 광원당 약 1 ms가 든다. 같은 직선의 타일 변을 합쳐 13.6 ms에서 8.7 ms로 줄였다. 배포 게임은 GPU 렌더러를 쓰므로 프레임 목표는 Metal 값으로 본다.

## 재현

저장소 루트에서 실행한다. `$W`는 빈 작업 폴더다.

```sh
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only lighting
qa=engine/tests/PlatformerLab.Lighting
python3 $qa/verify.py prepare samples/PlatformerLab "$W/PlatformerLab"
python3 $qa/verify.py prepare samples/PlatformerLab "$W/edit/PlatformerLab"
./monoon build "$W/PlatformerLab" release --profile distribution --output "$W/builds"
./monoon build "$W/PlatformerLab" native-aot --profile distribution --output "$W/builds"
python3 $qa/verify.py play "$W/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$W/release"
python3 $qa/verify.py play "$W/builds/NativeAOT/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$W/aot"
dotnet build $qa -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor editor/MoNoon.app/Contents/MacOS/MoNoon "$W/edit/PlatformerLab" \
  engine/.artifacts/bin/LightingProbe/release/LightingProbe.dll "$W/scene-play"
python3 $qa/verify.py compare "$W/scene-play" "$W/release" "$W/aot"
# Metal 창 캡처
MONOON_QA_WINDOW=engine/.artifacts/clip-qa/window sh engine/scripts/gui-lock.sh \
  python3 $qa/window.py game "$W/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$W/release-window.png"
# 벤치: 모드 off·0·4·8·8n을 5회 번갈아(metal은 GUI 잠금 안에서)
dotnet build engine/benchmarks/Lighting -c Release -m:1
dotnet engine/.artifacts/bin/Lighting/release/Engine.Checks.dll samples/PlatformerLab/Scenes/Cave.scene metal 8 600
```

조명 없는 화면이 바뀌지 않았는지는 [PlatformerLab.Effects](../../PlatformerLab.Effects/README.md)의 같은 명령(`play`·`editor`·`compare`)으로 확인한다.

## 확인하지 않은 것

- Windows·Linux x64(coordinator 몫). Win·Linux 에디터의 Avalonia 미리보기(어둡게 하고 빛을 더하는 근사, 그림자 없음)는 Mac에서 컴파일만 했다. D3D·Vulkan·OpenGL 렌더러의 `MOD`·렌더 타깃 동작도 실행하지 않았다.
- 움직이는 충돌체의 그림자는 틱 끝 위치를 쓰므로 실시간 보간 화면에서 최대 한 틱 앞선다. 회전한 콜라이더는 그림자를 만들 때 월드 모양을 새로 만들어 할당한다(물리 질의와 같다). 512×512칸보다 넓은 광원은 타일 그림자를 건너뛴다.
- 전체 화면·창 크기 변경 중 라이트맵 재생성은 창 모드에서만 봤다.
