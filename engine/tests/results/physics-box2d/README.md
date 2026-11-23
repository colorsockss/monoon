# 물리 엔진 Box2D 전환과 조인트·마찰 (로드맵 11.3)

2026-11-23, macOS 27.0 / Apple Silicon arm64. 직접 작성한 강체 해석기와 충돌 판정을 BRUTAL의 `Ahwoo.BRUTAL.Box2D` 2026.10.2(네이티브 box2d 3.1.1)로 옮기고, 회전·스프링·직선 이동(슬라이더)·용접 조인트와 마찰 계수를 추가했다. PlatformerLab에 흔들다리와 도르래 발판이 있는 Joints 씬을 만들고, 같은 입력에서 Scene Play·Release·Native AOT의 위치 기록이 바이트 단위로 같은지 확인했다.

## 전환 결정

| 항목 | 결정과 이유 |
| --- | --- |
| 참조 위치 | [Engine.Core](../../../src/Engine.Core/Engine.Core.csproj)가 패키지를 참조한다. 에디터 Scene Play, 헤드리스 검사, Release 플레이어, Native AOT 키트가 모두 Engine.Core를 쓰므로 한 곳에서 네 경로를 덮는다. 패키지가 `Ahwoo.BRUTAL.Core` 2026.10.4와 `Ahwoo.BRUTAL.Numerics` 2026.10.2를 함께 가져온다(같은 버전 계열, 승인 범위). Engine.Desktop의 Sdl 패키지는 Core 2026.10.1로 빌드됐지만 상위 버전에서 문제없이 실행됐다. |
| 길이 단위 | Box2D 길이 단위 1 = 1px(기본값). 선형 여유(slop)가 0.005px로 작아 질의 결과가 기존 정밀도에 가깝다. 대신 픽셀 규모에 맞게 월드를 조정했다: 반발 임계 25px/s(기존 해석기와 같음), 최대 밀어내기 속도 300px/s, 최대 속도 100,000px/s, 수면 끔, 작업 스레드 0(단일 스레드로 결정적). 월드 중력은 (0, 1)이고 각 강체의 Gravity(px/s²)를 중력 배율로 준다. 고정 틱마다 `Step(dt, 4)`. |
| 시뮬레이션 | Dynamic 강체, 조인트, 마찰·반발은 Box2D가 푼다. 강체 루트 하나가 Box2D 바디 하나이고, 루트가 움직이는 자식 콜라이더가 그 바디의 도형이다. 강체가 없는 고체 콜라이더와 타일맵은 정적 바디가 된다(씬에 Rigidbody 2D가 하나라도 있을 때만 월드를 만든다). 단방향 타일 면은 4점 Box2D 체인(오른쪽 면만 충돌)으로 만든다. 트리거는 월드에 넣지 않는다. |
| 질의·이동·접촉 | `CollisionGeometry`의 손으로 쓴 SAT·원형 캐스트를 Box2D 함수로 바꿨다: GJK 거리(`b2ShapeDistance`), 매니폴드(`b2Collide*`), 형상 캐스트(`b2ShapeCast`), 레이캐스트(`b2RayCastPolygon/Circle`). 후보는 기존 공간 격자에서 고른다. Box2D 월드 질의(`b2World_OverlapShape` 등)는 네이티브 콜백이 필요하고 트리거·타일 셀 단위 결과·후보 수 진단을 그대로 줄 수 없어 쓰지 않았다. 접촉 이벤트도 Box2D 접촉 이벤트 대신 기존 쌍 관리(Enter/Stay/Exit, 한 틱 안 통과)를 유지했다. Box2D 이벤트는 동적 바디가 끼지 않은 쌍(스크립트로 옮기는 두 콜라이더, 키네마틱끼리)을 보고하지 않기 때문이다. |
| 캐릭터 | Character Motor는 Box2D가 플랫포머 캐릭터에 권하는 키네마틱 컨트롤러로 유지했다. 이동 판정은 Box2D 형상 캐스트다. Box2D 월드에는 키네마틱 바디(발바닥 2px 위까지)로 들어가 동적 물체를 밀고, 서 있는 동적 물체에 `Mass × Gravity`를 가하며, 그 물체의 움직임을 따라간다. 발판에서 걷다가 1px 이하의 턱은 앞이 비어 있으면 넘는다(조인트의 부드러움으로 판자가 언덕보다 0.0x px 높아지는 경우). |
| 조인트 | 공통 `Joint2DComponent`(Connected) 아래 Distance(스프링 추가)·Revolute·Prismatic(에디터 이름 Slider Joint 2D)·Weld. Box2D 3.1에는 도르래 조인트가 없어 도르래 발판은 Slider 두 개와 [PulleyPair](../../../../samples/PlatformerLab/Scripts/PulleyPair.cs) 스크립트로 만들었다([사용법](../../../../docs/manual/platformer-physics.md#흔들다리와-도르래-발판)). |
| 씬 형식 | 새 조인트, 스프링을 켠 거리 조인트, 기본값(0.6)이 아닌 Friction이 있으면 스키마 29로 저장한다. 강체·조인트·캐릭터가 있는 씬을 내보낼 때 템플릿에 `MoNoonBox2D.version` 표식이 없으면 빌드를 거부한다(옛 해석기로 조용히 실행되지 않게). |
| 다각형 한도 | Box2D 다각형은 최대 8꼭짓점이다. 콜라이더·타일 충돌 다각형과 `Shape2D.Polygon`의 한도를 3–32에서 3–8로 줄였다. 저장소 샘플의 다각형은 모두 3–6꼭짓점이다. |

## 기준값 변화

| 검사 | 기존 | Box2D | 이유와 조치 |
| --- | --- | --- | --- |
| 동적 물체 충돌의 운동량 분배(질량 2가 60px/s로 질량 1에 충돌) | 두 물체 모두 40px/s(±0.1) | 39.834, 40.333px/s | Box2D 연성 접촉이 겹침을 풀며 남긴 0.5px/s 분리 속도. 운동량 120은 정확히 보존된다. 검사를 "운동량 보존(±0.01)과 각 속도 ±0.5"로 바꿨다. |
| PlatformerLab 진자(거리 조인트 64px) | 최대 흔들림 28.2996px, 길이 오차 0.0000076px | 29.7698px, 0.01458px | Box2D 연성 조인트(서브스텝 4). 샘플 합격 기준(오차 < 0.1px)은 그대로 통과한다. |
| PlatformerLab 경로(R) | 오른 높이 90px, 상대 변위 0px | 오른 높이 90.00px, 발판 이동 124.1269px, 상대 변위 0px | 변화 없음(이전 기록의 발판 이동 118.8px은 샘플 경로가 바뀌기 전 값). |
| [PlatformerLab 타일 충돌 380틱](osx-arm64/tile-collision-release.tsv) | SHA-256 `58ba77ef…` | SHA-256 `f30ed686…`(헤더 없는 원본) | 380행 중 25행의 Y가 기록 정밀도의 마지막 자리(0.0001px)만 다르다. 접지 오브젝트·법선·X·속도·판정(착지 35틱, 이음새 4개씩, 경사 오차 0.0001px, 틱당 2.5px)은 같다. Scene Play·Release·AOT 세 기록은 서로 동일하다. 기존 기준 파일은 그대로 두고 새 기록을 여기에 남겼다. |
| CollisionLab 공간 질의(1천·1만 개) | 대상 ID 해시 `32FA7AB7…`, `B5C817DB…` | 같음 | 288개 질의의 적중 순서와 후보 수(1만 개: 전체 16,772,778 → 격자 22,812)가 같다. 1만 개 전체 p95: Release 78.125 → 2.042µs에서 73.542 → 2.833µs, AOT 85.250 → 3.125µs. 격자 질의가 0.8µs 느려진 것은 P/Invoke 비용이다. [Release 로그](osx-arm64/spatial-release.log) · [AOT 로그](osx-arm64/spatial-native-aot.log) |
| CollisionLab 프레임 프로파일러 | R 프레임 131.407ms, 질의 97.0% | Release 138.556ms 96.9%, AOT 155.529ms 97.5%, Scene Play 195.4ms 89.0% | 종류별 1,344회, 합계 4,032회 질의는 같다. 합격 기준(50% 이상) 통과. |
| BarrageLab 프리팹 풀 | 명중 2,338, Exit 2,338, 활성 503, 생성 587/7,308, 재사용 6,721 | 같음 | Release·AOT 모두 동일. |

`Engine.Checks`의 접촉 검사(Enter/Stay/Exit, 한 틱 통과, 마스크, 콜백 중 제거), 질의 검사(접선·끝점·원점 내부·접촉은 겹침 아님·변환된 원·레이어 31·타일 4종 배치), 강체 검사(빠른 물체 34px 정지, 천장 아래 수직 발판, 12° 한도 경사 차단 등)는 기존 기댓값 그대로 통과했다. [물리 검사 출력](osx-arm64/physics-checks.txt)

## 조인트 검증

[PlatformerLab.Joints](../../PlatformerLab.Joints/README.md)의 300틱 입력 기록을 세 경로에서 재생했다. 관찰 스크립트가 매 틱 캐릭터, 판자 8개, 도르래 발판 2개의 위치·각도를 기록한다.

| 경로 | 결과 | 기록 |
| --- | --- | --- |
| 에디터 Scene Play | 언덕 도착(최종 X 738.9792), 가장 낮은 판자 Y 319.3756, 발판 A +16.0147px · B −16.0px | [300행](osx-arm64/joints-scene-play.tsv) |
| Release(배포용) | 같음 | [300행](osx-arm64/joints-release.tsv) |
| Native AOT(배포용) | 같음 | [300행](osx-arm64/joints-native-aot.tsv) |

세 TSV는 80,853바이트로 동일하다(SHA-256 `2d523cd089166006…`). 2026-11-23 `Affine2D`를 곱셈·덧셈 분리 계산으로 바꾼 뒤 세 경로를 다시 기록했다. 이전 기록은 `a00b9e4b00b4599dfc23…`(최종 X 738.9794)이고 63틱부터 0.0001px 단위로 다르다. [x64 결정론 기록](../x64-determinism/README.md#곱셈덧셈-분리-계산으로-변경) 게임 로그: `Joints crossing: bridge True, pulley True, 280 ticks.`

| 기능 | 헤드리스 검사(`Engine.Checks --physics-only`) | 결과 |
| --- | --- | --- |
| 회전 조인트 사슬(흔들다리) | 캐릭터가 걸어서 건넘, 무게로 처짐, 이웃 판자 회전축 간격 | 가장 낮은 판자 319.4, 간격 최대 0.311px |
| 회전 조인트 모터 | 180°/s 모터, 중력 0 | 180.0°/s, 중심 (8.00, 8.00) 유지 |
| 회전 조인트 각도 제한 | −10°…30°, 아래로 충격량 | 0.0°…30.1° |
| 거리 조인트 스프링 | 2Hz, 감쇠 0.2, 길이 40 | 0.8…66.7px 진동 |
| 거리 조인트(단단함) | 고정 앵커에 매단 상자 | 길이 오차 0.007px |
| 슬라이더 조인트 | X축, 0…50px 제한, 모터 100px/s·2,000 | X 450.01, Y 0.00 |
| 도르래 발판(슬라이더 2 + 스크립트) | 캐릭터 무게로 한쪽 하강, 반대쪽 상승, ±16px | +16.01, −16.00 |
| 용접 스프링 | 3Hz, 감쇠 0.1 | 최대 4.3px 처짐(정적 2.8px의 약 2배) |
| 마찰 | 20° 경사, Friction 0과 1 | 0은 1,510px 미끄러짐, 1은 0.00px |
| 반발 | Restitution 0.8 낙하 | 다시 Y 155.6까지 튀어오름 |

에디터에서 Plank 3(Revolute)과 Pulley deck A(Slider)를 선택해 1280·1600·1920px 창에서 Rigidbody 2D·조인트 행의 값 열과 레이블 열이 각각 한 위치에 있는지 확인했다(예: 1280px에서 레이블 x=1070, 값 x=1130). 이웃 판자 이름을 긴 이름으로 바꿔도 Connect 목록이 섹션 밖으로 나가지 않았다. [측정](osx-arm64/inspector.txt) · [Revolute 1280px](osx-arm64/inspector-revolute-1280.png) · [Slider 1920px](osx-arm64/inspector-slider-1920.png)

## 네이티브 라이브러리와 금지 파일

| 출력 | Box2D 파일 | SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring |
| --- | --- | --- |
| 플레이어 템플릿 osx-arm64 | `Brutal.Box2D.dll`, `libbox2d.dylib`(+ `.3`, `.3.1.1` 사본) | 없음 |
| 플레이어 템플릿 win-x64 | `Brutal.Box2D.dll`, `box2d.dll` | 없음 |
| 플레이어 템플릿 linux-x64 | `Brutal.Box2D.dll`, `libbox2d.so`(+ `.3`, `.3.1.1` 사본) | 없음 |
| PlatformerLab Release `.app`(osx-arm64) | 위와 같음, `MoNoonPlayer.files`에 포함 | 없음 |
| PlatformerLab Native AOT `.app`(osx-arm64) | `libbox2d*.dylib` 3개(관리 DLL은 실행 파일에 포함) | 없음 |
| PlatformerLab Release win-x64 / linux-x64(이 Mac에서 내보냄) | `box2d.dll` / `libbox2d.so*` | 없음 |
| 에디터 `MoNoon.app` | `Brutal.Box2D.dll`, `libbox2d*.dylib` | (에디터이므로 해당 없음) |

라이선스 고지는 `Licenses/BRUTAL/BRUTAL-BOX2D-THIRD-PARTY-NOTICES.txt`로 복사된다. 패키지가 macOS·Linux 라이브러리를 이름별 사본 3개(각 0.27MB·0.38MB)로 담고 있어 그대로 배포한다.

## 그 밖의 회귀(macOS arm64)

| 검사 | Release | Native AOT | Scene Play |
| --- | --- | --- | --- |
| `./monoon test` 전체(636개) | 통과(KeyRoom 세 플랫폼 내보내기 포함) | – | – |
| CollisionLab.Spatial | 통과 | 통과 | – |
| CollisionLab.FrameProfiler | 통과 | 통과 | 통과 |
| CollisionLab.SplitLoading | 통과 | 통과 | 통과 |
| PlatformerLab.TileCollision | 통과 | 통과 | 통과(타일셋 편집 포함) |
| BarrageLab.PrefabPools | 통과 | 통과 | – |
| KeyRoom.WorldCheckpoints(`ci.py osx-arm64`) | 통과 | 통과 | – |
| `./monoon test inventorylab`·`replay` | 통과 | 통과 | – |

실패한 실행이 없어 기록할 첫 예외는 없다.

## 조정 담당자의 x64 비교에 남긴 것

- Windows·Linux x64에서 Joints·타일 충돌 기록이 Mac과 같은지는 확인하지 않았다. Box2D 3.1은 같은 바이너리·같은 입력에서 결정적이지만, 아키텍처가 다른 네이티브 빌드(arm64 dylib과 x64 dll/so)끼리 부동소수 결과가 같다는 보장은 확인되지 않았다. 게스트에서 `PlatformerLab.Joints`의 세 경로를 실행해 Mac 기록과 비교해 주기 바란다. 다르면 같은 OS 안의 세 경로 일치만 판정 기준으로 삼는다.
- Native AOT 키트는 Engine.Core 프로젝트를 그대로 복사하므로 게스트에서 `Ahwoo.BRUTAL.Box2D` 2026.10.2(및 Core 2026.10.4, Numerics 2026.10.2) 복원이 필요하다.
- 기존 tile-collision·spatial-queries의 Windows·Linux 기준값은 옛 해석기 기준이다. 공간 질의 해시는 Mac에서 바뀌지 않았지만 게스트에서 다시 확인이 필요하다.

Linux x64 게스트(2026-11-23): Joints는 판정 기준을 통과했지만 Linux AOT만 JIT(Release·Scene Play)와 60틱부터, Linux JIT는 Mac과 84틱부터 최대 0.0005px 다르다(`DOTNET_EnableAVX2=0`으로 재현). 타일 충돌 세 경로는 Mac 새 기록과 같고 공간 질의 해시도 같다. [Linux 결과](../chapter11-linux-x64/README.md)
