# YAML 기본값과 결정성 API 검사

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10.0.101. 기준은 `24741bf`(wip/next1, 파티클 P.1–P.3 포함)다. 측정·실행하는 동안 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음). Windows·Linux는 실행하지 않았다.

## 1. VYaml이 빠진 키에 넣는 값

VYaml 1.6.1의 생성 포매터는 멤버마다 지역 변수를 `default(T)`로 만들고, 파일에 키가 없으면 그 값을 객체 초기화식(`new T { X = __X__ }`)으로 넣는다. 그래서 속성 초기값(`= 60`, `= true`)이 덮어써진다. `[YamlConstructor]` 생성자의 매개변수 기본값이 있으면 지역 변수를 그 값으로 시작한다. 단, 지역 변수의 형식은 멤버 형식을 따른다. 멤버가 값 형식(구조체)이면 매개변수를 `T? x = null`로 받아도 `default(T)`가 그대로 넘어온다.

### 감사 결과

[YamlDefaultChecks](../../Engine.Checks/YamlDefaultChecks.cs)로 Engine.Core·Engine.Authoring·Engine.Checks(에디터 모델 링크 파일 포함)의 `[YamlObject]` 형식 115개를 모두 확인했다(Engine.Desktop에는 YAML 형식이 없다). 수정 전 결과에서 실제로 선언 기본값과 다른 멤버는 9개 형식에 있었다.

| 형식(파일) | 멤버: 선언 기본값 → 수정 전 읽은 값 | 수정 전 키를 뺀 파일의 동작 |
| --- | --- | --- |
| `Line2DComponent`(씬·프리팹) | points 2점 → `[]`, width 4 → 0, startColor·endColor `#FFFFFF` → null | width·색이 빠지면 검증 오류. **points만 빠지면 조용히 빈 선** |
| `Trail2DComponent` | time 0.4 → 0, width 6 → 0, 색 → null, maxPoints 32 → 0, minStep 2 → 0 | 대부분 검증 오류. **minStep만 빠지면 조용히 0** |
| `PathFollower2DComponent` | points 2점 → `[]`, speed 60 → 0, repeat PingPong → Once, playOnStart true → false | points·speed는 검증 오류. **repeat·playOnStart는 조용히 Once·false** |
| `PolygonCollider2DComponent` | points 삼각형 → `[]` | 조용히 빈 다각형 |
| `UiDropdownComponent` | options `Option 1, Option 2` → `[]` | 검증 오류 |
| `Tilemap2DComponent` | cellSize 32×32 → null | 씬 검증 오류 |
| `LocalizationSettings`(GameSettings) | fallbackLocale `en` → null | 검증 오류 |
| `Tileset`(.tileset) | nextId 1 → 0 | 조용히 0(에디터가 `max(nextId, 최대 ID + 1)`을 써서 결과는 같다) |
| `ParticleEffect`(.particles) | lifetime (1,1)·speed (40,40)·size (8,8) → (0,0), color `#FFFFFF` → 빈 값 | lifetime·color는 검증 오류. **speed·size는 조용히 0**. P.1–P.3의 `[YamlConstructor]` 수정은 이 네 멤버에 효과가 없었다(멤버가 `record struct`라서) |

검사 쪽 사정으로 처음에 걸린 것(버그 아님): `ParticleBurst`(record struct는 `new T()`가 0을 준다. 생성자 기본값과 비교하도록 고침), `ScriptComponent`(C# `required` 키), `Tilemap2DComponent.chunks`(setter가 빠진 목록을 거부).

### 수정 방식(모든 형식에 같은 한 가지)

- 0·false·null이 아닌 기본값이 있는 멤버는 `[YamlConstructor]` 생성자의 매개변수 기본값으로도 적는다. 기존 컴포넌트(`BoxCollider2DComponent`, `UiSliderComponent`, `PositionTween2DComponent` 등)가 이미 쓰던 방식이다. `new T()`를 위한 매개변수 없는 생성자와 초기값은 그대로 둔다.
- 상수로 쓸 수 없는 기본값(배열, 중첩 레코드)은 `T? x = null`로 받아 `x ?? 초기값`(목록 속성은 `if (x != null) X = x`)으로 채운다.
- `ParticleRange`·`ParticleColors`는 `readonly record struct`에서 `sealed record`로 바꿨다. 빠진 키가 null로 생성자에 와야 하기 때문이다. `Rotation`의 기본값은 `new(0, 0)`으로 명시했다. 에셋을 읽을 때 방출기마다 객체 4개가 더 생기고 프레임당 할당은 없다.
- `Tileset`의 `NextId`는 위치 매개변수 `int NextId = 1`로 옮겼다(기존 `{ NextId = … }` 호출은 그대로 컴파일된다).
- 생성 코드는 소스 생성기가 만든 정적 코드라 리플렉션이 없고 Native AOT에서 같다.

### 기존 파일에 미치는 영향

키를 빼서 조용히 다르게 읽히던 경우(위 표의 굵은 글씨, Polygon, Tileset)가 이 저장소의 파일에 있는지 두 가지로 확인했다. 해당 파일이 없어서 마이그레이션이나 스키마 규칙은 넣지 않았다.

1. 엔진이 쓰는 파일에는 이 키가 항상 있다. `ProjectYaml`은 null만 생략하고 값 형식·문자열·배열은 항상 쓴다. Line·Trail·Path는 모두 `d523ec7`에서 키와 함께 처음 들어왔고, 그 뒤 추가된 키가 없다.
2. 커밋된 `.scene` 38·`.prefab` 12·`.tileset` 5·`.particles` 14·`GameSettings` 2 = 85개 파일을 수정 전과 수정 후 엔진으로 읽어 `ProjectYaml.Encode`로 다시 쓴 결과가 85개 모두 바이트 단위로 같다(임시 프로브, 커밋하지 않음). 손으로 키를 확인한 결과도 같다: 컴포넌트 블록과 흐름 표기 CollisionLab 다각형 88개 모두 points가 있고, `.particles` 14개 모두 lifetime·speed·size·color가 있다(rotation이 없는 파일은 이전·이후 모두 (0,0)).

저장소 밖에서 손으로 쓴 YAML이 위 키를 빼고 있었다면 이번 변경 뒤에는 코드의 기본값(예: Path Follower가 시작 시 PingPong 재생)으로 읽는다. 검증 오류가 나던 파일은 이제 열린다.

## 2. 결정성 API 정적 검사(결정 15, W10a)

[DeterminismScanChecks](../../Engine.Checks/DeterminismScanChecks.cs)가 `./monoon test` 앞부분에서 `engine/src/Engine.Core`, `engine/src/Engine.Desktop`, `samples/*/Scripts`의 C# 212개 파일을 줄 단위로 읽는다(`//` 뒤 주석 제외). 아래를 찾으면 파일·줄과 함께 실패한다.

- `Vector2/3/4`·`Matrix3x2`·`Matrix4x4`·`Quaternion`·`Plane`의 `Transform`·`TransformNormal`·`Lerp`·`Slerp`·`FusedMultiplyAdd`, `float/double.Lerp`, `MultiplyAddEstimate`
- `MathF.SinCos`, `Math.SinCos`
- Brutal.Numerics(docs/brutal.md 유지 결정): `float2/3/4`(·`double*`, 행렬 형식)의 `Lerp`·`Transform`·`FusedMultiplyAdd`·`Invert`, `float4x4`, `floatQuat`, `.Rows.Transform`

허용 목록은 검사 파일 안에 이유와 함께 둔다. 지금은 `engine/src/Engine.Desktop/SdlSpriteBatch.cs` 하나다(그 프레임의 SDL 정점 좌표만 만들고 시뮬레이션·trace·저장에 들어가지 않는다). 쓰이지 않는 허용 항목이 남으면 검사가 실패한다. 규칙은 [Transform 2D](../../../../docs/manual/transform-2d.md)에 적었다.

| 트리 | 결과 |
| --- | --- |
| `e5571eb^`(Affine2D 이전) | 실패, 68곳: Engine.Core의 `Vector2/3/4.Transform`·`TransformNormal`·`Lerp` 60곳, 샘플 스크립트(KeyRoom·PlatformerLab·SeedLab) `Vector2.Transform`·`TransformNormal` 6곳, BarrageLab `MathF.SinCos` 2곳 |
| `e5571eb`(첫 수정, `FusedMultiplyAdd`) | 실패, 4곳: `Transform2D.cs`의 `Vector3/4.FusedMultiplyAdd` 2곳(이후 곱셈·덧셈 분리로 바뀜), BarrageLab `MathF.SinCos` 2곳 |
| `24741bf`(이번 기준) | 실패, BarrageLab `MathF.SinCos` 2곳 |
| 이번 변경 | 통과 |

W10의 완료 기준("e5571eb 이전 커밋에서 실패, 이후 통과")에 비춰 보면, `e5571eb` 자체는 처음 고친 방식의 `FusedMultiplyAdd`와 당시 이미 있던 `SinCos`로 실패한다. Affine2D 관련 항목만 보면 이전 실패·이후 통과다.

### BarrageLab 수정

[BulletTurret.cs](../../../../samples/BarrageLab/Scripts/BulletTurret.cs)와 [SpiralBarrage.cs](../../../../samples/BarrageLab/Scripts/SpiralBarrage.cs)의 `MathF.SinCos`를 `MathF.Cos`·`MathF.Sin` 따로 호출로 바꿨다(Particles2D와 같은 방식).

BarrageLab의 기준값은 바뀌지 않았다(아래 표). 그래서 다시 기록하지 않았다. 이 Mac의 JIT과 Native AOT 모두 바꾼 뒤 같은 값이다. 바꾸기 전의 `SinCos`가 JIT에서 마지막 비트를 다르게 냈더라도 이 입력에서는 명중·화면 밖 판정이 같았다는 뜻이다.

## 3. 회귀와 결정성 기준값(macOS arm64)

에디터(`engine/scripts/build-editor-mac.sh`)와 세 플랫폼 템플릿(`engine/scripts/build-players.sh`)을 이번 소스로 다시 만든 뒤 [gates.sh](gates.sh)로 실행했다. Scene Play는 패키지 에디터(`editor/MoNoon.app`)에 각 프로브를 붙였다. 첫 예외는 없다. [실행 출력](gates.txt)

| 검사 | Scene Play | Release | Native AOT | 값 |
| --- | --- | --- | --- | --- |
| `./monoon test` | – | – | – | 통과. PASS 752줄(새 항목 2: YAML 형식 115개, 결정성 API 검사 212개 파일). [로그](checks.txt) |
| PlatformerLab.Joints 300틱 | 같음 | 같음 | 같음 | `2d523cd0…`(기준과 같음), 최종 X 738.9792 |
| PlatformerLab.Paths 400틱 | 같음 | 같음 | 같음 | `e37de6c0…`(기준과 같음), 캡처 같음 |
| BarrageLab.Lines 480틱 | 같음 | 같음 | 같음 | `6969a524…`(기준과 같음), 캡처 5장 같음 |
| ParticleLab Modules 600틱 | 같음 | 같음 | 같음 | `d20054b4…`(기준과 같음), 캡처 3장 같음 |
| ParticleLab Sorting 300틱 | 같음 | 같음 | 같음 | `1031c7ab…`(기준과 같음) |
| ParticleLab Stress 300틱 | 같음 | 같음 | 같음 | `4d3b06e2…`(기준과 같음), 최대 10,240개 |
| BarrageLab.PrefabPools 1,800틱 | – | 통과 | 통과 | 명중 2,338 = Exit, 활성 503, 생성 587, 재사용 6,721(기준과 같음) |
| InventoryLab 재생(`./monoon test replay`) | – | 같음 | 같음 | 171바이트, `EED41DEF…E70F795`(기준과 같음) |
| KeyRoom.WorldCheckpoints | – | 통과 | 통과 | `ci.py osx-arm64` 전 항목 통과 |
| 금지 파일(위 배포본 `.app` 16개): Engine.Authoring, Engine.Editor, SkiaSharp, Avalonia, MoNoon.dll | – | 없음 | 없음 | |

ParticleRange를 참조 형식으로 바꿔도 trace가 같은 이유: 값은 같고 읽는 위치만 바뀌었다. 난수 소비 순서와 계산식은 그대로다.

## 용량

| 항목 | 기준 `24741bf` | 이번 | 차이 |
| --- | ---: | ---: | ---: |
| `Engine.Core.dll`(Release 빌드) | 1,362,432 B | 1,363,968 B | +1,536 B |
| PlatformerLab Native AOT `MoNoonPlayer`(Distribution) | 10,501,872 B | 10,501,824 B | −48 B |

기준 AOT는 `git archive 24741bf`를 푼 폴더에서 AOT 키트를 만들어 같은 명령으로 빌드했다. 검사 두 개는 Engine.Checks에만 있어 배포본에 들어가지 않는다.

## 확인하지 않은 것

- Windows·Linux x64(조정 담당자 몫, 게스트 미사용). 결정성 검사는 소스만 읽어 OS와 상관없다.
- 정적 검사는 줄 단위 정규식이다. `using static System.Numerics.Vector2` 뒤의 `Transform(...)`, 별칭, 여러 줄에 걸친 호출, 연산자(`Matrix3x2 *`, `floatQuat` 곱)는 잡지 못한다. `floatQuat`·`float4x4`는 형식 이름 자체를 금지해 대신한다. `Vector*.Normalize`·`Dot`은 목록에 넣지 않았다(W10에 "등"으로만 있고 FMA 경로가 확인되지 않았다).
- 패키지(`samples/*/Mods`, `samples/*/Editor`)와 Engine.Player는 검사 대상이 아니다(브리프 범위: Engine.Core·Engine.Desktop·`samples/*/Scripts`).
- 저장소 밖 사용자 프로젝트의 손으로 쓴 YAML. 키를 뺀 파일은 이제 코드 기본값으로 읽는다(1절).

## 재현

```sh
./monoon test
# 결정성 검사만, 다른 트리에도: dotnet engine/.artifacts/bin/Engine.Checks/release/Engine.Checks.dll <트리 루트> --determinism-scan-only
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
sh engine/tests/results/yaml-defaults-determinism/gates.sh "$(mktemp -d)"
```
